using System.Text.Json;
using TravelAgency.OfferingsExpert;
using TravelAgency.OfferingsExpert.Model;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TravelAgency.Shared;
using Azure.AI.OpenAI;

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
builder.AddAzureOpenAIClient("openAiConnectionName");
builder.Services.AddTransient(builder => {
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddAzureOpenAIChatCompletion("gpt-4o", builder.GetService<AzureOpenAIClient>());
    
    return kernelBuilder.Build();
});

builder.Services.AddTransient<ChatWithDBAgent>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/get-offerings", async (ChatWithDBAgent agent, OfferingsExpertRequest request, HttpResponse response) =>
{
    var history = new ChatHistory();
    history.AddUserMessage(request.UserRequest);
    response.Headers.Append("Content-Type", "application/json");
    await foreach (var delta in agent.InvokeAsync(history))
    {
        history.AddAssistantMessage(delta.ToString());
        return delta.Items.Last().ToString();
    }

    return null;
}).WithName("GetOfferings");

app.MapDefaultEndpoints();

app.Run();