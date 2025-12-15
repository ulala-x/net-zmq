using FluentAssertions;
using Xunit;
using Net.Zmq;

namespace Net.Zmq.Core.Tests;

public class LibZmqTests
{
    [Fact]
    public void Context_ShouldBeCreatedSuccessfully()
    {
        // Act - test creating a context through high-level API
        using var ctx = new Context();

        // Assert - context should be created successfully
        ctx.Should().NotBeNull();
    }

    [Fact]
    public void Version_ShouldReturnValidVersion()
    {
        // Act
        var (major, minor, patch) = Context.Version;

        // Assert
        major.Should().BeGreaterOrEqualTo(4);
        minor.Should().BeGreaterOrEqualTo(0);
        patch.Should().BeGreaterOrEqualTo(0);
    }
}
