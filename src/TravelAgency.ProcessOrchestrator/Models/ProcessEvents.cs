using System;

namespace TravelAgency.ProcessOrchestrator.Models;

public class ProcessEvents
{
    public static readonly string RetrieveOfferings = nameof(RetrieveOfferings);
    public static readonly string OfferingsRetrieved = nameof(OfferingsRetrieved);
    public static readonly string PlanTrip = nameof(PlanTrip);
    public static readonly string TripPlanned = nameof(TripPlanned);
}
