(function () {
    'use strict';

    const onReady = window.FixItApp?.onReady || ((callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    });

    onReady(() => {
        let map = null;
        let marker = null;

        function setCoordinates(lat, lng) {
            const latitudeInput = document.getElementById('Latitude');
            const longitudeInput = document.getElementById('Longitude');
            const coordsDisplay = document.getElementById('coordsDisplay');

            if (!latitudeInput || !longitudeInput || !coordsDisplay) {
                return;
            }

            latitudeInput.value = lat.toFixed(4);
            longitudeInput.value = lng.toFixed(4);
            coordsDisplay.textContent = `${lat.toFixed(4)}, ${lng.toFixed(4)}`;

            if (marker) {
                marker.setLatLng([lat, lng]);
            } else {
                marker = L.marker([lat, lng]).addTo(map);
            }

            map.setView([lat, lng], 8);
        }

        function initMap() {
            const mapElement = document.getElementById('coordinatesMap');
            if (!mapElement || typeof window.L === 'undefined') {
                return;
            }

            map = L.map(mapElement).setView([20, 0], 2);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(map);

            map.on('click', (event) => {
                setCoordinates(event.latlng.lat, event.latlng.lng);
            });
        }

        initMap();

        document.querySelectorAll('.city-photo').forEach((image) => {
            image.addEventListener('error', () => {
                image.closest('.city-photo-frame')?.remove();
            }, { once: true });
        });
    });
})();
