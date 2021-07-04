using Qmmands;
using System;
using System.Collections.Generic;
using System.Text;
using ValorantSharp.Objects.Game;

namespace ValorantSharp.Objects
{
	public sealed class ValorantCommandContext : CommandContext
	{
		public ValorantMessage Message { get; }
		public ValorantFriend Friend { get; }
		public ValorantClient ValorantClient { get; }

		public ValorantCommandContext(ValorantMessage _message, ValorantFriend _friend, ValorantClient _client, IServiceProvider provider = null) : base(provider)
		{
			Message = _message;
			Friend = _friend;
			ValorantClient = _client;
		}
	}
}
