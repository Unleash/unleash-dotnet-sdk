using NUnit.Framework;
using Unleash.Streaming;
using FluentAssertions;

public class StreamingFailoverTests
{
    [Test]
    public void Suggests_Failing_Over_On_Network_Error_Fifth_Try()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;
        var first = now.Subtract(TimeSpan.FromMilliseconds(50));
        var second = now.Subtract(TimeSpan.FromMilliseconds(40));
        var third = now.Subtract(TimeSpan.FromMilliseconds(30));
        var fourth = now.Subtract(TimeSpan.FromMilliseconds(20));

        // Act
        var firstResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = first }, first);
        failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = second }, second);
        failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = third }, third);
        var fourthResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = fourth }, fourth);
        var lastResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = now }, now);

        // Assert
        firstResult.Should().BeFalse();
        fourthResult.Should().BeFalse();
        lastResult.Should().BeTrue();
    }

    [Test]
    public void Does_Not_Suggest_Failing_Over_On_Fifth_Network_Error_If_First_Is_Outside_Window()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;
        var first = now.Subtract(TimeSpan.FromMilliseconds(5000));
        var second = now.Subtract(TimeSpan.FromMilliseconds(40));
        var third = now.Subtract(TimeSpan.FromMilliseconds(30));
        var fourth = now.Subtract(TimeSpan.FromMilliseconds(20));

        // Act
        var firstResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = first }, first);
        failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = second }, second);
        failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = third }, third);
        var fourthResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = fourth }, fourth);
        var lastResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = now }, now);

        // Assert
        firstResult.Should().BeFalse();
        fourthResult.Should().BeFalse();
        lastResult.Should().BeFalse();
    }

    [Test]
    public void Suggests_Failing_Over_On_Sixth_Network_Error_If_First_Is_Outside_Window()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;
        var first = now.Subtract(TimeSpan.FromMilliseconds(5000));
        var second = now.Subtract(TimeSpan.FromMilliseconds(40));
        var third = now.Subtract(TimeSpan.FromMilliseconds(30));
        var fourth = now.Subtract(TimeSpan.FromMilliseconds(20));
        var fifth = now.Subtract(TimeSpan.FromMilliseconds(10));

        // Act
        var firstResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = first }, first);
        failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = second }, second);
        failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = third }, third);
        var fourthResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = fourth }, fourth);
        var fifthResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = fifth }, fifth);
        var lastResult = failoverStrategy.ShouldFailOver(new NetworkEventErrorArgs() { Message = "failed", OccurredAt = now }, now);

        // Assert
        firstResult.Should().BeFalse();
        fourthResult.Should().BeFalse();
        fifthResult.Should().BeFalse();
        lastResult.Should().BeTrue();
    }

    [Test]
    public void Suggests_Failing_Over_On_Http_Not_Found()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = failoverStrategy.ShouldFailOver(new HttpStatusFailEventArgs() { Message = "failed", StatusCode = 404, OccurredAt = now }, now);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Suggests_Failing_Over_On_Fifth_Server_Error()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;
        var first = now.Subtract(TimeSpan.FromMilliseconds(50));
        var second = now.Subtract(TimeSpan.FromMilliseconds(40));
        var third = now.Subtract(TimeSpan.FromMilliseconds(30));
        var fourth = now.Subtract(TimeSpan.FromMilliseconds(20));

        // Act
        var firstResult = failoverStrategy.ShouldFailOver(new HttpStatusFailEventArgs() { Message = "failed", StatusCode = 500, OccurredAt = first }, first);
        failoverStrategy.ShouldFailOver(new HttpStatusFailEventArgs() { Message = "failed", StatusCode = 500, OccurredAt = second }, second);
        failoverStrategy.ShouldFailOver(new HttpStatusFailEventArgs() { Message = "failed", StatusCode = 500, OccurredAt = third }, third);
        var fourthResult = failoverStrategy.ShouldFailOver(new HttpStatusFailEventArgs() { Message = "failed", StatusCode = 500, OccurredAt = fourth }, fourth);
        var lastResult = failoverStrategy.ShouldFailOver(new HttpStatusFailEventArgs() { Message = "failed", StatusCode = 500, OccurredAt = now }, now);

        // Assert
        firstResult.Should().BeFalse();
        fourthResult.Should().BeFalse();
        lastResult.Should().BeTrue();
    }

    [Test]
    public void Suggests_Failing_Over_On_Polling_Hint()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = failoverStrategy.ShouldFailOver(new ServerHintFailEventArgs() { Message = "failed", Hint = "polling", OccurredAt = now }, now);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Does_Not_Suggest_Failing_Over_On_Unknown_Hint()
    {
        // Arrange
        var failoverStrategy = new StreamingFailoverStrategy(5, 1000);
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = failoverStrategy.ShouldFailOver(new ServerHintFailEventArgs() { Message = "failed", Hint = "no-fail-over", OccurredAt = now }, now);

        // Assert
        result.Should().BeFalse();
    }
}