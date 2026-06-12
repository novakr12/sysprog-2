using Newtonsoft.Json;
using Sysprog2.Models;

namespace Sysprog2.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public const string PastUrl = "https://api.spacexdata.com/v5/launches/past";
        public const string UpcomingUrl = "https://api.spacexdata.com/v5/launches/upcoming";
        private const string RocketsUrl = "https://api.spacexdata.com/v4/rockets";
        private const string LaunchpadsUrl = "https://api.spacexdata.com/v4/launchpads";

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private async Task<List<T>> FetchListAsync<T>(string url)
        {
            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"SpaceX API greška: {response.ReasonPhrase}");

            string body = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<List<T>>(body) ?? new List<T>();
        }

        public Task<List<LaunchSummary>> FetchLaunchesAsync(string url) =>
            FetchListAsync<LaunchSummary>(url);

        public async Task<Dictionary<string, string>> FetchRocketNamesAsync() =>
            (await FetchListAsync<Rocket>(RocketsUrl)).ToDictionary(r => r.Id, r => r.Name);

        public async Task<Dictionary<string, string>> FetchLaunchpadNamesAsync() =>
            (await FetchListAsync<Launchpad>(LaunchpadsUrl)).ToDictionary(p => p.Id, p => p.Name);
    }
}
