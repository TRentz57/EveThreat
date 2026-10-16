namespace EveThreat.Server.Models
{
    public class ActiveCorp
    {
        public string CorpName { get; set; }
        public List<string> PilotNames { get; set; }
        public List<ZKillTranslatedTop> CorpKills { get; set; }
        public string AllianceName { get; set; }
        public List<string> AlliedCorps { get; set; } = new();
    }
}
