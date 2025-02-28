using System;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TravelAgency.Shared;

namespace TravelAgency.ProcessOrchestrator;

public class TripPlannerHttpClient(HttpClient httpClient)
{
    public async Task<string> PlanTripAsync(string userRequest, string offerings)
    {
        var payload = new PlanTripRequest { UserRequest = userRequest, Offerings = offerings };
        var response = await httpClient.PostAsync("/api/plan-trip", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream);
        var result = new StringBuilder();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            result.AppendLine(line);
        }

        var stringResult = result.ToString();
        return stringResult;
    }
}
