namespace EveThreat.Server.Models
{

    public class ZKillTranslatedTop
    {
        public long KillmailId { get; set; }
        public DateTime KillmailTime { get; set; }
        public List<ZKillTranslated> Attacker { get; set; }
        public ZKillTranslated Defender { get; set; }
        public string SystemName { get; set; }

    }
    public class ZKillTranslated
    {

        public string? CharacterName { get; set; }
        public string? CorpName { get; set; }
        public string? AllianceName { get; set; }
        public string? ShipName { get; set; }
        
    }
}
