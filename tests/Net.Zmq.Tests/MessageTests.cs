using FluentAssertions;
using System.Text;
using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class MessageTests
{
    [Fact]
    public void Message_EmptyConstructor_ShouldCreateEmptyMessage()
    {
        // Act
        using var msg = new Message();

        // Assert
        msg.Size.Should().Be(0);
    }

    [Fact]
    public void Message_SizeConstructor_ShouldCreateSizedMessage()
    {
        // Act
        using var msg = new Message(100);

        // Assert
        msg.Size.Should().Be(100);
    }

    [Fact]
    public void Message_ByteArrayConstructor_ShouldCopyData()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        using var msg = new Message(data);

        // Assert
        msg.Size.Should().Be(5);
        msg.Data.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public void Message_StringConstructor_ShouldEncodeUtf8()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        using var msg = new Message(text);

        // Assert
        msg.ToString().Should().Be(text);
    }

    [Fact]
    public void Message_Rebuild_ShouldReinitialize()
    {
        // Arrange
        using var msg = new Message(10);

        // Act
        msg.Rebuild(20);

        // Assert
        msg.Size.Should().Be(20);
    }

    [Fact]
    public void Message_Copy_ShouldDuplicateData()
    {
        // Arrange
        var source = new Message("Test data");
        var dest = new Message();

        // Act
        dest.Copy(source);

        // Assert
        dest.ToString().Should().Be("Test data");

        // Cleanup
        source.Dispose();
        dest.Dispose();
    }
}
