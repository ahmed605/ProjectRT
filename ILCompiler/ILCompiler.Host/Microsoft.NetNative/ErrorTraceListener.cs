using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.NetNative
{
	internal class ErrorTraceListener : DefaultTraceListener
	{
		public ErrorTraceListener()
		{
		}

		public override void Fail(string message, string detailMessage)
		{
			if (!string.Equals(Environment.GetEnvironmentVariable("NETNATIVE_DISABLEASSERTUI"), "true", StringComparison.OrdinalIgnoreCase))
			{
				base.Fail(message, detailMessage);
				return;
			}
			string str = string.Concat(new string[] { "Assertion failed: ", message, Environment.NewLine, detailMessage, Environment.NewLine, (new StackTrace(4)).ToString() });
			Console.Error.WriteLine(str);
			base.WriteLine(str);
			if (Debugger.IsAttached)
			{
				Debugger.Break();
			}
			Environment.Exit(-1);
		}

		public static void ReplaceDefaulTraceListener()
		{
			Debug.Listeners.Clear();
			Debug.Listeners.Add(new ErrorTraceListener());
		}
	}
}