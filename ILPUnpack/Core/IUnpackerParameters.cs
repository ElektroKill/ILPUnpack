namespace ILPUnpack.Core {
	internal interface IUnpackerParameters {
		string FilePath { get; }
		bool NoCleanup { get; }
		bool PreserveMetadata { get; }
		bool KeepExtraPEData { get; }
		bool DumpRuntime { get; }
	}
}
