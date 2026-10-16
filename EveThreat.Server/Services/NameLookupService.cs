using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace EveThreat.Server.Services
{
    public interface INameLookupService
    {
        Task<string> GetCharacterName(long characterId);
        Task<string> GetCorporationName(long corporationId);
        Task<string> GetAllianceName(long allianceId);
        Task<string> GetShipName(long typeId);
        Task<string> GetSystemName(long systemId);
    }

    public class NameLookupService : INameLookupService
    {
        private readonly HttpClient _http;

        // Caches (thread-safe)
        private readonly ConcurrentDictionary<long, string> _characterCache = new();
        private readonly ConcurrentDictionary<long, string> _corporationCache = new();
        private readonly ConcurrentDictionary<long, string> _allianceCache = new();
        private readonly ConcurrentDictionary<long, string> _shipCache = new();
        private readonly ConcurrentDictionary<long, string> _systemCache = new();

        public NameLookupService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent", "EveThreat Intel Tool");
        }

        public async Task<string> GetCharacterName(long characterId)
        {
            if (_characterCache.TryGetValue(characterId, out var cached))
                return cached;

            var url = $"https://esi.evetech.net/latest/characters/{characterId}/";
            var json = await _http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<CharacterInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var name = data?.Name ?? "Unknown Character";
            _characterCache[characterId] = name;
            return name;
        }

        public async Task<string> GetCorporationName(long corporationId)
        {
            if (_corporationCache.TryGetValue(corporationId, out var cached))
                return cached;

            var url = $"https://esi.evetech.net/latest/corporations/{corporationId}/";
            var json = await _http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<CorporationInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var name = data?.Name ?? "Unknown Corporation";
            _corporationCache[corporationId] = name;
            return name;
        }

        public async Task<string> GetAllianceName(long allianceId)
        {
            if (_allianceCache.TryGetValue(allianceId, out var cached))
                return cached;

            var url = $"https://esi.evetech.net/latest/alliances/{allianceId}/";
            var json = await _http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<AllianceInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var name = data?.Name ?? "Unknown Alliance";
            _allianceCache[allianceId] = name;
            return name;
        }

        public async Task<string> GetShipName(long typeId)
        {
            if (_shipCache.TryGetValue(typeId, out var cached))
                return cached;

            var url = $"https://esi.evetech.net/latest/universe/types/{typeId}/";
            var json = await _http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<TypeInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var name = data?.Name ?? "Unknown Ship";
            _shipCache[typeId] = name;
            return name;
        }

        public async Task<string> GetSystemName(long systemId)
        {
            if (_systemCache.TryGetValue(systemId, out var cached))
                return cached;

            var url = $"https://esi.evetech.net/latest/universe/systems/{systemId}/";
            var json = await _http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<SystemInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var name = data?.Name ?? "Unknown System";
            _systemCache[systemId] = name;
            return name;
        }
    }

    // Models
    public class CharacterInfo { public string Name { get; set; } }
    public class CorporationInfo { public string Name { get; set; } }
    public class AllianceInfo { public string Name { get; set; } }
    public class TypeInfo { public string Name { get; set; } }
    public class SystemInfo { public string Name { get; set; } }
}
