namespace EveThreat.Server.Models
{
    public class ZKillCache
    {
        public List<ZKillData> Kills { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        public long Location { get; set; } = 0;
        public IntelResult IntelResult { get; set; } = new();
    }
}
