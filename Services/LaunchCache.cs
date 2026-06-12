using Sysprog2.Models;

namespace Sysprog2.Services
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
        private readonly Dictionary<string, Task<List<LaunchSummary>>> _inFlight = new();
        private readonly object _lock = new object();
        private readonly TimeSpan _duration;
        private readonly Logger _logger;

        public LaunchCache(TimeSpan duration, Logger logger)
        {
            _duration = duration;
            _logger = logger;
        }

        public Task<List<LaunchSummary>> GetOrFetchAsync(string key, Func<Task<List<LaunchSummary>>> fetchFunc)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(_duration))
                {
                    _logger.Cache($"HIT: '{key}'");
                    return Task.FromResult(entry.Data);
                }

                if (_inFlight.TryGetValue(key, out var pending))
                {
                    _logger.Cache($"JOIN in-flight (stampede prevention): '{key}'");
                    return pending;
                }

                _logger.Cache($"MISS - preuzimanje podataka za: '{key}'");
                var task = FetchAndStoreAsync(key, fetchFunc);
                _inFlight[key] = task;
                return task;
            }
        }

        private async Task<List<LaunchSummary>> FetchAndStoreAsync(
            string key, Func<Task<List<LaunchSummary>>> fetchFunc)
        {
            try
            {
                var data = await fetchFunc();

                lock (_lock)
                {
                    _cache[key] = new CacheEntry { Data = data, Timestamp = DateTime.Now };
                    _inFlight.Remove(key);
                    _logger.Cache($"STORED: '{key}' ({data.Count} letova), ističe za {_duration.TotalSeconds}s");
                }

                return data;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _inFlight.Remove(key);
                }
                _logger.Error($"Greška pri preuzimanju '{key}': {ex.Message}");
                throw;
            }
        }
    }
}
