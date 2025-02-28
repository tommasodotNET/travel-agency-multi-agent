using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TravelAgency.ProcessOrchestrator;
using TravelAgency.ProcessOrchestrator.Models;
using TravelAgency.ProcessOrchestrator.Steps;

var builder = WebApplication.CreateBuilder(args);

var otelExporterEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otelExporterHeaders = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

var loggerFactory = LoggerFactory.Create(builder =>
{
    // Add OpenTelemetry as a logging provider
    builder.AddOpenTelemetry(options =>
    {
        options.AddOtlpExporter(exporter => {exporter.Endpoint = new Uri(otelExporterEndpoint); exporter.Headers = otelExporterHeaders; exporter.Protocol = OtlpExportProtocol.Grpc;});
        // Format log messages. This defaults to false.
        options.IncludeFormattedMessage = true;
    });

    builder.AddTraceSource("Microsoft.SemanticKernel");
    builder.SetMinimumLevel(LogLevel.Information);
});

using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Microsoft.SemanticKernel*")
    .AddOtlpExporter(exporter => {exporter.Endpoint = new Uri(otelExporterEndpoint); exporter.Headers = otelExporterHeaders; exporter.Protocol = OtlpExportProtocol.Grpc;})
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("Microsoft.SemanticKernel*")
    .AddOtlpExporter(exporter => {exporter.Endpoint = new Uri(otelExporterEndpoint); exporter.Headers = otelExporterHeaders; exporter.Protocol = OtlpExportProtocol.Grpc;})
    .Build();

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddHttpClient<OfferingsExpertHttpClient>(client => { client.BaseAddress = new("https+http://offerings-expert"); });
builder.Services.AddHttpClient<TripPlannerHttpClient>(client => { client.BaseAddress = new("https+http://trip-planner"); });
builder.Services.AddSingleton(builder => {
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.Services.AddSingleton(builder.GetRequiredService<OfferingsExpertHttpClient>());
    kernelBuilder.Services.AddSingleton(builder.GetRequiredService<TripPlannerHttpClient>());
    
    return kernelBuilder.Build();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapPost("/api/process-orchestrator", async (Kernel kernel, [FromBody] string userRequest) =>
{
    var processBuilder = new ProcessBuilder("ProcessDocument");
    var translateDocumentStep = processBuilder.AddStepFromType<RetrieveOfferingsStep>();
    var summarizeDocumentStep = processBuilder.AddStepFromType<PlanTripStep>();

    processBuilder
        .OnInputEvent(ProcessEvents.RetrieveOfferings)
        .SendEventTo(new(translateDocumentStep, RetrieveOfferingsStep.Functions.RetrieveOfferings, parameterName: ""));

    translateDocumentStep
        .OnEvent(ProcessEvents.OfferingsRetrieved)
        .SendEventTo(new(summarizeDocumentStep, PlanTripStep.Functions.PlanTrip, parameterName: ""));

    summarizeDocumentStep
        .OnEvent(ProcessEvents.TripPlanned)
        .StopProcess();

    var process = processBuilder.Build();
    using var runningProcess = await process.StartAsync(
        kernel,
        new KernelProcessEvent { Id = ProcessEvents.RetrieveOfferings, Data = userRequest }
    );

    return Results.Ok("Process completed successfully");
})
.WithName("ProcessOrchestrator");

app.MapDefaultEndpoints();

app.Run();