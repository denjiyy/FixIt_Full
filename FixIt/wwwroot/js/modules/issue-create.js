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
        const root = document.getElementById('issueCreateRoot');
        if (!root) return;

        const form = document.getElementById('issueForm');
        const liveRegion = document.getElementById('createLiveRegion');
        const titleInput = document.getElementById('issueTitle');
        const descriptionInput = document.getElementById('issueDescription');
        const tagInput = document.getElementById('tagInput');
        const addressInput = document.getElementById('issueAddress');
        const citySelect = document.getElementById('baseCity');
        const latitudeInput = document.getElementById('latitude');
        const longitudeInput = document.getElementById('longitude');
        const coordsDisplay = document.getElementById('coordsDisplay');
        const uploadZone = document.getElementById('uploadZone');
        const mediaInput = document.getElementById('mediaInput');
        const mediaPreviewGrid = document.getElementById('mediaPreviewGrid');
        const submitButton = document.getElementById('baseSubmitBtn');
        const suggestedTags = document.getElementById('suggestedTags');
        const categorySelect = document.getElementById('issueCategory');
        const prioritySelect = document.getElementById('issuePriority');
        const departmentInput = document.getElementById('issueDepartment');
        const aiSuggestionPanel = document.getElementById('aiIssueSuggestionPanel');
        const aiSuggestionStatus = document.getElementById('aiSuggestionStatus');
        const aiSuggestionSourceBadge = document.getElementById('aiSuggestionSourceBadge');
        const aiSuggestionConfidenceWrap = document.getElementById('aiSuggestionConfidenceWrap');
        const aiSuggestionConfidenceText = document.getElementById('aiSuggestionConfidenceText');
        const aiSuggestionConfidenceBar = document.getElementById('aiSuggestionConfidenceBar');
        const aiSuggestionSkeleton = document.getElementById('aiSuggestionSkeleton');

        if (!form || !titleInput || !descriptionInput || !tagInput || !addressInput || !citySelect ||
            !latitudeInput || !longitudeInput || !coordsDisplay || !uploadZone || !mediaInput ||
            !mediaPreviewGrid || !submitButton || !suggestedTags) {
            return;
        }

        const defaultLat = Number.parseFloat(root.dataset.defaultLat || '42.6977');
        const defaultLng = Number.parseFloat(root.dataset.defaultLng || '23.3219');
        const messages = {
            titleTooShort: root.dataset.titleTooShort || 'Title is too short.',
            descriptionTooShort: root.dataset.descriptionTooShort || 'Description is too short.',
            cityRequired: root.dataset.cityRequired || 'Please select a city.',
            invalidFileType: root.dataset.invalidFileType || 'Unsupported file type.',
            imageLimit: root.dataset.imageLimit || 'Image exceeds size limit.',
            videoLimit: root.dataset.videoLimit || 'Video exceeds size limit.',
            onlyFirstFiles: root.dataset.onlyFirstFiles || 'Only the first 10 files will be uploaded.',
            noSuggestions: root.dataset.noSuggestions || 'No suggestions available yet.',
            loadingSuggestions: root.dataset.loadingSuggestions || 'Loading suggestions...',
            suggestionError: root.dataset.suggestionError || 'Could not load suggestions right now.',
            notStarted: root.dataset.notStarted || 'Not started',
            locationPending: root.dataset.locationPending || 'Location pending',
            resolvingAddress: root.dataset.resolvingAddress || 'Resolving address...',
            geocodingError: root.dataset.geocodingError || 'Unable to resolve address right now.',
            geocodingEmpty: root.dataset.geocodingEmpty || 'No matching address found.',
            noMedia: root.dataset.noMedia || 'No media selected',
            filesSelectedLabel: root.dataset.filesSelectedLabel || 'files selected',
            submitLabel: root.dataset.submitLabel || 'Submit Report',
            submittingLabel: root.dataset.submittingLabel || 'Submitting...',
            aiSuggestionLoading: root.dataset.aiSuggestionLoading || 'Analyzing issue draft...',
            aiSuggestionReady: root.dataset.aiSuggestionReady || 'AI suggestions are ready. You can accept or override them.',
            aiSuggestionUnavailable: root.dataset.aiSuggestionUnavailable || 'AI suggestions are temporarily unavailable.',
            aiSuggestionIdle: root.dataset.aiSuggestionIdle || 'AI suggestions will appear after you add enough details.',
            aiGeneratedLabel: root.dataset.aiGeneratedLabel || 'AI-generated',
            fallbackLabel: root.dataset.fallbackLabel || 'Rule-based suggestion',
            confidenceLabel: root.dataset.confidenceLabel || 'Confidence',
            applySuggestionLabel: root.dataset.applySuggestionLabel || 'Suggestions applied'
        };

        const notify = (message, tone = 'info') => {
            if (typeof window.FixItNotify === 'function') {
                window.FixItNotify(message, tone);
            }
        };

        let map = null;
        let marker = null;
        let tagSuggestionTimer = null;
        let aiSuggestionTimer = null;
        let aiRequestSequence = 0;
        let lastGeocodeRequest = 0;
        let hasPinnedLocation = false;
        const geocodeCache = new Map();
        const geocodeThrottleMs = 500;
        const aiFieldOverrides = {
            category: false,
            priority: false,
            department: false
        };

        function hasCoordinates() {
            const lat = Number.parseFloat(latitudeInput.value || '');
            const lng = Number.parseFloat(longitudeInput.value || '');
            return Number.isFinite(lat) && Number.isFinite(lng);
        }

        function updateLiveRegion(message) {
            if (!liveRegion) return;
            liveRegion.textContent = message;
        }

        function updateCounter(input, counterId, maxLength) {
            const counter = document.getElementById(counterId);
            if (!counter) return;

            const update = () => {
                const length = input.value.length;
                counter.textContent = `${length} / ${maxLength}`;
                counter.classList.remove('near-limit', 'at-limit');
                if (length >= maxLength) {
                    counter.classList.add('at-limit');
                } else if (length >= maxLength * 0.85) {
                    counter.classList.add('near-limit');
                }
            };

            input.addEventListener('input', update);
            update();
        }

        function setValidation(id, message) {
            const element = document.getElementById(id);
            if (!element) return;
            element.textContent = message || '';
        }

        function clearClientValidation() {
            ['titleValidation', 'descriptionValidation', 'cityValidation'].forEach((id) => setValidation(id, ''));
        }

        function normalizeTags(value) {
            return Array.from(new Set(
                (value || '')
                    .split(',')
                    .map((part) => part.trim().toLowerCase())
                    .filter(Boolean)
            ));
        }

        function renderSuggestedTags(tags) {
            suggestedTags.innerHTML = '';

            if (!tags || tags.length === 0) {
                renderSuggestedTagsState(messages.noSuggestions);
                return;
            }

            tags.slice(0, 8).forEach((tag) => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'btn btn-sm btn-outline-primary';
                button.textContent = tag;
                button.addEventListener('click', () => {
                    const tagsValue = normalizeTags(tagInput.value);
                    if (!tagsValue.includes(tag.toLowerCase())) {
                        tagsValue.push(tag.toLowerCase());
                        tagInput.value = tagsValue.join(', ');
                        updateSummary();
                    }
                });
                suggestedTags.appendChild(button);
            });
        }

        function renderSuggestedTagsState(message, tone = 'hint') {
            suggestedTags.innerHTML = '';
            const status = document.createElement('small');
            status.className = `create-field__hint ${tone === 'error' ? 'text-danger' : ''}`.trim();
            status.textContent = message;
            suggestedTags.appendChild(status);
        }

        function requestTagSuggestions() {
            const title = titleInput.value.trim();
            const description = descriptionInput.value.trim();

            if (tagSuggestionTimer) {
                window.clearTimeout(tagSuggestionTimer);
            }

            if (title.length < 3 && description.length < 10) {
                renderSuggestedTags([]);
                return;
            }

            tagSuggestionTimer = window.setTimeout(async () => {
                renderSuggestedTagsState(messages.loadingSuggestions);
                try {
                    const response = await fetch('/api/analysis/suggest-tags', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ title, description })
                    });

                    if (!response.ok) {
                        renderSuggestedTagsState(messages.suggestionError, 'error');
                        updateLiveRegion(messages.suggestionError);
                        return;
                    }

                    const result = await response.json();
                    const suggestions = Array.isArray(result) ? result : [];
                    if (!suggestions.length) {
                        renderSuggestedTagsState(messages.noSuggestions);
                        return;
                    }

                    renderSuggestedTags(suggestions);
                } catch {
                    renderSuggestedTagsState(messages.suggestionError, 'error');
                    updateLiveRegion(messages.suggestionError);
                    notify(messages.suggestionError, 'error');
                }
            }, 350);
        }

        function setAiSkeleton(isLoading) {
            if (aiSuggestionPanel) {
                aiSuggestionPanel.classList.toggle('is-loading', isLoading);
            }
            if (aiSuggestionSkeleton) {
                aiSuggestionSkeleton.classList.toggle('d-none', !isLoading);
            }
        }

        function setAiConfidence(confidence) {
            if (!aiSuggestionConfidenceWrap || !aiSuggestionConfidenceText || !aiSuggestionConfidenceBar) {
                return;
            }

            if (!Number.isFinite(confidence)) {
                aiSuggestionConfidenceWrap.classList.add('d-none');
                return;
            }

            const normalized = Math.max(0, Math.min(100, Math.round(confidence)));
            aiSuggestionConfidenceWrap.classList.remove('d-none');
            aiSuggestionConfidenceText.textContent = `${normalized}%`;
            aiSuggestionConfidenceBar.style.width = `${normalized}%`;
        }

        function setAiStatus(message, tone = 'muted') {
            if (!aiSuggestionStatus) return;
            aiSuggestionStatus.textContent = message;
            aiSuggestionStatus.classList.remove('text-danger', 'text-success');
            if (tone === 'error') {
                aiSuggestionStatus.classList.add('text-danger');
            }
            if (tone === 'success') {
                aiSuggestionStatus.classList.add('text-success');
            }
        }

        function setAiSourceBadge(aiGenerated) {
            if (!aiSuggestionSourceBadge) return;

            aiSuggestionSourceBadge.textContent = aiGenerated ? messages.aiGeneratedLabel : messages.fallbackLabel;
            aiSuggestionSourceBadge.classList.toggle('is-fallback', !aiGenerated);
        }

        function normalizeOptionValue(value) {
            if (typeof value !== 'string') return '';
            return value.trim();
        }

        function applyDraftSuggestion(result) {
            const category = normalizeOptionValue(result?.category);
            const priority = normalizeOptionValue(result?.priority);
            const department = normalizeOptionValue(result?.department);
            const confidence = Number.parseFloat(result?.confidence);
            const aiGenerated = result?.aiGenerated === true;

            if (categorySelect && category && !aiFieldOverrides.category && Array.from(categorySelect.options).some((option) => option.value === category)) {
                categorySelect.value = category;
            }

            if (prioritySelect && priority && !aiFieldOverrides.priority && Array.from(prioritySelect.options).some((option) => option.value === priority)) {
                prioritySelect.value = priority;
            }

            if (departmentInput && department && !aiFieldOverrides.department) {
                departmentInput.value = department;
            }

            setAiSourceBadge(aiGenerated);
            setAiConfidence(confidence);
            setAiStatus(messages.aiSuggestionReady, 'success');
            updateLiveRegion(messages.applySuggestionLabel);
            updateSummary();
        }

        function shouldRequestDraftSuggestion() {
            return titleInput.value.trim().length >= 3 || descriptionInput.value.trim().length >= 10;
        }

        function getSuggestionImage() {
            const files = Array.from(mediaInput.files || []);
            const image = files.find((file) => file.type.startsWith('image/') && file.size <= 2 * 1024 * 1024);
            return image || null;
        }

        function queueDraftSuggestion() {
            if (!categorySelect && !prioritySelect && !departmentInput) {
                return;
            }

            if (aiSuggestionTimer) {
                window.clearTimeout(aiSuggestionTimer);
            }

            if (!shouldRequestDraftSuggestion()) {
                setAiSkeleton(false);
                setAiStatus(messages.aiSuggestionIdle);
                setAiConfidence(Number.NaN);
                return;
            }

            aiSuggestionTimer = window.setTimeout(() => {
                requestDraftSuggestion();
            }, 500);
        }

        async function requestDraftSuggestion() {
            const sequence = ++aiRequestSequence;
            const formData = new FormData();
            formData.append('title', titleInput.value.trim());
            formData.append('description', descriptionInput.value.trim());
            if (citySelect.value) {
                formData.append('cityId', citySelect.value);
            }

            const image = getSuggestionImage();
            if (image) {
                formData.append('image', image);
            }

            setAiSkeleton(true);
            setAiStatus(messages.aiSuggestionLoading);

            try {
                const response = await fetch('/api/analysis/issue-draft-suggestions', {
                    method: 'POST',
                    body: formData
                });

                if (sequence !== aiRequestSequence) {
                    return;
                }

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const result = await response.json();
                applyDraftSuggestion(result);
            } catch {
                if (sequence !== aiRequestSequence) {
                    return;
                }

                setAiSourceBadge(false);
                setAiStatus(messages.aiSuggestionUnavailable, 'error');
                setAiConfidence(Number.NaN);
                updateLiveRegion(messages.aiSuggestionUnavailable);
            } finally {
                if (sequence === aiRequestSequence) {
                    setAiSkeleton(false);
                }
            }
        }

        function updateSummary() {
            const summaryTitle = document.getElementById('summaryTitleValue');
            const summaryCity = document.getElementById('summaryCityValue');
            const summaryLocation = document.getElementById('summaryLocationValue');
            const summaryMedia = document.getElementById('summaryMediaValue');

            if (summaryTitle) {
                summaryTitle.textContent = titleInput.value.trim() || messages.notStarted;
            }

            if (summaryCity) {
                const selectedOption = citySelect.selectedOptions[0];
                summaryCity.textContent = selectedOption?.value ? selectedOption.text : messages.notStarted;
            }

            if (summaryLocation) {
                const lat = Number.parseFloat(latitudeInput.value || '');
                const lng = Number.parseFloat(longitudeInput.value || '');
                summaryLocation.textContent = !hasPinnedLocation || Number.isNaN(lat) || Number.isNaN(lng)
                    ? messages.locationPending
                    : `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
            }

            if (summaryMedia) {
                const fileCount = mediaInput.files?.length || 0;
                summaryMedia.textContent = fileCount > 0
                    ? `${fileCount} ${messages.filesSelectedLabel}`
                    : messages.noMedia;
            }

            updateProgress();
        }

        function updateProgress() {
            const detailsDone = titleInput.value.trim().length >= 3 && descriptionInput.value.trim().length >= 10;
            const locationDone = Boolean(citySelect.value && hasPinnedLocation && hasCoordinates());
            const mediaReady = mediaInput.files?.length > 0;

            const topStepper = {
                details: document.querySelector('[data-step-item="details"]'),
                location: document.querySelector('[data-step-item="location"]'),
                media: document.querySelector('[data-step-item="media"]')
            };

            const sideProgress = {
                details: document.querySelector('[data-progress-item="details"]'),
                location: document.querySelector('[data-progress-item="location"]'),
                media: document.querySelector('[data-progress-item="media"]')
            };

            const applyState = (element, state) => {
                if (!element) return;
                element.classList.remove('is-active', 'is-complete');
                if (state) {
                    element.classList.add(state);
                }
            };

            applyState(topStepper.details, detailsDone ? 'is-complete' : 'is-active');
            applyState(topStepper.location, detailsDone ? (locationDone ? 'is-complete' : 'is-active') : '');
            applyState(topStepper.media, locationDone ? (mediaReady ? 'is-complete' : 'is-active') : '');

            applyState(sideProgress.details, detailsDone ? 'is-complete' : 'is-active');
            applyState(sideProgress.location, locationDone ? 'is-complete' : (detailsDone ? 'is-active' : ''));
            applyState(sideProgress.media, mediaReady ? 'is-complete' : (locationDone ? 'is-active' : ''));
        }

        function setCoordinates(lat, lng) {
            latitudeInput.value = String(lat);
            longitudeInput.value = String(lng);
            hasPinnedLocation = true;
            coordsDisplay.textContent = `${lat.toFixed(5)}, ${lng.toFixed(5)}`;

            if (!map) return;

            if (marker) {
                map.removeLayer(marker);
            }

            marker = window.L.marker([lat, lng]).addTo(map);
            updateSummary();
            reverseGeocode(lat, lng);
        }

        function updateLocationField(value, isLoading) {
            addressInput.classList.toggle('is-loading', isLoading);
            if (isLoading) {
                if (!addressInput.value.trim()) {
                    addressInput.value = value;
                }
                return;
            }

            if (typeof value === 'string') {
                addressInput.value = value;
            }
        }

        async function reverseGeocode(lat, lng) {
            const cacheKey = `${lat.toFixed(4)},${lng.toFixed(4)}`;
            if (geocodeCache.has(cacheKey)) {
                applyGeocode(geocodeCache.get(cacheKey));
                return;
            }

            const now = Date.now();
            if (now - lastGeocodeRequest < geocodeThrottleMs) return;
            lastGeocodeRequest = now;
            updateLiveRegion(messages.resolvingAddress);
            updateLocationField(messages.resolvingAddress, true);

            try {
                const response = await fetch(`/api/geocoding/reverse?latitude=${lat}&longitude=${lng}`, {
                    headers: { Accept: 'application/json' }
                });

                if (!response.ok) {
                    updateLiveRegion(messages.geocodingError);
                    updateLocationField('', false);
                    return;
                }

                const result = await response.json();
                geocodeCache.set(cacheKey, result);
                applyGeocode(result);
                if (!result?.address) {
                    updateLiveRegion(messages.geocodingEmpty);
                }
            } catch {
                updateLiveRegion(messages.geocodingError);
                notify(messages.geocodingError, 'warning');
            } finally {
                updateLocationField(addressInput.value || '', false);
            }
        }

        function applyGeocode(result) {
            if (!result) return;
            if (result.address) {
                addressInput.value = result.address;
            }

            if (result.cityId) {
                citySelect.value = result.cityId;
            }

            updateSummary();
            queueDraftSuggestion();
        }

        function initMap() {
            if (!window.L) return;

            const existingLat = Number.parseFloat(latitudeInput.value || '');
            const existingLng = Number.parseFloat(longitudeInput.value || '');
            const hasExistingCoordinates = Number.isFinite(existingLat) && Number.isFinite(existingLng);
            const initialLat = hasExistingCoordinates ? existingLat : defaultLat;
            const initialLng = hasExistingCoordinates ? existingLng : defaultLng;

            map = window.L.map('map').setView([initialLat, initialLng], 13);
            window.L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors'
            }).addTo(map);

            map.on('click', (event) => {
                setCoordinates(event.latlng.lat, event.latlng.lng);
            });

            if (hasExistingCoordinates) {
                setCoordinates(existingLat, existingLng);
            }
        }

        function renderPreviews() {
            mediaPreviewGrid.innerHTML = '';

            const dataTransfer = new DataTransfer();
            const selectedFiles = Array.from(mediaInput.files || []).slice(0, 10);
            const hadTrimmedFiles = (mediaInput.files?.length || 0) > 10;

            selectedFiles.forEach((file) => {
                const isImage = file.type.startsWith('image/');
                const isVideo = file.type.startsWith('video/');
                const maxSize = isImage ? 5 * 1024 * 1024 : 100 * 1024 * 1024;

                if (!isImage && !isVideo) {
                    updateLiveRegion(messages.invalidFileType);
                    return;
                }

                if (file.size > maxSize) {
                    updateLiveRegion(isImage ? messages.imageLimit : messages.videoLimit);
                    return;
                }

                dataTransfer.items.add(file);
            });

            if (hadTrimmedFiles) {
                updateLiveRegion(messages.onlyFirstFiles);
            }

            mediaInput.files = dataTransfer.files;

            Array.from(mediaInput.files).forEach((file, index) => {
                const isImage = file.type.startsWith('image/');
                const item = document.createElement('div');
                item.className = 'media-preview-item';

                const removeButton = document.createElement('button');
                removeButton.type = 'button';
                removeButton.className = 'media-preview-remove';
                removeButton.innerHTML = '<i class="bi bi-x"></i>';
                removeButton.addEventListener('click', () => removeMedia(index));

                const badge = document.createElement('span');
                badge.className = 'media-preview-badge';
                badge.textContent = formatFileSize(file.size);

                if (isImage) {
                    const image = document.createElement('img');
                    image.alt = file.name;
                    image.src = URL.createObjectURL(file);
                    image.addEventListener('load', () => URL.revokeObjectURL(image.src), { once: true });
                    item.appendChild(image);
                } else {
                    const placeholder = document.createElement('div');
                    placeholder.className = 'preview-video-placeholder';
                    placeholder.innerHTML = `<i class="bi bi-play-circle-fill"></i><span>${file.name.split('.').pop()?.toUpperCase() || file.type.split('/').pop()?.toUpperCase() || 'MP4'}</span>`;
                    item.appendChild(placeholder);
                }

                item.appendChild(removeButton);
                item.appendChild(badge);
                mediaPreviewGrid.appendChild(item);
            });

            updateSummary();
            queueDraftSuggestion();
        }

        function removeMedia(index) {
            const dataTransfer = new DataTransfer();
            Array.from(mediaInput.files || []).forEach((file, fileIndex) => {
                if (fileIndex !== index) {
                    dataTransfer.items.add(file);
                }
            });
            mediaInput.files = dataTransfer.files;
            renderPreviews();
        }

        function formatFileSize(bytes) {
            if (bytes < 1024) return `${bytes} B`;
            if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
            return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
        }

        function validateForm() {
            clearClientValidation();

            let isValid = true;

            if (titleInput.value.trim().length < 3) {
                setValidation('titleValidation', messages.titleTooShort);
                isValid = false;
            }

            if (descriptionInput.value.trim().length < 10) {
                setValidation('descriptionValidation', messages.descriptionTooShort);
                isValid = false;
            }

            if (!citySelect.value || !hasPinnedLocation || !hasCoordinates()) {
                setValidation('cityValidation', messages.cityRequired);
                isValid = false;
            }

            if (!isValid) {
                updateLiveRegion(messages.cityRequired);
            }

            return isValid;
        }

        uploadZone.addEventListener('click', () => mediaInput.click());
        uploadZone.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                mediaInput.click();
            }
        });

        uploadZone.addEventListener('dragover', (event) => {
            event.preventDefault();
            uploadZone.classList.add('is-dragover');
        });

        uploadZone.addEventListener('dragleave', () => {
            uploadZone.classList.remove('is-dragover');
        });

        uploadZone.addEventListener('drop', (event) => {
            event.preventDefault();
            uploadZone.classList.remove('is-dragover');

            const dataTransfer = new DataTransfer();
            Array.from(event.dataTransfer?.files || []).forEach((file) => dataTransfer.items.add(file));
            mediaInput.files = dataTransfer.files;
            renderPreviews();
        });

        mediaInput.addEventListener('change', renderPreviews);

        [titleInput, descriptionInput, tagInput, citySelect, addressInput].forEach((element) => {
            element.addEventListener('input', updateSummary);
            element.addEventListener('change', updateSummary);
        });

        titleInput.addEventListener('input', () => {
            requestTagSuggestions();
            queueDraftSuggestion();
        });

        descriptionInput.addEventListener('input', () => {
            requestTagSuggestions();
            queueDraftSuggestion();
        });

        citySelect.addEventListener('change', queueDraftSuggestion);

        if (categorySelect) {
            categorySelect.addEventListener('change', () => {
                aiFieldOverrides.category = true;
            });
        }

        if (prioritySelect) {
            prioritySelect.addEventListener('change', () => {
                aiFieldOverrides.priority = true;
            });
        }

        if (departmentInput) {
            departmentInput.addEventListener('input', () => {
                aiFieldOverrides.department = true;
            });
        }

        form.addEventListener('submit', (event) => {
            if (!validateForm()) {
                event.preventDefault();
                return;
            }

            submitButton.disabled = true;
            submitButton.innerHTML = `<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>${messages.submittingLabel}</span>`;
        });

        updateCounter(titleInput, 'titleCounter', 200);
        updateCounter(descriptionInput, 'descriptionCounter', 5000);
        renderSuggestedTags([]);
        initMap();
        updateSummary();
        renderPreviews();
        setAiSourceBadge(true);
        setAiStatus(messages.aiSuggestionIdle);
        setAiConfidence(Number.NaN);
    });
})();
