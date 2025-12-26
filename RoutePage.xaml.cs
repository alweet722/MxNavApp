using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;

namespace NBNavApp;

public partial class RoutePage : ContentPage
{
    MemoryLayer? startLayer;
    MemoryLayer? destLayer;

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

    private void ShowStartPointOnMap(double lat, double lon)
    {
        Mapsui.Map map = MapControl.Map;
        (double x, double y) point = SphericalMercator.FromLonLat(lon, lat);

        PointFeature feature = new(point.ToMPoint());
        feature.Styles.Add(new SymbolStyle { SymbolScale = 1.0, });
        feature.Styles.Add(new LabelStyle
        {
            Text = "Start",
            Offset = new Offset(0, -20),
        });

        if (startLayer == null)
        {
            startLayer = new()
            {
                Name = "Start",
                Features = new[] { feature }
            };
        }
        else
        { startLayer.Features = new[] { feature }; }

        map.Layers.Add(startLayer);

        map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 1);
        MapControl.Refresh();
    }

    private void ShowDestinationPointOnMap(double lat, double lon)
    {
        Mapsui.Map map = MapControl.Map;
        (double x, double y) point = SphericalMercator.FromLonLat(lon, lat);

        PointFeature feature = new(point.ToMPoint());
        feature.Styles.Add(new SymbolStyle { SymbolScale = 1.0, });
        feature.Styles.Add(new LabelStyle
        {
            Text = "Destination",
            Offset = new Offset(0, -20),
        });

        if (destLayer == null)
        {
            destLayer = new()
            {
                Name = "Destination",
                Features = new[] { feature }
            };
        }
        else
        { destLayer.Features = new[] { feature }; }

        map.Layers.Add(destLayer);

        map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 1);
        MapControl.Refresh();
    }

    private async void OnStartCompleted(object sender, EventArgs e)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var geocode = await AddressGeocoder.GeocodeAddress(API_KEY, StartAddrInput.Text, cts.Token);
        if (geocode == null)
        {
            await DisplayAlertAsync("Geocoding", "No matches found.", "Close");
            return;
        }

        StartAddrInput.Text = geocode.Value.label;
        ShowStartPointOnMap(geocode.Value.lat, geocode.Value.lon);
    }

    private async void OnDestinationCompleted(object sender, EventArgs e)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var geocode = await AddressGeocoder.GeocodeAddress(API_KEY, DestAddrInput.Text, cts.Token);
        if (geocode == null)
        {
            await DisplayAlertAsync("Geocoding", "No matches found.", "Close");
            return;
        }

        DestAddrInput.Text = geocode.Value.label;
        ShowDestinationPointOnMap(geocode.Value.lat, geocode.Value.lon);
    }
}