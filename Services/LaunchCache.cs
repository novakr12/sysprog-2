using System.Threading;
using Sysprog1.Models;

namespace Sysprog1.Services
{
    public class LaunchCache
    {
        private class CacheEntry
        {
            public List<LaunchSummary> Data { get; set; } = new();
            public DateTime Timestamp { get; set; }
            public bool IsExpired(TimeSpan duration) => DateTime.Now - Timestamp > duration;
        }

        private readonly Dictionary<string, CacheEntry> _cache = new();
        private readonly HashSet<string> _inProgress = new();
        private readonly object _lock = new object();
        private readonly TimeSpan _duration;
        private readonly Logger _logger;

        public LaunchCache(TimeSpan duration, Logger logger)
        {
            _duration = duration;
            _logger = logger;
        }

        public List<LaunchSummary> GetOrFetch(string key, Func<List<LaunchSummary>> fetchFunc)
        {
            lock (_lock)
            {
                // Brza provera - keš hit
                if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(_duration))
                {
                    _logger.Cache($"HIT: '{key}'");
                    return entry.Data;
                }

                // Prevencija stampeda: ako neka nit već fetuje isti ključ, čekaj
                while (_inProgress.Contains(key))
                {
                    _logger.Cache($"WAIT (stampede prevention): '{key}'");
                    Monitor.Wait(_lock);
                }

                // Ponovna provera nakon čekanja (druga nit je možda popunila keš)
                if (_cache.TryGetValue(key, out entry) && !entry.IsExpired(_duration))
                {
                    _logger.Cache($"HIT after wait: '{key}'");
                    return entry.Data;
                }

                // Ova nit preuzima odgovornost za fetch
                _inProgress.Add(key);
                _logger.Cache($"MISS - preuzimanje podataka za: '{key}'");
            }

            // Fetch se izvršava van locka da ne bi blokirao ostale niti za druge ključeve
            List<LaunchSummary> data;
            try
            {
                data = fetchFunc();
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _inProgress.Remove(key);
                    Monitor.PulseAll(_lock);  // probudi čekajuće niti da prime grešku
                }
                _logger.Error($"Greška pri preuzimanju '{key}': {ex.Message}");
                throw;
            }

            lock (_lock)
            {
                _cache[key] = new CacheEntry { Data = data, Timestamp = DateTime.Now };
                _inProgress.Remove(key);
                Monitor.PulseAll(_lock);  // probudi sve niti koje su čekale ovaj ključ
                _logger.Cache($"STORED: '{key}' ({data.Count} letova), ističe za {_duration.TotalSeconds}s");
            }

            return data;
        }
    }
}
