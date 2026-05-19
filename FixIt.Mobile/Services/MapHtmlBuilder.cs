using System.Net;
using System.Text;
using System.Text.Json;
using FixIt.Mobile.Models;

namespace FixIt.Mobile.Services;

public static class MapHtmlBuilder
{
    public static HtmlWebViewSource BuildIssueMap(Issue issue)
    {
        var title = WebUtility.HtmlEncode(issue.Title);
        var address = WebUtility.HtmlEncode(issue.Address);
        var latitude = issue.Latitude ?? 0;
        var longitude = issue.Longitude ?? 0;

        return new HtmlWebViewSource
        {
            Html = BuildHtml(latitude, longitude, 15, $$"""
                L.marker([{{latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, {{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}])
                    .addTo(map)
                    .bindPopup('<strong>{{title}}</strong><br>{{address}}')
                    .openPopup();
                """)
        };
    }

    public static HtmlWebViewSource BuildHazardMap(IEnumerable<SafetyHazard> hazards)
    {
        var pins = hazards
            .Where(h => h.HasCoordinates)
            .Select(h => new
            {
                lat = h.Latitude,
                lng = h.Longitude,
                title = h.Title,
                address = h.Address,
                severity = h.Severity,
                confirmations = h.Confirmations
            })
            .ToList();

        var pinJson = JsonSerializer.Serialize(pins);
        return new HtmlWebViewSource
        {
            Html = BuildHtml(42.6977, 23.3219, 12, $$"""
                const pins = {{pinJson}};
                const bounds = [];
                pins.forEach(pin => {
                    if (!pin.lat || !pin.lng) return;
                    bounds.push([pin.lat, pin.lng]);
                    L.marker([pin.lat, pin.lng])
                        .addTo(map)
                        .bindPopup(`<strong>${escapeHtml(pin.title)}</strong><br>${escapeHtml(pin.severity)} · ${pin.confirmations}<br>${escapeHtml(pin.address)}`);
                });
                if (bounds.length > 0) {
                    map.fitBounds(bounds, { padding: [28, 28], maxZoom: 15 });
                }
                """)
        };
    }

    private static string BuildHtml(double latitude, double longitude, int zoom, string script)
    {
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lng = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var html = new StringBuilder();
        html.Append($$"""
            <!doctype html>
            <html>
            <head>
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
              <style>
                html, body, #map { height: 100%; width: 100%; margin: 0; background: #1e293b; }
                .leaflet-popup-content-wrapper, .leaflet-popup-tip { background: #0f172a; color: #f8fafc; }
                .leaflet-container { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
              </style>
            </head>
            <body>
              <div id="map"></div>
              <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
              <script>
                const map = L.map('map', { zoomControl: true }).setView([{{lat}}, {{lng}}], {{zoom}});
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 19,
                    attribution: '&copy; OpenStreetMap'
                }).addTo(map);
                function escapeHtml(value) {
                    return String(value ?? '').replace(/[&<>"']/g, c => ({
                        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
                    }[c]));
                }
            """);
        html.Append(script);
        html.Append("""
              </script>
            </body>
            </html>
            """);
        return html.ToString();
    }
}
