using System.Text;
using System.Text.Json;
using TravelAgency.Shared;

namespace TravelAgency.ProcessOrchestrator;

public class OfferingsExpertHttpClient(HttpClient httpClient)
{
    public async Task<string> GetOfferingssAsync(string userRequest)
    {
        var payload = new OfferingsExpertRequest { UserRequest = userRequest };
        var response = await httpClient.PostAsync("/api/get-offerings", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return responseContent;
    }
}
