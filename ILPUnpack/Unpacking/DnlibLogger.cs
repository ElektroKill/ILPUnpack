using System.Collections.Generic;
using dnlib.DotNet;

namespace ILPUnpack.Unpacking {
	internal sealed class DnlibLogger : ILogger {
		private readonly ILPUnpack.Core.ILogger internalLogger;
		private readonly HashSet<string> loggedErrors;

		internal DnlibLogger(ILPUnpack.Core.ILogger baseLogger) {
			internalLogger = baseLogger;
			loggedErrors = new HashSet<string>();
		}
		
		public void Log(object sender, LoggerEvent loggerEvent, string format, params object[] args) {
			string actualMessage = string.Format(format, args);
			if (loggedErrors.Add(actualMessage))
				internalLogger.Error(actualMessage);
		}

		public bool IgnoresEvent(LoggerEvent loggerEvent) {
			return loggerEvent != LoggerEvent.Error;
		}
	}
}