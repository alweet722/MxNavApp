using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace NBNavApp.Common.Services;

public class MapService
{
    public enum MapPointType { Start, Destination }

    readonly Mapsui.Map map;
    MemoryLayer? routeLayer;
    readonly Dictionary<MapPointType, MemoryLayer?> pointLayers = new()
    {
        { MapPointType.Start, null },
        { MapPointType.Destination, null }
    };
    MyLocationLayer? myLocationLayer;

    public MapService(Mapsui.Map map)
    {
        this.map = map;
    }

    public void ShowPoint(MapPointType type, double lat, double lon)
    {
        Mapsui.Styles.Color color;
        (double x, double y) point = SphericalMercator.FromLonLat(lon, lat);

        if (type == MapPointType.Start)
        { color = Mapsui.Styles.Color.LimeGreen; }
        else
        { color = Mapsui.Styles.Color.Maroon; }

        PointFeature feature = new(point.ToMPoint());
        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 1.0,
            Fill = new(color)
        });

        if (pointLayers[type] == null)
        {
            pointLayers[type] = new()
            {
                Name = type.ToString(),
                Features = new[] { feature }
            };
            map.Layers.Add(pointLayers[type]!);
        }
        else
        {
            MemoryLayer layer = pointLayers[type]!;
            layer.Features = new[] { feature };
        }

        map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 1, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);

        map.Refresh();
    }

    public void ShowRoute(List<(double lon, double lat)> routePoints)
    {
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

        map.Refresh();
    }

    public void ClearRoute()
    {
        if (routeLayer != null)
        {
            map.Layers.Remove(routeLayer);
            routeLayer = null;
        }
    }

    public void ClearPoints()
    {
        foreach (var key in pointLayers.Keys.ToList())
        {
            if (pointLayers[key] != null)
            {
                map.Layers.Remove(pointLayers[key]);
                pointLayers[key] = null;
            }
        }
    }

    public void InitializeLocationLayer(double latitude, double longitude)
    {
        if (myLocationLayer == null)
        {
            myLocationLayer = new MyLocationLayer(map)
            {
                Enabled = true,
                IsMoving = true
            };
            map.Layers.Add(myLocationLayer);
        }

        var point = SphericalMercator.FromLonLat(longitude, latitude);
        myLocationLayer.UpdateMyLocation(point.ToMPoint(), true);
        map.Refresh();
    }

    public void UpdateCurrentLocation(double latitude, double longitude, double? bearing = null)
    {
        if (myLocationLayer == null)
        {
            return;
        }

        var point = SphericalMercator.FromLonLat(longitude, latitude);
        myLocationLayer.UpdateMyLocation(point.ToMPoint(), true);

        if (bearing.HasValue)
        {
            myLocationLayer.UpdateMyDirection(bearing.Value, 0, true);
        }

        map.Refresh();
    }

    public void ClearCurrentLocation()
    {
        if (myLocationLayer != null)
        {
            map.Layers.Remove(myLocationLayer);
            myLocationLayer = null;
        }
    }
}
