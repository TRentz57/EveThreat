using System.Text.Json.Serialization;

namespace EveThreat.Server.Models
{
    public class LocationResponse
    {
        [JsonPropertyName("solar_system_id")]
        public long SolarSystemId { get; set; }

        [JsonPropertyName("station_id")]
        public long? StationId { get; set; }

        [JsonPropertyName("structure_id")]
        public long? StructureId { get; set; }
    }
}
