using System.Net;
using System.Text;
using System.Xml.Linq;
using FrontiereLiveGe.Api.Services.PublicData;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrontiereLiveGe.Api.Tests;

public sealed class BisonFuteProviderTests
{
    private static readonly DateTime CheckedAtUtc =
        new(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_KeepsOnlyCurrentEventsInsideGrandGeneva()
    {
        const string datex =
            """
            <d2LogicalModel xmlns="urn:datex2" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <publicationTime>2026-07-30T09:55:00Z</publicationTime>
              <situation id="situation-1">
                <overallSeverity>high</overallSeverity>
                <situationRecord id="record-local" xsi:type="Accident">
                  <overallStartTime>2026-07-30T09:30:00Z</overallStartTime>
                  <overallEndTime>2026-07-30T12:00:00Z</overallEndTime>
                  <situationRecordObservationTime>2026-07-30T09:50:00Z</situationRecordObservationTime>
                  <tpegDirection>eastBound</tpegDirection>
                  <roadNumber>N206</roadNumber>
                  <generalPublicComment>
                    <commentType>description</commentType>
                    <value>Accident avec voie neutralisée</value>
                  </generalPublicComment>
                  <generalPublicComment>
                    <commentType>locationDescriptor</commentType>
                    <value>près de Saint-Julien-en-Genevois</value>
                  </generalPublicComment>
                  <pointCoordinates>
                    <latitude>46.1406</latitude>
                    <longitude>6.1279</longitude>
                  </pointCoordinates>
                </situationRecord>
                <situationRecord id="record-outside" xsi:type="Roadworks">
                  <overallEndTime>2026-07-30T12:00:00Z</overallEndTime>
                  <pointCoordinates>
                    <latitude>48.8566</latitude>
                    <longitude>2.3522</longitude>
                  </pointCoordinates>
                </situationRecord>
                <situationRecord id="record-expired" xsi:type="AbnormalTraffic">
                  <overallEndTime>2026-07-30T08:00:00Z</overallEndTime>
                  <pointCoordinates>
                    <latitude>46.2</latitude>
                    <longitude>6.1</longitude>
                  </pointCoordinates>
                </situationRecord>
              </situation>
            </d2LogicalModel>
            """;
        var document = XDocument.Parse(datex);

        var result = BisonFuteProvider.Parse(document, CheckedAtUtc);

        Assert.Equal(3, result.RecordsCount);
        Assert.Equal(
            new DateTime(2026, 7, 30, 9, 55, 0, DateTimeKind.Utc),
            result.PublicationTimeUtc);

        var signal = Assert.Single(result.Signals);
        Assert.Equal("bison:situation-1:record-local", signal.Id);
        Assert.Equal("Accident", signal.Category);
        Assert.Equal("Critical", signal.Severity);
        Assert.Contains("N206", signal.Title);
        Assert.Contains("Saint-Julien", signal.Title);
        Assert.Equal(46.1406, signal.Latitude);
        Assert.Equal(6.1279, signal.Longitude);
        Assert.Equal(90, signal.TravelDirectionDegrees);
    }

    [Fact]
    public void Parse_DowngradesNearFutureEventAndSkipsDistantFutureEvent()
    {
        const string datex =
            """
            <d2LogicalModel xmlns="urn:datex2" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <publicationTime>2026-07-30T09:55:00Z</publicationTime>
              <situation id="future">
                <overallSeverity>high</overallSeverity>
                <situationRecord id="near" xsi:type="RoadOrCarriagewayClosure">
                  <overallStartTime>2026-07-30T18:00:00Z</overallStartTime>
                  <overallEndTime>2026-07-30T20:00:00Z</overallEndTime>
                  <pointCoordinates><latitude>46.1406</latitude><longitude>6.1279</longitude></pointCoordinates>
                </situationRecord>
                <situationRecord id="far" xsi:type="RoadOrCarriagewayClosure">
                  <overallStartTime>2026-08-02T18:00:00Z</overallStartTime>
                  <overallEndTime>2026-08-02T20:00:00Z</overallEndTime>
                  <pointCoordinates><latitude>46.1406</latitude><longitude>6.1279</longitude></pointCoordinates>
                </situationRecord>
              </situation>
            </d2LogicalModel>
            """;

        var result = BisonFuteProvider.Parse(XDocument.Parse(datex), CheckedAtUtc);

        var signal = Assert.Single(result.Signals);
        Assert.Equal("Info", signal.Severity);
        Assert.StartsWith("Upcoming", signal.Category);
        Assert.StartsWith("À venir", signal.Title);
    }

    [Fact]
    public void Parse_DerivesDirectionFromLinearGeometryWhenCardinalDirectionIsMissing()
    {
        const string datex =
            """
            <d2LogicalModel xmlns="urn:datex2" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <publicationTime>2026-07-30T09:55:00Z</publicationTime>
              <situation id="linear">
                <situationRecord id="aligned" xsi:type="Accident">
                  <overallStartTime>2026-07-30T09:30:00Z</overallStartTime>
                  <overallEndTime>2026-07-30T12:00:00Z</overallEndTime>
                  <directionRelativeOnLinearSection>aligned</directionRelativeOnLinearSection>
                  <pointCoordinates><latitude>46.1406</latitude><longitude>6.1200</longitude></pointCoordinates>
                  <pointCoordinates><latitude>46.1406</latitude><longitude>6.1300</longitude></pointCoordinates>
                </situationRecord>
              </situation>
            </d2LogicalModel>
            """;

        var result = BisonFuteProvider.Parse(XDocument.Parse(datex), CheckedAtUtc);

        var signal = Assert.Single(result.Signals);
        Assert.InRange(signal.TravelDirectionDegrees!.Value, 89, 91);
    }

    [Fact]
    public async Task Provider_RejectsDocumentTypeAndDoesNotResolveExternalEntities()
    {
        const string payload =
            """
            <!DOCTYPE data [
              <!ENTITY secret SYSTEM "file:///etc/passwd">
            ]>
            <d2LogicalModel xmlns="urn:datex2">
              <publicationTime>2026-07-30T09:55:00Z</publicationTime>
              <situation id="s1">&secret;</situation>
            </d2LogicalModel>
            """;
        var factory = new StubHttpClientFactory(payload);
        var provider = new BisonFuteProvider(
            factory,
            NullLogger<BisonFuteProvider>.Instance);

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal("Unavailable", snapshot.Source.Status);
        Assert.Empty(snapshot.Signals);
    }

    private sealed class StubHttpClientFactory(string responseBody) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(responseBody))
            {
                BaseAddress = new Uri("https://example.test/")
            };
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/xml")
            };
            return Task.FromResult(response);
        }
    }
}
