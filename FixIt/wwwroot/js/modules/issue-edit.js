(function () {
    'use strict';

    const onReady = window.FixItApp?.onReady || ((callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    });

    let map = null;
    let marker = null;
    const removedMediaIds = [];

    function wireCounter(inputId, outputId, maxLength) {
        const input = document.getElementById(inputId);
        const output = document.getElementById(outputId);
        if (!input || !output) return;

        const update = () => {
            output.textContent = String(input.value.length);
            output.parentElement?.classList.toggle('is-warning', input.value.length >= maxLength * 0.85);
        };

        input.addEventListener('input', update);
        update();
    }

    function initializeMap() {
        const mapElement = document.getElementById('map');
        if (!mapElement) return;

        const defaultLat = parseFloat(mapElement.dataset.defaultLat || '42.6977');
        const defaultLng = parseFloat(mapElement.dataset.defaultLng || '23.3219');

        // Initialize Leaflet map
        map = window.L.map('map').setView([defaultLat, defaultLng], 13);

        window.L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors',
            maxZoom: 19
        }).addTo(map);

        // Add marker at default location
        marker = window.L.marker([defaultLat, defaultLng]).addTo(map);
        updateCoordinateDisplay(defaultLat, defaultLng);

        // Handle map clicks to place marker
        map.on('click', function(e) {
            const lat = e.latlng.lat;
            const lng = e.latlng.lng;

            // Update marker position
            marker.setLatLng([lat, lng]);

            // Update input fields
            document.getElementById('Input_Latitude').value = lat.toFixed(6);
            document.getElementById('Input_Longitude').value = lng.toFixed(6);

            updateCoordinateDisplay(lat, lng);

            // Attempt reverse geocoding
            reverseGeocode(lat, lng);
        });

        // Handle coordinate input changes
        const latInput = document.getElementById('Input_Latitude');
        const lngInput = document.getElementById('Input_Longitude');
        
        if (latInput && lngInput) {
            const updateFromInputs = () => {
                const lat = parseFloat(latInput.value);
                const lng = parseFloat(lngInput.value);
                
                if (!isNaN(lat) && !isNaN(lng) && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180) {
                    marker.setLatLng([lat, lng]);
                    map.setView([lat, lng], map.getZoom());
                    updateCoordinateDisplay(lat, lng);
                }
            };

            latInput.addEventListener('change', updateFromInputs);
            lngInput.addEventListener('change', updateFromInputs);
        }
    }

    function updateCoordinateDisplay(lat, lng) {
        const coordsDisplay = document.getElementById('coordsDisplay');
        if (coordsDisplay) {
            coordsDisplay.textContent = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
        }
    }

    function reverseGeocode(lat, lng) {
        fetch(`/api/geocoding/reverse?latitude=${lat}&longitude=${lng}`)
            .then(response => response.json())
            .then(data => {
                if (data.address) {
                    const addressInput = document.getElementById('Input_Address');
                    if (addressInput && !addressInput.value) {
                        addressInput.value = data.address;
                    }
                }
            })
            .catch(error => console.warn('Reverse geocoding failed:', error));
    }

    function initializeMediaHandling() {
        const uploadZone = document.getElementById('editUploadZone');
        const fileInput = document.getElementById('NewMediaFiles');
        const previewContainer = document.getElementById('selectedFilesPreview');

        if (!uploadZone || !fileInput) return;

        // Handle drag and drop
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            uploadZone.addEventListener(eventName, preventDefaults, false);
        });

        function preventDefaults(e) {
            e.preventDefault();
            e.stopPropagation();
        }

        ['dragenter', 'dragover'].forEach(eventName => {
            uploadZone.addEventListener(eventName, () => {
                uploadZone.classList.add('dragover');
            });
        });

        ['dragleave', 'drop'].forEach(eventName => {
            uploadZone.addEventListener(eventName, () => {
                uploadZone.classList.remove('dragover');
            });
        });

        uploadZone.addEventListener('drop', (e) => {
            const dt = e.dataTransfer;
            const files = dt.files;
            fileInput.files = files;
            updateFilePreview();
        });

        uploadZone.addEventListener('click', () => fileInput.click());
        fileInput.addEventListener('change', updateFilePreview);

        function updateFilePreview() {
            previewContainer.innerHTML = '';
            if (fileInput.files.length === 0) return;

            const fileList = document.createElement('div');
            fileList.className = 'selected-files-list';

            for (let i = 0; i < fileInput.files.length; i++) {
                const file = fileInput.files[i];
                const fileItem = document.createElement('div');
                fileItem.className = 'file-item';
                fileItem.innerHTML = `
                    <i class="bi bi-file-earmark"></i>
                    <span>${file.name}</span>
                    <small>${(file.size / 1024 / 1024).toFixed(2)} MB</small>
                `;
                fileList.appendChild(fileItem);
            }

            previewContainer.appendChild(fileList);
        }
    }

    function initializeMediaRemoval() {
        const form = document.querySelector('form');
        if (!form) return;

        // Handle media item removal buttons
        document.addEventListener('click', (e) => {
            if (e.target.closest('.media-remove-btn')) {
                e.preventDefault();
                const btn = e.target.closest('.media-remove-btn');
                const mediaId = btn.dataset.mediaId;
                const mediaItem = btn.closest('.media-item');

                if (mediaId && mediaItem) {
                    // Add to removed list
                    removedMediaIds.push(mediaId);
                    // Hide the item
                    mediaItem.style.display = 'none';
                    mediaItem.classList.add('removed');
                }
            }
        });

        // Add hidden inputs for removed media IDs before form submission
        form.addEventListener('submit', (e) => {
            removedMediaIds.forEach(mediaId => {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'Input.MediaIdsToRemove';
                input.value = mediaId;
                form.appendChild(input);
            });
        });
    }

    onReady(() => {
        if (!document.getElementById('issueEditRoot')) return;
        
        // Initialize character counters
        wireCounter('Input_Title', 'charCount', 200);
        wireCounter('Input_Description', 'descCharCount', 5000);

        // Initialize map
        initializeMap();

        // Initialize media handling
        initializeMediaHandling();
        initializeMediaRemoval();
    });
})();
