using AssTrack.Domain.Models;
using AssTrack.Domain.Services;
using FluentAssertions;

namespace AssTrack.Tests.Domain;

public class GeofenceEvaluatorTests
{
    [Fact]
    public void HaversineDistance_Should_ReturnZero_ForSamePoint()
    {
        var distance = GeofenceEvaluator.HaversineDistance(51.5, -0.12, 51.5, -0.12);
        distance.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void IsInside_Should_ReturnTrue_WhenObservationIsWithinRadius()
    {
        var geofence = new Geofence { CenterLatitude = 51.5007, CenterLongitude = -0.1246, RadiusMeters = 1_500 };
        var observation = new Observation { Latitude = 51.5010, Longitude = -0.1416 };

        GeofenceEvaluator.IsInside(geofence, observation).Should().BeTrue();
    }
}
