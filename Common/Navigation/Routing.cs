using Mapsui.Projections;
using System.Text;
using System.Text.Json;

namespace NBNavApp;

public enum Instruction
{
    LEFT,
    RIGHT,
    SHARP_LEFT,
    SHARP_RIGHT,
    SLIGHT_LEFT,
    SLIGHT_RIGHT,
    STRAIGHT,
    ENTER_ROUNDABOUT,
    EXIT_ROUNDABOUT,
    U_TURN,
    GOAL,
    DEPART,
    KEEP_LEFT,
    KEEP_RIGHT
};

public enum RouteState
{
    NORMAL,
    OFF_ROUTE,
    REROUTE
};

public class NavState
{
    public int CurrentStepIndex { get; set; } = 0;
    public int LastSegIndex { get; set; } = 0;
    public (double lat, double lon) Start { get; set; }
    public (double lat, double lon) Destination { get; set; }
    public RouteState RouteState { get; set; } = RouteState.NORMAL;
}

public record PreparedStep(int index, Instruction type, string? instruction, int[] way_points, double[] coords);

public class RouteNavigation
{
    public enum AvoidFeatures
    {
        highways,
        tollways,
        ferries,
        fords
    }

    public record OrsRoutingProperties(List<OrsRoutingSegment> segments, OrsRoutingSummary summary);
    public record OrsRoutingStep(double distance, Instruction type, string? instruction, string? name, int[] way_points);
    public record OrsRoutingSegment(double distance, double duration, List<OrsRoutingStep> steps);
    public record OrsRoutingSummary(double distance, double duration);
    public record OrsRoutingGeometry(List<double[]> coordinates);
    public record OrsRoutingFeature(OrsRoutingGeometry geometry, OrsRoutingProperties properties);
    public record OrsRoutingResponse(List<OrsRoutingFeature> features);

    static readonly HttpClient client = new();

    public static async Task<OrsRoutingResponse?> GetRoutingResponseAsync(
        string apiKey,
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
            req.Headers.TryAddWithoutValidation("Authorization", apiKey);
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

    private static double HaversineMeters((double lon, double lat) start, (double lon, double lat) end)
    {
        const double EARTH_R = 6371000.0;

        double latStart = start.lat.ToRad();
        double latEnd = end.lat.ToRad();
        double dLat = (end.lat - start.lat).ToRad();
        double dLon = (end.lon - start.lon).ToRad();

        double s = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(latStart) * Math.Cos(latEnd) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(s), Math.Sqrt(1 - s));

        return c * EARTH_R;
    }

    public static (double[] dist, double[] seg) BuildTotalDist(List<(double lon, double lat)> pts)
    {
        double[] dist = new double[pts.Count];
        var seg = new double[Math.Max(0, pts.Count - 1)];

        double acc = 0;
        dist[0] = 0;

        for (int i = 1; i < pts.Count; ++i)
        {
            var len = HaversineMeters(pts[i - 1], pts[i]);
            seg[i - 1] = len;
            acc += len;
            dist[i] = acc;
        }
        return (dist, seg);
    }

    public static List<PreparedStep> PrepareSteps(List<OrsRoutingStep> steps, double[] distances)
    {
        List<PreparedStep> preparedSteps = new(steps.Count);

        for (int i = 0; i < steps.Count; ++i)
        {
            OrsRoutingStep step = steps[i];
            int start = Math.Clamp(step.way_points[0], 0, distances.Length - 1);
            int end = Math.Clamp(step.way_points[1], 0, distances.Length - 1);

            if (start > end)
            { (start, end) = (end, start); }

            preparedSteps.Add(new PreparedStep(
                index: i,
                type: step.type,
                instruction: step.instruction,
                way_points: [start, end],
                coords: [distances[start], distances[end]]
                ));
        }
        return preparedSteps;
    }

