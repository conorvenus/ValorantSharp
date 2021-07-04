using System;
using System.Collections.Generic;
using System.Text;
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

		public ValorantClientBuilder WithRegion(string glz, string xmpp, string xmppAuth)
		{
			_region = new ValorantRegion() { GLZRegion = glz, XMPPRegion = xmpp, XMPPAuthRegion = xmppAuth };
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
