using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;
using TravelAgency.ProcessOrchestrator.Models;
using TravelAgency.Shared;

namespace TravelAgency.ProcessOrchestrator.Steps;

public class RetrieveOfferingsStep : KernelProcessStep
{
    public static class Functions
    {
        public const string RetrieveOfferings = nameof(RetrieveOfferings);
    }

    [KernelFunction(Functions.RetrieveOfferings)]
    public async ValueTask RetrieveOfferingsAsync(KernelProcessStepContext context, Kernel kernel, string userRequest)
    {
        var offeringsExpertHttpClient = kernel.GetRequiredService<OfferingsExpertHttpClient>();
        var offerings = await offeringsExpertHttpClient.GetOfferingssAsync(userRequest);
        var planTripRequest = new PlanTripRequest() { UserRequest = userRequest, Offerings = offerings };
        Console.WriteLine($"Available offers found:\n{offerings}");
        await context.EmitEventAsync(new () { Id = ProcessEvents.OfferingsRetrieved, Data = planTripRequest });
    }
}