    public static List<(double x, double y)> ToMercator(List<(double lon, double lat)> pts)
    => pts.Select(p =>
    {
        var m = SphericalMercator.FromLonLat(p.lon, p.lat);
        return (m.x, m.y);
    }).ToList();

    private static (double t, double dPerp) ProjectPointToSegment(
        double x, double y,
        double startx, double starty,
        double endx, double endy)
    {
        double vx = endx - startx;
        double vy = endy - starty;
        double wx = x - startx;
        double wy = y - starty;

        double vv = Math.Pow(vx, 2) + Math.Pow(vy, 2);
        double t = vv <= 1e-9 ? 0 : (wx * vx + wy * vy) / vv;
        t = Math.Clamp(t, 0, 1);

        double px = startx + t * vx;
        double py = starty + t * vy;

        double dx = x - px;
        double dy = y - py;
        double d = Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));

        return (t, d);
    }

    public static (double s, double dPerp) MatchRouteToNextStep(
        (double x, double y) posXY,
        List<(double x, double y)> routeXY,
        double[] totalDistMeters,
        double[] segLen,
        List<PreparedStep> steps,
        NavState state,
        int extraSegments = 30)
    {
        PreparedStep step = steps[Math.Clamp(state.CurrentStepIndex, 0, steps.Count - 1)];
        int segStart = Math.Max(0, step.way_points[0] - extraSegments);
        int segEnd = Math.Min(routeXY.Count - 2, step.way_points[1] + extraSegments);

        segStart = Math.Min(segStart, state.LastSegIndex);
        segEnd = Math.Max(segEnd, state.LastSegIndex);

        double bestD = double.MaxValue;
        int bestI = Math.Clamp(state.LastSegIndex, segStart, segEnd);
        double bestT = 0;

        for (int i = segStart; i <= segEnd; ++i)
        {
            var a = routeXY[i];
            var b = routeXY[i + 1];
            var pr = ProjectPointToSegment(posXY.x, posXY.y, a.x, a.y, b.x, b.y);

            if (pr.dPerp < bestD)
            {
                bestD = pr.dPerp;
                bestI = i;
                bestT = pr.t;
            }
        }

        state.LastSegIndex = bestI;

        var s = totalDistMeters[bestI] + bestT * segLen[bestI];

        return (s, bestD);
    }

    public static (int currentStepIndex, int manouverIndex, double dist) ComputeStepAndDistance(double s, List<PreparedStep> steps, NavState state)
    {
        if (steps.Count == 0)
        { return (0, 0, 0); }

        int finalGoalIndex = steps.FindLastIndex(x => x.type == Instruction.GOAL);
        if (finalGoalIndex < 0)
        { finalGoalIndex = steps.Count - 1; }

        while (state.CurrentStepIndex < steps.Count - 1 && s > steps[state.CurrentStepIndex].coords[1] + 10)
        { state.CurrentStepIndex++; }

        while (state.CurrentStepIndex > 0 && s < steps[state.CurrentStepIndex].coords[0] - 20)
        { state.CurrentStepIndex--; }

        int stepIndex = state.CurrentStepIndex;

        if (stepIndex >= finalGoalIndex && steps[finalGoalIndex].type == Instruction.GOAL)
        { return (finalGoalIndex, finalGoalIndex, 0); }

        int next = Math.Min(stepIndex + 1, steps.Count - 1);

        while (next <= finalGoalIndex)
        {
            Instruction instr = steps[next].type;
            if (instr == Instruction.DEPART)
            {
                next++;
                continue;
            }
            if (instr == Instruction.GOAL && next != finalGoalIndex)
            {
                next++;
                continue;
            }
            break;
        }

        int manouverIndex = Math.Min(next, finalGoalIndex);

        int boundaryIndex = Math.Max(0, manouverIndex - 1);
        double dist = Math.Max(0, steps[boundaryIndex].coords[1] - s);

        return (stepIndex, manouverIndex, dist);
    }
}