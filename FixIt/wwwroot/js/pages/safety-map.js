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
        const configElement = document.getElementById('safetyMapConfig');
        if (!configElement) {
            return null;
        }

        try {
            return JSON.parse(configElement.textContent || '{}');
        } catch {
            return null;
        }
    }

    function distanceMeters(lat1, lng1, lat2, lng2) {
        const earthRadius = 6371000;
        const toRad = (value) => value * Math.PI / 180;
        const dLat = toRad(lat2 - lat1);
        const dLng = toRad(lng2 - lng1);

        const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
            Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) *
            Math.sin(dLng / 2) * Math.sin(dLng / 2);

        return 2 * earthRadius * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    }

    function buildClusters(hazards, radiusMeters) {
        const clusters = [];

        hazards.forEach((hazard) => {
            let cluster = clusters.find((candidate) =>
                distanceMeters(candidate.centerLat, candidate.centerLng, hazard.latitude, hazard.longitude) <= radiusMeters);

            if (!cluster) {
                cluster = {
                    id: `cluster-${clusters.length + 1}`,
                    centerLat: hazard.latitude,
                    centerLng: hazard.longitude,
                    hazards: []
                };
                clusters.push(cluster);
            }

            cluster.hazards.push(hazard);
            cluster.centerLat = cluster.hazards.reduce((sum, item) => sum + item.latitude, 0) / cluster.hazards.length;
            cluster.centerLng = cluster.hazards.reduce((sum, item) => sum + item.longitude, 0) / cluster.hazards.length;
        });

        return clusters;
    }

    function toIsoDate(value) {
        const date = new Date(value);
        return Number.isNaN(date.valueOf()) ? null : date.toISOString();
    }

    function buildClusterPayload(cluster) {
        const grouped = new Map();

        cluster.hazards.forEach((hazard) => {
            grouped.set(hazard.type, (grouped.get(hazard.type) || 0) + 1);
        });

        const createdDates = cluster.hazards
            .map((hazard) => new Date(hazard.createdAt))
            .filter((value) => !Number.isNaN(value.valueOf()))
            .sort((a, b) => a - b);

        return {
            latitude: cluster.centerLat,
            longitude: cluster.centerLng,
            totalReports: cluster.hazards.length,
            totalConfirmations: cluster.hazards.reduce((sum, hazard) => sum + (hazard.confirmations || 0), 0),
            fromUtc: createdDates.length ? createdDates[0].toISOString() : null,
            toUtc: createdDates.length ? createdDates[createdDates.length - 1].toISOString() : null,
            hazardTypes: Array.from(grouped.entries()).map(([type, count]) => ({ type, count }))
        };
    }

    function buildFallbackInsight(cluster) {
        const payload = buildClusterPayload(cluster);
        const dominant = payload.hazardTypes.sort((left, right) => right.count - left.count)[0];
        const dominantText = dominant ? `${dominant.count} ${dominant.type.toLowerCase()} reports` : `${payload.totalReports} mixed reports`;
        const from = payload.fromUtc ? new Date(payload.fromUtc).toLocaleDateString() : 'recent period';
        const to = payload.toUtc ? new Date(payload.toUtc).toLocaleDateString() : 'today';

        return `This area has ${dominantText} between ${from} and ${to}, with ${payload.totalConfirmations} confirmations from residents.`;
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

        const insightCard = document.getElementById('hazardInsightCard');
        const insightContent = document.getElementById('hazardInsightContent');
        const insightBadge = insightCard?.querySelector('.hazard-insight-card__badge') || null;

        const map = L.map(mapElement, { attributionControl: false }).setView([42.7339, 25.4858], 10);

        const confirmationLabel = config.confirmationLabel || 'Confirmations';
        const viewDetailsLabel = config.viewDetailsLabel || 'View details';
        const untitledHazardLabel = config.untitledHazardLabel || 'Untitled hazard';
        const unknownLabel = config.unknownLabel || 'Unknown';
        const insightLoadingLabel = config.insightLoadingLabel || 'Generating AI trend insight...';
        const insightErrorLabel = config.insightErrorLabel || 'Could not generate AI insight. Showing fallback insight.';
        const insightFallbackLabel = config.insightFallbackLabel || 'Rule-based insight';
        const insightAiLabel = config.insightAiLabel || 'AI-generated';
        const hazards = Array.isArray(config.hazards) ? config.hazards : [];
        const markers = {};
        const bounds = [];
        const clusters = buildClusters(hazards, 320);

        const cssRoot = getComputedStyle(document.documentElement);
        const cssColor = (variableName, fallbackVar) => {
            const fromVariable = cssRoot.getPropertyValue(variableName).trim();
            if (fromVariable) {
                return fromVariable;
            }
            return fallbackVar ? (cssRoot.getPropertyValue(fallbackVar).trim() || '') : '';
        };

        function setInsightLoading() {
            if (!insightContent || !insightBadge) return;

            insightBadge.textContent = insightAiLabel;
            insightBadge.classList.remove('is-fallback');
            insightContent.innerHTML = `
                <div class="app-inline-state app-inline-state--loading" role="status">
                    <span class="app-skeleton" style="width: 1rem; height: 1rem;"></span>
                    <span>${insightLoadingLabel}</span>
                </div>
            `;
        }

        function setInsightContent(content, aiGenerated) {
            if (!insightContent || !insightBadge) return;

            insightBadge.textContent = aiGenerated ? insightAiLabel : insightFallbackLabel;
            insightBadge.classList.toggle('is-fallback', !aiGenerated);
            insightContent.textContent = content;
        }

        async function streamInsight(cluster) {
            if (!insightContent) {
                return;
            }

            setInsightLoading();
            let streamedText = '';

            try {
                const response = await fetch('/api/safety/insights/cluster/stream', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(buildClusterPayload(cluster))
                });

                if (!response.ok || !response.body) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const reader = response.body.getReader();
                const decoder = new TextDecoder();
                let pending = '';
                let completeEvent = null;

                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;

                    pending += decoder.decode(value, { stream: true });
                    const lines = pending.split('\n');
                    pending = lines.pop() || '';

                    for (const line of lines) {
                        const trimmed = line.trim();
                        if (!trimmed) continue;

                        let event;
                        try {
                            event = JSON.parse(trimmed);
                        } catch {
                            continue;
                        }

                        if (event.type === 'chunk' && typeof event.text === 'string') {
                            streamedText += event.text;
                            setInsightContent(streamedText, event.aiGenerated !== false);
                        }

                        if (event.type === 'complete') {
                            completeEvent = event;
                        }

                        if (event.type === 'error') {
                            throw new Error(event.message || 'Insight stream failed.');
                        }
                    }
                }

                if (completeEvent && typeof completeEvent.content === 'string' && completeEvent.content.trim().length > 0) {
                    setInsightContent(completeEvent.content, completeEvent.aiGenerated !== false);
                    return;
                }

                if (streamedText.trim().length > 0) {
                    setInsightContent(streamedText, true);
                    return;
                }

                throw new Error('Empty streamed insight');
            } catch {
                const fallbackInsight = buildFallbackInsight(cluster);
                setInsightContent(fallbackInsight, false);
                if (typeof window.FixItNotify === 'function') {
                    window.FixItNotify(insightErrorLabel, 'warning');
                }
            }
        }

        function findClusterByHazardId(hazardId) {
            return clusters.find((cluster) => cluster.hazards.some((hazard) => String(hazard.id) === String(hazardId))) || null;
        }

        function createHazardPopup(hazard, pillClass) {
            const container = document.createElement('div');
            container.className = 'map-popup';

            const title = document.createElement('h6');
            title.className = 'mb-2';
            title.textContent = hazard.title || untitledHazardLabel;
            container.appendChild(title);

            const typeRow = document.createElement('div');
            typeRow.className = 'map-popup__row';
            typeRow.textContent = hazard.type || '';
            container.appendChild(typeRow);

            const severityRow = document.createElement('div');
            severityRow.className = 'map-popup__row';
            const severityPill = document.createElement('span');
            severityPill.className = `map-popup__pill ${pillClass}`;
            severityPill.textContent = hazard.severity || unknownLabel;
            severityRow.appendChild(severityPill);
            container.appendChild(severityRow);

            const descriptionRow = document.createElement('div');
            descriptionRow.className = 'map-popup__row';
            descriptionRow.textContent = hazard.description || '';
            container.appendChild(descriptionRow);

            const confirmationsRow = document.createElement('div');
            confirmationsRow.className = 'map-popup__row';
            const confirmationCount = document.createElement('strong');
            confirmationCount.textContent = String(hazard.confirmations ?? 0);
            confirmationsRow.appendChild(confirmationCount);
            confirmationsRow.append(` ${confirmationLabel}`);
            container.appendChild(confirmationsRow);

            const actions = document.createElement('div');
            actions.className = 'map-popup__actions';
            const detailsLink = document.createElement('a');
            detailsLink.className = 'btn btn-sm btn-primary';
            detailsLink.textContent = viewDetailsLabel;
            detailsLink.href = `/safety/view?hazardId=${encodeURIComponent(String(hazard.id || ''))}`;
            actions.appendChild(detailsLink);
            container.appendChild(actions);

            return container;
        }

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);

        const severityColors = {
            Low: cssColor('--severity-low', '--success'),
            Medium: cssColor('--severity-medium', '--warning'),
            High: cssColor('--severity-high', '--danger'),
            Critical: cssColor('--severity-critical', '--gray-900')
        };

        hazards.forEach((hazard) => {
            const color = severityColors[hazard.severity] || cssColor('--severity-unspecified', '--gray-500');
            const pillClass = `map-popup__pill--${String(hazard.severity || '').toLowerCase()}`;
            const marker = L.circleMarker([hazard.latitude, hazard.longitude], {
                radius: 8,
                fillColor: color,
                color,
                weight: 2,
                opacity: 0.8,
                fillOpacity: 0.7
            }).addTo(map);

            marker.bindPopup(createHazardPopup(hazard, pillClass));
            marker.bindTooltip(hazard.title || untitledHazardLabel, { direction: 'top', opacity: 0.92 });
            marker.on('mouseover', () => marker.setStyle({ radius: 10, fillOpacity: 0.92 }));
            marker.on('mouseout', () => marker.setStyle({ radius: 8, fillOpacity: 0.7 }));
            marker.on('click', () => {
                const cluster = findClusterByHazardId(hazard.id);
                if (cluster) {
                    streamInsight(cluster);
                }
            });

            markers[hazard.id] = marker;
            bounds.push([hazard.latitude, hazard.longitude]);
        });

        if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [24, 24] });
        }

        document.querySelectorAll('[data-hazard-focus]').forEach((item) => {
            item.addEventListener('click', () => {
                const id = item.getAttribute('data-id');
                const lat = parseFloat(item.getAttribute('data-lat') || '');
                const lng = parseFloat(item.getAttribute('data-lng') || '');

                if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                    return;
                }

                map.setView([lat, lng], 14);
                markers[id]?.openPopup();
                markers[id]?.setStyle({ radius: 11, weight: 3 });
                window.setTimeout(() => markers[id]?.setStyle({ radius: 8, weight: 2 }), 220);

                const cluster = findClusterByHazardId(id);
                if (cluster) {
                    streamInsight(cluster);
                }
            });
        });

        if (clusters.length > 0 && insightContent) {
            const latestCluster = clusters
                .slice()
                .sort((left, right) => right.hazards.length - left.hazards.length)[0];
            setInsightContent(buildFallbackInsight(latestCluster), false);
        }
    });
})();
