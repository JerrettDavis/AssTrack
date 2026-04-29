using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using FluentAssertions;

namespace AssTrack.Tests.Domain;

public class SpeedAlertEvaluatorTests
{
    [Fact]
    public void Evaluate_Should_ReturnAlert_WhenSpeedExceedsThreshold()
    {
        var observation = new Observation { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), SpeedKmh = 140.5 };

        var alert = SpeedAlertEvaluator.Evaluate(observation, Guid.NewGuid());

        alert.Should().NotBeNull();
        alert!.ObservedSpeedKmh.Should().Be(140.5);
        alert.ThresholdKmh.Should().Be(SpeedAlertEvaluator.DefaultThresholdKmh);
    }

    [Fact]
    public void Evaluate_Should_ReturnNull_WhenSpeedDoesNotExceedThreshold()
    {
        var observation = new Observation { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), SpeedKmh = 120.0 };

        var alert = SpeedAlertEvaluator.Evaluate(observation);

        alert.Should().BeNull();
    }
}
