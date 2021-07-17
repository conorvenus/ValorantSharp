using System;
using System.Net;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json.Linq;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Auth;

namespace ValorantSharp.API
{
	public class ValorantAPI
	{
		private string _version;
		private string _playerId;
		private readonly string _pdBaseUrl;
		private readonly string _glzBaseUrl;
		private readonly string _riotAuthUrl;
		private readonly string _riotSharedUrl;
		private RestClient _client;
		private ValorantLogger _logger;
		private ValorantRegion _region;

		public ContractEndpoints Contracts;
		public CurrentGameEndpoints CurrentGame;
		public PvpEndpoints PVP;
		public PartyEndpoints Party;
		public PregameEndpoints Pregame;
		public StoreEndpoints Store;

		internal ValorantAPI(ValorantLogger logger, ValorantRegion region)
		{
			_logger = logger;
			_region = region;
			_pdBaseUrl = $"https://pd.{region.GLZShard}.a.pvp.net";
			_glzBaseUrl = $"https://glz-{region.GLZRegion}-1.{region.GLZShard}.a.pvp.net";
			_riotAuthUrl = "https://auth.riotgames.com";
			_riotSharedUrl = $"https://shared.{region.GLZRegion}.a.pvp.net";
		}

		internal async Task<string> GetVersionAsync()
        {
			if(_version is null)
            {
				var jObj = JObject.Parse((await _client.ExecuteAsync(new RestRequest("https://valorant-api.com/v1/version", Method.GET))).Content);
				_version = jObj["data"]["riotClientVersion"].ToString();
			}
			return _version;
        }

		internal async Task<string> GetXMPPTokenAsync()
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

