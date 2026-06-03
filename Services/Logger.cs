using System.Threading;

namespace Sysprog1.Services
{
    public class Logger
    {
        private readonly object _lock = new object();

        private void Log(string level, string message)
        {
            lock (_lock)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level,-5}] [Thread-{Thread.CurrentThread.ManagedThreadId,2}] {message}");
            }
        }

        public void Info(string message) => Log("INFO", message);
        public void Warn(string message) => Log("WARN", message);
        public void Error(string message) => Log("ERROR", message);
        public void Cache(string message) => Log("CACHE", message);
    }
}
