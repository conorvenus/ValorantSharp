using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ValorantSharp.Enums;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Game;

namespace ValorantSharp.XMPP
{
	public class XMPPClient
	{
		private readonly string xmppHostname;

		private ValorantClient valClient;
		private readonly TcpClient _client;
		private SslStream client;

		private List<string> _xmlStack;
		private List<string> _xmlParsedMessages;
		private int _xmppMessagesHandled = 0;

		internal XMPPClient(ValorantRegion region, ValorantClient _valClient)
		{
			xmppHostname = $"{region.XMPPRegion}.chat.si.riotgames.com";
			_client = new TcpClient();
			valClient = _valClient;

			_xmlStack = new List<string>();
			_xmlParsedMessages = new List<string>();
		}

		public async Task ConnectAsync()
		{
			await _client.ConnectAsync(xmppHostname, 5223);
			client = new SslStream(_client.GetStream(), false);
			client.AuthenticateAsClient(xmppHostname);

			var _ = Task.Run(async () => await ParseStreamAsync());
		}

		internal async Task WriteAsync(string message, int skipCount = 0)
		{
			await client.WriteAsync(Encoding.UTF8.GetBytes(message), 0, Encoding.UTF8.GetBytes(message).Length);
			await ReceiveAsync(skipCount);
		}

