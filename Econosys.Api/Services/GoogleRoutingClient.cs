using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Econosys.Api.Services
{
    public sealed record GeoPoint(double Latitude, double Longitude);

    public sealed record RouteStep(GeoPoint Start, GeoPoint End, int DistanceMeters, string? Maneuver, string TravelMode);

    public sealed record RouteLeg(GeoPoint Start, GeoPoint End, int DistanceMeters, IReadOnlyList<RouteStep> Steps);

    public sealed record RouteRequest(GeoPoint Origin, GeoPoint Destination, IReadOnlyList<GeoPoint> Intermediates);

    public sealed class GoogleRoutingException : Exception
    {
        public GoogleRoutingException(string message, int? statusCode = null, string? googleMessage = null) : base(message)
        {
            StatusCode = statusCode;
            GoogleMessage = googleMessage;
        }

        public int? StatusCode { get; }

        /// <summary>Google's own error description (status and message), with the API key redacted.</summary>
        public string? GoogleMessage { get; }
    }

    public interface IGoogleRoutingClient
    {
        /// <summary>Returns null when Google finds no match for the address.</summary>
        Task<GeoPoint?> GeocodeAsync(string apiKey, string address, CancellationToken cancellationToken);

        Task<IReadOnlyList<RouteLeg>> ComputeRouteAsync(string apiKey, RouteRequest request, CancellationToken cancellationToken);
    }

    // Legacy parity: EconosysService.GetDeliveryLegsFromDeliveryList uses Google Geocoding and Routes (directions/v2:computeRoutes).
    public sealed class GoogleRoutingClient : IGoogleRoutingClient
    {
        public const string HttpClientName = "GoogleMaps";
        private const string GeocodeUrl = "https://maps.googleapis.com/maps/api/geocode/json";
        private const string RoutesUrl = "https://routes.googleapis.com/directions/v2:computeRoutes";
        // optimizedIntermediateWaypointIndex is mandatory in the mask when optimizeWaypointOrder is set, otherwise Google returns 400.
        private const string RoutesFieldMask = "routes.legs.distanceMeters,routes.legs.startLocation,routes.legs.endLocation,"
            + "routes.legs.steps.distanceMeters,routes.legs.steps.startLocation,routes.legs.steps.endLocation,"
            + "routes.legs.steps.navigationInstruction,routes.legs.steps.travelMode,routes.optimizedIntermediateWaypointIndex";

        private readonly IHttpClientFactory _httpClientFactory;

        public GoogleRoutingClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<GeoPoint?> GeocodeAsync(string apiKey, string address, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"{GeocodeUrl}?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(apiKey)}";
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateHttpErrorAsync("Geocoding request failed.", response, apiKey, cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
            if (status == "ZERO_RESULTS")
            {
                return null;
            }

            if (status != "OK"
                || !root.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                var errorMessage = root.TryGetProperty("error_message", out var errorElement) ? errorElement.GetString() : null;
                throw new GoogleRoutingException(
                    $"Geocoding returned status {status ?? "unknown"}.",
                    (int)response.StatusCode,
                    Redact(JoinNonEmpty(status, errorMessage), apiKey));
            }

            var location = results[0].GetProperty("geometry").GetProperty("location");
            return new GeoPoint(location.GetProperty("lat").GetDouble(), location.GetProperty("lng").GetDouble());
        }

        public async Task<IReadOnlyList<RouteLeg>> ComputeRouteAsync(string apiKey, RouteRequest request, CancellationToken cancellationToken)
        {
            var body = new Dictionary<string, object?>
            {
                ["origin"] = new { location = ToLocation(request.Origin), sideOfRoad = true },
                ["destination"] = new { location = ToLocation(request.Destination) },
                ["travelMode"] = "DRIVE",
                ["computeAlternativeRoutes"] = false,
                ["routeModifiers"] = new { avoidTolls = false, avoidHighways = false, avoidFerries = false },
                ["languageCode"] = "en-US",
                ["units"] = "METRIC",
            };

            if (request.Intermediates.Count > 0)
            {
                body["intermediates"] = request.Intermediates.Select(x => new { location = ToLocation(x) }).ToList();
                body["optimizeWaypointOrder"] = true;
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, RoutesUrl)
            {
                Content = JsonContent.Create(body),
            };
            message.Headers.Add("X-Goog-Api-Key", apiKey);
            message.Headers.Add("X-Goog-FieldMask", RoutesFieldMask);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateHttpErrorAsync("Routes request failed.", response, apiKey, cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("routes", out var routes)
                || routes.ValueKind != JsonValueKind.Array
                || routes.GetArrayLength() == 0)
            {
                return Array.Empty<RouteLeg>();
            }

            if (!routes[0].TryGetProperty("legs", out var legs) || legs.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<RouteLeg>();
            }

            return legs.EnumerateArray().Select(ParseLeg).ToList();
        }

        private static object ToLocation(GeoPoint point) => new
        {
            latLng = new { latitude = point.Latitude, longitude = point.Longitude },
        };

        // Google error bodies look like {"error":{"code":400,"message":"...","status":"INVALID_ARGUMENT"}}.
        private static async Task<GoogleRoutingException> CreateHttpErrorAsync(
            string message, HttpResponseMessage response, string apiKey, CancellationToken cancellationToken)
        {
            string? googleMessage = null;
            try
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    googleMessage = JoinNonEmpty(
                        error.TryGetProperty("status", out var status) ? status.GetString() : null,
                        error.TryGetProperty("message", out var errorMessage) ? errorMessage.GetString() : null);
                }
            }
            catch (JsonException)
            {
            }

            return new GoogleRoutingException(message, (int)response.StatusCode, Redact(googleMessage, apiKey));
        }

        private static string? JoinNonEmpty(params string?[] parts)
        {
            var value = string.Join(": ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
            return value.Length == 0 ? null : value;
        }

        private static string? Redact(string? value, string apiKey) =>
            value is null || string.IsNullOrEmpty(apiKey) ? value : value.Replace(apiKey, "***", StringComparison.Ordinal);

        private static RouteLeg ParseLeg(JsonElement leg)
        {
            var steps = leg.TryGetProperty("steps", out var stepsElement) && stepsElement.ValueKind == JsonValueKind.Array
                ? stepsElement.EnumerateArray().Select(ParseStep).ToList()
                : new List<RouteStep>();

            return new RouteLeg(ParseLocation(leg, "startLocation"), ParseLocation(leg, "endLocation"), ParseDistance(leg), steps);
        }

        private static RouteStep ParseStep(JsonElement step)
        {
            var maneuver = step.TryGetProperty("navigationInstruction", out var instruction)
                && instruction.TryGetProperty("maneuver", out var maneuverElement)
                    ? maneuverElement.GetString()
                    : null;
            var travelMode = step.TryGetProperty("travelMode", out var travelModeElement)
                ? travelModeElement.GetString() ?? string.Empty
                : string.Empty;

            return new RouteStep(ParseLocation(step, "startLocation"), ParseLocation(step, "endLocation"), ParseDistance(step), maneuver, travelMode);
        }

        private static int ParseDistance(JsonElement element) =>
            element.TryGetProperty("distanceMeters", out var distance) && distance.ValueKind == JsonValueKind.Number
                ? distance.GetInt32()
                : 0;

        private static GeoPoint ParseLocation(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var location) || !location.TryGetProperty("latLng", out var latLng))
            {
                throw new GoogleRoutingException($"Routes response is missing {propertyName}.");
            }

            return new GeoPoint(
                latLng.TryGetProperty("latitude", out var latitude) ? latitude.GetDouble() : 0,
                latLng.TryGetProperty("longitude", out var longitude) ? longitude.GetDouble() : 0);
        }

        public static string Invariant(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
