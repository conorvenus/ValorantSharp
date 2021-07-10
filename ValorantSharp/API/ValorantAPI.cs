using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ValorantSharp;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Auth;

namespace ValorantSharp.API
{
	internal class ValorantAPI
	{
		private RestClient _client;
		private string _playerId;
		private readonly string PDBaseURL;
		private readonly string GLZBaseURL;
		private readonly string RiotAuthURL;
		private readonly string RiotSharedURL;
		private ValorantLogger _logger;
		private ValorantRegion _region;

		internal ValorantAPI(ValorantLogger logger, ValorantRegion region)
		{
			_logger = logger;
			_region = region;
			PDBaseURL = $"https://pd.{region.GLZShard}.a.pvp.net";
			GLZBaseURL = $"https://glz-{region.GLZRegion}-1.{region.GLZShard}.a.pvp.net";
			RiotAuthURL = "https://auth.riotgames.com";
			RiotSharedURL = $"https://shared.{region.GLZRegion}.a.pvp.net";
		}

		private async Task<string> GetXMPPTokenAsync()
		{
			IRestResponse PASResponse = await _client.ExecuteAsync(new RestRequest($"https://riot-geo.pas.si.riotgames.com/pas/v1/service/chat", Method.GET));
			_logger.Debug("Requesting XMPP token.");
			try
			{
				return PASResponse.Content;
			}
			catch
			{
				return null;
			}
		}

		public async Task GetPlayerIdAsync()
		{
			IRestResponse PlayerIDResponse = await _client.ExecuteAsync(new RestRequest($"{RiotAuthURL}/userinfo", Method.GET));
			_playerId = JObject.Parse(PlayerIDResponse.Content)["sub"].ToString();
		}

		public async Task<ValorantResult> AuthAsync(AuthConfig authConfig)
		{
			_logger.Debug("Attempting to Auth with REST...");
			_client = new RestClient();
			_client.CookieContainer = new CookieContainer();
			var result = await _client.ExecuteAsync(
				new RestRequest($"{RiotAuthURL}/authorize?client_id=play-valorant-web-prod&response_type=token%20id_token&redirect_uri=https%3A%2F%2Fplayvalorant.com%2Fopt_in%2F%3Fredirect%3D%2Fdownload%2F",
								Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			result = await _client.ExecuteAsync(
				new RestRequest($"{RiotAuthURL}/api/v1/authorization",
					Method.PUT,
					DataFormat.Json)
				.AddParameter("application/json", JObject.FromObject(authConfig), ParameterType.RequestBody));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			else if (result.IsSuccessful && result.Content.Contains("error"))
				return new ValorantResult() { Error = "Invalid credentials provided.", isSuccessful = false };
			string AuthResponseURI = JObject.Parse(result.Content)["response"]["parameters"]["uri"].ToString();
			string AccessToken = AuthResponseURI.Split("access_token=")[1].Split('&')[0];
			string IDToken = AuthResponseURI.Split("id_token=")[1].Split('&')[0];
			TimeSpan ExpiresIn = TimeSpan.FromSeconds(int.Parse(AuthResponseURI.Split("expires_in=")[1]));
			_client.AddDefaultHeader("Authorization", $"Bearer {AccessToken}");
			result = await _client.ExecuteAsync(new RestRequest($"https://entitlements.auth.riotgames.com/api/token/v1", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			string EntitlementsToken = JObject.Parse(result.Content)["entitlements_token"].ToString();
			_client.AddDefaultHeader("X-Riot-Entitlements-JWT", EntitlementsToken);
			_client.AddDefaultHeader("X-Riot-ClientVersion", "release-02.11-shipping-9-567060");
			string XMPPToken = await GetXMPPTokenAsync();
			await GetPlayerIdAsync();
			return new ValorantResult() { isSuccessful = true, Data = new AuthResponse() { AccessToken = AccessToken, EntitlementsJWT = EntitlementsToken, ExpiresIn = ExpiresIn, IdToken = IDToken, XMPPToken = XMPPToken } };
		}

		/*
		 Contracts
		*/
		public async Task<ValorantResult> FetchContractDefinitions()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/contract-definitions/v2/definitions", Method.GET)); https://github.com/techchrism/valorant-api-docs/tree/trunk/docs/PVP%20Endpoints
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchContracts()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/contracts/v1/contracts/{_playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> ActivateContract(string contractId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/contracts/v1/contracts/{_playerId}/special/{contractId}", Method.POST));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchActiveStory()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/contract-definitions/v2/definitions/story", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchItemUpgrades()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/contract-definitions/v3/item-upgrades", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}

		/*
		 Current Game
		*/
		public async Task<ValorantResult> FetchActiveMatchId()
        {
			var result = await _client.ExecuteAsync(new RestRequest($"{GLZBaseURL}/core-game/v1/players/{_playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchActiveMatch(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{GLZBaseURL}/core-game/v1/matches/{matchId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchMatchLoadouts(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{GLZBaseURL}/core-game/v1/matches/{matchId}/loadouts", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchTeamChatMUCToken(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{GLZBaseURL}/core-game/v1/matches/{matchId}/teamchatmuctoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchAllChatMUCToken(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{GLZBaseURL}/core-game/v1/matches/{matchId}/allchatmuctoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> LeaveMatch(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{GLZBaseURL}/core-game/v1/players/{_playerId}/disassociate/{matchId}", Method.POST));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}

		/*
		 PVP
		*/
		public async Task<ValorantResult> FetchGameContent()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{RiotSharedURL}/content-service/v2/content", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchAccountXP()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/account-xp/v1/players/{_playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPlayerLoadout()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/personalization/v2/players/{_playerId}/playerloadout", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPlayerMMR()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/mmr/v1/players/{_playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchMatchHistory(int startIndex, int endIndex, string queue = null)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/match-history/v1/history/{_playerId}?startIndex={startIndex.ToString()}&endIndex={endIndex.ToString()}&queue={queue}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchMatchDetails(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/match-details/v1/matches/{matchId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchCompetitiveUpdates(int startIndex, int endIndex, string queue = null)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/mmr/v1/players/{_playerId}/competitiveupdates", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchLeaderboard(string seasonId, int startIndex = 0, int size = 510)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/mmr/v1/leaderboards/affinity/{_region.GLZRegion}/queue/competitive/season/{seasonId}?startIndex={startIndex}&size={size}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPlayerRestrictions()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{PDBaseURL}/restrictions/v2/penalties", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchConfig()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{RiotSharedURL}/v1/config/{_region.GLZRegion}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.ErrorException.Message, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}
}