		internal async Task WriteXMLAsync(XElement xml, int skipCount = 0)
		{
			await client.WriteAsync(Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting)), 0, Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting)).Length);
			await ReceiveAsync(skipCount);
		}

		internal async Task<string> ReceiveSingleAsync()
		{
			return await Task.Run(() =>
			{
				while (true)
				{
					if (_xmlParsedMessages.Count > 0 && _xmlParsedMessages[0] != null)
					{
						var temp = _xmlParsedMessages[0];
						_xmlParsedMessages.RemoveAt(0);
						return temp;
					}
				}
			});
		}

		internal async Task<XmlElement> ReceiveSingleXMLAsync()
		{
			return await Task.Run(async () =>
			{
				while (true)
				{
					if (_xmlParsedMessages.Count > 0 && _xmlParsedMessages[0] != null)
					{
						XmlDocument _document = new XmlDocument();
						try
						{
							_document.LoadXml(_xmlParsedMessages[0]);
							_xmlParsedMessages.RemoveAt(0);
							return _document.DocumentElement;
						}
						catch
						{
							_document.LoadXml("<root>" + _xmlParsedMessages[0] + "</root>");
							foreach (XmlNode child in _document.DocumentElement.ChildNodes)
							{
								_xmlParsedMessages.Add(child.OuterXml);
							}
							_xmlParsedMessages.RemoveAt(0);
							return await ReceiveSingleXMLAsync();
						}
					}
				}
			});
		}

		internal async Task<List<string>> ReceiveAsync(int count = 1)
		{
			return await Task.Run(() =>
			{
				List<string> messages = new List<string>();
				while (true)
				{
					if (messages.Count == count)
						break;

					if (_xmlParsedMessages.Count > 0 && _xmlParsedMessages[0] != null)
					{
						messages.Add(_xmlParsedMessages[0]);
						_xmlParsedMessages.RemoveAt(0);
					}
				}
				return messages;
			});
		}

		internal async Task HandleEventsAsync()
		{
			while (true)
			{
				try
				{
					XmlElement _message = await ReceiveSingleXMLAsync();
					switch (_message.Name)
					{
						case "presence":
							string _jid = _message.GetAttribute("from").Split("/")[0];
							ValorantFriend _previousFriend = (ValorantFriend)valClient.Friends.FirstOrDefault(friend => friend.jid == _jid);
							try
							{
								string _presenceEncoded = _message["games"]["valorant"]["p"].InnerText;
								string _presenceDecoded = Encoding.UTF8.GetString(Convert.FromBase64String(_presenceEncoded));
								ValorantPresence _presence = JsonConvert.DeserializeObject<ValorantPresence>(_presenceDecoded);
								_presence.jid = _jid;
								if (_previousFriend != null)
								{
									_previousFriend = (ValorantFriend)_previousFriend.Clone();
									int index = valClient.Friends.FindIndex(friend => friend.jid == _jid);
									valClient.Friends[index].isOnline = true;
									valClient.Friends[index].Presence = _presence;
									await valClient.OnFriendPresenceReceived(_previousFriend, valClient.Friends[index]);
								}
								else
									await valClient.OnPresenceReceived(_presence);
							}
							catch
							{
								if (_previousFriend != null)
								{
									_previousFriend = (ValorantFriend)_previousFriend.Clone();
									int index = valClient.Friends.FindIndex(friend => friend.jid == _jid);
									valClient.Friends[index].isOnline = false;
									valClient.Friends[index].Presence = null;
									await valClient.OnFriendPresenceReceived(_previousFriend, valClient.Friends[index]);
								}
							}
							break;
						case "message":
							_jid = _message.GetAttribute("from").Split("/")[0];
							DateTime _sentAt = DateTime.Parse(_message.GetAttribute("stamp")).ToLocalTime();
							string _content = _message["body"].InnerText;
							await valClient.OnMessageReceived(new ValorantMessage()
							{
								Content = _content,
								JID = _jid,
								SentAt = _sentAt
							});
							break;
						case "iq":
							XmlElement _item = null;
							try { _item = _message["query"]["item"]; } catch { }
							if (_item != null && _item.HasAttribute("subscription"))
							{
								_jid = _item.GetAttribute("jid");
								string _playerId = _item.GetAttribute("puuid");
								string _name = _item["id"].GetAttribute("name");
								string _tagline = _item["id"].GetAttribute("tagline");
								ValorantFriend _friend;

								switch (_item.GetAttribute("subscription"))
								{
									case "pending_in":
										_friend = new ValorantFriend(_name, _tagline, _playerId, _jid, valClient, ValorantFriendState.Incoming);
										valClient.Friends.Add(_friend);
										await valClient.OnFriendRequestReceived(_friend);
										break;
									case "pending_out":
										_friend = new ValorantFriend(_name, _tagline, _playerId, _jid, valClient, ValorantFriendState.Outgoing);
										valClient.Friends.Add(_friend);
										await valClient.OnFriendRequestSent(_friend);
										break;
									case "both":
										_friend = new ValorantFriend(_name, _tagline, _playerId, _jid, valClient);
										if (!valClient.Friends.Any(friend => friend.PlayerId == _playerId))
											valClient.Friends.Add(_friend);
										else
										{
											int index = valClient.Friends.FindIndex(friend => friend.PlayerId == _playerId);
											valClient.Friends[index] = _friend;
										}
										await valClient.OnFriendAdded(_friend);
										break;
									case "remove":
										await valClient.OnFriendRemoved(valClient.Friends.FirstOrDefault(xmppHostname => xmppHostname.PlayerId == _playerId));
										valClient.Friends.RemoveAll(x => x.PlayerId == _playerId);
										break;
								}
							}
							break;
					}
				}
				catch (Exception ex) { Console.WriteLine(ex); }

			}
		}

		private async Task ParseStreamAsync()
		{
			while (true)
			{
				byte[] xmppMessageBuffer = new byte[2048];
				await client.ReadAsync(xmppMessageBuffer, 0, xmppMessageBuffer.Length);
				string xmppMessage = Encoding.ASCII.GetString(xmppMessageBuffer);
				if (!string.IsNullOrEmpty(xmppMessage))
				{
					foreach (string raw_tag in xmppMessage.Split('>').Where(tag => !string.IsNullOrEmpty(tag.Replace("\0", ""))))
					{
						string tag = raw_tag;
						if (xmppMessage.Contains(raw_tag + ">"))
							tag = raw_tag + ">";
						if (_xmlStack.Count > 0)
						{
							if (tag.Contains($"</{_xmlStack[_xmlStack.Count - 1].Split('>')[0].Split(' ')[0].Replace("<", "").Replace(">", "")}"))
								await ParseXMLAsync(tag);
							else
								_xmlStack[_xmlStack.Count - 1] += tag;
						}
						else
						{
							if (_xmppMessagesHandled != 0 && _xmppMessagesHandled != 3)
							{
								_xmlStack.Add(tag);
								if (tag.EndsWith("/>"))
									await ParseXMLAsync(null);
							}
						}
					}
				}
				_xmppMessagesHandled += 1;
			}
		}

		private async Task ParseXMLAsync(string tag)
		{
			await Task.Run(() =>
			{
				string fullTag = _xmlStack[_xmlStack.Count - 1] + tag;
				fullTag = fullTag.Replace("\0", "");
				_xmlStack.RemoveAt(_xmlStack.Count - 1);
				_xmlParsedMessages.Add(fullTag);
			});
		}
	}
}
