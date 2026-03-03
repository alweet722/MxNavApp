using System.Text;
using System.Text.Json;

namespace NBNavApp.Common.Navigation
{
    public record OrsRoutingProperties(List<OrsRoutingSegment> segments, OrsRoutingSummary summary);
    public record OrsRoutingStep(double distance, Instruction type, string? instruction, string? name, int? exit_number, int[] way_points);
    public record OrsRoutingSegment(double distance, double duration, List<OrsRoutingStep> steps);
    public record OrsRoutingSummary(double distance, double duration);
    public record OrsRoutingGeometry(List<double[]> coordinates);
    public record OrsRoutingFeature(OrsRoutingGeometry geometry, OrsRoutingProperties properties);
    public record OrsRoutingResponse(List<OrsRoutingFeature> features);
    public record PreparedStep(int index, Instruction type, string? instruction, int? exit_number, int[] way_points, double[] coords);

    internal class OpenRouteServiceFunctions
    {
        public enum AvoidFeatures
        {
            highways,
            tollways,
            ferries,
            fords
        }

        static readonly HttpClient client = new();

        public static async Task<OrsRoutingResponse?> GetRoutingResponseAsync(
            double[] start,
            double[] destination,
            string[] avoidFeatures,
            CancellationToken ct)
        {
            {
                if (start.Length < 2 || destination.Length < 2)
                { return null; }

                string url = "https://api.openrouteservice.org/v2/directions/driving-car/geojson";

                var payload = new
                {
                    coordinates = new[]
                    {
                    start,
                    destination
                },
                    options = new
                    {
                        avoid_features = avoidFeatures,
                    },
                    roundabout_exits = true
                };

                using HttpRequestMessage req = new(HttpMethod.Post, url);
                req.Headers.TryAddWithoutValidation("Authorization", Constants.API_KEY);
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using HttpResponseMessage res = await client.SendAsync(req, ct);
                try
                { res.EnsureSuccessStatusCode(); }
                catch (HttpRequestException e)
                {
                    await MauiAlertService.ShowAlertAsync("Routing", $"Error while fetching route: {e.StatusCode}: {e.Message}");
                    return null;
                }

                string json = await res.Content.ReadAsStringAsync(ct);
                OrsRoutingResponse? data = JsonSerializer.Deserialize<OrsRoutingResponse>(json);
                if (data == null)
                { return null; }

                return data;
            }
        }

        public static List<(double lon, double lat)>? GetRoute(OrsRoutingResponse routingResponse)
        {
            List<double[]>? coordinates = routingResponse?.features?[0].geometry?.coordinates;
            if (coordinates == null || coordinates.Count == 0)
            {
                MauiAlertService.ShowAlertAsync("Routing", "Route contains no waypoints.");
                return null;
            }

            return coordinates.Select(c => (lon: c[0], lat: c[1])).ToList();
        }

        public static double GetDistance(OrsRoutingResponse routingResponse)
        { return routingResponse.features[0].properties.summary.distance; }

        public static double GetTimeToDest(OrsRoutingResponse routingResponse)
        { return routingResponse.features[0].properties.summary.duration; }

        public static List<OrsRoutingStep> GetRoutingSteps(OrsRoutingResponse routingResponse)
        { return routingResponse.features[0].properties.segments[0].steps; }
    }
}
