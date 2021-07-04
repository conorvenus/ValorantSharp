using System;
using System.Collections.Generic;
using System.Text;

using J = Newtonsoft.Json.JsonPropertyAttribute;

namespace ValorantSharp.Objects.Auth
{
	internal class AuthConfig
	{
		[J("type")] public string Type { get; } = "auth";
		[J("username")] public string Username { get; set; }
		[J("password")] public string Password { get; set; }
	}
}
