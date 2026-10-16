using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EveThreat.Server.IServices;
using EveThreat.Server.Models;
using Microsoft.Extensions.Configuration;

namespace EveThreat.Server.Services
{
 

    public class EveAuthService : IEveAuthService
    {
        private readonly IConfiguration _config;

        public EveAuthService(IConfiguration config)
        {
            _config = config;
        }

        public string BuildLoginUrl()
        {
            var clientId = _config["Eve:ClientId"];
            var callback = _config["Eve:CallbackUrl"];

            var state = Guid.NewGuid().ToString();
            StateStore.LastState = state;

            return "https://login.eveonline.com/v2/oauth/authorize" +
                   "?response_type=code" +
                   $"&redirect_uri={callback}" +
                   $"&client_id={clientId}" +
                   "&scope=esi-location.read_location.v1" +
                   $"&state={state}";
        }

        public async Task ProcessCallbackAsync(string code, string state)
        {
            if (state != StateStore.LastState)
                throw new Exception("Invalid state");

            var clientId = _config["Eve:ClientId"];
            var secret = _config["Eve:SecretKey"];
            var callback = _config["Eve:CallbackUrl"];

            using var client = new HttpClient();

            var tokenUrl = "https://login.eveonline.com/v2/oauth/token";

            var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{secret}");
            var authHeader = Convert.ToBase64String(authBytes);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authHeader);

            var body = new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "code", code },
        { "redirect_uri", callback }
    };

            var content = new FormUrlEncodedContent(body);

            var response = await client.PostAsync(tokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Token exchange failed: {json}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tokens = JsonSerializer.Deserialize<EveTokens>(json, options);
            Console.WriteLine($"DESERIALIZED: access={tokens?.AccessToken}, refresh={tokens?.RefreshToken}");

            if (tokens == null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
                throw new Exception("Token response missing refresh token.");

            await StoreRefreshToken(tokens.RefreshToken);
        }


        public async Task<string> RefreshAccessTokenAsync()
        {
            var clientId = _config["Eve:ClientId"];
            var secret = _config["Eve:SecretKey"];
            var refreshToken = await LoadRefreshToken();

            using var client = new HttpClient();

            var tokenUrl = "https://login.eveonline.com/v2/oauth/token";

            var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{secret}");
            var authHeader = Convert.ToBase64String(authBytes);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authHeader);

            var body = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            };

            var content = new FormUrlEncodedContent(body);

            var response = await client.PostAsync(tokenUrl, content);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Refresh failed: {json}");

            var tokens = JsonSerializer.Deserialize<EveTokens>(json);

            if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
                throw new Exception("Failed to refresh access token.");

            return tokens.AccessToken;
        }

        private Task StoreRefreshToken(string refreshToken)
        {
            // TODO: replace with DB / secure store
            File.WriteAllText("refresh.txt", refreshToken);
            return Task.CompletedTask;
        }

        private Task<string> LoadRefreshToken()
        {
            if (!File.Exists("refresh.txt"))
                throw new Exception("No stored refresh token.");

            var token = File.ReadAllText("refresh.txt");
            return Task.FromResult(token);
        }
    }

}
