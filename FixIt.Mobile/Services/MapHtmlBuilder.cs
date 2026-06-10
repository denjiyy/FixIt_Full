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

    /// <summary>
    /// Selectable Leaflet map. Map click and marker drag both navigate to
    /// <c>fixit://location?lat=…&amp;lng=…</c> so the host page can pick the
    /// new coordinates up via WebView.Navigating.
    /// </summary>
    public static HtmlWebViewSource BuildPickerMap(double latitude, double longitude, int zoom = 15)
    {
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lng = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new HtmlWebViewSource
        {
            Html = BuildHtml(latitude, longitude, zoom, $$"""
                const marker = L.marker([{{lat}}, {{lng}}], { draggable: true }).addTo(map);
                function emit(latlng) {
                    window.location.href = 'fixit://location?lat=' + latlng.lat + '&lng=' + latlng.lng;
                }
                map.on('click', function (e) {
                    marker.setLatLng(e.latlng);
                    emit(e.latlng);
                });
                marker.on('dragend', function () {
                    emit(marker.getLatLng());
                });
                """)
        };
    }

    /// <summary>
    /// Hazard map for HazardMode: renders existing hazards as severity-coloured
    /// markers and lets the user tap/drag a single "report" pin to place a new
    /// hazard. The report pin navigates to <c>fixit://location?lat=…&amp;lng=…</c>
    /// so the host page can pick the coordinates up via WebView.Navigating —
    /// the same bridge the issue-report map uses.
    /// </summary>
    public static HtmlWebViewSource BuildHazardMap(
        IEnumerable<SafetyHazard> hazards,
        double centerLatitude = 42.6977,
        double centerLongitude = 23.3219,
        int zoom = 13,
        string reportPinLabel = "Drag to adjust")
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
        var labelJson = JsonSerializer.Serialize(reportPinLabel);
        return new HtmlWebViewSource
        {
            Html = BuildHtml(centerLatitude, centerLongitude, zoom, $$"""
                const pins = {{pinJson}};
                const severityColor = (value) => {
                    switch (String(value || '').toLowerCase()) {
                        case 'critical': return '#dc2626';
                        case 'high': return '#ea580c';
                        case 'medium': return '#d97706';
                        case 'low': return '#16a34a';
                        default: return '#2563eb';
                    }
                };
                pins.forEach(pin => {
                    if (!pin.lat || !pin.lng) return;
                    L.circleMarker([pin.lat, pin.lng], {
                        radius: 9, color: '#0f172a', weight: 2,
                        fillColor: severityColor(pin.severity), fillOpacity: 0.85
                    }).addTo(map)
                        .bindPopup(`<strong>${escapeHtml(pin.title)}</strong><br>${escapeHtml(pin.severity)} · ${pin.confirmations}<br>${escapeHtml(pin.address)}`);
                });

                let reportMarker = null;
                function emitReport(latlng) {
                    window.location.href = 'fixit://location?lat=' + latlng.lat + '&lng=' + latlng.lng;
                }
                map.on('click', function (e) {
                    if (!reportMarker) {
                        reportMarker = L.marker(e.latlng, { draggable: true }).addTo(map);
                        reportMarker.bindTooltip({{labelJson}}, { permanent: false, direction: 'top' });
                        reportMarker.on('dragend', function () { emitReport(reportMarker.getLatLng()); });
                    } else {
                        reportMarker.setLatLng(e.latlng);
                    }
                    emitReport(e.latlng);
                });
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
