# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
