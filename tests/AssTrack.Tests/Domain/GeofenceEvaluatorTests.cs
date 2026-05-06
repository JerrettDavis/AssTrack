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

    [Fact]
    public void HaversineDistance_Should_Return_KnownDistance_ForTwoPoints()
    {
        // London to Paris is approximately 340 km (340,000 meters)
        var distance = GeofenceEvaluator.HaversineDistance(51.5074, -0.1278, 48.8566, 2.3522);
        distance.Should().BeApproximately(340_000, 10_000);
    }

    [Fact]
    public void IsInside_Should_ReturnFalse_WhenObservationIsOutsideRadius()
    {
        var geofence = new Geofence { CenterLatitude = 51.5007, CenterLongitude = -0.1246, RadiusMeters = 100 };
        // Observation is ~1 km away – clearly outside the 100 m radius
        var observation = new Observation { Latitude = 51.5100, Longitude = -0.1246 };

        GeofenceEvaluator.IsInside(geofence, observation).Should().BeFalse();
    }

    [Fact]
    public void IsInside_Should_ReturnTrue_WhenObservationIsInsidePolygon()
    {
        var geofence = new Geofence
        {
            ShapeType = "polygon",
            PolygonJson = """
                [
                  { "latitude": 36.0, "longitude": -96.0 },
                  { "latitude": 36.2, "longitude": -96.0 },
                  { "latitude": 36.2, "longitude": -95.7 },
                  { "latitude": 36.0, "longitude": -95.7 }
                ]
                """
        };
        var observation = new Observation { Latitude = 36.05, Longitude = -95.9 };

        GeofenceEvaluator.IsInside(geofence, observation).Should().BeTrue();
    }

    [Fact]
    public void IsInside_Should_ReturnFalse_WhenObservationIsOutsidePolygon()
    {
        var geofence = new Geofence
        {
            ShapeType = "polygon",
            PolygonJson = """
                [
                  { "latitude": 36.0, "longitude": -96.0 },
                  { "latitude": 36.2, "longitude": -96.0 },
                  { "latitude": 36.2, "longitude": -95.7 },
                  { "latitude": 36.0, "longitude": -95.7 }
                ]
                """
        };
        var observation = new Observation { Latitude = 36.4, Longitude = -95.9 };

        GeofenceEvaluator.IsInside(geofence, observation).Should().BeFalse();
    }
}
