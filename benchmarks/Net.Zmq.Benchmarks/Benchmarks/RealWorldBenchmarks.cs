using BenchmarkDotNet.Attributes;
using System.Buffers;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Real-world scenario benchmarks: external data[] input/output with ArrayPool.
///
/// Scenario: Router-to-Router multipart (identity + body)
/// - Input: Receive data[] from external source (ArrayPool)
/// - Send: Forward the data
/// - Recv: Receive the data
/// - Output: Deliver data[] to external consumer (ArrayPool)
///
/// Send variants:
/// 1. Direct: socket.Send(data[])
/// 2. Message: new Message(data) + socket.Send(message)
///
/// Recv variants:
/// 1. Direct: socket.Recv(buffer) + ArrayPool.Rent() + copy
/// 2. Message: RecvMessage() + ArrayPool.Rent() + copy
/// </summary>
[MemoryDiagnoser]
public class RealWorldBenchmarks
{
    [Params(64, 1024, 65536)]
    public int MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    private byte[] _inputData = null!; // Simulates external input
    private byte[] _identityBuffer = null!;
    private byte[] _recvBuffer = null!; // For direct recv

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;

    [GlobalSetup]
    public void Setup()
    {
        _inputData = new byte[MessageSize];
        _identityBuffer = new byte[64];
        _recvBuffer = new byte[MessageSize];
        Array.Fill(_inputData, (byte)'A');

        _ctx = new Context();

        // ROUTER/ROUTER setup
        _router1 = CreateSocket(SocketType.Router);
        _router2 = CreateSocket(SocketType.Router);
        _router2Id = "r2"u8.ToArray();
        _router1.SetOption(SocketOption.Routing_Id, "r1"u8.ToArray());
        _router2.SetOption(SocketOption.Routing_Id, _router2Id);
        _router1.Bind("tcp://127.0.0.1:0");
        _router2.Connect(_router1.GetOptionString(SocketOption.Last_Endpoint));

        Thread.Sleep(100);

        // Router handshake
        _router2.Send("r1"u8.ToArray(), SendFlags.SendMore);
        _router2.Send("hi"u8.ToArray());
        _router1.Recv(_identityBuffer);
        _router1.Recv(_identityBuffer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ctx.Shutdown();
        _router1?.Dispose();
        _router2?.Dispose();
        _ctx.Dispose();
    }

    private Socket CreateSocket(SocketType type)
    {
        var socket = new Socket(_ctx, type);
        socket.SetOption(SocketOption.Sndhwm, 0);
        socket.SetOption(SocketOption.Rcvhwm, 0);
        socket.SetOption(SocketOption.Linger, 0);
        return socket;
    }

    // ========================================
    // Baseline: SendDirect + RecvDirect
    // ========================================
    [Benchmark(Baseline = true)]
    public void SendDirect_RecvDirect()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            poller.Add(_router2, PollEvents.In);
            int n = 0;

            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    // Direct recv into fixed buffer
                    int size = _router2.Recv(_recvBuffer);

                    // Simulate external delivery: rent from pool + copy
                    var outputData = ArrayPool<byte>.Shared.Rent(size);
                    try
                    {
                        _recvBuffer.AsSpan(0, size).CopyTo(outputData);
                        // External consumer uses outputData here
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(outputData);
                    }

                    n++;
                }
            }
        });
        thread.Start();

        // Sender: direct send from external input data
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_inputData, SendFlags.DontWait);
        }

        thread.Join();
    }

    // ========================================
    // SendDirect + RecvMessage
    // ========================================
    [Benchmark]
    public void SendDirect_RecvMessage()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            poller.Add(_router2, PollEvents.In);
            int n = 0;

            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    // Recv into Message (native memory allocation)
                    using var msg = new Message();
                    _router2.Recv(msg);
                    int size = msg.Data.Length;

                    // Simulate external delivery: rent from pool + copy
                    var outputData = ArrayPool<byte>.Shared.Rent(size);
                    try
                    {
                        msg.Data.CopyTo(outputData);
                        // External consumer uses outputData here
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(outputData);
                    }

                    n++;
                }
            }
        });
        thread.Start();

        // Sender: direct send from external input data
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_inputData, SendFlags.DontWait);
        }

        thread.Join();
    }

    // ========================================
    // SendMessage + RecvDirect
    // ========================================
    [Benchmark]
    public void SendMessage_RecvDirect()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            poller.Add(_router2, PollEvents.In);
            int n = 0;

            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    // Direct recv into fixed buffer
                    int size = _router2.Recv(_recvBuffer);

                    // Simulate external delivery: rent from pool + copy
                    var outputData = ArrayPool<byte>.Shared.Rent(size);
                    try
                    {
                        _recvBuffer.AsSpan(0, size).CopyTo(outputData);
                        // External consumer uses outputData here
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(outputData);
                    }

                    n++;
                }
            }
        });
        thread.Start();

        // Sender: wrap input data in Message (allocate + copy)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            using var msg = new Message(MessageSize);
            _inputData.CopyTo(msg.Data);
            _router1.Send(msg, SendFlags.DontWait);
        }

        thread.Join();
    }

    // ========================================
    // SendMessage + RecvMessage (Worst case)
    // ========================================
    [Benchmark]
    public void SendMessage_RecvMessage()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            poller.Add(_router2, PollEvents.In);
            int n = 0;

            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    // Recv into Message (native memory allocation)
                    using var msg = new Message();
                    _router2.Recv(msg);
                    int size = msg.Data.Length;

                    // Simulate external delivery: rent from pool + copy
                    var outputData = ArrayPool<byte>.Shared.Rent(size);
                    try
                    {
                        msg.Data.CopyTo(outputData);
                        // External consumer uses outputData here
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(outputData);
                    }

                    n++;
                }
            }
        });
        thread.Start();

        // Sender: wrap input data in Message (allocate + copy)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            using var msg = new Message(MessageSize);
            _inputData.CopyTo(msg.Data);
            _router1.Send(msg, SendFlags.DontWait);
        }

        thread.Join();
    }
}
