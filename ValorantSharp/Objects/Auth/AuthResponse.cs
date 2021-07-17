using System;

using J = Newtonsoft.Json.JsonPropertyAttribute;

namespace ValorantSharp.Objects.Auth
{
	public class AuthResponse
	{
		public string AccessToken { get; set; }
		public string IdToken { get; set; }
		public string EntitlementsJWT { get; set; }
		public string XMPPToken { get; set; }
		public TimeSpan ExpiresIn { get; set; }
	}
}
