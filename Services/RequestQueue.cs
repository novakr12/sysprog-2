using System.Net;
using System.Threading;

namespace Sysprog1.Services
{
    public class RequestQueue
    {
        private readonly Queue<HttpListenerContext> _queue = new();
        private readonly object _lock = new object();
        private readonly int _maxSize;
        private readonly Logger _logger;
        private bool _stopped = false;

        public RequestQueue(int maxSize, Logger logger)
        {
            _maxSize = maxSize;
            _logger = logger;
        }

        // Producer: HTTP listener dodaje zahteve
        public bool Enqueue(HttpListenerContext context)
        {
            lock (_lock)
            {
                if (_stopped)
                    return false;

                if (_queue.Count >= _maxSize)
                {
                    _logger.Warn($"Red čekanja je pun ({_maxSize}), zahtev odbijen.");
                    return false;
                }

                _queue.Enqueue(context);
                _logger.Info($"Zahtev dodat u red. Veličina reda: {_queue.Count}");
                Monitor.Pulse(_lock);  // probudi jednog radnika
                return true;
            }
        }

        // Consumer: radna nit uzima zahtev; blokira ako je red prazan
        public HttpListenerContext? Dequeue()
        {
            lock (_lock)
            {
                while (_queue.Count == 0 && !_stopped)
                    Monitor.Wait(_lock);

                if (_stopped && _queue.Count == 0)
                    return null;

                var ctx = _queue.Dequeue();
                _logger.Info($"Zahtev preuzet iz reda. Preostalo: {_queue.Count}");
                return ctx;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopped = true;
                Monitor.PulseAll(_lock);  // probudi sve radnike da se ugase
            }
        }
    }
}
