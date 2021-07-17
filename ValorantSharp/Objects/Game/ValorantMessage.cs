using System;

namespace ValorantSharp.Objects.Game
{
	public class ValorantMessage
	{
		public string JID { get; internal set; }
		public string Content { get; internal set; }
		public DateTime SentAt { get; internal set; }
	}
}
