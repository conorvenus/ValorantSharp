using System.Collections.Generic;
using ValorantSharp.Enums;
using ValorantSharp.Exceptions;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Auth;

namespace ValorantSharp
{
	public class ValorantClientBuilder
	{
		private AuthConfig _authConfig;
		private ValorantRegion _region;
		private string _prefix = "!";

		private ValorantLogLevel logLevel = ValorantLogLevel.Error;
		private string datetimeFormat = "yyyy-MM-dd HH:mm:ss";

		public ValorantClientBuilder WithCredentials(string username, string password)
		{
			_authConfig = new AuthConfig()
			{
				Username = username,
				Password = password
			};
			return this;
		}

		public ValorantClientBuilder WithCommandsPrefix(string prefix)
		{
			_prefix = prefix;
			return this;
		}

		public ValorantClientBuilder WithLoggerConfig(ValorantLogLevel _logLevel, string _datetimeFormat = "yyyy-MM-dd HH:mm:ss")
		{
			logLevel = _logLevel;
			datetimeFormat = _datetimeFormat;
			return this;
		}

		public ValorantClientBuilder WithRegion(ValorantGLZRegion glz, ValorantXMPPRegion xmpp)
		{
			string glzRegion = glz.ToString().ToLower();
			string glzShard = (glzRegion == "latam" || glzRegion == "br") ? "na" : glzRegion;
			Dictionary<string, string> xmppRegionDicts = new Dictionary<string, string>() {
				{ "as2", "as2" },
				{ "br1", "br" },
				{ "eu1", "euw1" },
				{ "eu2", "eun1" },
				{ "eu3", "eu3" },
				{ "jp1", "jp1" },
				{ "kr1", "kr1" },
				{ "la1", "la1" },
				{ "la2", "la2" },
				{ "na1", "na2" },
				{ "oc1", "oc1" },
				{ "pb1", "pbe1" },
				{ "ru1", "ru1" },
				{ "sa1", "sa1" },
				{ "sa2", "sa2" },
				{ "sa3", "sa3" },
				{ "sa4", "sa4" },
				{ "tr1", "tr1" },
				{ "us2", "us2" },
			};
			string xmppAuthRegion = xmpp.ToString().ToLower().Replace("usbr1", "us-br1").Replace("usla2", "us-la2");
			string xmppRegion = xmppRegionDicts[xmppAuthRegion];
			_region = new ValorantRegion() { GLZRegion = glzRegion, GLZShard = glzShard, XMPPRegion = xmppRegion, XMPPAuthRegion = xmppAuthRegion };
			return this;
		}

		public ValorantClient Build()
		{
			if (_authConfig is null || _region is null)
				throw new ValorantException("Please ensure you provide the client builder with valid credentials.");

			return new ValorantClient(
				_authConfig,
				(ValorantRegion)_region,
				logLevel,
				_prefix,
				datetimeFormat
			);
		}
	}
}
