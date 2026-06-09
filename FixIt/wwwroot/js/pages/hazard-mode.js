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
        const configElement = document.getElementById('hazardModeConfig');
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
        const plural = (count, word) => `${count} ${word}${count === 1 ? '' : 's'}`;
        const dominant = payload.hazardTypes.sort((left, right) => right.count - left.count)[0];
        const dominantText = dominant
            ? plural(dominant.count, `${dominant.type.toLowerCase()} report`)
            : plural(payload.totalReports, 'mixed report');
        const from = payload.fromUtc ? new Date(payload.fromUtc).toLocaleDateString() : 'recent period';
        const to = payload.toUtc ? new Date(payload.toUtc).toLocaleDateString() : 'today';

        return `This area has ${dominantText} between ${from} and ${to}, with ${plural(payload.totalConfirmations, 'confirmation')} from residents.`;
    }

    onReady(() => {
        if (typeof window.L === 'undefined') {
            return;
        }

        const config = parseConfig();
        if (!config) {
            return;
        }

        let map;
        let userMarker;
        let hazardMarkers = {};
        let currentCity = config.currentCity || '';
        let selectedHazardId = null;
        let reportLocationLat = null;
        let reportLocationLng = null;
        let reportLocationAddress = '';
        let userLocation = { lat: 42.7339, lng: 25.4858 };
        let clusters = [];

        const localized = {
            yourLocation: 'Your location',
            latitudeLabel: 'Latitude',
            longitudeLabel: 'Longitude',
            locationSelected: 'Location selected',
            noHazardsNearby: 'No hazards nearby',
            failedToLoadHazards: 'Unable to load nearby hazards right now.',
            viewDetails: 'View details',
            typeLabel: 'Type',
            severityLabel: 'Severity',
            descriptionLabel: 'Description',
            locationLabel: 'Location',
            confirmationsLabel: 'Confirmations',
            reportedLabel: 'Reported',
            hazardConfirmed: 'Hazard confirmed.',
            fillAllRequiredFields: 'Fill in all required fields before submitting.',
            clickOnMapToSelectLocation: 'Click on the map to select a location',
            reportSubmitted: 'Report submitted.',
            errorSubmittingReport: 'We could not submit the report.',
            hazardUpdated: 'Hazard updated.',
            errorUpdatingHazard: 'We could not update the hazard.',
            hazardDeleted: 'Hazard deleted.',
            errorDeletingHazard: 'We could not delete the hazard.',
            hazardRestored: 'Hazard restored.',
            errorRestoringHazard: 'We could not restore the hazard.',
            confirmDeleteHazard: 'Are you sure you want to delete this hazard?',
            confirmRestoreHazard: 'Restore this hazard?',
            loadingAddress: 'Loading address...',
            loadingHazards: 'Loading hazards...',
            quickReportThanks: 'Thank you! Your report has been submitted and helps keep the community safe.',
            pleaseSelectLocation: 'Please click on the map to select a location.',
            noHazardsArea: 'No hazards in your area',
            hazardUpdatedShort: 'Hazard updated',
            hazardDeletedShort: 'Hazard deleted',
            hazardRestoredShort: 'Hazard restored',
            insightLoadingLabel: 'Generating AI trend insight...',
            insightErrorLabel: 'Could not generate AI insight. Showing fallback insight.',
            insightFallbackLabel: 'Rule-based insight',
            insightAiLabel: 'AI-generated',
            locating: 'Locating...',
            locationDenied: 'Location access is blocked. Allow location for this site in your browser, then tap My location again.',
            locationUnavailable: 'We could not determine your location. Showing the default coverage area.',
            locationTimeout: 'Locating timed out. Check your connection or device location settings and try again.',
            locationInsecure: 'Location needs a secure (HTTPS) connection. Open the site over HTTPS to use My location.',
            locationUnsupported: 'Your browser does not support location. Showing the default coverage area.'
        };

        if (config.localized && typeof config.localized === 'object') {
            Object.assign(localized, config.localized);
        }

        let geocodingCache = {};
        let lastGeocodingRequest = 0;
        let geocodingTimeout = null;
        const GEOCODING_DEBOUNCE_MS = 300;
        const GEOCODING_REQUEST_THROTTLE_MS = 500;
        let hazardRefreshInterval = null;
        const HAZARD_REFRESH_INTERVAL_MS = 5000;

        const elements = {};
        let hazardToast;
        const cssRoot = getComputedStyle(document.documentElement);
        const cssColor = (variableName, fallbackVariable) => {
            const fromVariable = cssRoot.getPropertyValue(variableName).trim();
            if (fromVariable) {
                return fromVariable;
            }
            return fallbackVariable ? (cssRoot.getPropertyValue(fallbackVariable).trim() || '') : '';
        };

        const severityColors = {
            Critical: cssColor('--severity-high', '--danger'),
            High: cssColor('--accent', '--warning'),
            Medium: cssColor('--severity-medium', '--warning'),
            Low: cssColor('--severity-low', '--success')
        };

        const severityIcons = {
            Critical: '🚨',
            High: '⚠️',
            Medium: '⚡',
            Low: 'ℹ️'
        };

        elements.hazardsList = document.getElementById('hazardsList');
        elements.hazardFilter = document.getElementById('hazardFilter');
        elements.severityFilter = document.getElementById('severityFilter');
        elements.typeFilter = document.getElementById('typeFilter');
        elements.reportLocationDisplay = document.getElementById('reportLocationDisplay');
        elements.reportLocationInput = document.getElementById('reportLocationInput');
        elements.totalHazardsCount = document.getElementById('totalHazardsCount');
        elements.criticalCount = document.getElementById('criticalCount');
        elements.highCount = document.getElementById('highCount');
        elements.hazardModalTitle = document.getElementById('hazardModalTitle');
        elements.hazardDetailContent = document.getElementById('hazardDetailContent');
        elements.confirmBtn = document.getElementById('confirmBtn');
        elements.editBtn = document.getElementById('editBtn');
        elements.deleteBtn = document.getElementById('deleteBtn');
        elements.restoreBtn = document.getElementById('restoreBtn');
        elements.myLocationBtn = document.getElementById('myLocationBtn');
        elements.submitQuickReportBtn = document.getElementById('submitQuickReportBtn');
        elements.saveHazardUpdateBtn = document.getElementById('saveHazardUpdateBtn');
        elements.insightCard = document.getElementById('hazardInsightCard');
        elements.insightContent = document.getElementById('hazardInsightContent');
        elements.insightBadge = elements.insightCard?.querySelector('.hazard-insight-card__badge') || null;
        const insightPlaceholder = (elements.insightContent?.textContent || 'Select a hazard to generate a cluster insight.').trim();
        const myLocationDefaultHtml = elements.myLocationBtn?.innerHTML || '';

        if (!elements.hazardsList) {
            return;
        }

        hazardToast = bootstrap.Toast.getOrCreateInstance(document.getElementById('hazardToast'));

        bindEvents();
        initializeMap();
        loadNearbyHazards();
        openDeepLinkedHazard();

        if (hazardRefreshInterval) {
            clearInterval(hazardRefreshInterval);
        }

        hazardRefreshInterval = setInterval(loadNearbyHazards, HAZARD_REFRESH_INTERVAL_MS);

        locateUser(false);

        function bindEvents() {
            elements.hazardFilter?.addEventListener('input', applyFilters);
            elements.severityFilter?.addEventListener('change', applyFilters);
            elements.typeFilter?.addEventListener('change', applyFilters);
            elements.myLocationBtn?.addEventListener('click', focusMyLocation);
            elements.confirmBtn?.addEventListener('click', confirmHazard);
            elements.editBtn?.addEventListener('click', openEditModal);
            elements.deleteBtn?.addEventListener('click', deleteHazard);
            elements.restoreBtn?.addEventListener('click', restoreHazard);
            elements.submitQuickReportBtn?.addEventListener('click', submitQuickReport);
            elements.saveHazardUpdateBtn?.addEventListener('click', submitHazardUpdate);

            document.addEventListener('click', (event) => {
                const detailTrigger = event.target.closest('[data-hazard-detail]');
                if (detailTrigger) {
                    event.preventDefault();
                    showHazardDetail(detailTrigger.getAttribute('data-hazard-detail'));
                    return;
                }

                const selectTrigger = event.target.closest('[data-select-hazard]');
                if (selectTrigger) {
                    selectHazard(selectTrigger.getAttribute('data-select-hazard'));
                }
            });
        }

        function initializeMap() {
            const mapElement = document.getElementById('hazardMap');
            if (!mapElement) {
                return;
            }

            map = L.map(mapElement).setView([userLocation.lat, userLocation.lng], 13);

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(map);

            userMarker = L.circleMarker([userLocation.lat, userLocation.lng], {
                radius: 8,
                fillColor: cssColor('--primary-500', '--primary'),
                color: cssColor('--primary-700', '--primary-dark'),
                weight: 2,
                opacity: 1,
                fillOpacity: 0.8
            }).addTo(map);
            userMarker.bindPopup(localized.yourLocation);

            map.on('click', (event) => {
                reportLocationLat = event.latlng.lat;
                reportLocationLng = event.latlng.lng;

                const coordDisplay = `${localized.latitudeLabel}: ${reportLocationLat.toFixed(6)}, ${localized.longitudeLabel}: ${reportLocationLng.toFixed(6)}`;
                if (elements.reportLocationDisplay) {
                    elements.reportLocationDisplay.textContent = coordDisplay;
                }
                updateLocationField(localized.loadingAddress, true);

                clearTimeout(geocodingTimeout);
                geocodingTimeout = setTimeout(() => {
                    reverseGeocode(reportLocationLat, reportLocationLng);
                }, GEOCODING_DEBOUNCE_MS);

                bootstrap.Modal.getOrCreateInstance(document.getElementById('reportHazardModal')).show();
            });
        }

        function focusMyLocation() {
            locateUser(true);
        }

        // Resolve the visitor's real position and re-center the map on it.
        // `triggeredByUser` is true for an explicit "My location" tap (we surface
        // toasts) and false for the silent attempt on page load (console only).
        function locateUser(triggeredByUser) {
            // The Geolocation API only resolves in a secure context (HTTPS or localhost).
            // Over plain HTTP it errors immediately, which is the usual reason the map
            // stays on the default coverage area.
            if (!window.isSecureContext) {
                if (triggeredByUser) {
                    showHazardNotice(localized.locationInsecure, 'danger');
                } else {
                    console.warn('[hazard-mode] geolocation unavailable: insecure context (needs HTTPS).');
                }
                return;
            }

            if (!navigator.geolocation) {
                if (triggeredByUser) {
                    showHazardNotice(localized.locationUnsupported, 'danger');
                }
                return;
            }

            setLocating(true);

            navigator.geolocation.getCurrentPosition(
                (position) => {
                    setLocating(false);
                    userLocation = {
                        lat: position.coords.latitude,
                        lng: position.coords.longitude
                    };
                    updateMapCenter();
                    loadNearbyHazards();
                },
                (error) => {
                    setLocating(false);
                    let message = localized.locationUnavailable;
                    if (error.code === error.PERMISSION_DENIED) {
                        message = localized.locationDenied;
                    } else if (error.code === error.TIMEOUT) {
                        message = localized.locationTimeout;
                    }

                    // Keep the default coverage area; only nag on an explicit request.
                    if (triggeredByUser) {
                        showHazardNotice(message, 'danger');
                    } else {
                        console.warn(`[hazard-mode] geolocation failed: ${message}`);
                    }
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 }
            );
        }

        function setLocating(isLocating) {
            const btn = elements.myLocationBtn;
            if (!btn) {
                return;
            }

            btn.disabled = isLocating;
            btn.innerHTML = isLocating
                ? `<i class="bi bi-geo-alt me-2"></i>${escapeHtml(localized.locating)}`
                : myLocationDefaultHtml;
        }

        function updateMapCenter() {
            if (!map || !userMarker) {
                return;
            }

            map.setView([userLocation.lat, userLocation.lng], 13);
            userMarker.setLatLng([userLocation.lat, userLocation.lng]);
        }

        async function reverseGeocode(lat, lng) {
            const cacheKey = `${lat.toFixed(4)},${lng.toFixed(4)}`;

            if (geocodingCache[cacheKey]) {
                reportLocationAddress = geocodingCache[cacheKey];
                updateLocationField(geocodingCache[cacheKey]);
                return;
            }

            const now = Date.now();
            if (now - lastGeocodingRequest < GEOCODING_REQUEST_THROTTLE_MS) {
                return;
            }

            lastGeocodingRequest = now;

            try {
                const response = await fetch(`/api/geocoding/reverse?latitude=${lat}&longitude=${lng}`, {
                    method: 'GET',
                    headers: {
                        Accept: 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`Geocoding API error: ${response.status}`);
                }

                const data = await response.json();
                if (data && data.address) {
                    geocodingCache[cacheKey] = data.address;
                    reportLocationAddress = data.address;
                    updateLocationField(data.address);
                    return;
                }

                const fallback = `${localized.latitudeLabel}: ${lat.toFixed(6)}, ${localized.longitudeLabel}: ${lng.toFixed(6)}`;
                reportLocationAddress = fallback;
                updateLocationField(fallback);
            } catch {
                const fallback = `${localized.latitudeLabel}: ${lat.toFixed(6)}, ${localized.longitudeLabel}: ${lng.toFixed(6)}`;
                reportLocationAddress = fallback;
                updateLocationField(fallback);
            }
        }

        function updateLocationField(address, isLoading = false) {
            if (!elements.reportLocationInput) {
                return;
            }

            elements.reportLocationInput.value = address;
            elements.reportLocationInput.classList.toggle('is-loading', isLoading);
        }

        async function loadNearbyHazards() {
            elements.hazardsList.innerHTML = `
                <div class="hazard-list-placeholder">
                    <i class="bi bi-arrow-repeat"></i>
                    <h3>${escapeHtml(localized.loadingHazards)}</h3>
                    <p class="mb-0">Please wait while we refresh nearby hazard data.</p>
                </div>
            `;

            try {
                const response = await fetch(`/api/safety/nearby-hazards?cityId=${encodeURIComponent(currentCity)}&latitude=${userLocation.lat}&longitude=${userLocation.lng}&radiusKm=10`);

                if (!response.ok) {
                    showHazardNotice(`${localized.failedToLoadHazards} (${response.status})`, 'danger');
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const data = await response.json();
                if (data.success && Array.isArray(data.data)) {
                    displayHazards(data.data);
                    updateStatistics(data.data);
                    return;
                }

                showHazardNotice(data.message || localized.failedToLoadHazards, 'danger');
                elements.hazardsList.innerHTML = `
                    <div class="hazard-list-placeholder hazard-list-placeholder--warn">
                        <i class="bi bi-exclamation-circle"></i>
                        <h3>${escapeHtml(localized.noHazardsArea)}</h3>
                        <p class="mb-0">Try adjusting the active radius by moving to a different area.</p>
                    </div>
                `;
            } catch {
                showHazardNotice(localized.failedToLoadHazards, 'danger');
                elements.hazardsList.innerHTML = `
                    <div class="hazard-list-placeholder hazard-list-placeholder--warn">
                        <i class="bi bi-wifi-off"></i>
                        <h3>${escapeHtml(localized.failedToLoadHazards)}</h3>
                        <p class="mb-0">The list will refresh automatically once the connection stabilizes.</p>
                    </div>
                `;
            }
        }

        function displayHazards(hazards) {
            if (!map) {
                return;
            }

            Object.values(hazardMarkers).forEach((marker) => map.removeLayer(marker));
            hazardMarkers = {};
            clusters = [];

            if (!hazards.length) {
                elements.hazardsList.innerHTML = `
                    <div class="hazard-list-placeholder hazard-list-placeholder--success">
                        <i class="bi bi-shield-check"></i>
                        <h3>${escapeHtml(localized.noHazardsNearby)}</h3>
                        <p class="mb-0">No incidents currently match the live radius around the selected location.</p>
                    </div>
                `;
                if (!selectedHazardId) {
                    resetInsight();
                }
                return;
            }

            elements.hazardsList.innerHTML = hazards.map((hazard) => {
                const severityKey = String(hazard.severity || '').toLowerCase();
                const distance = Number(hazard.distance || 0).toFixed(1);
                const title = escapeHtml(hazard.title || localized.locationSelected);
                const type = escapeHtml(hazard.type || '');
                const severity = escapeHtml(hazard.severity || '');
                const confirmations = Number(hazard.confirmations || 0);

                return `
                    <article class="hazard-item severity-${severityKey}" data-hazard-item data-type="${escapeAttribute(type)}">
                        <button type="button" class="hazard-item__select"
                                data-select-hazard="${hazard.id}"
                                aria-label="${title}">
                            <div class="hazard-item-header">
                                <span class="hazard-icon" aria-hidden="true">${severityIcons[hazard.severity] || '•'}</span>
                                <div class="hazard-info">
                                    <h3>${title}</h3>
                                    <small>${type} • ${distance} km away</small>
                                </div>
                            </div>
                            <div class="hazard-item-footer">
                                <span class="badge bg-${getSeverityBadgeClass(hazard.severity)}">${severity}</span>
                                <span class="badge bg-light text-dark">${confirmations} ${escapeHtml(localized.confirmationsLabel)}</span>
                            </div>
                        </button>
                        <button type="button" class="hazard-item__detail" data-hazard-detail="${hazard.id}">
                            ${escapeHtml(localized.viewDetails)}
                        </button>
                    </article>
                `;
            }).join('');

            hazards.forEach((hazard) => {
                const markerColor = severityColors[hazard.severity] || cssColor('--severity-unspecified', '--gray-500');
                const marker = L.circleMarker([hazard.latitude, hazard.longitude], {
                    radius: 10,
                    fillColor: markerColor,
                    color: cssColor('--gray-900', '--text'),
                    weight: 2,
                    opacity: 1,
                    fillOpacity: 0.8
                }).addTo(map);

                marker.bindPopup(`
                    <div class="map-popup">
                        <h6 class="mb-2">${escapeHtml(hazard.title || '')}</h6>
                        <div class="map-popup__row">${escapeHtml(hazard.severity || '')} • ${Number(hazard.confirmations || 0)} ${escapeHtml(localized.confirmationsLabel)}</div>
                        <div class="map-popup__actions">
                            <button type="button" class="btn btn-sm btn-primary" data-hazard-detail="${hazard.id}">
                                ${escapeHtml(localized.viewDetails)}
                            </button>
                        </div>
                    </div>
                `);
                marker.bindTooltip(escapeHtml(hazard.title || localized.locationSelected), { direction: 'top', opacity: 0.92 });

                hazardMarkers[hazard.id] = marker;
                marker.on('click', () => {
                    selectHazard(hazard.id);
                });
                marker.on('mouseover', () => marker.setStyle({ radius: 12, weight: 3 }));
                marker.on('mouseout', () => marker.setStyle({ radius: 10, weight: 2 }));
            });

            clusters = buildClusters(hazards, 320);
            if (!selectedHazardId) {
                const topCluster = clusters.slice().sort((left, right) => right.hazards.length - left.hazards.length)[0];
                if (topCluster) {
                    setInsightContent(buildFallbackInsight(topCluster), false);
                }
            }

            applyFilters();
        }

        function updateStatistics(hazards) {
            if (elements.totalHazardsCount) {
                elements.totalHazardsCount.textContent = String(hazards.length);
            }

            if (elements.criticalCount) {
                elements.criticalCount.textContent = String(hazards.filter((hazard) => hazard.severity === 'Critical').length);
            }

            if (elements.highCount) {
                elements.highCount.textContent = String(hazards.filter((hazard) => hazard.severity === 'High').length);
            }
        }

        function selectHazard(hazardId) {
            if (!hazardId) {
                return;
            }

            selectedHazardId = String(hazardId);

            document.querySelectorAll('[data-hazard-item]').forEach((item) => {
                const trigger = item.querySelector('[data-select-hazard]');
                const isActive = trigger?.getAttribute('data-select-hazard') === selectedHazardId;
                item.classList.toggle('is-active', Boolean(isActive));
            });

            if (hazardMarkers[selectedHazardId]) {
                const marker = hazardMarkers[selectedHazardId];
                map.setView(marker.getLatLng(), Math.max(map.getZoom(), 15));
                marker.openPopup();
                marker.setStyle({ radius: 13, weight: 3 });
                window.setTimeout(() => marker.setStyle({ radius: 10, weight: 2 }), 220);
            }

            const cluster = findClusterByHazardId(selectedHazardId);
            if (cluster) {
                streamInsight(cluster);
            }
        }

        function openDeepLinkedHazard() {
            const hazardId = new URLSearchParams(window.location.search).get('hazardId');
            if (hazardId) {
                showHazardDetail(hazardId);
            }
        }

        async function showHazardDetail(hazardId) {
            try {
                const response = await fetch(`/api/safety/${hazardId}`);
                const data = await response.json();

                if (!data.success) {
                    showHazardNotice(localized.failedToLoadHazards, 'danger');
                    return;
                }

                const hazard = data.data;
                window._currentHazard = hazard;
                selectedHazardId = String(hazardId);

                if (elements.hazardModalTitle) {
                    elements.hazardModalTitle.textContent = hazard.title;
                }

                if (elements.hazardDetailContent) {
                    elements.hazardDetailContent.innerHTML = `
                        <div class="hazard-detail">
                            <div class="hazard-detail__intro">
                                <span class="badge bg-${getSeverityBadgeClass(hazard.severity)}">${escapeHtml(hazard.severity)}</span>
                                <span class="badge bg-light text-dark">${escapeHtml(hazard.type)}</span>
                            </div>
                            <div class="detail-row">
                                <label>${escapeHtml(localized.descriptionLabel)}</label>
                                <p>${escapeHtml(hazard.description || '')}</p>
                            </div>
                            <div class="detail-row">
                                <label>${escapeHtml(localized.locationLabel)}</label>
                                <span>${escapeHtml(hazard.address || '')}</span>
                            </div>
                            <div class="detail-row">
                                <label>${escapeHtml(localized.confirmationsLabel)}</label>
                                <span>${Number(hazard.confirmations || 0)}</span>
                            </div>
                            <div class="detail-row">
                                <label>${escapeHtml(localized.reportedLabel)}</label>
                                <span>${escapeHtml(formatDateTime(hazard.createdAt))}</span>
                            </div>
                            <div class="detail-row">
                                <label>${escapeHtml(localized.latitudeLabel)} / ${escapeHtml(localized.longitudeLabel)}</label>
                                <span>${Number(hazard.latitude || 0).toFixed(5)}, ${Number(hazard.longitude || 0).toFixed(5)}</span>
                            </div>
                        </div>
                    `;
                }

                const canEdit = Boolean(hazard.canEdit);
                const canDelete = Boolean(hazard.canDelete);
                const canRestore = Boolean(hazard.canRestore);

                elements.editBtn?.classList.toggle('d-none', !canEdit);
                elements.deleteBtn?.classList.toggle('d-none', !canDelete);
                elements.restoreBtn?.classList.toggle('d-none', !canRestore);

                bootstrap.Modal.getOrCreateInstance(document.getElementById('hazardDetailModal')).show();
            } catch {
                showHazardNotice(localized.failedToLoadHazards, 'danger');
            }
        }

        async function confirmHazard() {
            if (!selectedHazardId) {
                return;
            }

            try {
                const response = await fetch(`/api/safety/${selectedHazardId}/confirm`, {
                    method: 'POST'
                });

                if (!response.ok) {
                    showHazardNotice(localized.errorUpdatingHazard, 'danger');
                    return;
                }

                showHazardNotice(localized.hazardConfirmed);
                bootstrap.Modal.getInstance(document.getElementById('hazardDetailModal'))?.hide();
                await loadNearbyHazards();
            } catch {
                showHazardNotice(localized.errorUpdatingHazard, 'danger');
            }
        }

        async function submitQuickReport() {
            const type = document.getElementById('reportType')?.value;
            const severity = document.getElementById('reportSeverity')?.value;
            const title = document.getElementById('reportTitle')?.value;
            const description = document.getElementById('reportDescription')?.value;

            if (!type || !severity || !title || !description) {
                showHazardNotice(localized.fillAllRequiredFields, 'danger');
                return;
            }

            if (reportLocationLat === null || reportLocationLng === null) {
                showHazardNotice(localized.pleaseSelectLocation, 'danger');
                return;
            }

            try {
                const response = await fetch('/api/safety/report', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        cityId: currentCity,
                        type,
                        severity,
                        title,
                        description,
                        latitude: reportLocationLat,
                        longitude: reportLocationLng,
                        address: reportLocationAddress
                    })
                });

                const responseData = await response.json();
                if (response.ok || response.status === 201) {
                    showHazardNotice(localized.quickReportThanks);
                    bootstrap.Modal.getInstance(document.getElementById('reportHazardModal'))?.hide();
                    document.getElementById('quickReportForm')?.reset();
                    reportLocationLat = null;
                    reportLocationLng = null;
                    reportLocationAddress = '';

                    if (elements.reportLocationDisplay) {
                        elements.reportLocationDisplay.textContent = localized.clickOnMapToSelectLocation;
                    }

                    updateLocationField('', false);
                    loadNearbyHazards();
                    return;
                }

                const errorMsg = responseData?.message || localized.errorSubmittingReport;
                showHazardNotice(errorMsg, 'danger');
            } catch (error) {
                showHazardNotice(`${localized.errorSubmittingReport}: ${error.message}`, 'danger');
            }
        }

        function applyFilters() {
            const searchTerm = (elements.hazardFilter?.value || '').trim().toLowerCase();
            const severity = elements.severityFilter?.value || '';
            const type = elements.typeFilter?.value || '';

            document.querySelectorAll('[data-hazard-item]').forEach((item) => {
                const matchesSearch = !searchTerm || item.textContent.toLowerCase().includes(searchTerm);
                const matchesSeverity = !severity || item.classList.contains(`severity-${severity.toLowerCase()}`);
                const itemType = (item.getAttribute('data-type') || '').toLowerCase();
                const matchesType = !type || itemType.includes(type.toLowerCase());
                item.hidden = !(matchesSearch && matchesSeverity && matchesType);
            });
        }

        function openEditModal() {
            const hazard = window._currentHazard;
            if (!hazard) {
                return;
            }

            document.getElementById('editHazardId').value = hazard.id;
            document.getElementById('editType').value = hazard.type || '';
            document.getElementById('editSeverity').value = hazard.severity || '';
            document.getElementById('editTitle').value = hazard.title || '';
            document.getElementById('editDescription').value = hazard.description || '';
            document.getElementById('editAddress').value = hazard.address || '';
            document.getElementById('editLatitude').value = hazard.latitude || '';
            document.getElementById('editLongitude').value = hazard.longitude || '';

            bootstrap.Modal.getOrCreateInstance(document.getElementById('editHazardModal')).show();
        }

        async function submitHazardUpdate() {
            const id = document.getElementById('editHazardId').value;
            const payload = {
                type: document.getElementById('editType').value || null,
                severity: document.getElementById('editSeverity').value || null,
                title: document.getElementById('editTitle').value || null,
                description: document.getElementById('editDescription').value || null,
                address: document.getElementById('editAddress').value || null,
                latitude: parseFloat(document.getElementById('editLatitude').value) || null,
                longitude: parseFloat(document.getElementById('editLongitude').value) || null
            };

            try {
                const response = await fetch(`/api/safety/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const result = await response.json();
                if (!response.ok) {
                    showHazardNotice(result?.message || localized.errorUpdatingHazard, 'danger');
                    return;
                }

                showHazardNotice(localized.hazardUpdated || localized.hazardUpdatedShort);
                bootstrap.Modal.getInstance(document.getElementById('editHazardModal'))?.hide();
                bootstrap.Modal.getInstance(document.getElementById('hazardDetailModal'))?.hide();
                loadNearbyHazards();
            } catch {
                showHazardNotice(localized.errorUpdatingHazard, 'danger');
            }
        }

        async function deleteHazard() {
            if (!selectedHazardId || !window.confirm(localized.confirmDeleteHazard)) {
                return;
            }

            try {
                const response = await fetch(`/api/safety/${selectedHazardId}`, { method: 'DELETE' });
                const result = await response.json();
                if (!response.ok) {
                    showHazardNotice(result?.message || localized.errorDeletingHazard, 'danger');
                    return;
                }

                showHazardNotice(localized.hazardDeleted || localized.hazardDeletedShort);
                bootstrap.Modal.getInstance(document.getElementById('hazardDetailModal'))?.hide();
                loadNearbyHazards();
            } catch {
                showHazardNotice(localized.errorDeletingHazard, 'danger');
            }
        }

        async function restoreHazard() {
            if (!selectedHazardId || !window.confirm(localized.confirmRestoreHazard)) {
                return;
            }

            try {
                const response = await fetch(`/api/safety/${selectedHazardId}/restore`, { method: 'POST' });
                const result = await response.json();
                if (!response.ok) {
                    showHazardNotice(result?.message || localized.errorRestoringHazard, 'danger');
                    return;
                }

                showHazardNotice(localized.hazardRestored || localized.hazardRestoredShort);
                bootstrap.Modal.getInstance(document.getElementById('hazardDetailModal'))?.hide();
                loadNearbyHazards();
            } catch {
                showHazardNotice(localized.errorRestoringHazard, 'danger');
            }
        }

        function findClusterByHazardId(hazardId) {
            return clusters.find((cluster) => cluster.hazards.some((hazard) => String(hazard.id) === String(hazardId))) || null;
        }

        function setInsightLoading() {
            if (!elements.insightContent || !elements.insightBadge) {
                return;
            }

            elements.insightBadge.textContent = localized.insightAiLabel;
            elements.insightBadge.classList.remove('is-fallback');
            elements.insightContent.innerHTML = `
                <div class="app-inline-state app-inline-state--loading" role="status">
                    <span class="app-skeleton" style="width: 1rem; height: 1rem;"></span>
                    <span>${escapeHtml(localized.insightLoadingLabel)}</span>
                </div>
            `;
        }

        function setInsightContent(content, aiGenerated) {
            if (!elements.insightContent || !elements.insightBadge) {
                return;
            }

            elements.insightBadge.textContent = aiGenerated ? localized.insightAiLabel : localized.insightFallbackLabel;
            elements.insightBadge.classList.toggle('is-fallback', !aiGenerated);
            elements.insightContent.textContent = content;
        }

        function resetInsight() {
            if (!elements.insightContent || !elements.insightBadge) {
                return;
            }

            elements.insightBadge.textContent = localized.insightAiLabel;
            elements.insightBadge.classList.remove('is-fallback');
            elements.insightContent.textContent = insightPlaceholder;
        }

        async function streamInsight(cluster) {
            if (!elements.insightContent) {
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
                setInsightContent(buildFallbackInsight(cluster), false);
                if (typeof window.FixItNotify === 'function') {
                    window.FixItNotify(localized.insightErrorLabel, 'warning');
                }
            }
        }

        function getSeverityBadgeClass(severity) {
            const classes = {
                Critical: 'danger',
                High: 'warning',
                Medium: 'info',
                Low: 'success'
            };
            return classes[severity] || 'secondary';
        }

        function showHazardNotice(message, variant = 'success') {
            if (typeof window.FixItNotify === 'function') {
                window.FixItNotify(message, variant === 'danger' ? 'error' : 'success');
                return;
            }

            const toast = document.getElementById('hazardToast');
            const body = document.getElementById('hazardToastBody');
            if (!toast || !body) {
                return;
            }

            body.textContent = message;
            toast.classList.remove('is-danger', 'is-success');
            toast.classList.add(variant === 'danger' ? 'is-danger' : 'is-success');
            hazardToast?.show();
        }

        function formatDateTime(value) {
            const date = new Date(value);
            return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
        }

        function escapeHtml(value) {
            return String(value)
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;')
                .replaceAll("'", '&#39;');
        }

        function escapeAttribute(value) {
            return escapeHtml(value);
        }
    });
})();
