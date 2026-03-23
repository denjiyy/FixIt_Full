/**
 * Map Detail Module
 * Handles map initialization and rendering for issue location
 * Note: Leaflet CSS is loaded conditionally in _Layout.cshtml
 * This module only handles dynamic JS loading and map rendering
 */
const MapDetail = (() => {
    'use strict';

    function init() {
        const mapEl = document.getElementById('detailMap');
        if (!mapEl) return;

        const lat = parseFloat(mapEl.dataset.latitude);
        const lng = parseFloat(mapEl.dataset.longitude);
        if (isNaN(lat) || isNaN(lng)) return;

        // If Leaflet JS is already loaded, render immediately
        if (window.L) {
            render(lat, lng);
        } else {
            // Dynamically load Leaflet JS (CSS already in _Layout.cshtml)
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/leaflet@1.9.4/dist/leaflet.js';
            script.onload = () => render(lat, lng);
            document.head.appendChild(script);
        }
    }

    function render(lat, lng) {
        const mapEl = document.getElementById('detailMap');
        if (!mapEl) return;
        try {
            const map = L.map('detailMap').setView([lat, lng], 14);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors'
            }).addTo(map);
            L.marker([lat, lng]).addTo(map);
        } catch (e) {
            console.error('Map render error:', e);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => init());
    } else {
        init();
    }

    return { init };
})();
