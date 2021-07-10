using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ValorantSharp.Enums;
using ValorantSharp.Exceptions;

namespace ValorantSharp.Objects.Game
{
	public class ValorantFriend : ValorantUser, ICloneable
	{
		internal readonly string jid;
		private ValorantClient valClient;
		private int messageId = 1;

		public ValorantFriendState FriendState { get; internal set; }
		public bool isOnline { get; internal set; }
		public ValorantPresence Presence { get; internal set; } = null;

		internal ValorantFriend(string _name, string _tagline, string _playerId, string _jid, ValorantClient _valClient, ValorantFriendState _state = ValorantFriendState.Friends)
		{
			Name = _name;
			Tagline = _tagline;
			PlayerId = _playerId;
			FriendState = _state;

			jid = _jid;
			valClient = _valClient;
		}

		public async Task AcceptAsync()
		{
			if (FriendState == ValorantFriendState.Incoming)
			{
				await valClient.WriteXMLAsync(new XElement("iq", new XAttribute("type", "set"), new XAttribute("id", "roster_add_10"),
										new XElement((XNamespace)"jabber:iq:riotgames:roster" + "query",
											new XElement("item", new XAttribute("subscription", "pending_out"), new XAttribute("puuid", PlayerId)))));
			}
		}

		public async Task RemoveOrDeclineAsync()
		{
			await valClient.WriteXMLAsync(
				new XElement("iq", new XAttribute("type", "set"), new XAttribute("id", "roster_remove_1"),
					new XElement((XNamespace)"jabber:iq:riotgames:roster" + "query",
						new XElement("item", new XAttribute("jid", jid), new XAttribute("subscription", "remove")))));
		}

		public async Task SendPresenceAsync(ValorantPresence presence)
		{
			if (FriendState == ValorantFriendState.Friends)
			{
				string presenceString = JsonConvert.SerializeObject(presence, Newtonsoft.Json.Formatting.Indented);
				string encodedPresence = Convert.ToBase64String(Encoding.UTF8.GetBytes(presenceString));
				await valClient.WriteXMLAsync(new XElement("presence", new XAttribute("id", $"presence_1"), new XAttribute("to", jid),
									new XElement("show", "chat"),
									new XElement("status"),
									new XElement("games",
										new XElement("valorant",
											new XElement("st", "chat"),
											new XElement("p", encodedPresence),
											new XElement("s.p", "valorant")))));
			}
		}

		public async Task SendMessageAsync(string message)
		{
			if (message.Length > 1000)
				throw new ValorantException("You can't send messages with over 1000 characters.");

			await valClient.WriteXMLAsync(new XElement("message", new XAttribute("id", $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}:{messageId}"), new XAttribute("to", jid), new XAttribute("type", "chat"),
												new XElement("body", message)));
			messageId++;
		}

		public object Clone()
		{
			return this.MemberwiseClone();
		}
	}
}