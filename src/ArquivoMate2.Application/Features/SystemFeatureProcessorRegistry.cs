using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Features
{
    public interface ISystemFeatureProcessorRegistry
    {
        ISystemFeatureProcessor? Get(string featureKey);
        IReadOnlyCollection<string> AvailableKeys { get; }
    }

    public class SystemFeatureProcessorRegistry : ISystemFeatureProcessorRegistry
    {
        private readonly Dictionary<string, ISystemFeatureProcessor> _map;
        public IReadOnlyCollection<string> AvailableKeys => _map.Keys;

        public SystemFeatureProcessorRegistry(IEnumerable<ISystemFeatureProcessor> processors, ILogger<SystemFeatureProcessorRegistry> logger)
        {
            _map = new(StringComparer.OrdinalIgnoreCase);
            foreach (var p in processors)
            {
                if (string.IsNullOrWhiteSpace(p.FeatureKey))
                {
                    logger.LogWarning("SystemFeatureProcessor ohne FeatureKey ignoriert: {Type}", p.GetType().Name);
                    continue;
                }
                if (_map.ContainsKey(p.FeatureKey))
                {
                    logger.LogWarning("Duplicate SystemFeatureProcessor für Key {FeatureKey} -> {Existing} vs {Duplicate}", p.FeatureKey, _map[p.FeatureKey].GetType().Name, p.GetType().Name);
                    continue;
                }
                _map[p.FeatureKey] = p;
            }
        }

        public ISystemFeatureProcessor? Get(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return null;
            return _map.TryGetValue(featureKey, out var p) ? p : null;
        }
    }
}
