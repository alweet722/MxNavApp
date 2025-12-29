using System.Text.Json;

namespace NBNavApp;

internal class AddressGeocoder
{
    public record OrsGeocodingProperties(string? label);
    public record OrsGeocodingGeometry(double[] coordinates);
    public record OrsGeocodingFeature(OrsGeocodingGeometry geometry, OrsGeocodingProperties properties);
    public record OrsGeocodingResponse(List<OrsGeocodingFeature> features);

    static readonly HttpClient client = new();

    public static async Task<(double lat, double lon, string label)?> GeocodeAddress(string apiKey, string address, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(address))
        { return null; }

        string url = $"https://api.openrouteservice.org/geocode/search?text={Uri.EscapeDataString(address)}&size=1";

        using HttpRequestMessage req = new(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", apiKey);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");

        HttpResponseMessage res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        string? json = await res.Content.ReadAsStringAsync(ct);
        OrsGeocodingResponse? geodata = JsonSerializer.Deserialize<OrsGeocodingResponse>(json);
        if (geodata == null)
        { return null; }

        OrsGeocodingFeature? feature = geodata.features[0];
        if (feature == null)
        { return null; }

        if (feature.geometry.coordinates == null || feature.geometry.coordinates.Length < 2)
        { return null; }

        double lon = feature.geometry.coordinates[0];
        double lat = feature.geometry.coordinates[1];
        string label = feature.properties.label ?? address;

        return (lat, lon, label);
    }
}