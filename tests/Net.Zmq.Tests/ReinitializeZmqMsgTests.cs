using System.Text;
using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

/// <summary>
/// ReinitializeZmqMsg 메서드 관련 단위 테스트.
/// Zero-copy 전송을 위한 zmq_msg_t 재초기화 동작을 검증합니다.
/// </summary>
public class ReinitializeZmqMsgTests
{
    #region SetActualDataSize Reinitialization Tests

    [Fact]
    public void SetActualDataSize_ReinitializesZmqMsg_WithActualSize()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);
        var data = "Hello"u8.ToArray();
        data.CopyTo(msg.Data);

        // Act
        msg.SetActualDataSize(data.Length);

        // Assert
        msg.ActualDataSize.Should().Be(data.Length);
        msg.BufferSize.Should().Be(1024);
        msg.Size.Should().Be(data.Length);

        msg.Dispose();
    }

    [Fact]
    public void SetActualDataSize_CanBeCalledMultipleTimes()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        // Act & Assert - 여러 번 크기 변경 가능
        msg.SetActualDataSize(100);
        msg.ActualDataSize.Should().Be(100);

        msg.SetActualDataSize(500);
        msg.ActualDataSize.Should().Be(500);

        msg.SetActualDataSize(50);
        msg.ActualDataSize.Should().Be(50);

        msg.Dispose();
    }

    [Fact]
    public void SetActualDataSize_WithZeroSize_ShouldWork()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        // Act
        msg.SetActualDataSize(0);

        // Assert
        msg.ActualDataSize.Should().Be(0);
        msg.Size.Should().Be(0);

        msg.Dispose();
    }

    [Fact]
    public void SetActualDataSize_WithBufferSize_ShouldWork()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        // Act
        msg.SetActualDataSize(1024);

        // Assert
        msg.ActualDataSize.Should().Be(1024);
        msg.Size.Should().Be(1024);

        msg.Dispose();
    }

    [Fact]
    public void SetActualDataSize_ExceedingBufferSize_ThrowsArgumentException()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        // Act & Assert
        var act = () => msg.SetActualDataSize(2000);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot exceed buffer size*");

        msg.Dispose();
    }

    [Fact]
    public void SetActualDataSize_OnNonPooledMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        using var msg = new Message(100);

        // Act & Assert
        var act = () => msg.SetActualDataSize(50);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*pooled messages*");
    }

    #endregion

    #region CopyFromNative Reinitialization Tests

    [Fact]
    public void Rent_WithReadOnlySpan_ReinitializesWithActualSize()
    {
        // Arrange
        var pool = new MessagePool();
        var data = "Hello, World!"u8.ToArray();

        // Act
        var msg = pool.Rent(data);

        // Assert
        msg.ActualDataSize.Should().Be(data.Length);
        msg.Size.Should().Be(data.Length);
        msg.Data.ToArray().Should().Equal(data);

        msg.Dispose();
    }

    [Fact]
    public void Rent_WithReadOnlySpan_SmallData_UsesLargerBucket()
    {
        // Arrange - 작은 데이터는 더 큰 버킷에 할당됨
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 }; // 5 bytes

        // Act
        var msg = pool.Rent(data);

        // Assert
        msg.ActualDataSize.Should().Be(5);
        msg.BufferSize.Should().BeGreaterThanOrEqualTo(5);
        msg.Size.Should().Be(5); // Size는 ActualDataSize 반환

        msg.Dispose();
    }

    #endregion

    #region Send Auto-Reinitialization Tests

    [Fact]
    public void Send_WithoutSetActualDataSize_ReinitializesAutomatically()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();
        var msg = pool.Rent(1024);
        var data = "Test data"u8.ToArray();
        data.CopyTo(msg.Data);
        // SetActualDataSize() 호출하지 않음 - Send에서 자동 재초기화

        // Act
        sender.Send(msg);

        // Assert - 수신 확인
        using var recvMsg = new Message();
        receiver.Recv(recvMsg);
        // 버퍼 크기 전체가 전송됨 (SetActualDataSize 미호출)
        recvMsg.Size.Should().Be(1024);

        msg.Dispose();
    }

    [Fact]
    public void Send_WithSetActualDataSize_SendsOnlyActualData()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();
        var data = "Hello, ZeroMQ!"u8.ToArray();
        var msg = pool.Rent(1024);
        data.CopyTo(msg.Data);
        msg.SetActualDataSize(data.Length);

        // Act
        sender.Send(msg);

        // Assert
        using var recvMsg = new Message();
        receiver.Recv(recvMsg);
        recvMsg.Size.Should().Be(data.Length);
        recvMsg.Data.ToArray().Should().Equal(data);

        msg.Dispose();
    }

    [Fact]
    public void Send_WithRentReadOnlySpan_SendsOnlyActualData()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();
        var data = "Hello from Rent(span)!"u8.ToArray();
        var msg = pool.Rent(data);

        // Act
        sender.Send(msg);

        // Assert
        using var recvMsg = new Message();
        receiver.Recv(recvMsg);
        recvMsg.Size.Should().Be(data.Length);
        recvMsg.Data.ToArray().Should().Equal(data);

        msg.Dispose();
    }

    #endregion

    #region Reuse After Send Tests

    [Fact]
    public void ReusedMessage_AfterSend_ReinitializesCorrectly()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 첫 번째 전송
        var data1 = "First message"u8.ToArray();
        var msg1 = pool.Rent(data1);
        sender.Send(msg1);
        msg1.Dispose(); // 풀에 반환

        using var recv1 = new Message();
        receiver.Recv(recv1);
        recv1.Data.ToArray().Should().Equal(data1);

        // Act - 두 번째 전송 (재사용된 메시지)
        var data2 = "Second"u8.ToArray();
        var msg2 = pool.Rent(data2);
        sender.Send(msg2);

        // Assert
        using var recv2 = new Message();
        receiver.Recv(recv2);
        recv2.Size.Should().Be(data2.Length);
        recv2.Data.ToArray().Should().Equal(data2);

        msg2.Dispose();
    }

    [Fact]
    public void ReusedMessage_WithDifferentSize_WorksCorrectly()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 첫 번째: 큰 데이터
        var largeData = new byte[500];
        new Random(42).NextBytes(largeData);
        var msg1 = pool.Rent(largeData);
        sender.Send(msg1);
        msg1.Dispose();

        using var recv1 = new Message();
        receiver.Recv(recv1);
        recv1.Size.Should().Be(500);

        // Act - 두 번째: 작은 데이터 (같은 버킷 재사용)
        var smallData = new byte[50];
        new Random(43).NextBytes(smallData);
        var msg2 = pool.Rent(smallData);
        sender.Send(msg2);

        // Assert
        using var recv2 = new Message();
        receiver.Recv(recv2);
        recv2.Size.Should().Be(50);
        recv2.Data.ToArray().Should().Equal(smallData);

        msg2.Dispose();
    }

    [Fact]
    public void ReusedMessage_MultipleRounds_AllSucceed()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();
        const int rounds = 10;

        for (int i = 0; i < rounds; i++)
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes($"Message {i}: {new string('X', i * 10)}");
            var msg = pool.Rent(data);

            // Act
            sender.Send(msg);
            msg.Dispose();

            // Assert
            using var recv = new Message();
            receiver.Recv(recv);
            recv.Size.Should().Be(data.Length);
            recv.Data.ToArray().Should().Equal(data);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ReinitializeZmqMsg_OnNonPooledMessage_IsNoOp()
    {
        // Arrange
        using var msg = new Message("Test");

        // ReinitializeZmqMsg is internal, but we can test via SetActualDataSize
        // which throws for non-pooled messages
        var act = () => msg.SetActualDataSize(2);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetActualDataSize_NegativeSize_ThrowsArgumentException()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        // Act & Assert
        var act = () => msg.SetActualDataSize(-1);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*cannot be negative*");

        msg.Dispose();
    }

    [Fact]
    public void DisposedMessage_SetActualDataSize_ThrowsObjectDisposedException()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);
        msg.Dispose();

        // Act & Assert
        var act = () => msg.SetActualDataSize(100);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Send_AfterSetActualDataSize_ThenReuse_WorksCorrectly()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 첫 번째 사용
        var msg1 = pool.Rent(1024);
        var data1 = "First"u8.ToArray();
        data1.CopyTo(msg1.Data);
        msg1.SetActualDataSize(data1.Length);
        sender.Send(msg1);
        msg1.Dispose();

        using var recv1 = new Message();
        receiver.Recv(recv1);
        recv1.Size.Should().Be(data1.Length);

        // 두 번째 사용 (재사용)
        var msg2 = pool.Rent(1024);
        var data2 = "Second message longer"u8.ToArray();
        data2.CopyTo(msg2.Data);
        msg2.SetActualDataSize(data2.Length);
        sender.Send(msg2);

        using var recv2 = new Message();
        receiver.Recv(recv2);
        recv2.Size.Should().Be(data2.Length);
        recv2.Data.ToArray().Should().Equal(data2);

        msg2.Dispose();
    }

    #endregion

    #region ZeroCopy Verification Tests

    [Fact]
    public void ZeroCopy_DataIntegrity_VerifyNoCorruption()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 대용량 데이터로 zero-copy 시 데이터 무결성 검증
        var data = new byte[100_000];
        new Random(12345).NextBytes(data);

        var msg = pool.Rent(data);
        sender.Send(msg);
        msg.Dispose();

        using var recv = new Message();
        receiver.Recv(recv);

        recv.Size.Should().Be(data.Length);
        recv.Data.ToArray().Should().Equal(data);
    }

    [Fact]
    public void ZeroCopy_SmallMessage_DataIntegrity()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 작은 메시지도 zero-copy로 전송
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var msg = pool.Rent(data);
        sender.Send(msg);
        msg.Dispose();

        using var recv = new Message();
        receiver.Recv(recv);

        recv.Size.Should().Be(3);
        recv.Data.ToArray().Should().Equal(data);
    }

    [Fact]
    public void ZeroCopy_ExactBucketSize_WorksCorrectly()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 버킷 크기와 정확히 일치하는 데이터
        var bucketSize = 1024;
        var data = new byte[bucketSize];
        new Random(999).NextBytes(data);

        var msg = pool.Rent(data);
        msg.ActualDataSize.Should().Be(bucketSize);
        msg.BufferSize.Should().BeGreaterThanOrEqualTo(bucketSize);

        sender.Send(msg);
        msg.Dispose();

        using var recv = new Message();
        receiver.Recv(recv);

        recv.Size.Should().Be(bucketSize);
        recv.Data.ToArray().Should().Equal(data);
    }

    #endregion

    #region Concurrent Reinitialization Tests

    [Fact]
    public void ConcurrentSend_MultipleMessages_AllSucceed()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();
        const int messageCount = 100;
        var random = new Random(42);

        // 순차적으로 메시지 전송 (inproc은 동일 스레드에서 송수신)
        for (int i = 0; i < messageCount; i++)
        {
            var dataSize = random.Next(10, 1000);
            var data = new byte[dataSize];
            random.NextBytes(data);

            var msg = pool.Rent(data);
            sender.Send(msg);
            msg.Dispose();
        }

        // 모든 메시지 수신
        for (int i = 0; i < messageCount; i++)
        {
            using var recv = new Message();
            receiver.Recv(recv);
            recv.Size.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region Rent(int size) Flow Tests

    [Fact]
    public void RentIntSize_WriteData_SetActualDataSize_Send_WorksCorrectly()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // Rent(int size) → 데이터 직접 쓰기 → SetActualDataSize → Send 플로우
        var msg = pool.Rent(1024);
        var data = "Direct write test"u8.ToArray();

        // DataPtr에 직접 쓰기
        data.CopyTo(msg.Data);

        // 실제 크기 설정
        msg.SetActualDataSize(data.Length);

        // Act
        sender.Send(msg);
        msg.Dispose();

        // Assert
        using var recv = new Message();
        receiver.Recv(recv);
        recv.Size.Should().Be(data.Length);
        recv.Data.ToArray().Should().Equal(data);
    }

    [Fact]
    public void RentIntSize_Reuse_ZmqMsgReinitializedOnSend()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // 첫 번째: Rent(span)으로 전송
        var data1 = "First with Rent(span)"u8.ToArray();
        var msg1 = pool.Rent(data1);
        sender.Send(msg1);
        msg1.Dispose();

        using var recv1 = new Message();
        receiver.Recv(recv1);
        recv1.Size.Should().Be(data1.Length);

        // 두 번째: Rent(int)로 전송 (재사용된 메시지, zmq_msg_t는 nullified 상태)
        var msg2 = pool.Rent(1024);
        var data2 = "Second with Rent(int)"u8.ToArray();
        data2.CopyTo(msg2.Data);
        msg2.SetActualDataSize(data2.Length);
        sender.Send(msg2);
        msg2.Dispose();

        using var recv2 = new Message();
        receiver.Recv(recv2);
        recv2.Size.Should().Be(data2.Length);
        recv2.Data.ToArray().Should().Equal(data2);
    }

    [Fact]
    public void RentIntSize_NoSetActualDataSize_SendsBufferSize()
    {
        // Arrange
        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        var endpoint = $"inproc://reinit-test-{Guid.NewGuid()}";
        receiver.Bind(endpoint);
        sender.Connect(endpoint);

        var pool = new MessagePool();

        // Rent(int) 후 SetActualDataSize 호출 안 함
        var msg = pool.Rent(512);
        var data = "Short"u8.ToArray();
        data.CopyTo(msg.Data);
        // SetActualDataSize() 미호출!

        // Act
        sender.Send(msg);
        msg.Dispose();

        // Assert - 버퍼 크기 전체가 전송됨
        using var recv = new Message();
        receiver.Recv(recv);
        recv.Size.Should().Be(512);  // 버퍼 전체 크기
    }

    #endregion
}
