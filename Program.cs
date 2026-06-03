using System.Net;
using Sysprog1.Services;

namespace Sysprog1
{
    class Program
    {
        private const int WorkerCount = 4;
        private const int QueueMaxSize = 200;
        private const int CacheDurationSeconds = 10;

        static void Main(string[] args)
        {
            var logger = new Logger();
            var queue = new RequestQueue(QueueMaxSize, logger);

            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var apiService = new ApiService(httpClient);

            logger.Info("Preuzimanje naziva raketa i lansirnih mesta...");
            var rocketNames = apiService.FetchRocketNames();
            var launchpadNames = apiService.FetchLaunchpadNames();
            var nameResolver = new NameResolver(rocketNames, launchpadNames);
            logger.Info($"Učitano {rocketNames.Count} raketa i {launchpadNames.Count} lansirnih mesta.");

            var cache = new LaunchCache(TimeSpan.FromSeconds(CacheDurationSeconds), logger);
            var handler = new RequestHandler(apiService, cache, logger, nameResolver);
            var workerPool = new WorkerPool(WorkerCount, queue, handler, logger);

            workerPool.Start();

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");

            try
            {
                listener.Start();
                logger.Info("Server sluša na http://localhost:5000/");
                logger.Info($"Radne niti: {WorkerCount} | Red čekanja: {QueueMaxSize} | Keš TTL: {CacheDurationSeconds}s");
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
                workerPool.Stop();
            };

            // Glavna nit = producer: prima zahteve i stavlja ih u red
            while (listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();  // blokira dok ne stigne zahtev

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
