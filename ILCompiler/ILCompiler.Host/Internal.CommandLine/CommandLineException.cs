using System;

namespace Internal.CommandLine
{
	internal class CommandLineException : Exception
	{
		public CommandLineException(string message) : base(message)
		{
		}
	}
}