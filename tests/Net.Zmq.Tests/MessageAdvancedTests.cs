using FluentAssertions;
using System.Text;
using Xunit;

namespace Net.Zmq.Tests;

/// <summary>
/// Advanced unit tests for the Message struct covering edge cases and complex scenarios.
/// </summary>
[Collection("Sequential")]
public class MessageAdvancedTests
{
    [Fact]
    public void Message_LargeData_ShouldHandle()
    {
        // Arrange - Create 1MB of data
        const int size = 1024 * 1024; // 1MB
        var largeData = new byte[size];

        // Fill with pattern for verification
        for (int i = 0; i < size; i++)
        {
            largeData[i] = (byte)(i % 256);
        }

        // Act
        using var msg = new Message(largeData);

        // Assert
        msg.Size.Should().Be(size);

        // Verify data integrity
        var retrievedData = msg.Data.ToArray();
        retrievedData.Should().HaveCount(size);

        // Sample verification (checking every 1000th byte for performance)
        for (int i = 0; i < size; i += 1000)
        {
            retrievedData[i].Should().Be((byte)(i % 256),
                $"byte at index {i} should match pattern");
        }
    }

    [Fact]
    public void Message_Move_ShouldTransferOwnership()
    {
        // Arrange
        var testData = "Original message data"u8.ToArray();
        var source = new Message(testData);
        var destination = new Message();

        var originalSize = source.Size;

        // Act
        destination.Move(source);

        // Assert
        destination.Size.Should().Be(originalSize);
        destination.ToString().Should().Be("Original message data");

        // Source should be empty after move (ZMQ behavior)
        source.Size.Should().Be(0);

        // Cleanup
        source.Dispose();
        destination.Dispose();
    }

    [Fact]
    public void Message_Copy_ShouldDuplicateData()
    {
        // Arrange
        var testData = "Test data for copy"u8.ToArray();
        var source = new Message(testData);
        var destination = new Message();

        var originalSize = source.Size;

        // Act
        destination.Copy(source);

        // Assert
        destination.Size.Should().Be(originalSize);
        destination.ToString().Should().Be("Test data for copy");

        // Source should remain unchanged after copy
        source.Size.Should().Be(originalSize);
        source.ToString().Should().Be("Test data for copy");

        // Verify they are independent (modify destination)
        var destData = destination.Data;
        if (destData.Length > 0)
        {
            destData[0] = (byte)'X';
        }

        // Source should not be affected
        source.Data[0].Should().NotBe((byte)'X');

        // Cleanup
        source.Dispose();
        destination.Dispose();
    }

