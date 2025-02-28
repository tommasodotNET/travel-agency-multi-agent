using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Runtime;
using TravelAgency.ProcessOrchestrator.Models;
using TravelAgency.Shared;

namespace TravelAgency.ProcessOrchestrator.Steps;

public class PlanTripStep : KernelProcessStep
{
    public static class Functions
    {
        public const string PlanTrip = nameof(PlanTrip);
    }

    [KernelFunction(Functions.PlanTrip)]
    public async ValueTask PlanTripAsync(KernelProcessStepContext context, Kernel kernel, PlanTripRequest planTripRequest)
    {
        var tripPlannerHttpClient = kernel.GetRequiredService<TripPlannerHttpClient>();
        var plannedTrip = await tripPlannerHttpClient.PlanTripAsync(planTripRequest.UserRequest, planTripRequest.Offerings);
        Console.WriteLine($"Trip Planning discussion:\n{plannedTrip}");
        await context.EmitEventAsync(new () { Id = ProcessEvents.TripPlanned, Data = plannedTrip });
    }
}
