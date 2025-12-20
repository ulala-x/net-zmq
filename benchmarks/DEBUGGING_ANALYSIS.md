# Performance Analysis Archive

## Note

This file previously contained detailed performance analysis of the MessagePool implementation, which has been removed from Net.Zmq in favor of a simpler 4-strategy approach:

1. **ByteArray** - Simple managed memory allocation
2. **ArrayPool** - Pooled managed buffers (best for ≤512B)
3. **Message** - Native libzmq message structure
4. **MessageZeroCopy** - Unmanaged memory with zero-copy (best for >512B)

The MessagePool feature was removed because:
- Added complexity without clear performance benefits for most use cases
- ArrayPool and MessageZeroCopy cover the performance spectrum effectively
- Simpler API surface area improves maintainability

For current performance recommendations, see:
- [docs/benchmarks.md](/docs/benchmarks.md)
- [benchmarks/Net.Zmq.Benchmarks/README.md](Net.Zmq.Benchmarks/README.md)
