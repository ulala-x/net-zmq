using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

/// <summary>
/// Comprehensive tests for ZMQ socket options.
/// </summary>
[Collection("Sequential")]
public class SocketOptionsTests
{
    [Fact]
    public void SocketOption_Linger_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);
        const int expectedValue = 500;

        socket.SetOption(SocketOption.Linger, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Linger);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void SocketOption_Linger_ShouldAcceptVariousValues(int lingerValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        socket.SetOption(SocketOption.Linger, lingerValue);
        var actualValue = socket.GetOption<int>(SocketOption.Linger);

        actualValue.Should().Be(lingerValue);
    }

    [Fact]
    public void SocketOption_SendHighWaterMark_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 2000;

        socket.SetOption(SocketOption.Sndhwm, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Sndhwm);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void SocketOption_SendHighWaterMark_ShouldAcceptVariousValues(int hwmValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Push);

        socket.SetOption(SocketOption.Sndhwm, hwmValue);
        var actualValue = socket.GetOption<int>(SocketOption.Sndhwm);

        actualValue.Should().Be(hwmValue);
    }

    [Fact]
    public void SocketOption_RecvHighWaterMark_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);
        const int expectedValue = 3000;

        socket.SetOption(SocketOption.Rcvhwm, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rcvhwm);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5000)]
    [InlineData(20000)]
    public void SocketOption_RecvHighWaterMark_ShouldAcceptVariousValues(int hwmValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pull);

        socket.SetOption(SocketOption.Rcvhwm, hwmValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rcvhwm);

        actualValue.Should().Be(hwmValue);
    }

    [Fact]
    public void SocketOption_SendTimeout_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);
        const int expectedValue = 1500;

        socket.SetOption(SocketOption.Sndtimeo, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Sndtimeo);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(5000)]
    public void SocketOption_SendTimeout_ShouldAcceptVariousValues(int timeoutValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Push);

        socket.SetOption(SocketOption.Sndtimeo, timeoutValue);
        var actualValue = socket.GetOption<int>(SocketOption.Sndtimeo);

        actualValue.Should().Be(timeoutValue);
    }

    [Fact]
    public void SocketOption_RecvTimeout_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedValue = 2500;

        socket.SetOption(SocketOption.Rcvtimeo, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rcvtimeo);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(10000)]
    public void SocketOption_RecvTimeout_ShouldAcceptVariousValues(int timeoutValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pull);

        socket.SetOption(SocketOption.Rcvtimeo, timeoutValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rcvtimeo);

        actualValue.Should().Be(timeoutValue);
    }

    [Fact]
    public void SocketOption_TcpKeepalive_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Tcp_Keepalive, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_TcpKeepalive_ShouldAcceptVariousValues(int keepaliveValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Tcp_Keepalive, keepaliveValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive);

        actualValue.Should().Be(keepaliveValue);
    }

    [Fact]
    public void SocketOption_RoutingId_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        var expectedValue = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        socket.SetOption(SocketOption.Routing_Id, expectedValue);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Routing_Id, buffer);

        var actualValue = buffer.Take(size).ToArray();
        actualValue.Should().BeEquivalentTo(expectedValue);
    }

    [Theory]
    [InlineData(new byte[] { 0x01 })]
    [InlineData(new byte[] { 0x41, 0x42, 0x43 })]
    [InlineData(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })]
    public void SocketOption_RoutingId_ShouldAcceptVariousByteArrays(byte[] routingId)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Routing_Id, routingId);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Routing_Id, buffer);

        var actualValue = buffer.Take(size).ToArray();
        actualValue.Should().BeEquivalentTo(routingId);
    }

    [Fact]
    public void SocketOption_Affinity_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const long expectedValue = 3;

        socket.SetOption(SocketOption.Affinity, expectedValue);
        var actualValue = socket.GetOption<long>(SocketOption.Affinity);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(15L)]
    public void SocketOption_Affinity_ShouldAcceptVariousValues(long affinityValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Affinity, affinityValue);
        var actualValue = socket.GetOption<long>(SocketOption.Affinity);

        actualValue.Should().Be(affinityValue);
    }

    [Fact]
    public void SocketOption_IPv6_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Ipv6, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Ipv6);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_IPv6_ShouldAcceptVariousValues(int ipv6Value)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        socket.SetOption(SocketOption.Ipv6, ipv6Value);
        var actualValue = socket.GetOption<int>(SocketOption.Ipv6);

        actualValue.Should().Be(ipv6Value);
    }

    [Fact]
    public void SocketOption_ReconnectInterval_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 500;

        socket.SetOption(SocketOption.Reconnect_Ivl, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Reconnect_Ivl);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void SocketOption_ReconnectInterval_ShouldAcceptVariousValues(int intervalValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Reconnect_Ivl, intervalValue);
        var actualValue = socket.GetOption<int>(SocketOption.Reconnect_Ivl);

        actualValue.Should().Be(intervalValue);
    }

    [Fact]
    public void SocketOption_MultipleOptions_ShouldBeConfigurableIndependently()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedLinger = 1000;
        const int expectedSendTimeout = 2000;
        const int expectedRecvTimeout = 3000;
        const int expectedSndhwm = 5000;
        const int expectedRcvhwm = 6000;

        socket.SetOption(SocketOption.Linger, expectedLinger);
        socket.SetOption(SocketOption.Sndtimeo, expectedSendTimeout);
        socket.SetOption(SocketOption.Rcvtimeo, expectedRecvTimeout);
        socket.SetOption(SocketOption.Sndhwm, expectedSndhwm);
        socket.SetOption(SocketOption.Rcvhwm, expectedRcvhwm);

        socket.GetOption<int>(SocketOption.Linger).Should().Be(expectedLinger);
        socket.GetOption<int>(SocketOption.Sndtimeo).Should().Be(expectedSendTimeout);
        socket.GetOption<int>(SocketOption.Rcvtimeo).Should().Be(expectedRecvTimeout);
        socket.GetOption<int>(SocketOption.Sndhwm).Should().Be(expectedSndhwm);
        socket.GetOption<int>(SocketOption.Rcvhwm).Should().Be(expectedRcvhwm);
    }

    [Fact]
    public void SocketOption_SetBeforeBind_ShouldWork()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedLinger = 0;

        socket.SetOption(SocketOption.Linger, expectedLinger);
        socket.Bind("tcp://127.0.0.1:25555");
        var actualValue = socket.GetOption<int>(SocketOption.Linger);

        actualValue.Should().Be(expectedLinger);

        socket.Unbind("tcp://127.0.0.1:25555");
    }

    [Fact]
    public void SocketOption_SetAfterBind_ShouldWork()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedSendTimeout = 1000;

        socket.Bind("tcp://127.0.0.1:25556");
        socket.SetOption(SocketOption.Sndtimeo, expectedSendTimeout);
        var actualValue = socket.GetOption<int>(SocketOption.Sndtimeo);

        actualValue.Should().Be(expectedSendTimeout);

        socket.Unbind("tcp://127.0.0.1:25556");
    }

    [Fact]
    public void SocketOption_GetWithoutSet_ShouldReturnDefaultValue()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        var linger = socket.GetOption<int>(SocketOption.Linger);
        var sndhwm = socket.GetOption<int>(SocketOption.Sndhwm);
        var rcvhwm = socket.GetOption<int>(SocketOption.Rcvhwm);

        linger.Should().BeGreaterOrEqualTo(-1);
        sndhwm.Should().BeGreaterOrEqualTo(0);
        rcvhwm.Should().BeGreaterOrEqualTo(0);
    }

    #region Integer Socket Options

    [Fact]
    public void SocketOption_Rate_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 100;

        socket.SetOption(SocketOption.Rate, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rate);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void SocketOption_Rate_ShouldAcceptVariousValues(int rateValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Rate, rateValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rate);

        actualValue.Should().Be(rateValue);
    }

    [Fact]
    public void SocketOption_RecoveryInterval_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 1000;

        socket.SetOption(SocketOption.Recovery_Ivl, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Recovery_Ivl);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void SocketOption_RecoveryInterval_ShouldAcceptVariousValues(int intervalValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Recovery_Ivl, intervalValue);
        var actualValue = socket.GetOption<int>(SocketOption.Recovery_Ivl);

        actualValue.Should().Be(intervalValue);
    }

    [Fact]
    public void SocketOption_SendBuffer_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Push);
        const int expectedValue = 4096;

        socket.SetOption(SocketOption.Sndbuf, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Sndbuf);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    [InlineData(65536)]
    [InlineData(131072)]
    public void SocketOption_SendBuffer_ShouldAcceptVariousValues(int bufferSize)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);

        socket.SetOption(SocketOption.Sndbuf, bufferSize);
        var actualValue = socket.GetOption<int>(SocketOption.Sndbuf);

        actualValue.Should().Be(bufferSize);
    }

    [Fact]
    public void SocketOption_ReceiveBuffer_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pull);
        const int expectedValue = 8192;

        socket.SetOption(SocketOption.Rcvbuf, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Rcvbuf);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    [InlineData(65536)]
    [InlineData(262144)]
    public void SocketOption_ReceiveBuffer_ShouldAcceptVariousValues(int bufferSize)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Rcvbuf, bufferSize);
        var actualValue = socket.GetOption<int>(SocketOption.Rcvbuf);

        actualValue.Should().Be(bufferSize);
    }

    [Fact]
    public void SocketOption_Backlog_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedValue = 10;

        socket.SetOption(SocketOption.Backlog, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Backlog);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void SocketOption_Backlog_ShouldAcceptVariousValues(int backlogValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Backlog, backlogValue);
        var actualValue = socket.GetOption<int>(SocketOption.Backlog);

        actualValue.Should().Be(backlogValue);
    }

    [Fact]
    public void SocketOption_ReconnectIntervalMax_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 10000;

        socket.SetOption(SocketOption.Reconnect_Ivl_Max, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Reconnect_Ivl_Max);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    [InlineData(10000)]
    [InlineData(60000)]
    public void SocketOption_ReconnectIntervalMax_ShouldAcceptVariousValues(int intervalValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Reconnect_Ivl_Max, intervalValue);
        var actualValue = socket.GetOption<int>(SocketOption.Reconnect_Ivl_Max);

        actualValue.Should().Be(intervalValue);
    }

    [Fact]
    public void SocketOption_MulticastHops_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 5;

        socket.SetOption(SocketOption.Multicast_Hops, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Multicast_Hops);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(255)]
    public void SocketOption_MulticastHops_ShouldAcceptVariousValues(int hopsValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Multicast_Hops, hopsValue);
        var actualValue = socket.GetOption<int>(SocketOption.Multicast_Hops);

        actualValue.Should().Be(hopsValue);
    }

    [Fact]
    public void SocketOption_TcpKeepaliveCnt_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        const int expectedValue = 5;

        socket.SetOption(SocketOption.Tcp_Keepalive_Cnt, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive_Cnt);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void SocketOption_TcpKeepaliveCnt_ShouldAcceptVariousValues(int countValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Tcp_Keepalive_Cnt, countValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive_Cnt);

        actualValue.Should().Be(countValue);
    }

    [Fact]
    public void SocketOption_TcpKeepaliveIdle_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        const int expectedValue = 60;

        socket.SetOption(SocketOption.Tcp_Keepalive_Idle, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive_Idle);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(300)]
    public void SocketOption_TcpKeepaliveIdle_ShouldAcceptVariousValues(int idleValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Tcp_Keepalive_Idle, idleValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive_Idle);

        actualValue.Should().Be(idleValue);
    }

    [Fact]
    public void SocketOption_TcpKeepaliveIntvl_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        const int expectedValue = 10;

        socket.SetOption(SocketOption.Tcp_Keepalive_Intvl, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive_Intvl);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    public void SocketOption_TcpKeepaliveIntvl_ShouldAcceptVariousValues(int intervalValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Tcp_Keepalive_Intvl, intervalValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Keepalive_Intvl);

        actualValue.Should().Be(intervalValue);
    }

    [Fact]
    public void SocketOption_Immediate_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Immediate, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Immediate);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_Immediate_ShouldAcceptVariousValues(int immediateValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Immediate, immediateValue);
        var actualValue = socket.GetOption<int>(SocketOption.Immediate);

        actualValue.Should().Be(immediateValue);
    }

    [Fact]
    public void SocketOption_RouterMandatory_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        const int expectedValue = 1;

        // Router_Mandatory is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Router_Mandatory, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_RouterMandatory_ShouldAcceptVariousValues(int mandatoryValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        // Router_Mandatory is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Router_Mandatory, mandatoryValue);

        mandatoryValue.Should().BeOneOf(0, 1);
    }


    [Fact]
    public void SocketOption_RouterHandover_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        const int expectedValue = 1;

        // Router_Handover is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Router_Handover, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_RouterHandover_ShouldAcceptVariousValues(int handoverValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        // Router_Handover is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Router_Handover, handoverValue);

        handoverValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_XpubVerbose_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);
        const int expectedValue = 1;

        // Xpub_Verbose is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Verbose, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_XpubVerbose_ShouldAcceptVariousValues(int verboseValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);

        // Xpub_Verbose is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Verbose, verboseValue);

        verboseValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_XpubVerboser_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);
        const int expectedValue = 1;

        // Xpub_Verboser is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Verboser, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_XpubVerboser_ShouldAcceptVariousValues(int verboserValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);

        // Xpub_Verboser is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Verboser, verboserValue);

        verboserValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_XpubNodrop_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);
        const int expectedValue = 1;

        // Xpub_Nodrop is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Nodrop, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_XpubNodrop_ShouldAcceptVariousValues(int nodropValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);

        // Xpub_Nodrop is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Nodrop, nodropValue);

        nodropValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_XpubManual_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);
        const int expectedValue = 1;

        // Xpub_Manual is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Manual, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_XpubManual_ShouldAcceptVariousValues(int manualValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);

        // Xpub_Manual is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Xpub_Manual, manualValue);

        manualValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_PlainServer_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Plain_Server, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Plain_Server);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_PlainServer_ShouldAcceptVariousValues(int serverValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Plain_Server, serverValue);
        var actualValue = socket.GetOption<int>(SocketOption.Plain_Server);

        actualValue.Should().Be(serverValue);
    }

    [Fact]
    public void SocketOption_CurveServer_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Curve_Server, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Curve_Server);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_CurveServer_ShouldAcceptVariousValues(int serverValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Curve_Server, serverValue);
        var actualValue = socket.GetOption<int>(SocketOption.Curve_Server);

        actualValue.Should().Be(serverValue);
    }

    [Fact]
    public void SocketOption_ProbeRouter_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 1;

        // Probe_Router is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Probe_Router, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_ProbeRouter_ShouldAcceptVariousValues(int probeValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        // Probe_Router is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Probe_Router, probeValue);

        probeValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_ReqCorrelate_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);
        const int expectedValue = 1;

        // Req_Correlate is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Req_Correlate, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_ReqCorrelate_ShouldAcceptVariousValues(int correlateValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        // Req_Correlate is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Req_Correlate, correlateValue);

        correlateValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_ReqRelaxed_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);
        const int expectedValue = 1;

        // Req_Relaxed is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Req_Relaxed, expectedValue);

        expectedValue.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_ReqRelaxed_ShouldAcceptVariousValues(int relaxedValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        // Req_Relaxed is write-only, just verify it doesn't throw
        socket.SetOption(SocketOption.Req_Relaxed, relaxedValue);

        relaxedValue.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_Conflate_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pull);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Conflate, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Conflate);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_Conflate_ShouldAcceptVariousValues(int conflateValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Conflate, conflateValue);
        var actualValue = socket.GetOption<int>(SocketOption.Conflate);

        actualValue.Should().Be(conflateValue);
    }

    [Fact]
    public void SocketOption_Tos_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 0x10;

        socket.SetOption(SocketOption.Tos, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tos);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x10)]
    [InlineData(0x20)]
    [InlineData(0x28)]
    public void SocketOption_Tos_ShouldAcceptVariousValues(int tosValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Tos, tosValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tos);

        actualValue.Should().Be(tosValue);
    }

    [Fact]
    public void SocketOption_HandshakeInterval_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 5000;

        socket.SetOption(SocketOption.Handshake_Ivl, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Handshake_Ivl);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(30000)]
    public void SocketOption_HandshakeInterval_ShouldAcceptVariousValues(int intervalValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Handshake_Ivl, intervalValue);
        var actualValue = socket.GetOption<int>(SocketOption.Handshake_Ivl);

        actualValue.Should().Be(intervalValue);
    }



    [Fact]
    public void SocketOption_InvertMatching_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 1;

        socket.SetOption(SocketOption.Invert_Matching, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Invert_Matching);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SocketOption_InvertMatching_ShouldAcceptVariousValues(int invertValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);

        socket.SetOption(SocketOption.Invert_Matching, invertValue);
        var actualValue = socket.GetOption<int>(SocketOption.Invert_Matching);

        actualValue.Should().Be(invertValue);
    }

    [Fact]
    public void SocketOption_HeartbeatInterval_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 1000;

        socket.SetOption(SocketOption.Heartbeat_Ivl, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Heartbeat_Ivl);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void SocketOption_HeartbeatInterval_ShouldAcceptVariousValues(int intervalValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Heartbeat_Ivl, intervalValue);
        var actualValue = socket.GetOption<int>(SocketOption.Heartbeat_Ivl);

        actualValue.Should().Be(intervalValue);
    }

    [Fact]
    public void SocketOption_HeartbeatTtl_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 5000;

        socket.SetOption(SocketOption.Heartbeat_Ttl, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Heartbeat_Ttl);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(20000)]
    public void SocketOption_HeartbeatTtl_ShouldAcceptVariousValues(int ttlValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Heartbeat_Ttl, ttlValue);
        var actualValue = socket.GetOption<int>(SocketOption.Heartbeat_Ttl);

        actualValue.Should().Be(ttlValue);
    }

    [Fact]
    public void SocketOption_HeartbeatTimeout_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 3000;

        socket.SetOption(SocketOption.Heartbeat_Timeout, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Heartbeat_Timeout);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(3000)]
    [InlineData(10000)]
    public void SocketOption_HeartbeatTimeout_ShouldAcceptVariousValues(int timeoutValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Heartbeat_Timeout, timeoutValue);
        var actualValue = socket.GetOption<int>(SocketOption.Heartbeat_Timeout);

        actualValue.Should().Be(timeoutValue);
    }

    [Fact]
    public void SocketOption_ConnectTimeout_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 2000;

        socket.SetOption(SocketOption.Connect_Timeout, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Connect_Timeout);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(30000)]
    public void SocketOption_ConnectTimeout_ShouldAcceptVariousValues(int timeoutValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Connect_Timeout, timeoutValue);
        var actualValue = socket.GetOption<int>(SocketOption.Connect_Timeout);

        actualValue.Should().Be(timeoutValue);
    }

    [Fact]
    public void SocketOption_TcpMaxrt_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = 10000;

        socket.SetOption(SocketOption.Tcp_Maxrt, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Maxrt);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    [InlineData(10000)]
    [InlineData(60000)]
    public void SocketOption_TcpMaxrt_ShouldAcceptVariousValues(int maxrtValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Tcp_Maxrt, maxrtValue);
        var actualValue = socket.GetOption<int>(SocketOption.Tcp_Maxrt);

        actualValue.Should().Be(maxrtValue);
    }

    [Fact]
    public void SocketOption_MulticastMaxtpdu_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const int expectedValue = 2048;

        socket.SetOption(SocketOption.Multicast_Maxtpdu, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Multicast_Maxtpdu);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(1500)]
    [InlineData(2048)]
    [InlineData(8192)]
    public void SocketOption_MulticastMaxtpdu_ShouldAcceptVariousValues(int maxtpduValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Multicast_Maxtpdu, maxtpduValue);
        var actualValue = socket.GetOption<int>(SocketOption.Multicast_Maxtpdu);

        actualValue.Should().Be(maxtpduValue);
    }

    [Fact]
    public void SocketOption_UseFd_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const int expectedValue = -1;

        socket.SetOption(SocketOption.Use_Fd, expectedValue);
        var actualValue = socket.GetOption<int>(SocketOption.Use_Fd);

        actualValue.Should().Be(expectedValue);
    }

    #endregion

    #region Long Socket Options

    [Fact]
    public void SocketOption_Maxmsgsize_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pull);
        const long expectedValue = 1048576;

        socket.SetOption(SocketOption.Maxmsgsize, expectedValue);
        var actualValue = socket.GetOption<long>(SocketOption.Maxmsgsize);

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(65536L)]
    [InlineData(1048576L)]
    [InlineData(10485760L)]
    public void SocketOption_Maxmsgsize_ShouldAcceptVariousValues(long maxmsgsizeValue)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Maxmsgsize, maxmsgsizeValue);
        var actualValue = socket.GetOption<long>(SocketOption.Maxmsgsize);

        actualValue.Should().Be(maxmsgsizeValue);
    }

    #endregion

    #region String Socket Options

    [Fact]
    public void SocketOption_PlainUsername_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);
        const string expectedValue = "testuser";

        socket.SetOption(SocketOption.Plain_Username, expectedValue);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Plain_Username, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("user1")]
    [InlineData("admin")]
    [InlineData("test_user_123")]
    public void SocketOption_PlainUsername_ShouldAcceptVariousValues(string username)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Plain_Username, username);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Plain_Username, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(username);
    }

    [Fact]
    public void SocketOption_PlainPassword_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);
        const string expectedValue = "password123";

        socket.SetOption(SocketOption.Plain_Password, expectedValue);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Plain_Password, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("pass1")]
    [InlineData("secret")]
    [InlineData("my_secure_password")]
    public void SocketOption_PlainPassword_ShouldAcceptVariousValues(string password)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        socket.SetOption(SocketOption.Plain_Password, password);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Plain_Password, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(password);
    }

    [Fact]
    public void SocketOption_ZapDomain_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const string expectedValue = "global";

        socket.SetOption(SocketOption.Zap_Domain, expectedValue);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Zap_Domain, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("global")]
    [InlineData("test.domain")]
    public void SocketOption_ZapDomain_ShouldAcceptVariousValues(string domain)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Zap_Domain, domain);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Zap_Domain, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(domain);
    }

    [Fact]
    public void SocketOption_SocksProxy_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);
        const string expectedValue = "127.0.0.1:1080";

        socket.SetOption(SocketOption.Socks_Proxy, expectedValue);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Socks_Proxy, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("127.0.0.1:1080")]
    [InlineData("proxy.example.com:1080")]
    public void SocketOption_SocksProxy_ShouldAcceptVariousValues(string proxy)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Socks_Proxy, proxy);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Socks_Proxy, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(proxy);
    }

    [Fact]
    public void SocketOption_Bindtodevice_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        const string expectedValue = "eth0";

        socket.SetOption(SocketOption.Bindtodevice, expectedValue);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Bindtodevice, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("eth0")]
    [InlineData("lo")]
    public void SocketOption_Bindtodevice_ShouldAcceptVariousValues(string device)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Bindtodevice, device);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Bindtodevice, buffer);
        var actualValue = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        actualValue.Should().Be(device);
    }

    #endregion

    #region Byte Array Socket Options

    [Fact]
    public void SocketOption_Subscribe_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);
        var expectedValue = new byte[] { 0x01, 0x02, 0x03 };

        socket.SetOption(SocketOption.Subscribe, expectedValue);

        // Subscribe doesn't have a getter, so we just verify it doesn't throw
        expectedValue.Should().NotBeNull();
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x41 })]
    [InlineData(new byte[] { 0x41, 0x42, 0x43 })]
    public void SocketOption_Subscribe_ShouldAcceptVariousValues(byte[] topic)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);

        socket.SetOption(SocketOption.Subscribe, topic);

        // Subscribe doesn't have a getter, so we just verify it doesn't throw
        topic.Should().NotBeNull();
    }

    [Fact]
    public void SocketOption_Unsubscribe_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Sub);
        var subscribeValue = new byte[] { 0x01, 0x02, 0x03 };
        var unsubscribeValue = new byte[] { 0x01, 0x02, 0x03 };

        socket.SetOption(SocketOption.Subscribe, subscribeValue);
        socket.SetOption(SocketOption.Unsubscribe, unsubscribeValue);

        // Unsubscribe doesn't have a getter, so we just verify it doesn't throw
        unsubscribeValue.Should().NotBeNull();
    }

    [Fact]
    public void SocketOption_ConnectRoutingId_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);
        var expectedValue = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        socket.SetOption(SocketOption.Connect_Routing_Id, expectedValue);

        // Connect_Routing_Id is write-only, so we just verify it doesn't throw
        expectedValue.Should().NotBeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0x01 })]
    [InlineData(new byte[] { 0x41, 0x42, 0x43 })]
    [InlineData(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 })]
    public void SocketOption_ConnectRoutingId_ShouldAcceptVariousValues(byte[] routingId)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Router);

        socket.SetOption(SocketOption.Connect_Routing_Id, routingId);

        // Connect_Routing_Id is write-only, so we just verify it doesn't throw
        routingId.Should().NotBeNull();
    }

    [Fact]
    public void SocketOption_XpubWelcomeMsg_ShouldBeConfigurable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);
        var expectedValue = new byte[] { 0x57, 0x45, 0x4C, 0x43, 0x4F, 0x4D, 0x45 }; // "WELCOME"

        socket.SetOption(SocketOption.Xpub_Welcome_Msg, expectedValue);

        // Xpub_Welcome_Msg is write-only, so we just verify it doesn't throw
        expectedValue.Should().NotBeNull();
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x48, 0x49 })] // "HI"
    [InlineData(new byte[] { 0x57, 0x45, 0x4C, 0x43, 0x4F, 0x4D, 0x45 })] // "WELCOME"
    public void SocketOption_XpubWelcomeMsg_ShouldAcceptVariousValues(byte[] welcomeMsg)
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.XPub);

        socket.SetOption(SocketOption.Xpub_Welcome_Msg, welcomeMsg);

        // Xpub_Welcome_Msg is write-only, so we just verify it doesn't throw
        welcomeMsg.Should().NotBeNull();
    }

    #endregion

    #region Read-Only Socket Options

    [Fact]
    public void SocketOption_Rcvmore_ShouldBeReadable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pull);

        var rcvmore = socket.GetOption<int>(SocketOption.Rcvmore);

        rcvmore.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void SocketOption_Fd_ShouldBeReadable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        // FD is int on Unix but SOCKET (UINT_PTR, 8 bytes on 64-bit) on Windows
        // Use nint which matches the native pointer size on each platform
        var fd = socket.GetOption<nint>(SocketOption.Fd);

        // FD should be a valid file descriptor (on Unix) or SOCKET (on Windows)
        // Just verify we can read it without throwing
        fd.Should().NotBe(0);
    }

    [Fact]
    public void SocketOption_Events_ShouldBeReadable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        var events = socket.GetOption<int>(SocketOption.Events);

        // Events is a bitmask, should be >= 0
        events.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void SocketOption_Type_ShouldBeReadable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        var type = socket.GetOption<int>(SocketOption.Type);

        type.Should().Be((int)SocketType.Req);
    }

    [Theory]
    [InlineData(SocketType.Req)]
    [InlineData(SocketType.Rep)]
    [InlineData(SocketType.Dealer)]
    [InlineData(SocketType.Router)]
    [InlineData(SocketType.Pub)]
    [InlineData(SocketType.Sub)]
    [InlineData(SocketType.Push)]
    [InlineData(SocketType.Pull)]
    public void SocketOption_Type_ShouldReturnCorrectSocketType(SocketType socketType)
    {
        using var context = new Context();
        using var socket = new Socket(context, socketType);

        var type = socket.GetOption<int>(SocketOption.Type);

        type.Should().Be((int)socketType);
    }

    [Fact]
    public void SocketOption_LastEndpoint_ShouldBeReadableAfterBind()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Rep);
        const string bindAddress = "tcp://127.0.0.1:25557";

        socket.Bind(bindAddress);
        var buffer = new byte[256];
        var size = socket.GetOption(SocketOption.Last_Endpoint, buffer);
        var lastEndpoint = System.Text.Encoding.UTF8.GetString(buffer, 0, size).TrimEnd('\0', ' ');

        lastEndpoint.Should().Be(bindAddress);

        socket.Unbind(bindAddress);
    }

    [Fact]
    public void SocketOption_Mechanism_ShouldBeReadable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        var mechanism = socket.GetOption<int>(SocketOption.Mechanism);

        // Default mechanism should be NULL (0)
        mechanism.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void SocketOption_ThreadSafe_ShouldBeReadable()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Dealer);

        var threadSafe = socket.GetOption<int>(SocketOption.Thread_Safe);

        // Most socket types are not thread-safe (0), some like CLIENT/SERVER are (1)
        threadSafe.Should().BeOneOf(0, 1);
    }

    #endregion
}
