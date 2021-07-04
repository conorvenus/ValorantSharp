﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using ValorantSharp.Enums;
using ValorantSharp.Objects.Game;

namespace ValorantSharp.Tests
{
	class Program
	{
		private static ValorantClient valorantClient;
		static async Task Main()
		{
			valorantClient = new ValorantClientBuilder()
				.WithCredentials("username", "password")
				.WithRegion("eu", "ru1", "ru1")
				.WithCommandsPrefix("!")
				.Build();

			valorantClient.AddModules(Assembly.GetExecutingAssembly());

			valorantClient.FriendRequestReceived += FriendRequestReceived;
			valorantClient.Ready += Ready;

			await valorantClient.LoginAsync();

			await Task.Delay(-1);
		}

		private static async Task Ready(Objects.Auth.AuthResponse authResponse)
		{
			// Send message upon ValorantSharp ready.
			ValorantFriend friend = valorantClient.Friends
				.FirstOrDefault(friend => friend.Name == "narkxmpp");
			if (friend != null)
				await friend.SendMessageAsync("I am now online!");
		}

		private static async Task FriendRequestReceived(ValorantFriend friend)
		{
			// Automatically accept all friend requests.
			await friend.AcceptAsync();
		}
	}
}