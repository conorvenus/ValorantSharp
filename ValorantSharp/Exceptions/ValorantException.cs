using System;
using System.Collections.Generic;
using System.Text;

namespace ValorantSharp.Exceptions
{
	public class ValorantException : Exception
	{
		public ValorantException(string message) : base(message) { }
	}
}
