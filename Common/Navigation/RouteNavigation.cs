namespace NBNavApp.Common.Navigation;

public enum Instruction : byte
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
    KEEP_RIGHT,
    END
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

public readonly record struct SpeedState(Location? PrevLoc, DateTimeOffset? PrevTime);

public class RouteNavigation
{
    public static (double[] dist, double[] seg) BuildTotalDist(List<(double lon, double lat)> pts)
    {
        double[] dist = new double[pts.Count];
        var seg = new double[Math.Max(0, pts.Count - 1)];

        double acc = 0;
        dist[0] = 0;

        for (int i = 1; i < pts.Count; ++i)
        {
            var len = GeoFunctions.HaversineMeters(pts[i - 1], pts[i]);
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
                exit_number: step.exit_number,
                way_points: [start, end],
                coords: [distances[start], distances[end]]
                ));
        }
        return preparedSteps;
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
            var pr = GeoFunctions.ProjectPointToSegment(posXY.x, posXY.y, a.x, a.y, b.x, b.y);

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

    public static (int currentStepIndex, int manouverIndex, double dist, int exit) ComputeStepAndDistance(double s, List<PreparedStep> steps, NavState state)
    {
        if (steps.Count == 0)
        { return (0, 0, 0, 0); }

        int finalGoalIndex = steps.FindLastIndex(x => x.type == Instruction.GOAL);
        if (finalGoalIndex < 0)
        { finalGoalIndex = steps.Count - 1; }

        while (state.CurrentStepIndex < steps.Count - 1 && s > steps[state.CurrentStepIndex].coords[1] + 20)
        { state.CurrentStepIndex++; }

        while (state.CurrentStepIndex > 0 && s < steps[state.CurrentStepIndex].coords[0] - 20)
        { state.CurrentStepIndex--; }

        int stepIndex = state.CurrentStepIndex;

        if (stepIndex >= finalGoalIndex && steps[finalGoalIndex].type == Instruction.GOAL)
        { return (finalGoalIndex, finalGoalIndex, 0, 0); }

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
        int exit = steps[manouverIndex].exit_number ?? 0;

        return (stepIndex, manouverIndex, dist, exit);
    }

    public static (double speedMps, SpeedState nextState) ComputeSpeed(Location loc, SpeedState state)
    {
        var now = loc.Timestamp;

        if (state.PrevLoc is null || state.PrevTime is null)
        { return (loc.Speed ?? 0, new(loc, now)); }

        double dt = (now - state.PrevTime.Value).TotalSeconds;
        if (dt < 0.5)
        { return (loc.Speed ?? 0, state); }

        double d = GeoFunctions.HaversineMeters((state.PrevLoc.Longitude, state.PrevLoc.Latitude), (loc.Longitude, loc.Latitude));

        return (d / dt, new(loc, now));
    }

    public static (double lon, double lat) PointAtS(
        List<(double lon, double lat)> geometry,
        double[] totalDist,
        double[] segLen,
        double s)
    {
        s = Math.Clamp(s, 0, totalDist[^1]);

        int i = Array.BinarySearch(totalDist, s);
        if (i < 0)
        { i = ~i - 1; }
        i = Math.Clamp(i, 0, geometry.Count - 2);

        double t = segLen[i] > 1e-6 ? (s - totalDist[i]) / segLen[i] : 0;

        double lon = geometry[i].lon + t * (geometry[i + 1].lon - geometry[i].lon);
        double lat = geometry[i].lat + t * (geometry[i + 1].lat - geometry[i].lat);
        return (lon, lat);
    }
}