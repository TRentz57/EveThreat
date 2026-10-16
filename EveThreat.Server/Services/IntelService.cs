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
               var zkilldata =  await GetZkillData(loc);
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
                Kills = kills,
                ActiveCorp = activeCorp.CorpName,
                ActivePilots = activePilots.PilotNames,
                ActiveTime = await GetActiveTime(activeCorp),
                PopularShips = await GetShips(activeCorp),
            };
            var t= result;
            return t;
        }

        public async Task<ActiveCorp> GetActiveCorp(List<ZKillTranslatedTop> kills)
        {
            var corpKillCount = new Dictionary<string, int>();
            var corpDeathCount = new Dictionary<string, int>();
            var corpDates = new Dictionary<string, HashSet<DateTime>>();
            var corpKills = new Dictionary<string, List<ZKillTranslatedTop>>();

            foreach (var k in kills)
            {
                var date = k.KillmailTime.Date;

                // Defender = DEATH (presence)
                var defCorp = k.Defender.CorpName ?? "npc";

                if (!corpDeathCount.ContainsKey(defCorp))
                    corpDeathCount[defCorp] = 0;

                corpDeathCount[defCorp]++;

                if (!corpDates.ContainsKey(defCorp))
                    corpDates[defCorp] = new HashSet<DateTime>();

                corpDates[defCorp].Add(date);

                if (!corpKills.ContainsKey(defCorp))
                    corpKills[defCorp] = new List<ZKillTranslatedTop>();

                corpKills[defCorp].Add(k);

                // Attackers = KILL (activity)
                foreach (var atk in k.Attacker)
                {
                    var atkCorp = atk.CorpName ?? "npc";

                    if (!corpKillCount.ContainsKey(atkCorp))
                        corpKillCount[atkCorp] = 0;

                    corpKillCount[atkCorp]++;

                    if (!corpDates.ContainsKey(atkCorp))
                        corpDates[atkCorp] = new HashSet<DateTime>();

                    corpDates[atkCorp].Add(date);

                    if (!corpKills.ContainsKey(atkCorp))
                        corpKills[atkCorp] = new List<ZKillTranslatedTop>();

                    corpKills[atkCorp].Add(k);
                }
            }

            // Compute consistency-first scores
            var corpScores = new Dictionary<string, double>();

            foreach (var corp in corpDates.Keys)
            {
                int killsTotal = corpKillCount.ContainsKey(corp) ? corpKillCount[corp] : 0;
                int deathsTotal = corpDeathCount.ContainsKey(corp) ? corpDeathCount[corp] : 0;
                int activeDays = corpDates[corp].Count;

                int weeksActive = corpDates[corp]
                    .Select(d => System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
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

                corpScores[corp] = score;
            }

            // Pick top corp
            var topCorp = corpScores.OrderByDescending(x => x.Value).First();
            var corpName = topCorp.Key;
            var scoreValue = topCorp.Value;

            // Likelihood tier
            string likelihood =
                scoreValue >= 200 ? "High likelihood" :
                scoreValue >= 100 ? "Medium likelihood" :
                "Low likelihood";

            // Unique kills for this corp
            var uniqueKills = corpKills[corpName]
                .GroupBy(k => k.KillmailId)
                .Select(g => g.First())
                .ToList();

            return new ActiveCorp
            {
                CorpName = $"{corpName} ({likelihood})",
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
            return await TranslateAllAsync(kills.Where(x => x.Killmail_Time >= DateTime.UtcNow.AddMonths(-2))
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
            if (id == null) return null;

            var url = $"https://esi.evetech.net/latest/characters/{id}/";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return "Unknown";

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<CharacterResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
);

            return data?.Name ?? "Unknown";
        }
        private async Task<string?> TranslateCorpAsync(long? id)
        {
            if (id == null) return null;

            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/corporations/{id}/"
            );

            var data = JsonSerializer.Deserialize<CharacterResponse>(json,
                 new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
 );
            return data?.Name ?? "Unknown";
        }
        private async Task<string?> TranslateAllianceAsync(long? id)
        {
            if (id == null) return null;

            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/alliances/{id}/"
            );

            var data = JsonSerializer.Deserialize<CharacterResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
);
            return data?.Name ?? "Unknown";
        }
        private async Task<string> TranslateShipAsync(long? typeId)
        {
            if (typeId == null) return null;
            var json = await _http.GetStringAsync(
                $"https://esi.evetech.net/latest/universe/types/{typeId}/"
            );
           
            var data = JsonSerializer.Deserialize<CharacterResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                return data?.Name ?? "Unknown Ship";

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
