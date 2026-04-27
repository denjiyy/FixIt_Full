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
        const configElement = document.getElementById('settingsPrivacyConfig');
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
        const config = parseConfig();
        if (!config) {
            return;
        }

        const apiBase = '/api/safety';
        const antiForgeryToken = config.antiForgeryToken || '';

        const severitySelect = document.getElementById('severityThreshold');
        if (severitySelect && config.severityThreshold) {
            severitySelect.value = config.severityThreshold;
        }

        document.addEventListener('change', (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const action = target.getAttribute('data-privacy-action');
            if (!action) {
                return;
            }

            if (action === 'toggle-anonymous' && target instanceof HTMLInputElement) {
                toggleAnonymousReporting(target.checked, target);
                return;
            }

            if (action === 'alert-pref' && target instanceof HTMLInputElement) {
                const preference = target.getAttribute('data-pref-key');
                if (!preference) {
                    return;
                }

                saveAlertPreference(preference, target.checked);
                return;
            }

            if (action === 'alert-radius' && target instanceof HTMLInputElement) {
                const value = target.value;
                updateAlertRadius(value);
                return;
            }

            if (action === 'severity-threshold' && target instanceof HTMLSelectElement) {
                saveSeverityThreshold(target.value);
                return;
            }

            if (action === 'profile-visibility' && target instanceof HTMLInputElement) {
                const visibility = target.value;
                saveProfileVisibility(visibility);
            }
        });

        async function toggleAnonymousReporting(enabled, checkbox) {
            try {
                const response = await fetch(`${apiBase}/anonymous-reporting/toggle`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': antiForgeryToken
                    },
                    credentials: 'include',
                    body: JSON.stringify({ enabled })
                });

                if (response.ok) {
                    showToast(`Anonymous reporting ${enabled ? 'enabled' : 'disabled'} successfully`);
                    window.setTimeout(() => window.location.reload(), 1000);
                    return;
                }

                showToast('Failed to update settings', 'error');
                checkbox.checked = !enabled;
            } catch {
                showToast('Error updating settings', 'error');
                checkbox.checked = !enabled;
            }
        }

        function updateAlertRadius(value) {
            const radiusDisplay = document.getElementById('radiusDisplay');
            if (radiusDisplay) {
                radiusDisplay.textContent = `${value} km`;
            }

            saveAlertPreference('alertRadius', value);
        }

        async function saveAlertPreference(preference, value) {
            try {
                const response = await fetch(`${apiBase}/alert-preferences`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ [preference]: value })
                });

                if (response.ok) {
                    showToast('Alert preference saved');
                }
            } catch {
                showToast('Unable to save alert preference right now.', 'error');
            }
        }

        async function saveSeverityThreshold(threshold) {
            try {
                const response = await fetch(`${apiBase}/alert-preferences/severity`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ threshold })
                });

                if (response.ok) {
                    showToast('Severity threshold updated');
                }
            } catch {
                showToast('Unable to update severity threshold right now.', 'error');
            }
        }

        async function saveProfileVisibility(visibility) {
            try {
                const response = await fetch('/api/users/profile-visibility', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ visibility })
                });

                if (response.ok) {
                    showToast(`Profile visibility changed to ${visibility}`);
                }
            } catch {
                showToast('Unable to update profile visibility right now.', 'error');
            }
        }

        function showToast(message, type = 'success') {
            if (typeof window.FixItNotify === 'function') {
                window.FixItNotify(message, type === 'error' ? 'error' : 'success');
                return;
            }

            const toastEl = document.getElementById('settingsToast');
            const messageEl = document.getElementById('toastMessage');
            if (!toastEl || !messageEl) {
                return;
            }

            const headerEl = toastEl.querySelector('.toast-header');
            if (!headerEl) {
                return;
            }

            messageEl.textContent = message;

            if (type === 'error') {
                headerEl.className = 'toast-header bg-danger text-white';
                headerEl.innerHTML = '<i class="bi bi-exclamation-circle me-2"></i><strong class="me-auto">Error</strong><button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>';
            } else {
                headerEl.className = 'toast-header bg-success text-white';
                headerEl.innerHTML = '<i class="bi bi-check-circle me-2"></i><strong class="me-auto">Success</strong><button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>';
            }

            bootstrap.Toast.getOrCreateInstance(toastEl).show();
        }
    });
})();
