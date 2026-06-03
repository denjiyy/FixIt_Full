// Browse: gallery <-> map toggle with a lazily-initialised Leaflet map.
(function () {
    'use strict';

    function ready(cb) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', cb, { once: true });
        } else {
            cb();
        }
    }

    ready(function () {
        var toggle = document.getElementById('browseToggle');
        var gallery = document.getElementById('browseGallery');
        var mapPanel = document.getElementById('browseMap');
        if (!toggle || !gallery || !mapPanel) {
            return;
        }

        var mapEl = document.getElementById('issuesLeaflet');
        var map = null;

        var statusColor = {
            New: '#0ea5e9',
            Confirmed: '#7c3aed',
            InProgress: '#f59e0b',
            Fixed: '#10b981',
            Rejected: '#ef4444',
            Duplicate: '#64748b',
            Archived: '#64748b'
        };

        function readPoints() {
            var el = document.getElementById('issuesMapConfig');
            if (!el) {
                return [];
            }
            try {
                return JSON.parse(el.textContent || '[]');
            } catch (e) {
                return [];
            }
        }

        function escapeHtml(value) {
            return String(value == null ? '' : value)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;');
        }

        function initMap() {
            if (map || typeof L === 'undefined' || !mapEl) {
                return;
            }

            map = L.map(mapEl, { attributionControl: false, scrollWheelZoom: false })
                .setView([42.7339, 25.4858], 6);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(map);

            var points = readPoints();
            var bounds = [];

            points.forEach(function (p) {
                if (typeof p.lat !== 'number' || typeof p.lng !== 'number') {
                    return;
                }
                var color = statusColor[p.status] || '#0ea5e9';
                var icon = L.divIcon({
                    className: 'fixit-pin',
                    html: '<span style="display:block;width:18px;height:18px;border-radius:50% 50% 50% 0;'
                        + 'transform:rotate(-45deg);background:' + color
                        + ';box-shadow:0 2px 6px rgba(15,23,42,.35);border:2px solid #fff"></span>',
                    iconSize: [18, 18],
                    iconAnchor: [9, 16]
                });
                var marker = L.marker([p.lat, p.lng], { icon: icon }).addTo(map);
                marker.bindPopup(
                    '<strong>' + escapeHtml(p.title) + '</strong><br>'
                    + '<span style="color:#64748b">' + escapeHtml(p.address) + '</span><br>'
                    + '<a href="/issues/' + encodeURIComponent(p.id) + '">View issue &rarr;</a>'
                );
                bounds.push([p.lat, p.lng]);
            });

            if (bounds.length) {
                map.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 });
            }
        }

        function setView(view) {
            var isMap = view === 'map';
            gallery.style.display = isMap ? 'none' : '';
            mapPanel.style.display = isMap ? '' : 'none';

            Array.prototype.forEach.call(toggle.querySelectorAll('button[data-view]'), function (btn) {
                btn.classList.toggle('on', btn.getAttribute('data-view') === view);
            });

            if (isMap) {
                initMap();
                // Leaflet needs a size recalculation once its container becomes visible.
                window.setTimeout(function () {
                    if (map) {
                        map.invalidateSize();
                    }
                }, 60);
            }

            try {
                window.localStorage.setItem('fixit.browseView', view);
            } catch (e) {
                /* ignore storage failures */
            }
        }

        toggle.addEventListener('click', function (e) {
            var btn = e.target.closest('button[data-view]');
            if (!btn) {
                return;
            }
            setView(btn.getAttribute('data-view'));
        });

        var saved = 'gallery';
        try {
            saved = window.localStorage.getItem('fixit.browseView') || 'gallery';
        } catch (e) {
            saved = 'gallery';
        }
        // An explicit ?view=map / ?view=gallery wins (shareable, and survives reload).
        var requested = new URLSearchParams(window.location.search).get('view');
        if (requested === 'map' || requested === 'gallery') {
            saved = requested;
        }
        setView(saved);
    });
})();
