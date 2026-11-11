using System.Net;
using System.Text;
using System.Text.Json;
using Econosys.Api.Services;
using Xunit;

namespace Econosys.Api.Tests;

// Exercises the real GoogleRoutingClient against an in-process HTTP handler; nothing is sent to Google.
public class GoogleRoutingClientTests
{
    private const string DummyKey = "dummy-key";

    [Fact]
    public async Task OptimizedRouteRequestsWaypointIndexInFieldMaskAndParsesLegs()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            {"routes":[{"optimizedIntermediateWaypointIndex":[0],"legs":[
              {"distanceMeters":1200,"startLocation":{"latLng":{"latitude":1,"longitude":2}},"endLocation":{"latLng":{"latitude":3,"longitude":4}},
               "steps":[{"distanceMeters":1200,"startLocation":{"latLng":{"latitude":1,"longitude":2}},"endLocation":{"latLng":{"latitude":3,"longitude":4}},
                         "navigationInstruction":{"maneuver":"FERRY"},"travelMode":"DRIVE"}]}]}]}
            """));
        var client = new GoogleRoutingClient(new SingleClientFactory(handler));

        var legs = await client.ComputeRouteAsync(
            DummyKey,
            new RouteRequest(new GeoPoint(1, 2), new GeoPoint(5, 6), new[] { new GeoPoint(3, 4) }),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(DummyKey, request.Headers["X-Goog-Api-Key"]);
        Assert.Contains("routes.optimizedIntermediateWaypointIndex", request.Headers["X-Goog-FieldMask"].Split(','));
        using var body = JsonDocument.Parse(request.Body!);
        Assert.True(body.RootElement.GetProperty("optimizeWaypointOrder").GetBoolean());
        Assert.Equal(3, body.RootElement.GetProperty("intermediates")[0].GetProperty("location").GetProperty("latLng").GetProperty("latitude").GetDouble());
        Assert.Equal("DRIVE", body.RootElement.GetProperty("travelMode").GetString());

        var leg = Assert.Single(legs);
        Assert.Equal(1200, leg.DistanceMeters);
        Assert.Equal(new GeoPoint(3, 4), leg.End);
        Assert.Equal("FERRY", Assert.Single(leg.Steps).Maneuver);
    }

    [Fact]
    public async Task GoogleErrorMessageIsCapturedWithoutTheApiKey()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest,
            $$$"""{"error":{"code":400,"message":"Invalid request for key {{{DummyKey}}}.","status":"INVALID_ARGUMENT"}}"""));
        var client = new GoogleRoutingClient(new SingleClientFactory(handler));

        var error = await Assert.ThrowsAsync<GoogleRoutingException>(() => client.ComputeRouteAsync(
            DummyKey,
            new RouteRequest(new GeoPoint(1, 2), new GeoPoint(5, 6), Array.Empty<GeoPoint>()),
            CancellationToken.None));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("INVALID_ARGUMENT: Invalid request for key ***.", error.GoogleMessage);
    }

    [Fact]
    public async Task GeocodingDenialIncludesGoogleErrorMessage()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"REQUEST_DENIED","error_message":"This API project is not authorized to use this API.","results":[]}"""));
        var client = new GoogleRoutingClient(new SingleClientFactory(handler));

        var error = await Assert.ThrowsAsync<GoogleRoutingException>(() => client.GeocodeAsync(DummyKey, "Testgatan 1, 111 11 Teststad, SVERIGE", CancellationToken.None));

        Assert.Equal("REQUEST_DENIED: This API project is not authorized to use this API.", error.GoogleMessage);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed record RecordedRequest(Dictionary<string, string> Headers, string? Body);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Headers.ToDictionary(x => x.Key, x => string.Join(",", x.Value)),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
