(function () {
    'use strict';

    const onReady = window.FixItApp?.onReady || ((callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    });

    function parseConfig() {
        const configElement = document.getElementById('heatmapsConfig');
        if (!configElement) {
            return null;
        }

        try {
            return JSON.parse(configElement.textContent || '{}');
        } catch {
            return null;
        }
    }

    onReady(() => {
        if (typeof window.L === 'undefined') {
            return;
        }

        const config = parseConfig();
        const mapElement = document.getElementById('map');
        if (!config || !mapElement) {
            return;
        }

        const cityLat = Number.parseFloat(config.cityLat);
        const cityLng = Number.parseFloat(config.cityLng);
        if (!Number.isFinite(cityLat) || !Number.isFinite(cityLng)) {
            return;
        }

        const map = L.map(mapElement).setView([cityLat, cityLng], 13);
        const statusLabelText = config.statusLabelText || 'Status';
        const priorityLabelText = config.priorityLabelText || 'Priority';
        const viewDetailsText = config.viewDetailsText || 'View details';
        const untitledIssueText = config.untitledIssueText || 'Untitled issue';
        const unknownText = config.unknownText || 'Unknown';
        const markersData = Array.isArray(config.markersData) ? config.markersData : [];

        const cssRoot = getComputedStyle(document.documentElement);
        const cssColor = (variableName, fallbackVar) => {
            const fromVariable = cssRoot.getPropertyValue(variableName).trim();
            if (fromVariable) {
                return fromVariable;
            }
            return fallbackVar ? (cssRoot.getPropertyValue(fallbackVar).trim() || '') : '';
        };

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors',
            maxZoom: 19
        }).addTo(map);

        const priorityColors = {
            Critical: cssColor('--severity-high', '--danger'),
            High: cssColor('--accent', '--warning'),
            Medium: cssColor('--severity-medium', '--warning'),
            Low: cssColor('--severity-low', '--success'),
            Unspecified: cssColor('--severity-unspecified', '--gray-500')
        };

        function createIssuePopup(marker, pillClass) {
            const container = document.createElement('div');
            container.className = 'map-popup';

            const title = document.createElement('h6');
            title.className = 'mb-2';
            title.textContent = marker.title || untitledIssueText;
            container.appendChild(title);

            const statusLabelRow = document.createElement('div');
            statusLabelRow.className = 'map-popup__row';
            const statusLabel = document.createElement('span');
            statusLabel.className = 'map-popup__label';
            statusLabel.textContent = statusLabelText;
            statusLabelRow.appendChild(statusLabel);
            container.appendChild(statusLabelRow);

            const statusValueRow = document.createElement('p');
            statusValueRow.className = 'map-popup__row';
            const statusPill = document.createElement('span');
            statusPill.className = 'map-popup__pill map-popup__pill--muted';
            statusPill.textContent = marker.status || unknownText;
            statusValueRow.appendChild(statusPill);
            container.appendChild(statusValueRow);

            const priorityLabelRow = document.createElement('div');
            priorityLabelRow.className = 'map-popup__row';
            const priorityLabel = document.createElement('span');
            priorityLabel.className = 'map-popup__label';
            priorityLabel.textContent = priorityLabelText;
            priorityLabelRow.appendChild(priorityLabel);
            container.appendChild(priorityLabelRow);

            const priorityValueRow = document.createElement('p');
            priorityValueRow.className = 'map-popup__row';
            const priorityPill = document.createElement('span');
            priorityPill.className = `map-popup__pill ${pillClass}`;
            priorityPill.textContent = marker.priority || unknownText;
            priorityValueRow.appendChild(priorityPill);
            container.appendChild(priorityValueRow);

            const actions = document.createElement('div');
            actions.className = 'map-popup__actions';
            const detailsLink = document.createElement('a');
            detailsLink.className = 'btn btn-sm btn-primary';
            detailsLink.textContent = viewDetailsText;
            detailsLink.href = `/issues/${encodeURIComponent(String(marker.issueId || ''))}`;
            actions.appendChild(detailsLink);
            container.appendChild(actions);

            return container;
        }

        let openCount = 0;
        let resolvedCount = 0;
        const markerGroup = L.featureGroup();

        markersData.forEach((marker) => {
            const color = priorityColors[marker.priority] || cssColor('--severity-unspecified', '--gray-500');
            const pillClass = `map-popup__pill--${String(marker.priority || '').toLowerCase()}`;

            if (marker.status === 'Fixed' || marker.status === 'Rejected') {
                resolvedCount += 1;
            } else {
                openCount += 1;
            }

            const circle = L.circleMarker([marker.latitude, marker.longitude], {
                radius: 10,
                fillColor: color,
                color: cssColor('--white', '--surface-strong'),
                weight: 2,
                opacity: 1,
                fillOpacity: 0.85,
                className: 'issue-marker'
            });

            circle.bindPopup(createIssuePopup(marker, pillClass));
            circle.bindTooltip(marker.title || untitledIssueText, { direction: 'top', opacity: 0.92 });
            circle.on('click', () => {
                const safeIssueId = encodeURIComponent(String(marker.issueId || ''));
                circle.setStyle({ radius: 12, weight: 3 });
                window.setTimeout(() => circle.setStyle({ radius: 10, weight: 2 }), 220);
                window.location.href = `/issues/${safeIssueId}`;
            });
            circle.on('mouseover', function () {
                this.setStyle({ weight: 3, radius: 12, fillOpacity: 1 });
            });
            circle.on('mouseout', function () {
                this.setStyle({ weight: 2, radius: 10, fillOpacity: 0.85 });
            });

            circle.addTo(map);
            markerGroup.addLayer(circle);
        });

        if (markersData.length > 0) {
            map.fitBounds(markerGroup.getBounds().pad(0.1));
        }

        const openCountElement = document.getElementById('openCount');
        const resolvedCountElement = document.getElementById('resolvedCount');
        const totalCountElement = document.getElementById('totalCount');

        if (openCountElement) {
            openCountElement.textContent = String(openCount);
        }

        if (resolvedCountElement) {
            resolvedCountElement.textContent = String(resolvedCount);
        }

        if (totalCountElement) {
            totalCountElement.textContent = String(markersData.length);
        }
    });
})();
