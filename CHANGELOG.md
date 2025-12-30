[![English](https://img.shields.io/badge/lang:en-red.svg)](CHANGELOG.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](CHANGELOG.ko.md)

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] - 2025-12-30

### Removed
- **MessagePool completely removed** - Removed MessagePool class and all related code for API simplification
  - Deleted `MessagePool.cs`
  - Deleted `MessagePoolTests.cs`
  - Deleted `ReinitializeZmqMsgTests.cs`
  - Removed pool-related fields/methods from `Message.cs` (`_poolDataPtr`, `_isFromPool`, `PrepareForReuse()`, etc.)
  - Removed `Recv(Message, int expectSize)` overload from `Socket.cs`
  - Removed MessagePooled benchmarks

### Changed
- **Simplified Message class** - Cleaner structure after removing pooling-related code
  - Removed pool-related branches from `Size`, `Data`, `DataPtr` properties
  - Simplified `Send()`, `Dispose()` methods
- **Consolidated to 4 message buffer strategies**
  - ByteArray (Baseline)
  - ArrayPool (recommended for ≤1KB)
  - Message
  - MessageZeroCopy (special cases only)

### Documentation
- **Complete benchmark documentation update** (`docs/benchmarks.md`, `docs/benchmarks.ko.md`)
  - Removed all MessagePool content
  - Removed MessageZeroCopy recommendation (slower than Message in most cases)
  - Added GC optimization settings section (Server GC + Batch Latency Mode)
- **README update** - Recommendations based on 4 strategies
- **Benchmark README update** - Reflects latest results

### Recommendations
- **≤1KB messages**: Use ArrayPool (best performance, minimal GC)
- **≥64KB messages**: Use ArrayPool or Message
- **≥128KB messages**: Use ArrayPool or Message (3.5x+ faster than ByteArray)
- **MessageZeroCopy**: Only for special cases when native memory already exists

## [0.4.1] - 2025-12-27

### Added
- **SetActualDataSize() public API** - Allows setting actual data size after writing directly to Data Span in pooled messages

## [0.4.0] - 2025-12-26

### Added
- **MessagePool** - Native memory buffer reuse with thread-local cache for high-performance scenarios
- **MessagePool benchmarks** - Performance comparison with other memory strategies
- **Application-level error codes** - `EBUFFERSMALL` and `ESIZEMISMATCH` constants in ZmqConstants
- **Descriptive exception messages** - ZmqException now includes meaningful error descriptions for buffer validation errors

### Changed
- **ZmqException improvements** - Buffer validation errors now throw with specific error codes and descriptive messages instead of relying on errno

### Fixed
- **Benchmark fairness** - Removed shared receive buffer to ensure accurate performance measurements

## [0.2.0] - 2025-12-22

### Changed
- **Send() now returns bool** - indicates success/failure for non-blocking sends
- **Poller refactored to instance-based design** - zero-allocation polling
- **MessageBufferStrategyBenchmarks changed to pure blocking mode** for accurate measurement

### Added
- **TryRecv() methods** - non-blocking receive with explicit success indicator
- **PureBlocking mode** in ReceiveModeBenchmarks for accurate comparison
- **128KB and 256KB message size tests** in benchmarks
- **Korean translations** for all documentation, samples, and templates
- **DocFX documentation** with GitHub Pages deployment
- **LOH (Large Object Heap) impact analysis** in benchmark documentation
- **One-Size-Fits-All recommendation section** - Message recommended for consistent performance

### Removed
- **RecvBytes() and TryRecvBytes()** - caused double-copy and GC pressure; use `Recv(Span<byte>)` or `Recv(Message)` instead
- **MessagePool** - simplified memory strategies based on benchmark findings

### Documentation
- Updated benchmark results with new receive modes and memory strategies
- Added memory strategy selection guide based on message size
- Key findings documented:
  - Single socket: PureBlocking recommended
  - Multiple sockets: Poller recommended
  - Memory: Message recommended (GC-free, consistent performance)
  - MessageZeroCopy beneficial at 256KB+

## [0.1.0] - 2025-12-14

### Added
- Initial release
- Context management with option support
- All socket types (REQ, REP, PUB, SUB, PUSH, PULL, DEALER, ROUTER, PAIR, XPUB, XSUB, STREAM)
- Message API with Span<byte> support
- Polling support
- Z85 encoding/decoding utilities
- CURVE key generation utilities
- Proxy support
- SafeHandle-based resource management

### Socket Features
- Bind/Connect/Unbind/Disconnect
- Send/Recv with multiple overloads
- TrySend/TryRecv for non-blocking operations
- Full socket options support
- Subscribe/Unsubscribe for PUB/SUB pattern

### Platforms
- Windows x64, x86, ARM64
- Linux x64, ARM64
- macOS x64, ARM64 (Apple Silicon)

### Core Components
- NetZeroMQ: High-level API with cppzmq-style interface
- NetZeroMQ.Core: Low-level P/Invoke bindings
- NetZeroMQ.Native: Native library package with multi-platform support

### Performance
- Zero-copy operations with Span<byte>
- Efficient memory management with SafeHandle
- Native performance through direct P/Invoke

### Safety
- Comprehensive null reference type annotations
- SafeHandle-based resource cleanup
- Thread-safe socket operations
