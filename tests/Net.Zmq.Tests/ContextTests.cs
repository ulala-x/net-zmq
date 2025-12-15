using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class ContextTests
{
    [Fact]
    public void Context_ShouldBeCreatedWithDefaults()
    {
        // Act
        using var ctx = new Context();

        // Assert - context should be created successfully
        ctx.Should().NotBeNull();
    }

    [Fact]
    public void Context_ShouldBeCreatedWithCustomOptions()
    {
        // Act
        using var ctx = new Context(ioThreads: 2, maxSockets: 512);

        // Assert
        ctx.GetOption(ContextOption.IoThreads).Should().Be(2);
        ctx.GetOption(ContextOption.MaxSockets).Should().Be(512);
    }

    [Fact]
    public void Context_Version_ShouldReturnValidVersion()
    {
        // Act
        var (major, minor, patch) = Context.Version;

        // Assert
        major.Should().BeGreaterOrEqualTo(4);
    }

    [Fact]
    public void Context_Has_ShouldCheckCapabilities()
    {
        // Act & Assert
        // "ipc" capability should be available on most platforms
        Context.Has("ipc").Should().BeTrue();
    }
}
