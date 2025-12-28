using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using System.Diagnostics;

namespace NBNavApp;

public partial class RoutePage : ContentPage
{
    enum PointType
    {
        Start,
        Destination
    }

    MemoryLayer? pinLayer;
    MemoryLayer? routeLayer;

    (double lat, double lon) start;
    (double lat, double lon) dest;

    const string API_KEY = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6ImE2Y2NjNGFmZjdhYTQ3NjliMjZjMTRjNmFmYjBjNDhlIiwiaCI6Im11cm11cjY0In0=";

    public RoutePage()
    {
        InitializeComponent();

        Mapsui.Map map = new();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        MapControl.Map = map;

        (double x, double y) defaultCenter = SphericalMercator.FromLonLat(13.723076680216279, 51.05120761645636);
        map.Navigator.CenterOnAndZoomTo(defaultCenter.ToMPoint(), 10);
    }

    private void ShowPointOnMap(PointType type, double lat, double lon)
    {
        Mapsui.Styles.Color color;
        Mapsui.Map map = MapControl.Map;
        (double x, double y) point = SphericalMercator.FromLonLat(lon, lat);

        if (type == PointType.Start)
        { color = Mapsui.Styles.Color.LimeGreen; }
        else
        { color = Mapsui.Styles.Color.Maroon; }

        PointFeature feature = new(point.ToMPoint());
        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 1.0,
            Fill = new(color)
        });
        feature.Styles.Add(new LabelStyle
        {
            Offset = new Offset(0, -20),
            BackColor = null,
        });

        if (pinLayer == null)
        {
            pinLayer = new()
            {
                Name = "PinLayer",
                Features = new[] { feature }
            };
        }
        else
        {
            if (pinLayer.Features == null)
            { pinLayer.Features = new[] { feature }; }
            else
            { pinLayer.Features = pinLayer.Features.Append(feature); }
        }

        if (!map.Layers.Contains(pinLayer))
        { map.Layers.Add(pinLayer); }

        map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 1, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);
        if (start != (0, 0) && dest != (0, 0))
        { RouteBtn.IsEnabled = true; }
        MapControl.Refresh();
    }

    private void ShowRoute(List<(double lon, double lat)> routePoints)
    {
        Mapsui.Map map = MapControl.Map;

        List<(double x, double y)> mercPts = routePoints.Select(p => SphericalMercator.FromLonLat(p.lon, p.lat)).ToList();
        Coordinate[] pts = mercPts.Select(p => new Coordinate(p.x, p.y)).ToArray();

        LineString lineString = new(pts);

        GeometryFeature feature = new()
        {
            Geometry = lineString,
        };

        feature.Styles.Add(new VectorStyle
        {
            Line = new Pen
            {
                Width = 6,
                Color = Mapsui.Styles.Color.Cyan
            }
        });

        if (routeLayer == null)
        {
            routeLayer = new()
            {
                Name = "Route",
                Features = new[] { feature }
            };
            map.Layers.Add(routeLayer);
        }
        else
        { routeLayer.Features = new[] { feature }; }

        Envelope env = lineString.EnvelopeInternal;
        MRect box = new(env.MinX, env.MinY, env.MaxX, env.MaxY);
        map.Navigator.ZoomToBox(box, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);

        MapControl.Refresh();
    }

    private async void OnEntryCompleted(object sender, EventArgs e)
    {
        Entry entry = (Entry)sender;
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var geocode = await AddressGeocoder.GeocodeAddress(API_KEY, entry.Text, cts.Token);
        if (geocode == null)
        {
            await DisplayAlertAsync("Geocoding", "No matches found.", "Close");
            DriveBtn.IsEnabled = false;
            return;
        }

        if (entry.ClassId == "DestAddrInput")
        {
            dest = (geocode.Value.lat, geocode.Value.lon);
            DestAddrInput.Text = geocode.Value.label;
            ShowPointOnMap(PointType.Destination, geocode.Value.lat, geocode.Value.lon);
        }
        else if (entry.ClassId == "StartAddrInput")
        {
            start = (geocode.Value.lat, geocode.Value.lon);
            StartAddrInput.Text = geocode.Value.label;
            ShowPointOnMap(PointType.Start, geocode.Value.lat, geocode.Value.lon);
        }
    }

    private async void OnRouteClicked(object sender, EventArgs e)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));

        List<string> avoidFeatures = new();
        if (AvoidHighwaysSw.IsToggled)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.highways.ToString()); }
        if (AvoidTollSw.IsToggled)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.tollways.ToString()); }

        var routingResponse = await RouteNavigation.GetRoutingResponseAsync(API_KEY, [start.lon, start.lat], [dest.lon, dest.lat], avoidFeatures.ToArray(), cts.Token);

        if (routingResponse == null)
        {
            await DisplayAlertAsync("Route", "No route found.", "Close");
            DriveBtn.IsEnabled = false;
            return;
        }

        var route = RouteNavigation.GetRoute(routingResponse);
        if (route == null)
        {
            await DisplayAlertAsync("Route", "Could not parse route.", "Close");
            DriveBtn.IsEnabled = false;
            return;
        }

        ShowRoute(route);
        TimeSpan timeToDestination = TimeSpan.FromSeconds(RouteNavigation.GetTimeToDest(routingResponse));
        DistanceToDestLabel.Text = $"{RouteNavigation.GetDistance(routingResponse) / 1000:F1} km";
        TimeToDestLabel.Text = $"{timeToDestination.Hours}:{timeToDestination.Minutes}";
        DriveBtn.IsEnabled = true;
    }

    private async void OnDriveClicked(object sender, EventArgs e)
    { }
}