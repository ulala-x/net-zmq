[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

# Native Binaries

This directory contains the libzmq native binaries.

## Directory Structure

```
native/
├── build/
│   └── NetZeroMQ.Native.targets    # MSBuild targets (automatic copy configuration)
└── runtimes/
    ├── win-x64/native/          # Windows x64
    │   └── libzmq.dll
    ├── win-x86/native/          # Windows x86
    │   └── libzmq.dll
    ├── win-arm64/native/        # Windows ARM64
    │   └── libzmq.dll
    ├── linux-x64/native/        # Linux x64
    │   └── libzmq.so.5
    ├── linux-arm64/native/      # Linux ARM64
    │   └── libzmq.so.5
    ├── osx-x64/native/          # macOS x64 (Intel)
    │   └── libzmq.5.dylib
    └── osx-arm64/native/        # macOS ARM64 (Apple Silicon)
        └── libzmq.5.dylib
```

## Binary Sources

Native binaries are built from [libzmq-native](https://github.com/ulala-x/libzmq-native).
libsodium is statically linked and distributed as a single file.

### Supported Platforms

| Platform | Architecture | Runtime ID | Library File |
|----------|--------------|------------|--------------|
| Windows | x64 | win-x64 | libzmq.dll |
| Windows | x86 | win-x86 | libzmq.dll |
| Windows | ARM64 | win-arm64 | libzmq.dll |
| Linux | x64 | linux-x64 | libzmq.so.5 |
| Linux | ARM64 | linux-arm64 | libzmq.so.5 |
| macOS | x64 (Intel) | osx-x64 | libzmq.5.dylib |
| macOS | ARM64 (Apple Silicon) | osx-arm64 | libzmq.5.dylib |

## Usage

### NuGet Package Reference

When you reference the NetZeroMQ.Native NuGet package, the appropriate native library is automatically copied to the output directory.

```xml
<ItemGroup>
  <PackageReference Include="NetZeroMQ.Native" Version="0.1.0" />
</ItemGroup>
```

### Automatic Copy Mechanism

The `NetZeroMQ.Native.targets` file automatically handles:

1. **RuntimeIdentifier Detection**: Automatically detects the RID of the current build environment
2. **Platform-Specific Library Selection**: Selects the native library matching the RID
3. **Output Directory Copy**: Automatically copies the selected library to the build output directory

### Debugging Information

You can check the following diagnostic information during build (Verbosity: detailed):

```
NetZeroMQ.Native: RuntimeIdentifier = win-x64
NetZeroMQ.Native: Platform = x64
NetZeroMQ.Native: OS = Windows_NT
NetZeroMQ.Native: Native library name = libzmq.dll
```

## Updating Native Binaries

To update to a new libzmq version:

1. Build the latest binaries from the [libzmq-native](https://github.com/ulala-x/libzmq-native) repository
2. Copy each platform-specific binary to the corresponding `runtimes/{rid}/native/` directory
3. Update the NetZeroMQ.Native package version
4. Regenerate and deploy the NuGet package

## License

libzmq follows the MPL-2.0 license.
For details, see [ZeroMQ License](https://github.com/zeromq/libzmq/blob/master/LICENSE).
