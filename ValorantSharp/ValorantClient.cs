using System;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using Qmmands;
using Newtonsoft.Json;
using ValorantSharp.API;
using ValorantSharp.XMPP;
using ValorantSharp.Enums;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Auth;
using ValorantSharp.Objects.Game;
using ValorantSharp.Exceptions;

namespace ValorantSharp
{
	public class ValorantClient : IAsyncDisposable
	{
		internal AuthConfig authConfig;
		internal ValorantRegion region;
		internal string prefix;

		private readonly ValorantLogger _logger;
		private readonly ValorantAPI _apiClient;
		private readonly ValorantXMPP _xmppClient;
		private readonly CommandService _service = new CommandService();

		public List<ValorantFriend> Friends { get; internal set; }

		/// <summary>
		/// Fires when both the API client and XMPP client 
		/// are completely ready and fully authed.
		/// </summary>
		public event Func<AuthResponse, Task> Ready;

		/// <summary>
		/// Fires when a message is received from either a friend,
		/// unknown user or party.
		/// </summary>
		public event Func<ValorantMessage, Task> MessageReceived;

		/// <summary>
		/// Fires when an initial or updated presence
		/// is sent to the client.
		/// </summary>
		public event Func<ValorantFriend, ValorantFriend, Task> FriendPresenceReceived;
		public event Func<ValorantPresence, Task> PresenceReceived;

		/// <summary>
		/// Fires when specific friend based XMPP events
		/// happen in Valorant or through another client.
		/// </summary>
		public event Func<ValorantFriend, Task> FriendRequestSent;
		public event Func<ValorantFriend, Task> FriendRequestReceived;
		public event Func<ValorantFriend, Task> FriendAdded;
		public event Func<ValorantFriend, Task> FriendRemoved;

		internal ValorantClient(AuthConfig _authConfig, ValorantRegion _region, ValorantLogLevel _logLevel, string _prefix, string _datetimeFormat = "yyyy-MM-dd HH:mm:ss")
		{
			authConfig = _authConfig;
			region = _region;
			prefix = _prefix;

			_logger = new ValorantLogger(_logLevel, _datetimeFormat);

			_apiClient = new ValorantAPI(_logger, _region);
			_xmppClient = new ValorantXMPP(this, _logger, region, Friends);
		}

		public ValorantAPI GetAPIClient() { return _apiClient; }

		public void AddModules(Assembly assembly)
		{
			_logger.Debug("Attempting to add Valorant command modules...");
			var modules = _service.AddModules(assembly: assembly);
			foreach (var module in modules)
			{
				_logger.Debug($"Added {module.Name} command module.");
			}
			if (modules.Count > 0)
			{
				_logger.Info($"[COMMANDS_READY] The Valorant command services are now available for use.");
				return;
			}
			_logger.Debug($"No Valorant command modules were found.");
		}

		internal async Task WriteXMLAsync(XElement xml) => await _xmppClient._client.WriteXMLAsync(xml);

		internal async Task OnMessageReceived(ValorantMessage message)
		{
			_logger.Event($"[MESSAGE_RECEIVED]");
			if (MessageReceived != null) await MessageReceived?.Invoke(message);

			if (!CommandUtilities.HasPrefix(message.Content, prefix, out string output))
				return;

			IResult result = await _service.ExecuteAsync(output, new ValorantCommandContext(message, Friends.FirstOrDefault(friend => friend.jid == message.JID), this));
		}
		internal async Task OnFriendRequestReceived(ValorantFriend friend)
		{
			_logger.Event($"[FRIEND_REQUEST_RECEIVED]");
			if (FriendRequestReceived != null) await FriendRequestReceived?.Invoke(friend);
		}
		internal async Task OnFriendRequestSent(ValorantFriend friend)
		{
			_logger.Event($"[FRIEND_REQUEST_SENT]");
			if (FriendRequestSent != null) await FriendRequestSent?.Invoke(friend);
		}
		internal async Task OnFriendAdded(ValorantFriend friend)
		{
			_logger.Event($"[FRIEND_ADDED]");
			if (FriendAdded != null) await FriendAdded?.Invoke(friend);
		}
		internal async Task OnFriendRemoved(ValorantFriend friend)
		{
			_logger.Event($"[FRIEND_REMOVED]");
			if (FriendRemoved != null) await FriendRemoved?.Invoke(friend);
		}
		internal async Task OnFriendPresenceReceived(ValorantFriend oldFriend, ValorantFriend newFriend)
		{
			_logger.Event($"[FRIEND_PRESENCE_RECEIVED]");
			if (FriendPresenceReceived != null) await FriendPresenceReceived?.Invoke(oldFriend, newFriend);
		}
		internal async Task OnPresenceReceived(ValorantPresence presence)
		{
			_logger.Event($"[PRESENCE_RECEIVED]");
			if (PresenceReceived != null) await PresenceReceived?.Invoke(presence);
		}

