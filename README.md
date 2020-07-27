# ILP Unpack
An ILProtector unpacker utilizing reflection to restore the method bodies and decrypt strings. This unpacker should work on any ILProtector configuration.

### Usage
*Remember to use the version appropriate to the CLR version used by the module.*

Just drag and drop your file and hope for the best!

##### For advanced users:
There are some command line options available. The first argument always has to be the filename.

###### Command Line Options:
* `--help` or `-h` - Displays a help screen.
* `--noClean` or `-c` - Disables the cleanup of unused code.
* `--preserveMD` or `-p` - Preserves all metadata when writing.
* `--keepPE` or `-k` - Preserves all additional PE information when writing
* `--dumpRuntime` or `-d` - Dumps the ILProtector runtime to disk.

### Credits
* [0xd4d](https://github.com/0xd4d) - [dnlib](https://github.com/0xd4d/dnlib)
* [Andreas Pardeike](https://github.com/pardeike) - [Harmony](https://github.com/pardeike/Harmony)
