using System;
using ValorantSharp.Enums;

namespace ValorantSharp
{
	public class ValorantLogger
	{
		private readonly object consoleLock = new object();
		private readonly ValorantLogLevel logLevel;
		private readonly string datetimeFormat;

		internal ValorantLogger(ValorantLogLevel _logLevel, string _datetimeFormat = "yyyy-MM-dd HH:mm:ss")
		{
			logLevel = _logLevel;
			datetimeFormat = _datetimeFormat;
		}

		public void Info(string text)
		{
			if (logLevel >= ValorantLogLevel.Info)
				WriteFormattedLog(ValorantLogLevel.Info, text);
		}

		public void Event(string text)
		{
			if (logLevel >= ValorantLogLevel.Event)
				WriteFormattedLog(ValorantLogLevel.Event, text);
		}

		public void Debug(string text)
		{
			if (logLevel >= ValorantLogLevel.Debug)
				WriteFormattedLog(ValorantLogLevel.Debug, text);
		}

		public void Warning(string text)
		{
			if (logLevel >= ValorantLogLevel.Warning)
				WriteFormattedLog(ValorantLogLevel.Warning, text);
		}

		public void Error(string text)
		{
			if (logLevel >= ValorantLogLevel.Error)
				WriteFormattedLog(ValorantLogLevel.Error, text);
		}

		private void WriteFormattedLog(ValorantLogLevel _logLevel, string text)
		{
			lock (consoleLock)
			{
				Console.Write($"[{DateTime.Now.ToString(datetimeFormat)}] ");
				switch (_logLevel)
				{
					case ValorantLogLevel.Info:
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.Write("[INFO]");
						break;
					case ValorantLogLevel.Event:
						Console.ForegroundColor = ConsoleColor.Green;
						Console.Write("[EVENT]");
						break;
					case ValorantLogLevel.Debug:
						Console.ForegroundColor = ConsoleColor.Gray;
						Console.Write("[DEBUG]");
						break;
					case ValorantLogLevel.Warning:
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write("[WARNING]");
						break;
					case ValorantLogLevel.Error:
						Console.ForegroundColor = ConsoleColor.Red;
						Console.Write("[ERROR]");
						break;
				}
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine(" " + text);
			}
		}
	}
}
