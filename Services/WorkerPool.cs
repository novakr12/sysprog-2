using System.Threading;

namespace Sysprog1.Services
{
    public class WorkerPool
    {
        private readonly Thread[] _workers;
        private readonly RequestQueue _queue;
        private readonly RequestHandler _handler;
        private readonly Logger _logger;

        public WorkerPool(int workerCount, RequestQueue queue, RequestHandler handler, Logger logger)
        {
            _queue = queue;
            _handler = handler;
            _logger = logger;
            _workers = new Thread[workerCount];

            for (int i = 0; i < workerCount; i++)
            {
                _workers[i] = new Thread(WorkerLoop)
                {
                    Name = $"Worker-{i + 1}",
                    IsBackground = true
                };
            }
        }

        public void Start()
        {
            foreach (var worker in _workers)
                worker.Start();
            _logger.Info($"WorkerPool pokrenut sa {_workers.Length} radnih niti.");
        }

        private void WorkerLoop()
        {
            _logger.Info("Radna nit pokrenuta.");

            while (true)
            {
                var context = _queue.Dequeue();  // blokira dok nema zahteva (Monitor.Wait)

                if (context == null)
                    break;  // null = signal za gašenje

                try
                {
                    _handler.Handle(context);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Neuhvaćena greška: {ex.Message}");
                }
            }

            _logger.Info("Radna nit zaustavljena.");
        }

        public void Stop()
        {
            _queue.Stop();  // šalje PulseAll svim radnicima
            foreach (var worker in _workers)
                worker.Join();  // čeka da se svaka nit završi
            _logger.Info("WorkerPool zaustavljen.");
        }
    }
}
