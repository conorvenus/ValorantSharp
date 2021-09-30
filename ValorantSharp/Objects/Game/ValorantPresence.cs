using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ValorantSharp.Objects.Game
{
	public class ValorantPresence
	{
		[JsonIgnore]
		public string jid { get; set; }

		public bool isValid { get; set; } = true;
		public string sessionLoopState { get; set; } = "MENUS";
		public string partyOwnerSessionLoopState { get; set; } = "MENUS";
		public string customGameName { get; set; } = "";
		public string customGameTeam { get; set; } = "";
		public string partyOwnerMatchMap { get; set; } = "";
		public string partyOwnerMatchCurrentTeam { get; set; } = "";
		public int partyOwnerMatchScoreAllyTeam { get; set; } = 0;
		public int partyOwnerMatchScoreEnemyTeam { get; set; } = 0;
		public string partyOwnerProvisioningFlow { get; set; } = "Invalid";
		public string provisioningFlow { get; set; } = "Invalid";
		public string matchMap { get; set; } = "";
		public string partyId { get; set; } = "";
		public bool isPartyOwner { get; set; } = true;
		public string partyName { get; set; } = "";
		public string partyState { get; set; } = "DEFAULT";
		public string partyAccessibility { get; set; } = "CLOSED";
		public int maxPartySize { get; set; } = 5;
		public string queueId { get; set; } = "unrated";
		public bool partyLFM { get; set; } = false;
		public string partyClientVersion { get; set; } = "release-03.00-shipping-22-574489";
		public int partySize { get; set; } = 1;
		public string tournamentId { get; set; } = "";
		public string rosterId { get; set; } = "";
		public long partyVersion { get; set; } = 1624747525203;
		public string queueEntryTime { get; set; } = "0001.01.01-00.00.00";
		public string playerCardId { get; set; } = "c89194bd-4710-b54e-8d6c-60be6274fbb2";
		public string playerTitleId { get; set; } = "566b6a77-4f72-af35-6d17-43be14e73cb7";
        public string preferredLevelBorderId { get; set; } = "";
		public int accountLevel { get; set; } = 1;
        public int competitiveTier { get; set; } = 24;
		public int leaderboardPosition { get; set; } = 0;
		public bool isIdle { get; set; } = false;
	}
}
