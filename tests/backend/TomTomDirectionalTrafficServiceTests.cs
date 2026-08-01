using System.Text.Json;
using FrontiereLiveGe.Api.Services;

namespace FrontiereLiveGe.Api.Tests;

public sealed class TomTomDirectionalTrafficServiceTests
{
    [Fact]
    public void ResponseModel_ParsesLiveAndFreeFlowTravelTimes()
    {
        const string json =
            """
            {
              "routes": [{
                "summary": {
                  "travelTimeInSeconds": 780,
                  "trafficDelayInSeconds": 180,
                  "noTrafficTravelTimeInSeconds": 600
                }
              }]
            }
            """;

        var response = JsonSerializer.Deserialize<TomTomDirectionalTrafficService.TomTomRoutesResponse>(json);

        var summary = Assert.Single(response!.Routes).Summary;
        Assert.NotNull(summary);
        Assert.Equal(780, summary.TravelTimeInSeconds);
        Assert.Equal(180, summary.TrafficDelayInSeconds);
        Assert.Equal(600, summary.NoTrafficTravelTimeInSeconds);
    }
}
