namespace Sysprog1.Services
{
    public class NameResolver
    {
        private readonly Dictionary<string, string> _rockets;
        private readonly Dictionary<string, string> _launchpads;

        public NameResolver(Dictionary<string, string> rockets, Dictionary<string, string> launchpads)
        {
            _rockets = rockets;
            _launchpads = launchpads;
        }

        public string? ResolveRocket(string? id) =>
            id != null && _rockets.TryGetValue(id, out var name) ? name : id;

        public string? ResolveLaunchpad(string? id) =>
            id != null && _launchpads.TryGetValue(id, out var name) ? name : id;
    }
}
