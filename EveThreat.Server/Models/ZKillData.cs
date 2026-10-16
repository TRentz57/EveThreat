namespace EveThreat.Server.Models
{
    public class ZKillData
    {
        public long Killmail_Id { get; set; }
        public DateTime Killmail_Time { get; set; }
        public long Solar_System_Id { get; set; }
        public long? War_Id { get; set; }
        public ZKillVictim Victim { get; set; }
        public List<ZKillAttacker> Attackers { get; set; }
        public ZKillPosition Position { get; set; }
        public List<ZKillItem> Items { get; set; }
        public ZKillZkb Zkb { get; set; }
    }

    public class ZKillVictim
    {
        public long? Character_Id { get; set; }
        public long? Corporation_Id { get; set; }
        public long? Alliance_Id { get; set; }
        public long? Faction_Id { get; set; }
        public long Ship_Type_Id { get; set; }
        public long Damage_Taken { get; set; }
        public ZKillPosition Position { get; set; }
    }

    public class ZKillAttacker
    {
        public long? Character_Id { get; set; }
        public long? Corporation_Id { get; set; }
        public long? Alliance_Id { get; set; }
        public long? Faction_Id { get; set; }
        public long? Ship_Type_Id { get; set; }
        public long? Weapon_Type_Id { get; set; }
        public bool Final_Blow { get; set; }
        public int Damage_Done { get; set; }
        public float? Security_Status { get; set; }
    }

    public class ZKillItem
    {
        public long Item_Type_Id { get; set; }
        public int Quantity_Dropped { get; set; }
        public int Quantity_Destroyed { get; set; }
        public int? Singleton { get; set; }
        public List<ZKillItem> Items { get; set; } 
    }

    public class ZKillPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class ZKillZkb
    {
        public long Location_Id { get; set; }
        public string Hash { get; set; }
        public long Fitted_Value { get; set; }
        public long Total_Value { get; set; }
        public long Points { get; set; }
        public bool Npc { get; set; }
        public bool Solo { get; set; }
        public bool Awox { get; set; }
    }
}
