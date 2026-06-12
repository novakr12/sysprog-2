using System.Net;
using Sysprog2.Services;

namespace Sysprog2
{
    class Program
    {
        private const int MaxParallelism = 4;
        private const int QueueMaxSize = 200;
        private const int CacheDurationSeconds = 10;

        static void Main(string[] args)
        {
            var logger = new Logger();
            var queue = new RequestQueue(QueueMaxSize, logger);

            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var apiService = new ApiService(httpClient);

            logger.Info("Preuzimanje naziva raketa i lansirnih mesta...");

            var rocketsTask = apiService.FetchRocketNamesAsync();
            var launchpadsTask = apiService.FetchLaunchpadNamesAsync();

            NameResolver nameResolver;
            try
            {
                nameResolver = Task.WhenAll(rocketsTask, launchpadsTask).ContinueWith(_ =>
                {
                    var resolver = new NameResolver(rocketsTask.Result, launchpadsTask.Result);
                    logger.Info($"Učitano {rocketsTask.Result.Count} raketa i {launchpadsTask.Result.Count} lansirnih mesta.");
                    return resolver;
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var reason = (ex as AggregateException)?.GetBaseException().Message ?? ex.Message;
                logger.Error($"Nije moguće preuzeti podatke sa SpaceX API-a: {reason}");
                logger.Error("Server se ne pokreće. Proverite mrežnu vezu i dostupnost API-a, pa pokušajte ponovo.");
                return;
            }

            var cache = new LaunchCache(TimeSpan.FromSeconds(CacheDurationSeconds), logger);
            var handler = new RequestHandler(apiService, cache, logger, nameResolver);
            var dispatcher = new Dispatcher(MaxParallelism, queue, handler, logger);

            dispatcher.Start();

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");

            try
            {
                listener.Start();
                logger.Info("Server sluša na http://localhost:5000/");
                logger.Info($"Maks. paralelnih obrada: {MaxParallelism} | Red čekanja: {QueueMaxSize} | Keš TTL: {CacheDurationSeconds}s");
                logger.Info("Pritisnite Ctrl+C za zaustavljanje.");
            }
            catch (Exception ex)
            {
                logger.Error($"Greška pri pokretanju servera: {ex.Message}");
                return;
            }

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                logger.Info("Gašenje servera...");
                listener.Stop();
                dispatcher.Stop();
            };

            while (listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();

                    if (!queue.Enqueue(context))
                    {
                        context.Response.StatusCode = 503;
                        context.Response.Close();
                        logger.Warn("503 Service Unavailable — red pun.");
                    }
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error($"Greška u prijemu zahteva: {ex.Message}");
                }
            }

            logger.Info("Server ugašen.");
        }
    }
}
