using System.Threading;

namespace Sysprog2.Services
{
    public class Dispatcher
    {
        private readonly Thread _dispatcherThread;
        private readonly RequestQueue _queue;
        private readonly RequestHandler _handler;
        private readonly Logger _logger;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxParallelism;

        public Dispatcher(int maxParallelism, RequestQueue queue, RequestHandler handler, Logger logger)
        {
            _maxParallelism = maxParallelism;
            _queue = queue;
            _handler = handler;
            _logger = logger;
            _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

            _dispatcherThread = new Thread(DispatchLoop)
            {
                Name = "Dispatcher",
                IsBackground = true
            };
        }

        public void Start()
        {
            _dispatcherThread.Start();
            _logger.Info($"Dispatcher pokrenut. Maks. paralelnih obrada: {_maxParallelism}");
        }

        private void DispatchLoop()
        {
            _logger.Info("Dispatcher nit pokrenuta.");

            while (true)
            {
                var ctx = _queue.Dequeue();
                if (ctx == null)
                    break;

                _semaphore.Wait();

                var work = Task.Run(() => _handler.HandleAsync(ctx));

                work.ContinueWith(
                    t => _logger.Warn($"Obrada zahteva neuspešna: {t.Exception!.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);

                work.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        RequestHandler.SendError(ctx, t.Exception!.GetBaseException());

                    try { ctx.Response.Close(); } catch { }
                    _semaphore.Release();
                });
            }

            _logger.Info("Dispatcher nit zaustavljena.");
        }

        public void Stop()
        {
            _queue.Stop();
            _dispatcherThread.Join();

            for (int i = 0; i < _maxParallelism; i++)
                _semaphore.Wait();

            _logger.Info("Dispatcher zaustavljen, sve obrade dovršene.");
        }
    }
}
