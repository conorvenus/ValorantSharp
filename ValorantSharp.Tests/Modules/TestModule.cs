using Qmmands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValorantSharp.Objects;
using ValorantSharp.Objects.Game;

namespace ValorantSharp.Tests.Modules
{
	public sealed class TestModule : ModuleBase<ValorantCommandContext>
	{
		[Command("help")]
		public async Task HelpAsync()
		{
			if (Context.Friend != null)
			{
				// A friend sent the message.
				await Context.Friend.SendMessageAsync("Hey man, you need some help?");
			}
			else
			{
				// A party or some other unknown JID sent the message.
			}
		}
	}
}
