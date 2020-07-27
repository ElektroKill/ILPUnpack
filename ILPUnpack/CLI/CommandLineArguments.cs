using ILPUnpack.Core;

namespace ILPUnpack.CLI {
	internal sealed class CommandLineArguments : IUnpackerParameters {
		internal bool ShowHelp { get; set; }

		public string FilePath { get; set; }

		public bool NoCleanup { get; set; }

		public bool PreserveMetadata { get; set; }

		public bool KeepExtraPEData { get; set; }

		public bool DumpRuntime { get; set; }
	}
}
