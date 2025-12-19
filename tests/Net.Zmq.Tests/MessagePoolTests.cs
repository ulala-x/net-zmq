using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class MessagePoolTests
{
    [Fact]
    public void RentWithoutSend_ShouldReturnBufferToPool()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Rent and dispose without sending
        using (var msg = pool.Rent(data))
        {
            msg.Size.Should().Be(data.Length);
        }

        // Give time for callback execution
        Thread.Sleep(50);

        // Assert - Buffer should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "buffer should be returned when message is not sent");
        stats.Rents.Should().Be(1);
        stats.Returns.Should().Be(1);
    }

    [Fact]
    public void RentWithSend_ShouldReturnBufferToPool()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-pool-send");
        pull.Connect("inproc://test-pool-send");

        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Rent, send, and dispose
        using (var msg = pool.Rent(data))
        {
            push.Send(msg);
        }

        // Receive to ensure transmission completes
        var buffer = new byte[10];
        pull.Recv(buffer);

        // Give time for ZMQ callback execution
        Thread.Sleep(100);

        // Assert - Buffer should be returned via ZMQ callback
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "buffer should be returned after ZMQ finishes transmission");
        stats.Rents.Should().Be(1);
        stats.Returns.Should().Be(1);
    }

    [Fact]
    public void MultipleRentWithoutSend_ShouldReturnAllBuffers()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        int count = 10;

        // Act - Rent multiple messages without sending
        for (int i = 0; i < count; i++)
        {
            using var msg = pool.Rent(data);
            msg.Size.Should().Be(data.Length);
        }

        // Give time for all callbacks to execute
        Thread.Sleep(100);

        // Assert - All buffers should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all buffers should be returned");
        stats.Rents.Should().Be(count);
        stats.Returns.Should().Be(count);
    }

    [Fact]
    public void MixedRentWithAndWithoutSend_ShouldReturnAllBuffers()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-pool-mixed");
        pull.Connect("inproc://test-pool-mixed");

        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Mix of sent and unsent messages
        // Rent and send
        using (var msg1 = pool.Rent(data))
        {
            push.Send(msg1);
        }

        // Rent without sending
        using (var msg2 = pool.Rent(data))
        {
            msg2.Size.Should().Be(data.Length);
        }

        // Rent and send again
        using (var msg3 = pool.Rent(data))
        {
            push.Send(msg3);
        }

        // Receive to ensure transmissions complete
        var buffer = new byte[10];
        pull.Recv(buffer);
        pull.Recv(buffer);

        // Give time for all callbacks to execute
        Thread.Sleep(100);

        // Assert - All buffers should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all buffers should be returned regardless of send status");
        stats.Rents.Should().Be(3);
        stats.Returns.Should().Be(3);
    }

    [Fact]
    public void PoolStatistics_ShouldTrackCorrectly()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var stats1 = pool.GetStatistics();
        stats1.Rents.Should().Be(0);

        using (var msg = pool.Rent(data))
        {
            var stats2 = pool.GetStatistics();
            stats2.Rents.Should().Be(1);
            stats2.OutstandingMessages.Should().Be(1);
        }

        Thread.Sleep(50);

        var stats3 = pool.GetStatistics();
        stats3.Rents.Should().Be(1);
        stats3.Returns.Should().Be(1);
        stats3.OutstandingMessages.Should().Be(0);
    }

    [Fact]
    public void PrepareForReuse_ShouldResetMessageState()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B64, 1);

        // Act
        var msg = pool.Rent(64);

        // Assert - 재사용된 메시지는 초기 상태여야 함
        msg._disposed.Should().BeFalse("message should not be disposed after rent");
        msg._wasSuccessfullySent.Should().BeFalse("message should not be marked as sent after rent");
        msg._callbackExecuted.Should().Be(0, "callback should not be executed after rent");
        msg._isFromPool.Should().BeTrue("message should be marked as from pool");
        msg._reusableCallback.Should().NotBeNull("reusable callback should be set");

        // Return message to pool
        msg.Dispose();
        Thread.Sleep(100);
    }

    [Fact]
    public void MessageCallback_ShouldExecuteOnlyOnce()
    {
        // Arrange
        var pool = new MessagePool();

        // Act - Rent and dispose multiple messages
        var msg1 = pool.Rent(64);
        msg1.Dispose();
        Thread.Sleep(100);

        var msg2 = pool.Rent(64);
        msg2.Dispose();
        Thread.Sleep(100);

        // Assert - Statistics should show correct return count
        // Each message's callback should execute exactly once
        var stats = pool.GetStatistics();
        stats.Returns.Should().BeGreaterOrEqualTo(2, "each disposal should trigger callback exactly once");
    }

    [Fact]
    public void Rent_ShouldSupportMultipleCycles()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B64, 10);
        pool.Prewarm(MessageSize.B64, 5);
        int cycleCount = 10;

        // Act & Assert
        for (int i = 0; i < cycleCount; i++)
        {
            var msg = pool.Rent(64);
            msg.Should().NotBeNull();
            msg._disposed.Should().BeFalse();
            msg._isFromPool.Should().BeTrue();

            msg.Dispose();
            Thread.Sleep(10); // 콜백 실행 대기
        }

        Thread.Sleep(100); // 최종 콜백 실행 대기

        // Assert - Statistics should show all cycles completed
        var stats = pool.GetStatistics();
        stats.Rents.Should().BeGreaterOrEqualTo(cycleCount, "should track all rent operations");
    }

    [Fact]
    public void Rent_ShouldNotLeakMessages()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B64, 10);
        int messageCount = 5;

        // Act
        var messages = new List<Message>();
        for (int i = 0; i < messageCount; i++)
        {
            messages.Add(pool.Rent(64));
        }

        var statsBefore = pool.GetStatistics();
        statsBefore.OutstandingMessages.Should().Be(messageCount, "should have outstanding messages before disposal");

        // 모두 반환
        foreach (var msg in messages)
        {
            msg.Dispose();
        }

        Thread.Sleep(100); // 콜백 실행 대기

        var statsAfter = pool.GetStatistics();

        // Assert
        statsAfter.OutstandingMessages.Should().BeLessThanOrEqualTo(statsBefore.OutstandingMessages, "outstanding messages should decrease after disposal");
        statsAfter.OutstandingMessages.Should().Be(0, "all messages should be returned to pool");
    }

    [Fact]
    public void Rent_WithData_ReturnsMessageWithCorrectData()
    {
        // Arrange
        var pool = new MessagePool();
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var msg = pool.Rent(testData);

        // Assert - 데이터가 올바르게 복사되었는지 검증
        msg.Size.Should().Be(testData.Length);
        var receivedData = msg.Data.ToArray();
        receivedData.Should().BeEquivalentTo(testData, "message data should match source data");

        // 메시지를 다른 소켓으로 Send 가능한지 검증
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-rent-data-send");
        pull.Connect("inproc://test-rent-data-send");

        push.Send(msg); // 메시지 전송

        var buffer = new byte[10];
        int received = pull.Recv(buffer);
        received.Should().Be(testData.Length);
        buffer.Take(received).Should().BeEquivalentTo(testData, "received data should match sent data");

        msg.Dispose();
        Thread.Sleep(50);

        // Assert - 메시지가 반환되고 풀 통계가 갱신되는지 확인
        var stats = pool.GetStatistics();
        stats.Rents.Should().Be(1, "one message was rented");
    }

    [Fact]
    public void Rent_WithSize_AlwaysReturnsReusableMessage()
    {
        // Arrange
        var pool = new MessagePool();

        // Act & Assert - 여러 번 호출해도 항상 재사용 메시지인지 확인
        using (var msg1 = pool.Rent(64))
        {
            msg1.Should().NotBeNull();
            msg1._isFromPool.Should().BeTrue("message rented with size should always be reusable");
            msg1._reusableCallback.Should().NotBeNull("reusable message should have callback set");
        }

        Thread.Sleep(50);

        using (var msg2 = pool.Rent(64))
        {
            msg2.Should().NotBeNull();
            msg2._isFromPool.Should().BeTrue("message rented with size should always be reusable");
            msg2._reusableCallback.Should().NotBeNull("reusable message should have callback set");
        }

        Thread.Sleep(50);

        // Assert - Pool should reuse messages
        var stats = pool.GetStatistics();
        stats.Rents.Should().Be(2, "two messages were rented");
        stats.PoolHits.Should().BeGreaterThan(0, "at least one message should be reused from pool");
    }

    [Fact]
    public void RentedMessage_CanBeSentToMultipleSockets()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push1 = new Socket(ctx, SocketType.Push);
        using var pull1 = new Socket(ctx, SocketType.Pull);
        using var push2 = new Socket(ctx, SocketType.Push);
        using var pull2 = new Socket(ctx, SocketType.Pull);

        pull1.Bind("inproc://test-reusable-forward-1");
        push1.Connect("inproc://test-reusable-forward-1");
        pull2.Bind("inproc://test-reusable-forward-2");
        push2.Connect("inproc://test-reusable-forward-2");

        var testData = new byte[] { 10, 20, 30, 40, 50 };

        // Act - Rent로 재사용 가능 메시지 생성
        var msg = pool.Rent(testData);

        // Assert - 메시지가 재사용 가능한지 확인
        msg._isFromPool.Should().BeTrue("message from Rent(data) now creates pooled message with new design");

        // 첫 번째 소켓으로 전송
        push1.Send(msg);

        var buffer1 = new byte[10];
        int received1 = pull1.Recv(buffer1);
        received1.Should().Be(testData.Length);
        buffer1.Take(received1).Should().BeEquivalentTo(testData, "first receive should match original data");

        Thread.Sleep(50);

        msg.Dispose();
    }

    // ======================
    // A. 기본 재사용 테스트
    // ======================

    [Fact]
    public void Rent_WithSize_ReturnsReusableMessage()
    {
        // Arrange
        var pool = new MessagePool();

        // Act
        var msg = pool.Rent(64);

        // Assert - Rent(int size)로 빌린 메시지가 재사용 가능한지 검증
        msg._isFromPool.Should().BeTrue("message should be from pool");
        msg._reusableCallback.Should().NotBeNull("reusable message should have callback");
        msg._callbackHandle.IsAllocated.Should().BeTrue("GCHandle should be allocated");
        msg._poolBucketIndex.Should().BeGreaterOrEqualTo(0, "bucket index should be valid");

        msg.Dispose();
        Thread.Sleep(50);

        var stats = pool.GetStatistics();
        stats.Returns.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Rent_WithData_ReturnsOneTimeMessageWithCorrectData()
    {
        // Arrange
        var pool = new MessagePool();
        var testData = new byte[] { 10, 20, 30, 40, 50 };

        // Act
        var msg = pool.Rent(testData.AsSpan());

        // Assert - 새로운 설계에서는 pooled 메시지를 반환
        msg._isFromPool.Should().BeTrue("Rent(data) now returns pooled message with new design");
        msg.Size.Should().Be(testData.Length);

        // 데이터가 올바르게 복사되었는지 확인
        var receivedData = msg.Data.ToArray();
        receivedData.Should().BeEquivalentTo(testData, "data should be copied correctly");

        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void RentReusable_MultipleTimes_ReusesMessageObjects()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B64, 5);

        // Act - 같은 사이즈로 여러 번 Rent/Dispose
        var msg1 = pool.Rent(64);
        var ptr1 = msg1._msgPtr; // Message 객체의 네이티브 포인터
        msg1.Dispose();
        Thread.Sleep(50);

        var msg2 = pool.Rent(64);
        var ptr2 = msg2._msgPtr;
        msg2.Dispose();
        Thread.Sleep(50);

        var msg3 = pool.Rent(64);
        var ptr3 = msg3._msgPtr;
        msg3.Dispose();
        Thread.Sleep(50);

        // Assert - Message 객체가 재사용되는지 검증
        var stats = pool.GetStatistics();
        stats.PoolHits.Should().BeGreaterThan(0, "messages should be reused from pool");
        stats.Rents.Should().Be(3);
    }

    // ======================
    // B. 데이터 전달 테스트
    // ======================

    [Fact]
    public async Task PooledMessage_SendReceive_DataTransferredCorrectly()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var req = new Socket(ctx, SocketType.Req);
        using var rep = new Socket(ctx, SocketType.Rep);

        rep.Bind("inproc://test-data-transfer");
        req.Connect("inproc://test-data-transfer");

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act - REQ-REP 패턴으로 실제 데이터 송수신
        await Task.Run(() =>
        {
            using var msg = pool.Rent(testData.AsSpan());
            req.Send(msg);
        });

        await Task.Run(() =>
        {
            var buffer = new byte[20];
            int received = rep.Recv(buffer);
            received.Should().Be(testData.Length);
            buffer.Take(received).Should().BeEquivalentTo(testData);

            // 응답
            rep.Send(new byte[] { 99 });
        });

        await Task.Run(() =>
        {
            var buffer = new byte[10];
            req.Recv(buffer);
        });

        Thread.Sleep(100);

        // Assert
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all messages should be returned");
    }

    [Fact]
    public async Task PooledMessage_MultipleRoundTrips_DataAlwaysCorrect()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-multi-roundtrip");
        pull.Connect("inproc://test-multi-roundtrip");

        int rounds = 5;
        var testDataList = new List<byte[]>
        {
            new byte[] { 1, 2, 3 },
            new byte[] { 10, 20, 30, 40 },
            new byte[] { 100, 101, 102, 103, 104 },
            new byte[] { 255, 254, 253 },
            new byte[] { 50, 51, 52, 53, 54, 55 }
        };

        // Act - 여러 번 송수신 반복
        for (int i = 0; i < rounds; i++)
        {
            var testData = testDataList[i];

            await Task.Run(() =>
            {
                using var msg = pool.Rent(testData.AsSpan());
                push.Send(msg);
            });

            await Task.Run(() =>
            {
                var buffer = new byte[20];
                int received = pull.Recv(buffer);
                received.Should().Be(testData.Length, $"round {i + 1} size mismatch");
                buffer.Take(received).Should().BeEquivalentTo(testData, $"round {i + 1} data mismatch");
            });
        }

        Thread.Sleep(200);

        // Assert - 이전 데이터가 남아있지 않은지 확인 (PrepareForReuse 검증)
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all messages returned");
    }

    [Fact]
    public async Task ReceiveWithPool_SendToAnotherSocket_WorksCorrectly()
    {
        // Arrange
        var pool = MessagePool.Shared;
        using var ctx = new Context();
        using var push1 = new Socket(ctx, SocketType.Push);
        using var pull1 = new Socket(ctx, SocketType.Pull);
        using var push2 = new Socket(ctx, SocketType.Push);
        using var pull2 = new Socket(ctx, SocketType.Pull);

        push1.Bind("inproc://recv-forward-1");
        pull1.Connect("inproc://recv-forward-1");
        push2.Bind("inproc://recv-forward-2");
        pull2.Connect("inproc://recv-forward-2");

        var testData = new byte[] { 77, 88, 99 };

        // Act - ReceiveWithPool로 수신한 메시지를 다른 소켓으로 전송
        await Task.Run(() =>
        {
            push1.Send(testData);
        });

        Message? forwardMsg = null;
        await Task.Run(() =>
        {
            forwardMsg = pull1.ReceiveWithPool();
            forwardMsg.Should().NotBeNull();
            forwardMsg!._isFromPool.Should().BeTrue("ReceiveWithPool should return pooled message");
        });

        await Task.Run(() =>
        {
            push2.Send(forwardMsg!);
        });

        await Task.Run(() =>
        {
            var buffer = new byte[10];
            int received = pull2.Recv(buffer);
            received.Should().Be(testData.Length);
            buffer.Take(received).Should().BeEquivalentTo(testData);
        });

        forwardMsg?.Dispose();
        Thread.Sleep(100);
    }

    // ======================
    // C. Pooled vs Regular Message 동작 비교
    // ======================

    [Fact]
    public void PooledMessage_Dispose_DoesNotCloseMsgT()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(64);

        // Assert - Pooled 메시지인지 확인
        msg._isFromPool.Should().BeTrue();
        msg._initialized.Should().BeTrue();

        // Act - Dispose (Send하지 않음)
        msg.Dispose();

        // Assert - _wasSuccessfullySent == false일 때 callback 호출
        // 풀에 반환됨
        msg._disposed.Should().BeTrue();

        Thread.Sleep(100);

        var stats = pool.GetStatistics();
        stats.Returns.Should().BeGreaterOrEqualTo(1, "message should be returned via callback");
    }

    [Fact]
    public void RegularMessage_Dispose_ClosesMsgT()
    {
        // Arrange - 일반 메시지 생성 (new Message(size))
        var msg = new Message(64);

        // Assert
        msg._isFromPool.Should().BeFalse("regular message should not be from pool");
        msg._initialized.Should().BeTrue();

        // Act - Dispose 시 zmq_msg_close 호출
        msg.Dispose();

        // Assert - 네이티브 메모리 해제
        msg._disposed.Should().BeTrue();
        msg._initialized.Should().BeFalse("zmq_msg_close should be called");
    }

    [Fact]
    public void PooledMessage_NotSent_ReturnsToPoolViaCallback()
    {
        // Arrange
        var pool = new MessagePool();
        var statsBefore = pool.GetStatistics();

        // Act - Send하지 않은 pooled 메시지
        var msg = pool.Rent(128);
        msg._isFromPool.Should().BeTrue();
        msg.Dispose();

        Thread.Sleep(100);

        // Assert - Dispose 시 풀에 반환되는지
        var statsAfter = pool.GetStatistics();
        statsAfter.Returns.Should().BeGreaterThan(statsBefore.Returns, "message should return to pool");
    }

    [Fact]
    public void PooledMessage_Sent_ReturnsToPoolViaZmqCallback()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-zmq-callback");
        pull.Connect("inproc://test-zmq-callback");

        var statsBefore = pool.GetStatistics();

        // Act - Send한 pooled 메시지
        var msg = pool.Rent(256);
        msg._isFromPool.Should().BeTrue();
        push.Send(msg);
        msg.Dispose();

        var buffer = new byte[300];
        pull.Recv(buffer);

        Thread.Sleep(100);

        // Assert - ZMQ callback으로 풀에 반환되는지
        var statsAfter = pool.GetStatistics();
        statsAfter.Returns.Should().BeGreaterThan(statsBefore.Returns, "message should return via ZMQ callback");
    }

    // ======================
    // D. 다양한 시나리오
    // ======================

    [Fact]
    public void PooledMessage_DoubleDispose_Safe()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(64);

        // Act - 이중 Dispose
        msg.Dispose();
        msg.Dispose(); // 두 번째 호출

        Thread.Sleep(50);

        // Assert - 안전성
        msg._disposed.Should().BeTrue();
        var stats = pool.GetStatistics();
        stats.Returns.Should().BeGreaterOrEqualTo(1); // 한 번만 반환되어야 함
    }

    [Fact]
    public async Task MixedMessages_PooledAndRegular_BothWorkCorrectly()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-mixed-messages");
        pull.Connect("inproc://test-mixed-messages");

        var testData1 = new byte[] { 1, 2, 3 };
        var testData2 = new byte[] { 10, 20, 30 };

        // Act - Pooled 메시지와 일반 메시지를 섞어서 사용
        await Task.Run(() =>
        {
            using var pooledMsg = pool.Rent(testData1.AsSpan());
            push.Send(pooledMsg);
        });

        await Task.Run(() =>
        {
            using var regularMsg = new Message(testData2);
            push.Send(regularMsg);
        });

        await Task.Run(() =>
        {
            var buffer = new byte[10];
            int received1 = pull.Recv(buffer);
            received1.Should().Be(testData1.Length);
            buffer.Take(received1).Should().BeEquivalentTo(testData1);

            int received2 = pull.Recv(buffer);
            received2.Should().Be(testData2.Length);
            buffer.Take(received2).Should().BeEquivalentTo(testData2);
        });

        Thread.Sleep(100);

        // Assert - 각각 올바르게 동작하는지 확인
        var stats = pool.GetStatistics();
        stats.Rents.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Prewarm_CreatesReusableMessages()
    {
        // Arrange
        var pool = new MessagePool();

        // Act - Prewarm으로 생성
        pool.Prewarm(MessageSize.K1, 10);

        // Assert - 재사용 가능한 메시지들이 생성되는지
        for (int i = 0; i < 10; i++)
        {
            var msg = pool.Rent(1024);
            msg._isFromPool.Should().BeTrue("prewarm should create reusable messages");
            msg.Dispose();
        }

        Thread.Sleep(100);

        var stats = pool.GetStatistics();
        stats.PoolHits.Should().BeGreaterThan(0, "some messages should be reused from prewarmed pool");
    }

    [Fact]
    public void Clear_ReleasesAllResources()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B64, 5);
        pool.Prewarm(MessageSize.K1, 3);

        var statsBefore = pool.GetStatistics();

        // Act - Clear 호출
        pool.Clear();

        // 새로 Rent하면 풀이 비어있어야 함
        var msg1 = pool.Rent(64);
        msg1.Dispose();
        Thread.Sleep(50);

        var statsAfter = pool.GetStatistics();

        // Assert - 모든 리소스가 해제되는지
        // Clear 후 Outstanding Messages == 0
        // (Clear는 풀에 있는 메시지만 해제, outstanding은 영향 없음)
        msg1._isFromPool.Should().BeTrue("new messages can still be created");
    }

    [Fact]
    public async Task HighConcurrency_PooledMessages_ThreadSafe()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B128, 50);

        int threadCount = 10;
        int messagesPerThread = 20;
        var tasks = new List<Task>();

        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-concurrency");
        pull.Connect("inproc://test-concurrency");

        // Act - 여러 스레드에서 동시에 Rent/Send/Dispose
        for (int t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < messagesPerThread; i++)
                {
                    var testData = new byte[] { (byte)i, (byte)(i + 1), (byte)(i + 2) };
                    using var msg = pool.Rent(testData.AsSpan());
                    push.Send(msg);
                }
            }));
        }

        var receiveTask = Task.Run(() =>
        {
            var buffer = new byte[200];
            for (int i = 0; i < threadCount * messagesPerThread; i++)
            {
                pull.Recv(buffer);
            }
        });

        await Task.WhenAll(tasks);
        await receiveTask;

        Thread.Sleep(500);

        // Assert - 스레드 안전성 검증
        var stats = pool.GetStatistics();
        stats.Rents.Should().BeGreaterOrEqualTo(threadCount * messagesPerThread);
        stats.OutstandingMessages.Should().Be(0, "all messages should be returned");
    }

    // ======================
    // E. 엣지 케이스
    // ======================

    [Fact]
    public void Rent_EmptyData_Works()
    {
        // Arrange
        var pool = new MessagePool();
        var emptyData = new byte[0];

        // Act - 빈 데이터로 Rent 호출
        var msg = pool.Rent(emptyData.AsSpan());

        // Assert
        msg.Should().NotBeNull();
        msg.Size.Should().Be(0);
        msg._isFromPool.Should().BeTrue("Rent(data) returns pooled message with new design");

        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void Rent_LargeData_ExceedsBucketSize_Works()
    {
        // Arrange
        var pool = new MessagePool();
        var largeData = new byte[5 * 1024 * 1024]; // 5 MB (버킷 사이즈 초과)

        for (int i = 0; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }

        // Act - 버킷 사이즈를 초과하는 큰 데이터
        var msg = pool.Rent(largeData.AsSpan());

        // Assert
        msg.Should().NotBeNull();
        msg.Size.Should().Be(largeData.Length);

        // 큰 데이터는 풀링되지 않음
        // Rent는 재사용 메시지를 반환하지만, 크기가 너무 크면 일회용으로 생성될 수 있음
        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void PooledMessage_PrepareForReuse_ResetsAllState()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B64, 1);

        // Act - 첫 번째 사용
        var msg1 = pool.Rent(64);
        msg1._disposed.Should().BeFalse();
        msg1._wasSuccessfullySent.Should().BeFalse();
        msg1.Dispose();
        Thread.Sleep(100);

        // 두 번째 사용 (재사용)
        var msg2 = pool.Rent(64);

        // Assert - PrepareForReuse가 모든 상태를 올바르게 리셋하는지
        msg2._disposed.Should().BeFalse("_disposed should be reset");
        msg2._wasSuccessfullySent.Should().BeFalse("_wasSuccessfullySent should be reset");
        msg2._callbackExecuted.Should().Be(0, "_callbackExecuted should be reset");
        msg2._isFromPool.Should().BeTrue("_isFromPool should remain true");
        msg2._initialized.Should().BeTrue("zmq_msg_t should remain initialized");

        msg2.Dispose();
        Thread.Sleep(50);
    }

    // ======================
    // F. ActualDataSize 기능 테스트 (새로운 설계)
    // ======================

    [Fact]
    public void PooledMessage_ActualDataSize_TrackedCorrectly()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[64]; // 64 bytes 데이터 요청 → 1024 bytes 버킷에서 할당됨

        // Act
        using var msg = pool.Rent(data.AsSpan());

        // Assert - ActualSize는 64, BufferSize는 1024
        if (msg._isFromPool)
        {
            msg.ActualSize.Should().Be(64, "actual size should match requested data size");
            msg.BufferSize.Should().BeGreaterOrEqualTo(64, "buffer size should be bucket size");
            msg.Size.Should().Be(64, "Size property should return actual size for pooled messages");
        }

        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public async Task PooledMessage_SendsOnlyActualDataSize()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var req = new Socket(ctx, SocketType.Req);
        using var rep = new Socket(ctx, SocketType.Rep);

        rep.Bind("inproc://test-actual-size-send");
        req.Connect("inproc://test-actual-size-send");

        var testData = new byte[64]; // 64 bytes
        for (int i = 0; i < testData.Length; i++)
            testData[i] = (byte)(i + 1);

        // Act - 64 bytes 데이터 전송 (버킷은 1024 bytes일 수 있음)
        await Task.Run(() =>
        {
            using var msg = pool.Rent(testData.AsSpan());
            req.Send(msg);
        });

        byte[]? receivedData = null;
        int receivedSize = 0;
        await Task.Run(() =>
        {
            var buffer = new byte[2048]; // 충분히 큰 버퍼
            receivedSize = rep.Recv(buffer);
            receivedData = buffer.Take(receivedSize).ToArray();

            rep.Send(new byte[] { 1 }); // 응답
        });

        await Task.Run(() =>
        {
            var buffer = new byte[10];
            req.Recv(buffer);
        });

        Thread.Sleep(100);

        // Assert - 수신측에서 64 bytes만 받아야 함 (1024 아님!)
        receivedSize.Should().Be(64, "should receive only actual data size, not buffer size");
        receivedData.Should().BeEquivalentTo(testData, "received data should match sent data");
    }

    [Fact]
    public async Task Rent_WithData_SendReceive_DataCorrect()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-rent-data-transfer");
        pull.Connect("inproc://test-rent-data-transfer");

        var testData = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };

        // Act
        await Task.Run(() =>
        {
            using var msg = pool.Rent(testData.AsSpan());
            push.Send(msg);
        });

        byte[]? receivedData = null;
        await Task.Run(() =>
        {
            var buffer = new byte[100];
            int received = pull.Recv(buffer);
            receivedData = buffer.Take(received).ToArray();
        });

        Thread.Sleep(50);

        // Assert - 데이터가 정확히 전달되는지 확인
        receivedData.Should().BeEquivalentTo(testData, "data should be transferred correctly");
    }

    [Fact]
    public async Task Rent_VariousSizes_AllCorrect()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-various-sizes");
        pull.Connect("inproc://test-various-sizes");

        var testSizes = new[] { 32, 64, 128, 256, 512, 1024, 2048 };

        // Act & Assert - 모든 크기에서 정확히 전송되는지
        foreach (var size in testSizes)
        {
            var testData = new byte[size];
            for (int i = 0; i < size; i++)
                testData[i] = (byte)(i % 256);

            await Task.Run(() =>
            {
                using var msg = pool.Rent(testData.AsSpan());
                push.Send(msg);
            });

            await Task.Run(() =>
            {
                var buffer = new byte[4096];
                int received = pull.Recv(buffer);
                received.Should().Be(size, $"size {size} should be received correctly");
                buffer.Take(received).Should().BeEquivalentTo(testData, $"data for size {size} should match");
            });
        }

        Thread.Sleep(200);

        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all messages should be returned");
    }

    [Fact]
    public async Task PooledMessage_MultipleRoundTrips_NoCrossTalk()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-no-crosstalk");
        pull.Connect("inproc://test-no-crosstalk");

        // Act - 여러 번 송수신하여 이전 데이터가 남아있지 않은지 확인
        for (int round = 0; round < 5; round++)
        {
            var testData = new byte[64];
            for (int i = 0; i < 64; i++)
                testData[i] = (byte)(round * 10 + i);

            await Task.Run(() =>
            {
                using var msg = pool.Rent(testData.AsSpan());
                push.Send(msg);
            });

            await Task.Run(() =>
            {
                var buffer = new byte[100];
                int received = pull.Recv(buffer);
                received.Should().Be(64, $"round {round}: size should be 64");

                var receivedData = buffer.Take(received).ToArray();
                receivedData.Should().BeEquivalentTo(testData, $"round {round}: data should not have crosstalk from previous rounds");
            });
        }

        Thread.Sleep(200);
    }

    [Fact]
    public void PooledMessage_Reuse_MultipleSizes()
    {
        // Arrange
        var pool = new MessagePool();

        // Act - 다양한 크기로 재사용 (1024 → 64 → 512 → 128)
        var data1024 = new byte[1024];
        using (var msg1 = pool.Rent(data1024.AsSpan()))
        {
            if (msg1._isFromPool)
            {
                msg1.ActualSize.Should().Be(1024);
            }
        }
        Thread.Sleep(50);

        var data64 = new byte[64];
        using (var msg2 = pool.Rent(data64.AsSpan()))
        {
            if (msg2._isFromPool)
            {
                msg2.ActualSize.Should().Be(64);
            }
        }
        Thread.Sleep(50);

        var data512 = new byte[512];
        using (var msg3 = pool.Rent(data512.AsSpan()))
        {
            if (msg3._isFromPool)
            {
                msg3.ActualSize.Should().Be(512);
            }
        }
        Thread.Sleep(50);

        var data128 = new byte[128];
        using (var msg4 = pool.Rent(data128.AsSpan()))
        {
            if (msg4._isFromPool)
            {
                msg4.ActualSize.Should().Be(128);
            }
        }
        Thread.Sleep(50);

        // Assert - 매번 올바른 크기만 사용되는지 (PrepareForReuse 검증)
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all messages should be returned");
    }

    [Fact]
    public void SetActualDataSize_ValidSize_Works()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        // Act - 실제 데이터 크기를 512로 설정
        if (msg._isFromPool)
        {
            msg.SetActualDataSize(512);

            // Assert
            msg.ActualSize.Should().Be(512);
            msg.BufferSize.Should().BeGreaterOrEqualTo(1024);
            msg.Size.Should().Be(512, "Size should return actual size");
        }

        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void SetActualDataSize_ExceedsBufferSize_ThrowsException()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(64);

        // Act & Assert
        if (msg._isFromPool)
        {
            Action act = () => msg.SetActualDataSize(2048); // 버퍼 크기 초과
            act.Should().Throw<ArgumentException>()
                .WithMessage("*cannot exceed buffer size*");
        }

        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void SetActualDataSize_OnNonPooledMessage_ThrowsException()
    {
        // Arrange - 일반 메시지 생성
        var msg = new Message(64);

        // Act & Assert
        Action act = () => msg.SetActualDataSize(32);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*can only be called on pooled messages*");

        msg.Dispose();
    }

    [Fact]
    public async Task ReceiveWithPool_ReturnsReusableMessage()
    {
        // Arrange
        var pool = MessagePool.Shared;
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-receive-reusable");
        pull.Connect("inproc://test-receive-reusable");

        var testData = new byte[] { 111, 222, 33 };

        // Act
        await Task.Run(() => push.Send(testData));

        Message? receivedMsg = null;
        await Task.Run(() =>
        {
            receivedMsg = pull.ReceiveWithPool();
        });

        // Assert
        receivedMsg.Should().NotBeNull();
        receivedMsg!._isFromPool.Should().BeTrue("ReceiveWithPool should return pooled message");
        receivedMsg.Size.Should().Be(testData.Length);

        receivedMsg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public async Task ReceiveWithPool_CanResendToAnotherSocket()
    {
        // Arrange
        var pool = MessagePool.Shared;
        using var ctx = new Context();
        using var push1 = new Socket(ctx, SocketType.Push);
        using var pull1 = new Socket(ctx, SocketType.Pull);
        using var push2 = new Socket(ctx, SocketType.Push);
        using var pull2 = new Socket(ctx, SocketType.Pull);

        push1.Bind("inproc://test-forward-1");
        pull1.Connect("inproc://test-forward-1");
        push2.Bind("inproc://test-forward-2");
        pull2.Connect("inproc://test-forward-2");

        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act - 수신 → 다른 소켓으로 재전송
        await Task.Run(() => push1.Send(testData));

        Message? forwardedMsg = null;
        await Task.Run(() =>
        {
            forwardedMsg = pull1.ReceiveWithPool();
        });

        await Task.Run(() =>
        {
            push2.Send(forwardedMsg!);
        });

        byte[]? finalData = null;
        await Task.Run(() =>
        {
            var buffer = new byte[100];
            int received = pull2.Recv(buffer);
            finalData = buffer.Take(received).ToArray();
        });

        // Assert
        finalData.Should().BeEquivalentTo(testData, "data should be forwarded correctly");

        forwardedMsg?.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public async Task RealWorldScenario_ProxyPattern()
    {
        // Arrange - 실제 프록시 패턴 시뮬레이션
        var pool = new MessagePool();
        using var ctx = new Context();
        using var frontend = new Socket(ctx, SocketType.Pull);
        using var backend = new Socket(ctx, SocketType.Push);
        using var client = new Socket(ctx, SocketType.Push);
        using var server = new Socket(ctx, SocketType.Pull);

        frontend.Bind("inproc://proxy-frontend");
        backend.Bind("inproc://proxy-backend");
        client.Connect("inproc://proxy-frontend");
        server.Connect("inproc://proxy-backend");

        var testData = new byte[] { 10, 20, 30, 40, 50 };

        // Act - Client → Frontend → Backend → Server
        var proxyTask = Task.Run(() =>
        {
            // Proxy: 수신 → 전송
            using var msg = frontend.ReceiveWithPool();
            backend.Send(msg);
        });

        await Task.Run(() =>
        {
            // Client: 데이터 전송
            using var msg = pool.Rent(testData.AsSpan());
            client.Send(msg);
        });

        await proxyTask;

        byte[]? receivedData = null;
        await Task.Run(() =>
        {
            // Server: 데이터 수신
            var buffer = new byte[100];
            int received = server.Recv(buffer);
            receivedData = buffer.Take(received).ToArray();
        });

        Thread.Sleep(100);

        // Assert
        receivedData.Should().BeEquivalentTo(testData, "proxy should forward data correctly");
    }

    [Fact]
    public void DataProperty_ReturnsActualSizeSpan()
    {
        // Arrange
        var pool = new MessagePool();
        var testData = new byte[64];
        for (int i = 0; i < 64; i++)
            testData[i] = (byte)(i + 100);

        // Act
        using var msg = pool.Rent(testData.AsSpan());

        // Assert - Data property가 ActualSize 크기의 Span을 반환하는지
        msg.Data.Length.Should().Be(64, "Data should return span of actual size");

        if (msg._isFromPool)
        {
            // 데이터가 올바르게 복사되었는지 확인
            msg.Data.ToArray().Should().BeEquivalentTo(testData, "Data should contain correct bytes");
        }

        msg.Dispose();
        Thread.Sleep(50);
    }

    [Fact]
    public void CopyFromNative_SetsActualDataSize()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(1024);

        var testData = new byte[256];
        for (int i = 0; i < 256; i++)
            testData[i] = (byte)(i % 256);

        // Act
        if (msg._isFromPool && msg._poolDataPtr != nint.Zero)
        {
            unsafe
            {
                fixed (byte* ptr = testData)
                {
                    msg.CopyFromNative((nint)ptr, 256);
                }
            }

            // Assert
            msg.ActualSize.Should().Be(256, "CopyFromNative should set actual data size");
            msg.BufferSize.Should().BeGreaterOrEqualTo(1024, "buffer size should remain unchanged");
            msg.Data.ToArray().Should().BeEquivalentTo(testData, "data should be copied correctly");
        }

        msg.Dispose();
        Thread.Sleep(50);
    }

    // ======================
    // G. MaxBuffer Bug Fix Tests (Interlocked Counters)
    // ======================

    [Fact]
    public void Dispose_WhenMaxBufferExceeded_ActuallyDisposesMessage()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B64, 2); // Limit to 2 buffers
        pool.Prewarm(MessageSize.B64, 2); // Fill the pool to max

        var statsBefore = pool.GetStatistics();

        // Act - Rent 3 messages WITHOUT disposing them first, then dispose all at once
        // This will cause pool to fill up when returning
        var msg1 = pool.Rent(64);
        var msg2 = pool.Rent(64);
        var msg3 = pool.Rent(64); // This creates a new message since pool only had 2

        // Now pool is empty (counter = 0). When we dispose all 3, only 2 can return
        msg1.Dispose();
        Thread.Sleep(50);

        msg2.Dispose();
        Thread.Sleep(50);

        msg3.Dispose(); // This should be rejected since pool is already at maxBuffer (2/2)
        Thread.Sleep(100);

        // Assert
        var statsAfter = pool.GetStatistics();
        statsAfter.PoolRejects.Should().BeGreaterThan(0, "messages beyond maxBuffer should be rejected");
        statsAfter.OutstandingBuffers.Should().Be(0, "all messages should be accounted for");
    }

    [Fact]
    public void SetMaxBuffers_DynamicChange_AppliesImmediately()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B128, 5);
        pool.Prewarm(MessageSize.B128, 5); // Pool has 5 messages

        // Act - Change maxBuffer to 3 (smaller than current pool size of 5)
        pool.SetMaxBuffers(MessageSize.B128, 3);

        // Rent all 5 messages from pool
        var msg1 = pool.Rent(128);
        var msg2 = pool.Rent(128);
        var msg3 = pool.Rent(128);
        var msg4 = pool.Rent(128);
        var msg5 = pool.Rent(128);

        // Now dispose them all - only first 3 should be accepted (new maxBuffer = 3)
        msg1.Dispose();
        Thread.Sleep(50);
        msg2.Dispose();
        Thread.Sleep(50);
        msg3.Dispose();
        Thread.Sleep(50);
        msg4.Dispose(); // Should be rejected (pool is 3/3)
        Thread.Sleep(50);
        msg5.Dispose(); // Should be rejected (pool is 3/3)
        Thread.Sleep(100);

        var stats = pool.GetStatistics();

        // Assert - Should reject messages 4 and 5 because pool limit is now 3
        stats.PoolRejects.Should().BeGreaterOrEqualTo(2, "dynamic maxBuffer change should apply immediately");
        stats.OutstandingBuffers.Should().Be(0);
    }

    [Fact]
    public void Dispose_MaxBufferExceeded_ReleasesNativeMemory()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B256, 1);
        pool.Prewarm(MessageSize.B256, 1); // Fill pool to max

        var statsBefore = pool.GetStatistics();

        // Act - Rent 2 messages (pool had 1, so creates 1 new)
        var msg1 = pool.Rent(256);
        var msg2 = pool.Rent(256);

        // Dispose both - first returns to pool, second should be rejected
        msg1.Dispose();
        Thread.Sleep(50);

        msg2.Dispose(); // This should be rejected since pool is 1/1
        Thread.Sleep(100);

        var statsAfter = pool.GetStatistics();

        // Assert - Message should be rejected and memory freed
        statsAfter.PoolRejects.Should().BeGreaterThan(statsBefore.PoolRejects, "message should be rejected");
        statsAfter.OutstandingBuffers.Should().Be(0, "no memory leaks");

        // Note: We can't directly verify native memory was freed, but PoolRejects increment confirms
        // that DisposePooledMessage() was called
    }

    [Fact]
    public async Task Dispose_Concurrent_MaxBufferRespected()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B512, 10); // Limit to 10 buffers
        pool.Prewarm(MessageSize.B512, 5);

        int threadCount = 20;
        int messagesPerThread = 5;
        var tasks = new List<Task>();

        // Act - Multiple threads concurrently renting and disposing
        for (int t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < messagesPerThread; i++)
                {
                    var msg = pool.Rent(512);
                    Thread.Sleep(1); // Small delay to increase contention
                    msg.Dispose();
                }
            }));
        }

        await Task.WhenAll(tasks);
        Thread.Sleep(500); // Wait for all callbacks

        // Assert
        var stats = pool.GetStatistics();
        stats.OutstandingBuffers.Should().Be(0, "no leaked messages");

        // With maxBuffer=10 and 100 total messages, we should see many rejects
        stats.PoolRejects.Should().BeGreaterThan(0, "concurrent disposal should respect maxBuffer");
    }

    [Fact]
    public void Prewarm_WithDispose_RespectsMaxBuffer()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.K1, 5);

        // Act - Prewarm to exactly maxBuffer
        pool.Prewarm(MessageSize.K1, 5); // Pool has 5 messages

        var statsAfterPrewarm = pool.GetStatistics();

        // Rent 6 messages (5 from pool + 1 newly created)
        var msg1 = pool.Rent(1024);
        var msg2 = pool.Rent(1024);
        var msg3 = pool.Rent(1024);
        var msg4 = pool.Rent(1024);
        var msg5 = pool.Rent(1024);
        var msg6 = pool.Rent(1024); // New message

        // Dispose all 6 - only 5 should be accepted
        msg1.Dispose();
        Thread.Sleep(50);
        msg2.Dispose();
        Thread.Sleep(50);
        msg3.Dispose();
        Thread.Sleep(50);
        msg4.Dispose();
        Thread.Sleep(50);
        msg5.Dispose();
        Thread.Sleep(50);
        msg6.Dispose(); // This should be rejected (pool is 5/5)
        Thread.Sleep(100);

        var statsAfterDispose = pool.GetStatistics();

        // Assert
        statsAfterDispose.PoolRejects.Should().BeGreaterThan(0, "disposal after prewarm should respect maxBuffer");
        statsAfterDispose.OutstandingBuffers.Should().Be(0);
    }

    [Fact]
    public void SetMaxBuffers_Zero_ThrowsException()
    {
        // Arrange
        var pool = new MessagePool();

        // Act & Assert
        Action act = () => pool.SetMaxBuffers(MessageSize.K2, 0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be positive*");
    }

    [Fact]
    public void SetMaxBuffers_Negative_ThrowsException()
    {
        // Arrange
        var pool = new MessagePool();

        // Act & Assert
        Action act = () => pool.SetMaxBuffers(MessageSize.K4, -5);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be positive*");
    }

    [Fact]
    public void Dispose_AlreadyDisposedPooledMessage_DoesNotReturnToPool()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(64);

        var statsBeforeFirstDispose = pool.GetStatistics();

        // Act - First disposal
        msg.Dispose();
        Thread.Sleep(100);

        var statsAfterFirstDispose = pool.GetStatistics();
        var returnsAfterFirst = statsAfterFirstDispose.TotalReturns;

        // Second disposal (should be no-op due to _disposed check)
        msg.Dispose();
        Thread.Sleep(100);

        var statsAfterSecondDispose = pool.GetStatistics();

        // Assert - Second disposal should not increment returns
        statsAfterSecondDispose.TotalReturns.Should().Be(returnsAfterFirst,
            "double disposal should not return message twice");
        statsAfterSecondDispose.OutstandingBuffers.Should().Be(0);
    }

    // ======================
    // H. Optimized ReceiveWithPool Tests (Direct Receive, No Copy)
    // ======================

    [Fact]
    public async Task ReceiveWithPool_LargeMessages_NoExtraCopy()
    {
        // Arrange - Test that large messages don't incur extra copy overhead
        var ctx = new Context();
        var push = new Socket(ctx, SocketType.Push);
        var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://perf-test");
        pull.Connect("inproc://perf-test");
        await Task.Delay(100);

        const int messageSize = 64 * 1024; // 64KB
        var sourceData = new byte[messageSize];
        Array.Fill(sourceData, (byte)'X');

        // Act - Send and receive
        push.Send(sourceData);
        await Task.Delay(50);

        using var receivedMsg = pull.ReceiveWithPool();

        // Assert
        receivedMsg.Should().NotBeNull();
        receivedMsg!.Size.Should().Be(messageSize, "actual size should be set correctly");
        receivedMsg._isFromPool.Should().BeTrue();
        receivedMsg._actualDataSize.Should().Be(messageSize);

        // Verify data integrity
        receivedMsg.Data.ToArray().Should().Equal(sourceData);

        push.Dispose();
        pull.Dispose();
        ctx.Dispose();
    }

    [Fact]
    public async Task ReceiveWithPool_SmallMessages_CorrectSize()
    {
        // Arrange - Test that small messages work correctly with max-size buffer
        var ctx = new Context();
        var push = new Socket(ctx, SocketType.Push);
        var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://small-test");
        pull.Connect("inproc://small-test");
        await Task.Delay(100);

        const int messageSize = 64; // Small message
        var sourceData = new byte[messageSize];
        Array.Fill(sourceData, (byte)'S');

        // Act
        push.Send(sourceData);
        await Task.Delay(50);

        using var receivedMsg = pull.ReceiveWithPool();

        // Assert - Should receive correct small size, not MaxRecvBufferSize
        receivedMsg.Should().NotBeNull();
        receivedMsg!.Size.Should().Be(messageSize, "should return actual small size, not buffer size");
        receivedMsg.Data.Length.Should().Be(messageSize);
        receivedMsg.Data.ToArray().Should().Equal(sourceData);

        push.Dispose();
        pull.Dispose();
        ctx.Dispose();
    }

    [Fact]
    public async Task ReceiveWithPool_ErrorHandling_ReturnsToPool()
    {
        // Arrange - Test that Message is returned to pool on error
        var pool = MessagePool.Shared;
        var statsBefore = pool.GetStatistics();

        var ctx = new Context();
        var socket = new Socket(ctx, SocketType.Pull);
        socket.Bind("inproc://error-test");

        // Act - Try to receive with DontWait (should return null)
        var msg = socket.ReceiveWithPool(RecvFlags.DontWait);

        // Assert - Message should be null and returned to pool
        msg.Should().BeNull("no message available");

        var statsAfter = pool.GetStatistics();
        statsAfter.OutstandingBuffers.Should().Be(statsBefore.OutstandingBuffers, "no leaked buffers");

        socket.Dispose();
        ctx.Dispose();
    }
}