    [Fact]
    public void Message_DirectDataAccess_ShouldWork()
    {
        // Arrange
        using var msg = new Message(10);

        // Act - Direct access via Span<byte>
        var data = msg.Data;
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 10);
        }

        // Assert
        msg.Size.Should().Be(10);
        var retrievedData = msg.Data;

        for (int i = 0; i < retrievedData.Length; i++)
        {
            retrievedData[i].Should().Be((byte)(i * 10),
                $"byte at index {i} should be {i * 10}");
        }
    }

    [Fact]
    public void Message_UnicodeString_ShouldPreserve()
    {
        // Arrange - Various Unicode characters
        var testStrings = new[]
        {
            "English text",
            "한글 텍스트",
            "日本語テキスト",
            "中文文本",
            "Русский текст",
            "العربية",
            "עברית",
            "Emoji: 😀🎉🚀💻",
            "Mixed: Hello世界🌍"
        };

        foreach (var testString in testStrings)
        {
            // Act
            using var msg = new Message(testString);

            // Assert
            msg.ToString().Should().Be(testString,
                $"Unicode string should be preserved: {testString}");

            // Verify byte-level accuracy
            var expectedBytes = Encoding.UTF8.GetBytes(testString);
            msg.Size.Should().Be(expectedBytes.Length);
            msg.Data.ToArray().Should().BeEquivalentTo(expectedBytes);
        }
    }

    [Fact]
    public void Message_BinaryData_ShouldPreserve()
    {
        // Arrange - Binary data with all byte values
        var binaryData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            binaryData[i] = (byte)i;
        }

        // Act
        using var msg = new Message(binaryData);

        // Assert
        msg.Size.Should().Be(256);
        var retrievedData = msg.Data.ToArray();

        for (int i = 0; i < 256; i++)
        {
            retrievedData[i].Should().Be((byte)i,
                $"binary byte at index {i} should be {i}");
        }
    }

    [Fact]
    public void Message_Rebuild_ShouldReinitialize()
    {
        // Arrange
        using var msg = new Message("Initial data");
        var initialSize = msg.Size;
        initialSize.Should().BeGreaterThan(0);

        // Act - Rebuild to empty
        msg.Rebuild();

        // Assert
        msg.Size.Should().Be(0, "message should be empty after Rebuild()");

        // Act - Rebuild with new size
        msg.Rebuild(50);

        // Assert
        msg.Size.Should().Be(50, "message should have new size after Rebuild(50)");

        // Verify data is accessible
        var data = msg.Data;
        data.Length.Should().Be(50);

        // Write to the rebuilt message
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // Verify write succeeded
        msg.Data[10].Should().Be(10);
    }

    [Fact]
    public void Message_Empty_ShouldHaveZeroSize()
    {
        // Act
        using var msg = new Message();

        // Assert
        msg.Size.Should().Be(0, "empty message should have size 0");
        msg.Data.Length.Should().Be(0, "empty message data span should have length 0");
        msg.Data.IsEmpty.Should().BeTrue("empty message data span should be empty");

        // ToString on empty message should return empty string
        msg.ToString().Should().BeEmpty("empty message ToString should return empty string");

        // ToArray on empty message should return empty array
        msg.ToArray().Should().BeEmpty("empty message ToArray should return empty array");
    }

    [Fact]
    public void Message_Dispose_ShouldReleaseResources()
    {
        // Arrange
        var msg = new Message("Test data");
        msg.Size.Should().BeGreaterThan(0);

        // Act
        msg.Dispose();

        // Assert - Multiple dispose calls should be safe
        var act = () => msg.Dispose();
        act.Should().NotThrow("multiple Dispose calls should be safe");

        // After dispose, accessing Data should throw
        var accessAct = () => { var _ = msg.Data; };
        accessAct.Should().Throw<InvalidOperationException>()
            .WithMessage("Message not initialized");

        // After dispose, accessing Size should throw
        var sizeAct = () => { var _ = msg.Size; };
        sizeAct.Should().Throw<InvalidOperationException>()
            .WithMessage("Message not initialized");
    }

    [Fact]
    public void Message_EmptyByteArray_ShouldCreateZeroSizeMessage()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act
        using var msg = new Message(emptyData);

        // Assert
        msg.Size.Should().Be(0);
        msg.Data.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Message_EmptyString_ShouldCreateZeroSizeMessage()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act
        using var msg = new Message(emptyString);

        // Assert
        msg.Size.Should().Be(0);
        msg.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Message_NegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => new Message(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Message_RebuildNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        using var msg = new Message(10);

        // Act
        var act = () => msg.Rebuild(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Message_MultipleRebuild_ShouldWork()
    {
        // Arrange
        using var msg = new Message("Initial");

        // Act & Assert - Multiple rebuilds
        msg.Rebuild(10);
        msg.Size.Should().Be(10);

        msg.Rebuild(20);
        msg.Size.Should().Be(20);

        msg.Rebuild();
        msg.Size.Should().Be(0);

        msg.Rebuild(5);
        msg.Size.Should().Be(5);
    }

    [Fact]
    public void Message_ToArray_ShouldReturnCopy()
    {
        // Arrange
        var original = new byte[] { 1, 2, 3, 4, 5 };
        using var msg = new Message(original);

        // Act
        var array1 = msg.ToArray();
        var array2 = msg.ToArray();

        // Assert
        array1.Should().BeEquivalentTo(original);
        array2.Should().BeEquivalentTo(original);
        array1.Should().NotBeSameAs(array2, "ToArray should create new array each time");

        // Modifying returned array should not affect message
        array1[0] = 99;
        msg.Data[0].Should().Be(1, "modifying ToArray result should not affect message");
    }

    [Fact]
    public void Message_VeryLargeData_ShouldHandle()
    {
        // Arrange - 10MB message
        const int size = 10 * 1024 * 1024;

        // Act
        using var msg = new Message(size);

        // Assert
        msg.Size.Should().Be(size);
        msg.Data.Length.Should().Be(size);

        // Write and read some data to verify it's functional
        var data = msg.Data;
        data[0] = 42;
        data[size - 1] = 255;

        msg.Data[0].Should().Be(42);
        msg.Data[size - 1].Should().Be(255);
    }

    [Fact]
    public void Message_SpecialCharacters_ShouldPreserve()
    {
        // Arrange - String with special characters
        var specialChars = "Line1\nLine2\rLine3\tTab\0Null\x01SOH\x7FDEL";

        // Act
        using var msg = new Message(specialChars);

        // Assert
        msg.ToString().Should().Be(specialChars);

        // Verify byte-level
        var expectedBytes = Encoding.UTF8.GetBytes(specialChars);
        msg.Data.ToArray().Should().BeEquivalentTo(expectedBytes);
    }

    [Fact]
    public void Message_CopyToEmptyDestination_ShouldWork()
    {
        // Arrange
        var source = new Message("Source data");
        var destination = new Message(); // Empty message

        // Act
        destination.Copy(source);

        // Assert
        destination.Size.Should().Be(source.Size);
        destination.ToString().Should().Be("Source data");

        // Cleanup
        source.Dispose();
        destination.Dispose();
    }

    [Fact]
    public void Message_MoveToEmptyDestination_ShouldWork()
    {
        // Arrange
        var source = new Message("Source data");
        var destination = new Message(); // Empty message
        var originalSize = source.Size;

        // Act
        destination.Move(source);

        // Assert
        destination.Size.Should().Be(originalSize);
        destination.ToString().Should().Be("Source data");
        source.Size.Should().Be(0);

        // Cleanup
        source.Dispose();
        destination.Dispose();
    }
}