		internal async Task<ValorantResult> AuthAsync(AuthConfig authConfig)
		{
			_logger.Debug("Attempting to Auth with REST...");
			_client = new RestClient();
			_client.CookieContainer = new CookieContainer();
			await GetVersionAsync();
			var result = await _client.ExecuteAsync(
				new RestRequest($"{_riotAuthUrl}/authorize?client_id=play-valorant-web-prod&response_type=token%20id_token&redirect_uri=https%3A%2F%2Fplayvalorant.com%2Fopt_in%2F%3Fredirect%3D%2Fdownload%2F",
								Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			result = await _client.ExecuteAsync(
				new RestRequest($"{_riotAuthUrl}/api/v1/authorization",
					Method.PUT,
					DataFormat.Json)
				.AddParameter("application/json", JObject.FromObject(authConfig), ParameterType.RequestBody));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			else if (result.IsSuccessful && result.Content.Contains("error"))
				return new ValorantResult() { Error = "Invalid credentials provided.", isSuccessful = false };
			string AuthResponseURI = JObject.Parse(result.Content)["response"]["parameters"]["uri"].ToString();
			string AccessToken = AuthResponseURI.Split("access_token=")[1].Split('&')[0];
			string IDToken = AuthResponseURI.Split("id_token=")[1].Split('&')[0];
			TimeSpan ExpiresIn = TimeSpan.FromSeconds(int.Parse(AuthResponseURI.Split("expires_in=")[1]));
			_client.AddDefaultHeader("Authorization", $"Bearer {AccessToken}");
			result = await _client.ExecuteAsync(new RestRequest($"https://entitlements.auth.riotgames.com/api/token/v1", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			string EntitlementsToken = JObject.Parse(result.Content)["entitlements_token"].ToString();
			_client.AddDefaultHeader("X-Riot-Entitlements-JWT", EntitlementsToken);
			_client.AddDefaultHeader("X-Riot-ClientVersion", _version);
			string XMPPToken = await GetXMPPTokenAsync();
			await GetPlayerIdAsync();
			Contracts = new ContractEndpoints(_client, _pdBaseUrl);
			CurrentGame = new CurrentGameEndpoints(_client, _glzBaseUrl);
			PVP = new PvpEndpoints(_client, _pdBaseUrl, _riotSharedUrl, _region.GLZRegion);
			Party = new PartyEndpoints(_client, _glzBaseUrl);
			Pregame = new PregameEndpoints(_client, _glzBaseUrl);
			Store = new StoreEndpoints(_client, _pdBaseUrl);
			return new ValorantResult() { isSuccessful = true, Data = new AuthResponse() { AccessToken = AccessToken, EntitlementsJWT = EntitlementsToken, ExpiresIn = ExpiresIn, IdToken = IDToken, XMPPToken = XMPPToken } };
		}

		public async Task<string> GetPlayerIdAsync()
		{
			if (_playerId is null)
			{
				IRestResponse PlayerIDResponse = await _client.ExecuteAsync(new RestRequest($"{_riotAuthUrl}/userinfo", Method.GET));
				_playerId = JObject.Parse(PlayerIDResponse.Content)["sub"].ToString();
			}
			return _playerId;
		}
	}

	public class ContractEndpoints
    {
		private string _pdBaseUrl;
		private RestClient _client;

		internal ContractEndpoints(RestClient client, string pdBaseUrl)
        {
			_client = client;
			_pdBaseUrl = pdBaseUrl;
        }

		public async Task<ValorantResult> FetchContractDefinitionsAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/contract-definitions/v2/definitions", Method.GET)); https://github.com/techchrism/valorant-api-docs/tree/trunk/docs/PVP%20Endpoints
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchContractsAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/contracts/v1/contracts/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> ActivateContractAsync(string playerId, string contractId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/contracts/v1/contracts/{playerId}/special/{contractId}", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchActiveStoryAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/contract-definitions/v2/definitions/story", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchItemUpgradesAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/contract-definitions/v3/item-upgrades", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}

	public class CurrentGameEndpoints
    {
		private string _glzBaseUrl;
		private RestClient _client;

		internal CurrentGameEndpoints(RestClient client, string glzBaseUrl)
		{
			_client = client;
			_glzBaseUrl = glzBaseUrl;
		}

		public async Task<ValorantResult> FetchActiveMatchIdAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/core-game/v1/players/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchActiveMatchAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/core-game/v1/matches/{matchId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchMatchLoadoutsAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/core-game/v1/matches/{matchId}/loadouts", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchTeamChatMUCTokenAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/core-game/v1/matches/{matchId}/teamchatmuctoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchAllChatMUCTokenAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/core-game/v1/matches/{matchId}/allchatmuctoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> LeaveMatchAsync(string playerId, string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/core-game/v1/players/{playerId}/disassociate/{matchId}", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}

	public class PvpEndpoints
    {
		private string _region;
		private string _pdBaseUrl;
		private string _riotSharedUrl;
		private RestClient _client;

		internal PvpEndpoints(RestClient client, string pdBaseUrl, string riotSharedUrl, string region)
		{
			_client = client;
			_pdBaseUrl = pdBaseUrl;
			_riotSharedUrl = riotSharedUrl;
			_region = region;
		}

		public async Task<ValorantResult> FetchGameContentAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_riotSharedUrl}/content-service/v2/content", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchAccountXPAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/account-xp/v1/players/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPlayerLoadoutAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/personalization/v2/players/{playerId}/playerloadout", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPlayerMMRAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/mmr/v1/players/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchMatchHistoryAsync(string playerId, int startIndex, int endIndex, string queue = null)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/match-history/v1/history/{playerId}?startIndex={startIndex.ToString()}&endIndex={endIndex.ToString()}&queue={queue}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchMatchDetailsAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/match-details/v1/matches/{matchId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchCompetitiveUpdatesAsync(string playerId, int startIndex, int endIndex, string queue = null)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/mmr/v1/players/{playerId}/competitiveupdates", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchLeaderboardAsync(string seasonId, int startIndex = 0, int size = 510)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/mmr/v1/leaderboards/affinity/{_region}/queue/competitive/season/{seasonId}?startIndex={startIndex}&size={size}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPlayerRestrictionsAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/restrictions/v2/penalties", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchConfigAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_riotSharedUrl}/v1/config/{_region}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}

	public class PartyEndpoints
    {
		private string _glzBaseUrl;
		private RestClient _client;

		internal PartyEndpoints(RestClient client, string glzBaseUrl)
		{
			_client = client;
			_glzBaseUrl = glzBaseUrl;
		}

		public async Task<ValorantResult> FetchPartyIdAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/players/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> KickPlayerFromPartyAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/players/{playerId}", Method.DELETE));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPartyAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}", Method.DELETE));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> SetMemberReadyAsync(string playerId, string partyId, bool ready)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/members/{playerId}/setReady", Method.POST).AddJsonBody($"{{\"ready\":{ready}}}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> ChangeQueueAsync(string partyId, string queueId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/queue", Method.POST).AddJsonBody($"{{\"queueID\":\"{queueId}\"}}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> StartCustomGameAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/startcustomgame", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> EnterMatchmakingQueueAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/matchmaking/join", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> LeaveMatchmakingQueueAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/matchmaking/leave", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> SetPartyAccessibilityAsync(string partyId, string accessibility)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/accessibility", Method.POST).AddJsonBody($"{{\"accessibility\":\"{accessibility}\"}}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> SetCustomGameSettingsAsync(string partyId, string gamePod, string map = "Ascent", string mode = "/Game/GameModes/Bomb/BombGameMode.BombGameMode_C", bool allowGameModifiers = false, bool playOutAllRounds = true, bool skipMatchHistory = false, bool tournamentMode = false, bool isOvertimeWinByTwo = true)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/customgamesettings", Method.POST).AddJsonBody($"{{\"Map\":\"/Game/Maps/{map}/{map}\",\"Mode\":\"{mode}\",\"GamePod\":\"{gamePod}\",\"GameRules\":{{\"AllowGameModifiers\":\"{allowGameModifiers}\",\"PlayOutAllRounds\":\"{playOutAllRounds}\",\"SkipMatchHistory\":\"{skipMatchHistory}\",\"TournamentMode\":\"{tournamentMode}\",\"IsOvertimeWinByTwo\":\"{isOvertimeWinByTwo}\"}}}}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> InviteToPartyAsync(string partyId, string name, string tag)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/invites/name/{name}/tag/{tag}", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> RequestToJoinPartyAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/request", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> DeclineRequestToJoinPartyAsync(string partyId, string requestId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/request/{requestId}/decline", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchCustomGameConfigsAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/customgameconfigs", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPartyMUCTokenAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/muctoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPartyVoiceTokenAsync(string partyId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/parties/v1/parties/{partyId}/voicetoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}

	public class PregameEndpoints
    {
		private string _glzBaseUrl;
		private RestClient _client;

		internal PregameEndpoints(RestClient client, string glzBaseUrl)
		{
			_client = client;
			_glzBaseUrl = glzBaseUrl;
		}

		public async Task<ValorantResult> FetchPregameIdAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/players/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPregameMatchAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/matches/{matchId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPregameMatchLoadoutsAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/matches/{matchId}/loadouts", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPregameChatTokenAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/matches/{matchId}/chattoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchPregameVoiceTokenAsync(string matchId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/matches/{matchId}/voicetoken", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> PregameSelectAgentAsync(string matchId, uint agentId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/matches/{matchId}/select/{agentId}", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> PregameLockAgentAsync(string matchId, uint agentId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_glzBaseUrl}/pregame/v1/matches/{matchId}/lock/{agentId}", Method.POST).AddJsonBody("{}"));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}

	public class StoreEndpoints
    {
		private string _pdBaseUrl;
		private RestClient _client;

		internal StoreEndpoints(RestClient client, string pdBaseUrl)
		{
			_client = client;
			_pdBaseUrl = pdBaseUrl;
		}

		public async Task<ValorantResult> FetchStoreOffersAsync()
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/store/v1/offers/", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchStorefrontAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/store/v2/storefront/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchWalletAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/store/v1/wallet/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchOrderAsync(string orderId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/store/v1/order/{orderId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
		public async Task<ValorantResult> FetchEntitlementsAsync(string playerId)
		{
			var result = await _client.ExecuteAsync(new RestRequest($"{_pdBaseUrl}/store/v1/entitlements/{playerId}", Method.GET));
			if (!result.IsSuccessful)
				return new ValorantResult() { Error = result.Content, isSuccessful = false };
			return new ValorantResult() { isSuccessful = true, Data = result.Content };
		}
	}
}
