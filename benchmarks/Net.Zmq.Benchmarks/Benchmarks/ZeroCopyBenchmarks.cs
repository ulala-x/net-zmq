using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Net.Zmq.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ZeroCopyBenchmarks
{
    [Params(64, 1024, 65536)]
    public int MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    private byte[] _sendData = null!;
    private byte[] _recvBuffer = null!;
    private byte[] _identityBuffer = null!;

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sendData = new byte[MessageSize];
        _recvBuffer = new byte[MessageSize];
        _identityBuffer = new byte[64];
        Array.Fill(_sendData, (byte)'A');

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

    [Benchmark(Baseline = true)]
    public void Send_NormalCopy()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            int idx = poller.Add(_router2, PollEvents.In);
            int n = 0;
            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                }
            }
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        thread.Join();
    }

    [Benchmark]
    public void Send_MessageCopy()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            int idx = poller.Add(_router2, PollEvents.In);
            int n = 0;
            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                }
            }
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            using var msg = new Message(MessageSize);
            _sendData.CopyTo(msg.Data);
            _router1.Send(msg, SendFlags.DontWait);
        }

        thread.Join();
    }

    [Benchmark]
    public void Send_ZeroCopy_NativeMemory()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            int idx = poller.Add(_router2, PollEvents.In);
            int n = 0;
            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                }
            }
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            unsafe
            {
                var ptr = NativeMemory.Alloc((nuint)MessageSize);
                try
                {
                    var span = new Span<byte>(ptr, MessageSize);
                    _sendData.CopyTo(span);

                    using var msg = new Message((nint)ptr, MessageSize, (p) =>
                    {
                        unsafe { NativeMemory.Free((void*)p); }
                    });
                    _router1.Send(msg, SendFlags.DontWait);
                }
                catch
                {
                    NativeMemory.Free(ptr);
                    throw;
                }
            }
        }

        thread.Join();
    }

    [Benchmark]
    public void Send_ZeroCopy_Marshal()
    {
        var thread = new Thread(() =>
        {
            using var poller = new Poller(1);
            int idx = poller.Add(_router2, PollEvents.In);
            int n = 0;
            while (n < MessageCount)
            {
                poller.Poll(-1);
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                }
            }
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            var ptr = Marshal.AllocHGlobal(MessageSize);
            try
            {
                Marshal.Copy(_sendData, 0, ptr, MessageSize);

                using var msg = new Message(ptr, MessageSize, Marshal.FreeHGlobal);
                _router1.Send(msg, SendFlags.DontWait);
            }
            catch
            {
                Marshal.FreeHGlobal(ptr);
                throw;
            }
        }

        thread.Join();
    }
}
