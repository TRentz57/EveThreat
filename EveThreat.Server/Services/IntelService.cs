using EveThreat.Server.IServices;
using EveThreat.Server.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EveThreat.Server.Services
{
    public class IntelService : IIntelService
    {
        private readonly IEveAuthService _auth;
        private readonly HttpClient _http;
        private readonly ZKillCache _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
        private readonly Dictionary<long, string> _characterCache = new();
        private readonly Dictionary<long, string> _corpCache = new();
        private readonly Dictionary<long, string> _allianceCache = new();
        private readonly Dictionary<long, string> _shipCache = new();
        private static readonly HashSet<string> NpcStarterCorps =
        [
            "State War Academy",
            "School of Applied Knowledge",
            "Science and Trade Institute",
            "Center for Advanced Studies",
            "Federal Navy Academy",
            "Republic University",
            "University of Caille",
            "Royal Amarr Institute",
            "Imperial Academy"
        ];

        public IntelService(IEveAuthService auth, HttpClient http, ZKillCache zKillCache)
        {
            _auth = auth;
            _http = http;
            _cache = zKillCache;

        }
        public async Task<IntelResult> GetIntelAsync()
        {
            var loc = await GetLocationAsync();
            if (loc.SolarSystemId != _cache.Location)
            {
                var zkilldata = await GetZkillData(loc);
                var intelResult = await AggregateData(zkilldata);
                _cache.IntelResult = intelResult;
                return intelResult;
            }

            return _cache.IntelResult;
        }

        public async Task<IntelResult> AggregateData(List<ZKillTranslatedTop> kills)
        {
            var activeCorp = await GetActiveCorp(kills);
            var activePilots = await GetActivePilot(activeCorp);

            var result = new IntelResult
            {
                Kills = activeCorp.CorpKills,
                ActiveCorp = activeCorp.CorpName,
                ActivePilots = activePilots.PilotNames,
                ActiveTime = await GetActiveTime(activeCorp),
                PopularShips = await GetShips(activeCorp),
                AllianceName = activeCorp.AllianceName,
                AlliedCorps = activeCorp.AlliedCorps
            };
            var t = result;
            return t;
        }

        public async Task<ActiveCorp> GetActiveCorp(List<ZKillTranslatedTop> kills)
        {
            // NEW: NPC activity still counts,
            // but at reduced weight.
            const double npcWeightMultiplier = 0.25;


            var corpKillCount = new Dictionary<string, int>();
            var corpDeathCount = new Dictionary<string, int>();
            var corpDates = new Dictionary<string, HashSet<DateTime>>();
            var corpKills = new Dictionary<string, List<ZKillTranslatedTop>>();

            // Track alliance membership
            var corpAllianceMap = new Dictionary<string, string>();

            foreach (var k in kills)
            {
                var date = k.KillmailTime.Date;

                // Defender = death / presence
                var defCorp = k.Defender.CorpName ?? "npc";
                var defAlliance = k.Defender.AllianceName ?? "No Alliance";

                if (!corpDeathCount.ContainsKey(defCorp))
                    corpDeathCount[defCorp] = 0;

                corpDeathCount[defCorp]++;

                if (!corpDates.ContainsKey(defCorp))
                    corpDates[defCorp] = new HashSet<DateTime>();

                corpDates[defCorp].Add(date);

                if (!corpKills.ContainsKey(defCorp))
                    corpKills[defCorp] = new List<ZKillTranslatedTop>();

                corpKills[defCorp].Add(k);

                if (!corpAllianceMap.ContainsKey(defCorp))
                    corpAllianceMap[defCorp] = defAlliance;

                // Attackers = kill activity
                foreach (var atk in k.Attacker)
                {
                    var atkCorp = atk.CorpName ?? "npc";
                    var atkAlliance = atk.AllianceName ?? "No Alliance";

                    if (!corpKillCount.ContainsKey(atkCorp))
                        corpKillCount[atkCorp] = 0;

                    corpKillCount[atkCorp]++;

                    if (!corpDates.ContainsKey(atkCorp))
                        corpDates[atkCorp] = new HashSet<DateTime>();

                    corpDates[atkCorp].Add(date);

                    if (!corpKills.ContainsKey(atkCorp))
                        corpKills[atkCorp] = new List<ZKillTranslatedTop>();

                    corpKills[atkCorp].Add(k);

                    if (!corpAllianceMap.ContainsKey(atkCorp))
                        corpAllianceMap[atkCorp] = atkAlliance;
                }
            }

            var corpScores = new Dictionary<string, double>();

            foreach (var corp in corpDates.Keys)
            {
                int killsTotal = corpKillCount.ContainsKey(corp)
                    ? corpKillCount[corp]
                    : 0;

                int deathsTotal = corpDeathCount.ContainsKey(corp)
                    ? corpDeathCount[corp]
                    : 0;

                int activeDays = corpDates[corp].Count;

                int weeksActive = corpDates[corp]
                    .Select(d =>
                        System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                            d,
                            System.Globalization.CalendarWeekRule.FirstDay,
                            DayOfWeek.Monday))
                    .Distinct()
                    .Count();

                double score =
                    (activeDays * 30) +
                    (weeksActive * 15) +
                    (deathsTotal * 2) +
                    (killsTotal * 1);

                // NEW: NPC activity counts,
                // but much lower than player corps.
                if (corp.Equals("npc", StringComparison.OrdinalIgnoreCase))
                {
                    score *= npcWeightMultiplier;
                }

                if (NpcStarterCorps.Contains(corp))
                {
                    score *= 0.10;
                }

                corpScores[corp] = score;
            }

            // DEBUG: Uncomment while tuning algorithm

            foreach (var corp in corpScores
                .OrderByDescending(x => x.Value)
                .Take(10))
            {
                Console.WriteLine(
                    $"{corp.Key} | Score:{corp.Value}");
            }


            var topCorp = corpScores
                .OrderByDescending(x => x.Value)
                .First();

            var corpName = topCorp.Key;
            var scoreValue = topCorp.Value;

            string likelihood =
                scoreValue >= 200 ? "High likelihood" :
                scoreValue >= 100 ? "Medium likelihood" :
                "Low likelihood";

            // Determine winning alliance
            corpAllianceMap.TryGetValue(corpName, out var winningAlliance);

            var uniqueKills = corpKills[corpName]
                .GroupBy(k => k.KillmailId)
                .Select(g => g.First())
                .ToList();

            var alliedCorps = new List<string>();

            // If corp belongs to an alliance,
            // include alliance member activity.
            if (!string.IsNullOrWhiteSpace(winningAlliance) &&
                winningAlliance != "No Alliance")
            {
                alliedCorps = corpAllianceMap
                    .Where(x =>
                        x.Value == winningAlliance &&
                        x.Key != corpName)
                    .Select(x => x.Key)
                    .Distinct()
                    .ToList();

                foreach (var alliedCorp in alliedCorps)
                {
                    if (!corpKills.ContainsKey(alliedCorp))
                        continue;

                    uniqueKills.AddRange(corpKills[alliedCorp]);
                }

                uniqueKills = uniqueKills
                    .GroupBy(k => k.KillmailId)
                    .Select(g => g.First())
                    .ToList();
            }


            return new ActiveCorp
            {
                CorpName = $"{corpName} ({likelihood})",

                // If no alliance exists, return null
                AllianceName =
                    string.IsNullOrWhiteSpace(winningAlliance) ||
                    winningAlliance == "No Alliance"
                        ? null
                        : winningAlliance,

                AlliedCorps = alliedCorps,

                CorpKills = uniqueKills
            };
        }









        public async Task<ActiveCorp> GetActivePilot(ActiveCorp corp)
        {
            var pilotList = new List<string>();
            var cleanCorpName = corp.CorpName.Split(" (")[0];

            foreach (var k in corp.CorpKills)
            {
                if (k.Defender.CorpName == cleanCorpName)
                {
                    if (!pilotList.Contains(k.Defender.CharacterName))
                        pilotList.Add(k.Defender.CharacterName);
                }

                foreach (var atk in k.Attacker)
                {
                    if (atk.CorpName == cleanCorpName)
                    {
                        if (!pilotList.Contains(atk.CharacterName))
                            pilotList.Add(atk.CharacterName);
                    }
                }
            }

            corp.PilotNames = pilotList;
            return corp;
        }


        public async Task<string> GetActiveTime(ActiveCorp corp)
        {
            var kills = corp.CorpKills
                .OrderBy(k => k.KillmailTime)
                .ToList();

            if (kills.Count == 0)
                return "No activity";

            var window = TimeSpan.FromMinutes(10);

            int bestCount = 0;
            DateTime bestStart = DateTime.MinValue;

            for (int i = 0; i < kills.Count; i++)
            {
                var start = kills[i].KillmailTime;
                var end = start + window;

                int count = kills.Count(k =>
                    k.KillmailTime >= start &&
                    k.KillmailTime <= end);

                if (count > bestCount)
                {
                    bestCount = count;
                    bestStart = start;
                }
            }

            var bestEnd = bestStart + window;

            return $"{bestStart:t} - {bestEnd:t} ({bestCount} kills)";
        }

        public async Task<List<string>> GetShips(ActiveCorp corp)
        {
            var shipsList = new List<string>();
            var cleanCorpName = corp.CorpName.Split(" (")[0];

            foreach (var k in corp.CorpKills)
            {
                foreach (var atk in k.Attacker)
                {
                    if (atk.CorpName == cleanCorpName)
                    {
                        if (!shipsList.Contains(atk.ShipName))
                            shipsList.Add(atk.ShipName);
                    }
                }

                if (k.Defender.CorpName == cleanCorpName)
                {
                    if (!shipsList.Contains(k.Defender.ShipName))
                        shipsList.Add(k.Defender.ShipName);
                }
            }

            return shipsList;
        }


        public async Task<List<ZKillTranslatedTop>> GetZkillData(LocationResponse loc)
        {
           
            _http.DefaultRequestHeaders.Add("User-Agent", "EveThreat Intel Tool");

            var response = await _http.GetAsync(
                $"https://zkillboard.com/api/kills/solarSystemID/{loc.SolarSystemId}/"
            );

            var json = await response.Content.ReadAsStringAsync();

            var kills = JsonSerializer.Deserialize<List<ZKillData>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _cache.Location = loc.SolarSystemId;
            var t = kills.Where(x => x.Killmail_Time >= DateTime.UtcNow.AddMonths(-2));
            return await TranslateAllAsync(kills.Where(x => x.Killmail_Time >= DateTime.UtcNow.AddMonths(-4))
                .ToList());
            
            
        }


        public async Task<LocationResponse> GetLocationAsync()
        {
            var accessToken = await _auth.RefreshAccessTokenAsync();
                     
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var verifyResponse = await _http.GetAsync("https://login.eveonline.com/oauth/verify");
            var verifyJson = await verifyResponse.Content.ReadAsStringAsync();

            var verify = JsonSerializer.Deserialize<VerifyResponse>(verifyJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var characterId = verify.CharacterID;

            var url = $"https://esi.evetech.net/latest/characters/{characterId}/location/";
            var locResponse = await _http.GetAsync(url);
            var locJson = await locResponse.Content.ReadAsStringAsync();

            var location = JsonSerializer.Deserialize<LocationResponse>(locJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
           
                
            
           
            

            return location;
        }

        public async Task<List<ZKillTranslatedTop>> TranslateAllAsync(List<ZKillData> kills)
        {
            // Run system translation first (unchanged)
            var systemName = await TranslateSystemAsync(kills.FirstOrDefault().Solar_System_Id);

            // Prepare a list of tasks for parallel execution
            var tasks = kills.Select(async k =>
            {
                // Attacker translations (unchanged logic)
                var attackers = new List<ZKillTranslated>();
                foreach (var attacker in k.Attackers)
                {
                    var AttackerTranslated = new ZKillTranslated
                    {
                        CharacterName = await TranslateCharacterAsync(attacker.Character_Id),
                        CorpName = await TranslateCorpAsync(attacker.Corporation_Id),
                        AllianceName = await TranslateAllianceAsync(attacker.Alliance_Id),
                        ShipName = await TranslateShipAsync(attacker.Ship_Type_Id),
                    };

                    attackers.Add(AttackerTranslated);
                }

                // Defender translation (unchanged logic)
                var DefenderTranslated = new ZKillTranslated
                {
                    CharacterName = await TranslateCharacterAsync(k.Victim.Character_Id),
                    CorpName = await TranslateCorpAsync(k.Victim.Corporation_Id),
                    AllianceName = await TranslateAllianceAsync(k.Victim.Alliance_Id),
                    ShipName = await TranslateShipAsync(k.Victim.Ship_Type_Id),
                };

                // Build final translated kill (unchanged logic)
                return new ZKillTranslatedTop
                {
                    KillmailId = k.Killmail_Id,
                    KillmailTime = k.Killmail_Time,
                    SystemName = systemName,
                    Attacker = attackers,
                    Defender = DefenderTranslated,
                };
            });

            // Execute all kill translations in parallel
            return (await Task.WhenAll(tasks)).ToList();
        }

        private async Task<string?> TranslateCharacterAsync(long? id)
        {
            if (id == null)
                return null;

            if (_cache.CharacterCache.TryGetValue(id.Value, out var cached))
                return cached;

            var url = $"https://esi.evetech.net/latest/characters/{id}/";

            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return "Unknown";

            var json = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<CharacterResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var name = data?.Name ?? "Unknown";

            _cache.CharacterCache.TryAdd(id.Value, name);

            return name;
        }
        private async Task<string?> TranslateCorpAsync(long? id)
        {
            if (id == null)
                return null;

            if (_cache.CorpCache.TryGetValue(id.Value, out var cached))
                return cached;

            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/corporations/{id}/");

            var data = JsonSerializer.Deserialize<CharacterResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var name = data?.Name ?? "Unknown";

            _cache.CorpCache.TryAdd(id.Value, name);

            return name;
        }
        private async Task<string?> TranslateAllianceAsync(long? id)
        {
            if (id == null)
                return null;

            if (_cache.AllianceCache.TryGetValue(id.Value, out var cached))
                return cached;

            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/alliances/{id}/");

            var data = JsonSerializer.Deserialize<CharacterResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var name = data?.Name ?? "Unknown";

            _cache.AllianceCache.TryAdd(id.Value, name);

            return name;
        }
        private async Task<string> TranslateShipAsync(long? typeId)
        {
            if (typeId == null)
                return null;

            if (_cache.ShipCache.TryGetValue(typeId.Value, out var cached))
                return cached;

            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/universe/types/{typeId}/");

            var data = JsonSerializer.Deserialize<CharacterResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var name = data?.Name ?? "Unknown Ship";

            _cache.ShipCache.TryAdd(typeId.Value, name);

            return name;
        }
        private async Task<string> TranslateSystemAsync(long systemId)
        {
            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/universe/systems/{systemId}/"
            );

            var data = JsonSerializer.Deserialize<CharacterResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
);
            return data?.Name ?? "Unknown System";
        }



    }
}
