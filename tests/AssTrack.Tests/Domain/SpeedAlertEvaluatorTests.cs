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

    [Fact]
    public void Evaluate_Should_ReturnAlert_WithCorrectAssetId()
    {
        var assetId = Guid.NewGuid();
        var observation = new Observation { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), SpeedKmh = 150.0 };

        var alert = SpeedAlertEvaluator.Evaluate(observation, assetId);

        alert.Should().NotBeNull();
        alert!.AssetId.Should().Be(assetId);
    }

    [Fact]
    public void Evaluate_Should_ReturnNull_WhenSpeedIsNull()
    {
        var observation = new Observation { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), SpeedKmh = null };

        var alert = SpeedAlertEvaluator.Evaluate(observation);

        alert.Should().BeNull();
    }

    [Fact]
    public void ShouldAlert_WhenSpeedExceedsCustomThreshold()
    {
        var observation = new Observation { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), SpeedKmh = 90.0 };

        var alert = SpeedAlertEvaluator.Evaluate(observation, Guid.NewGuid(), 80.0);

        alert.Should().NotBeNull();
        alert!.ObservedSpeedKmh.Should().Be(90.0);
        alert.ThresholdKmh.Should().Be(80.0);
    }

    [Fact]
    public void ShouldNotAlert_WhenSpeedBelowCustomThreshold()
    {
        var observation = new Observation { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), SpeedKmh = 75.0 };

        var alert = SpeedAlertEvaluator.Evaluate(observation, Guid.NewGuid(), 80.0);

        alert.Should().BeNull();
    }
}
