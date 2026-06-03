using Newtonsoft.Json;
using Sysprog1.Models;

namespace Sysprog1.Services
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

        private List<T> FetchList<T>(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = _httpClient.Send(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"SpaceX API greška: {response.ReasonPhrase}");

            using var reader = new StreamReader(response.Content.ReadAsStream());
            string body = reader.ReadToEnd();

            return JsonConvert.DeserializeObject<List<T>>(body) ?? new List<T>();
        }

        public List<LaunchSummary> FetchLaunches(string url) => FetchList<LaunchSummary>(url);

        public Dictionary<string, string> FetchRocketNames() =>
            FetchList<Rocket>(RocketsUrl).ToDictionary(r => r.Id, r => r.Name);

        public Dictionary<string, string> FetchLaunchpadNames() =>
            FetchList<Launchpad>(LaunchpadsUrl).ToDictionary(p => p.Id, p => p.Name);
    }
}