		public async Task LoginAsync()
		{
			var response = await _apiClient
				.AuthAsync(authConfig)
				.ConfigureAwait(false);

			if (!response.isSuccessful)
			{
				_logger.Error(response.Error);
				throw new ValorantException(response.Error);
			}

			var _authResponse = (AuthResponse)response.Data;

			_logger.Debug("Successfully Authed with REST.");
			_logger.Info($"[REST_READY] The Valorant REST API is now available for use.");

			response = await _xmppClient
				.AuthAsync(_authResponse)
				.ConfigureAwait(false);

			if (!response.isSuccessful)
			{
				_logger.Error(response.Error);
				throw new ValorantException(response.Error);
			}

			_logger.Debug("Successfully Authed with XMPP.");
			_logger.Info($"[XMPP_READY] The Valorant XMPP services are now available for use.");

			await SendPresenceAsync(new ValorantPresence()
			{
				partySize = 2,
				queueId = "ValorantBot",
				sessionLoopState = "INGAME",
				partyOwnerSessionLoopState = "INGAME",
				partyOwnerMatchScoreAllyTeam = 24,
				partyOwnerMatchScoreEnemyTeam = 7,
				accountLevel = 9999,
				playerCardId = "9fb348bc-41a0-91ad-8a3e-818035c4e561"
			});

			if (Ready != null)
			{
				await Ready.Invoke(_authResponse);
				_logger.Event($"[READY] Logged in as: {(response.Data as ValorantUser).Name}#{(response.Data as ValorantUser).Tagline}");
			}
		}

		public async Task SendFriendRequestAsync(string name, string tagline)
		{
			await WriteXMLAsync(new XElement("iq", new XAttribute("type", "set"), new XAttribute("id", "roster_add_10"),
									new XElement((XNamespace)"jabber:iq:riotgames:roster" + "query",
										new XElement("item", new XAttribute("subscription", "pending_out"),
											new XElement("id", new XAttribute("name", name), new XAttribute("tagline", tagline))))));
		}

		public async Task SendFriendRequestAsync(string playerId)
		{
			await WriteXMLAsync(new XElement("iq", new XAttribute("type", "set"), new XAttribute("id", "roster_add_10"),
									new XElement((XNamespace)"jabber:iq:riotgames:roster" + "query",
										new XElement("item", new XAttribute("subscription", "pending_out"), new XAttribute("puuid", playerId)))));
		}

		public async Task SendPresenceAsync(ValorantPresence presence)
		{
			presence.partyClientVersion = await _apiClient.GetVersionAsync();
			string presenceString = JsonConvert.SerializeObject(presence, Formatting.Indented);
			string encodedPresence = Convert.ToBase64String(Encoding.UTF8.GetBytes(presenceString));
			await WriteXMLAsync(new XElement("presence", new XAttribute("id", $"presence_1"),
								new XElement("show", "chat"),
								new XElement("status"),
								new XElement("games",
									new XElement("valorant",
										new XElement("st", "chat"),
										new XElement("p", encodedPresence),
										new XElement("s.p", "valorant")))));
		}

		public async ValueTask DisposeAsync()
		{
		}
	}
}
