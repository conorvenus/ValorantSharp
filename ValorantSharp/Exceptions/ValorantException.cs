using System;

namespace ValorantSharp.Exceptions
{
	public class ValorantException : Exception
	{
		public ValorantException(string message) : base(message) { }
	}
}
