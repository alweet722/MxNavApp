using NBNavApp.Common.Util;
using System.Text.Json;

namespace NBNavApp;

internal class AddressGeocoder
{
    public record OrsGeocodingProperties(string? label);
    public record OrsGeocodingGeometry(double[] coordinates);
    public record OrsGeocodingFeature(OrsGeocodingGeometry geometry, OrsGeocodingProperties properties);
    public record OrsGeocodingResponse(List<OrsGeocodingFeature> features);

    static readonly HttpClient client = new();

    public static async Task<(double lat, double lon, string label)?> GeocodeAddress(string address, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(address))
        { return null; }

        HttpResponseMessage res;

        string url = $"https://api.openrouteservice.org/geocode/search?text={Uri.EscapeDataString(address)}&size=1";

        using HttpRequestMessage req = new(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", Constants.API_KEY);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");

        try
        { res = await client.SendAsync(req, ct); }
        catch (Exception)
        {
            await MauiAlertService.ShowAlertAsync("Geocoding", "Server not available.");
            return null;
        }

        try
        { res.EnsureSuccessStatusCode(); }
        catch (HttpRequestException e)
        {
            await MauiAlertService.ShowAlertAsync("Geocoding", $"Error while resolving address: {e.StatusCode}: {e.Message}");
            return null;
        }

        string? json = await res.Content.ReadAsStringAsync(ct);
        OrsGeocodingResponse? geodata = JsonSerializer.Deserialize<OrsGeocodingResponse>(json);
        if (geodata == null)
        {
            await MauiAlertService.ShowAlertAsync("Geocoding", "No matches found.");
            return null;
        }

        OrsGeocodingFeature? feature = geodata.features[0];
        if (feature == null)
        {
            await MauiAlertService.ShowAlertAsync("Geocoding", "No matches found.");
            return null;
        }

        if (feature.geometry.coordinates == null || feature.geometry.coordinates.Length < 2)
        {
            await MauiAlertService.ShowAlertAsync("Geocoding", "No matches found.");
            return null;
        }

        double lon = feature.geometry.coordinates[0];
        double lat = feature.geometry.coordinates[1];
        string label = feature.properties.label ?? address;

        return (lat, lon, label);
    }
}