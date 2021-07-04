using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ValorantSharp.Enums;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Auth;
using ValorantSharp.Objects.Game;

namespace ValorantSharp.XMPP
{
	internal class ValorantXMPP
	{
		internal XMPPClient _client;
		private ValorantClient _valClient;
		private string _jid;
		private ValorantRegion _region;

		private ValorantLogger _logger;

		public ValorantUser User = new ValorantUser();

		internal ValorantXMPP(ValorantClient client, ValorantLogger logger, ValorantRegion region, List<ValorantFriend> friends)
		{
			_logger = logger;
			_region = region;
			_client = new XMPPClient(region, client);
			_valClient = client;
		}

		internal async Task SendStreamDeclaration()
		{
			await _client.WriteAsync(new XDeclaration("1.0", "UTF-8", "no").ToString());
			await _client.WriteAsync($"<stream:stream to=\"{_region.XMPPAuthRegion}.pvp.net\" xml:lang=\"en\" version=\"1.0\" xmlns=\"jabber:client\" xmlns:stream=\"http://etherx.jabber.org/streams\">", 1);
		}

		internal async Task<ValorantResult> AuthAsync(AuthResponse response)
		{ 
			try
			{
				_logger.Debug("Connecting to XMPP...");
				await _client.ConnectAsync();
				_logger.Debug("Connected to XMPP.");
				await SendStreamDeclaration();
				await _client.WriteXMLAsync(new XElement((XNamespace)"urn:ietf:params:xml:ns:xmpp-sasl" + "auth", new XAttribute("mechanism", "X-Riot-RSO-PAS"),
									new XElement("rso_token", response.AccessToken),
									new XElement("pas_token", response.XMPPToken)));
				_logger.Debug("Attempting to Auth with XMPP...");
				if ((await _client.ReceiveSingleAsync()).Contains("success"))
				{
					await SendStreamDeclaration();
					await _client.WriteXMLAsync(new XElement("iq", new XAttribute("id", "_xmpp_bind1"), new XAttribute("type", "set"),
										new XElement((XNamespace)"urn:ietf:params:xml:ns:xmpp-bind" + "bind",
											new XElement("puuid-mode", new XAttribute("enabled", true)),
											new XElement("resource", "RC-VALORANT-SHARP"))));
					_jid = (await _client.ReceiveAsync())[0].Split("<jid>")[1].Split("</jid>")[0];
					await _client.WriteXMLAsync(new XElement("iq", new XAttribute("id", "xmpp_entitlements_0"), new XAttribute("type", "set"),
										new XElement((XNamespace)"urn:riotgames:entitlements" + "entitlements",
											new XElement("token", response.EntitlementsJWT))), 1);
					await _client.WriteXMLAsync(new XElement("iq", new XAttribute("id", "set_rxep_1"), new XAttribute("type", "set"),
										new XElement((XNamespace)"urn:riotgames:rxep" + "rxcep", "&lt;last-online-state enabled='true' /&gt;")), 1);
					await _client.WriteXMLAsync(new XElement("iq", new XAttribute("id", "_xmpp_session1"), new XAttribute("type", "set"),
										new XElement((XNamespace)"urn:ietf:params:xml:ns:xmpp-session" + "session")));

					var _userId = (await _client.ReceiveSingleXMLAsync())["session"]["id"];
					User.Name = _userId.GetAttribute("name");
					User.Tagline = _userId.GetAttribute("tagline");
					await GetFriendsAsync();
					var _ = Task.Run(async () => await _client.HandleEventsAsync());
					return new ValorantResult() { isSuccessful = true, Data = User };
				}
			}
			catch (Exception ex) { Console.WriteLine(ex); }
			return new ValorantResult() { isSuccessful = false, Error = "Failed to auth XMPP with Valorant." };
		}

		internal async Task GetFriendsAsync()
		{
			_logger.Debug("Getting friends list from XMPP.");
			_valClient.Friends = new List<ValorantFriend>();
			await _client.WriteXMLAsync(new XElement("iq", new XAttribute("type", "get"),
								new XElement((XNamespace)"jabber:iq:riotgames:roster" + "query")));
			XmlDocument RosterXML = new XmlDocument();
			RosterXML.LoadXml(await _client.ReceiveSingleAsync());
			foreach (XmlNode Item in RosterXML.FirstChild.FirstChild.ChildNodes)
			{
				switch (Item.Attributes.GetNamedItem("subscription").Value)
				{
					case "both":
						_valClient.Friends.Add(new ValorantFriend(Item.LastChild.Attributes.GetNamedItem("name").Value, Item.LastChild.Attributes.GetNamedItem("tagline").Value, Item.Attributes.GetNamedItem("puuid").Value, Item.Attributes.GetNamedItem("jid").Value, _valClient));
						break;
					case "pending_out":
						_valClient.Friends.Add(new ValorantFriend(Item.LastChild.Attributes.GetNamedItem("name").Value, Item.LastChild.Attributes.GetNamedItem("tagline").Value, Item.Attributes.GetNamedItem("puuid").Value, Item.Attributes.GetNamedItem("jid").Value, _valClient, ValorantFriendState.Outgoing));
						break;
					case "pending_in":
						_valClient.Friends.Add(new ValorantFriend(Item.LastChild.Attributes.GetNamedItem("name").Value, Item.LastChild.Attributes.GetNamedItem("tagline").Value, Item.Attributes.GetNamedItem("puuid").Value, Item.Attributes.GetNamedItem("jid").Value, _valClient, ValorantFriendState.Incoming));
						break;
				}
			}
			_logger.Debug("Got friends list from XMPP.");
		}
	}
}
