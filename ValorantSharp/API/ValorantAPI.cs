using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ValorantSharp.Enums;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Auth;

namespace ValorantSharp.API
{
	internal class ValorantAPI
	{
		private RestClient _client;
		private readonly string RiotAuthURL = "https://auth.riotgames.com/";
		private readonly string GLZBaseURL;
		private ValorantLogger _logger;
		private ValorantRegion _region;

		internal ValorantAPI(ValorantLogger logger, ValorantRegion region)
		{
			_logger = logger;
			_region = region;
			GLZBaseURL = $"https://glz-{region.GLZRegion}-1.{region.GLZRegion}.a.pvp.net/";
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

		public async Task<string> GetPlayerIdAsync()
		{
			IRestResponse PlayerIDResponse = await _client.ExecuteAsync(new RestRequest($"{RiotAuthURL}/userinfo", Method.GET));
			return JObject.Parse(PlayerIDResponse.Content)["sub"].ToString();
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
			return new ValorantResult() { isSuccessful = true, Data = new AuthResponse() { AccessToken = AccessToken, EntitlementsJWT = EntitlementsToken, ExpiresIn = ExpiresIn, IdToken = IDToken, XMPPToken = XMPPToken } };
		}
	}
}
