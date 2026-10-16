using System.Collections.Concurrent;

namespace EveThreat.Server.Models
{
    public class ZKillCache
    {
        public List<ZKillData> Kills { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        public long Location { get; set; } = 0;
        public IntelResult IntelResult { get; set; } = new();
        public ConcurrentDictionary<long, string> CharacterCache { get; set; }
        = new();

        public ConcurrentDictionary<long, string> CorpCache { get; set; }
            = new();

        public ConcurrentDictionary<long, string> AllianceCache { get; set; }
            = new();

        public ConcurrentDictionary<long, string> ShipCache { get; set; }
            = new();
    }
}
