using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sysprog1.Models;

namespace Sysprog1.Services
{
    public class RequestHandler
    {
        private readonly ApiService _apiService;
        private readonly LaunchCache _cache;
        private readonly Logger _logger;
        private readonly NameResolver _nameResolver;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public RequestHandler(ApiService apiService, LaunchCache cache, Logger logger, NameResolver nameResolver)
        {
            _apiService = apiService;
            _cache = cache;
            _logger = logger;
            _nameResolver = nameResolver;
        }

        public void Handle(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.HttpMethod != "GET" || request.Url?.AbsolutePath.ToLower() != "/launches")
                {
                    SendResponse(response, HttpStatusCode.NotFound, new { error = "Endpoint nije nadjen. Koristite GET /launches" });
                    return;
                }

                var qs = request.QueryString;

                if (!TryParseParams(qs, response,
                    out string? missionName, out bool? success, out int? flightNumber,
                    out string? rocket, out string? launchpad,
                    out DateTime? dateFrom, out DateTime? dateTo, out bool? upcoming))
                    return;

                string url = upcoming == true ? ApiService.UpcomingUrl : ApiService.PastUrl;
                string cacheKey = upcoming == true ? "upcoming" : "past";

                var allLaunches = _cache.GetOrFetch(cacheKey, () => _apiService.FetchLaunches(url));

                var results = allLaunches
                    .Where(l =>
                    {
                        bool matchMission = string.IsNullOrEmpty(missionName) ||
                                           l.Name.Contains(missionName, StringComparison.OrdinalIgnoreCase);
                        bool matchSuccess = !success.HasValue || l.Success == success.Value;
                        bool matchFlight = !flightNumber.HasValue || l.FlightNumber == flightNumber.Value;
                        bool matchRocket = string.IsNullOrEmpty(rocket) ||
                                          (_nameResolver.ResolveRocket(l.Rocket)
                                              ?.Contains(rocket, StringComparison.OrdinalIgnoreCase) == true);
                        bool matchLaunchpad = string.IsNullOrEmpty(launchpad) ||
                                             (_nameResolver.ResolveLaunchpad(l.Launchpad)
                                                 ?.Contains(launchpad, StringComparison.OrdinalIgnoreCase) == true);
                        bool matchDateFrom = !dateFrom.HasValue || l.DateUtc >= dateFrom.Value;
                        bool matchDateTo = !dateTo.HasValue || l.DateUtc <= dateTo.Value;

                        return matchMission && matchSuccess && matchFlight && matchRocket &&
                               matchLaunchpad && matchDateFrom && matchDateTo;
                    })
                    .Select(l => new LaunchResponse
                    {
                        Id = l.Id,
                        Name = l.Name,
                        DateUtc = l.DateUtc,
                        Success = l.Success,
                        FlightNumber = l.FlightNumber,
                        RocketName = _nameResolver.ResolveRocket(l.Rocket),
                        LaunchpadName = _nameResolver.ResolveLaunchpad(l.Launchpad),
                        Details = l.Details,
                        Upcoming = l.Upcoming,
                        Links = l.Links
                    })
                    .ToList();

                if (results.Count == 0)
                {
                    SendResponse(response, HttpStatusCode.NotFound, new { error = "Nisu nadjeni letovi koji odgovaraju zadatim filterima." });
                    return;
                }

                SendResponse(response, HttpStatusCode.OK, results);
                _logger.Info($"200 OK — {request.Url} ({results.Count} letova)");
            }
            catch (Exception ex)
            {
                _logger.Error($"Greška pri obradi zahteva: {ex.Message}");
                try { SendResponse(response, HttpStatusCode.InternalServerError, new { error = $"Interna greška: {ex.Message}" }); } catch { }
            }
            finally
            {
                response.Close();
            }
        }

        private bool TryParseParams(
            NameValueCollection qs, HttpListenerResponse response,
            out string? missionName, out bool? success, out int? flightNumber,
            out string? rocket, out string? launchpad,
            out DateTime? dateFrom, out DateTime? dateTo, out bool? upcoming)
        {
            missionName = qs["missionName"];
            rocket = qs["rocketName"];
            launchpad = qs["launchpad"];
            success = null; flightNumber = null; dateFrom = null; dateTo = null; upcoming = null;

            if (!string.IsNullOrEmpty(qs["success"]))
            {
                if (bool.TryParse(qs["success"], out bool s)) success = s;
                else { SendResponse(response, HttpStatusCode.BadRequest, new { error = "Neispravan 'success'. Očekuje se true ili false." }); return false; }
            }
            if (!string.IsNullOrEmpty(qs["flightNumber"]))
            {
                if (int.TryParse(qs["flightNumber"], out int fn)) flightNumber = fn;
                else { SendResponse(response, HttpStatusCode.BadRequest, new { error = "Neispravan 'flightNumber'. Očekuje se ceo broj." }); return false; }
            }
            if (!string.IsNullOrEmpty(qs["dateFrom"]))
            {
                if (DateTime.TryParse(qs["dateFrom"], out DateTime df)) dateFrom = df.ToUniversalTime();
                else { SendResponse(response, HttpStatusCode.BadRequest, new { error = "Neispravan 'dateFrom'. Format: yyyy-MM-dd." }); return false; }
            }
            if (!string.IsNullOrEmpty(qs["dateTo"]))
            {
                if (DateTime.TryParse(qs["dateTo"], out DateTime dt)) dateTo = dt.ToUniversalTime().AddDays(1).AddTicks(-1);
                else { SendResponse(response, HttpStatusCode.BadRequest, new { error = "Neispravan 'dateTo'. Format: yyyy-MM-dd." }); return false; }
            }
            if (!string.IsNullOrEmpty(qs["upcoming"]))
            {
                if (bool.TryParse(qs["upcoming"], out bool u)) upcoming = u;
                else { SendResponse(response, HttpStatusCode.BadRequest, new { error = "Neispravan 'upcoming'. Očekuje se true ili false." }); return false; }
            }

            return true;
        }

        private static void SendResponse(HttpListenerResponse response, HttpStatusCode statusCode, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, JsonSettings);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                response.StatusCode = (int)statusCode;
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Flush();
            }
            catch { }
        }
    }
}
