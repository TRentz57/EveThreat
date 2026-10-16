namespace EveThreat.Server.Models
{
    public class IntelResult
    {
        public List<ZKillTranslatedTop> Kills { get; set; }
        public string ActiveCorp {  get; set; }
        public List<string> ActivePilots { get; set; }
        public string ActiveTime { get; set; }
        public List<string> PopularShips { get; set; }



    }
}
