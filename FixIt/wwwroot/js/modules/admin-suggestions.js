/**
 * Admin Suggestions Module
 * Handles fetching, displaying, and acting on AI suggestions for admin tasks
 */
const AdminSuggestions = (() => {
    'use strict';

    const messages = {
        loading: 'Loading suggestions...',
        empty: 'No suggestions available.',
        loadError: 'Unable to load suggestions right now.',
        actionError: 'Could not update suggestion status.'
    };
    const notify = (message, tone = 'info') => {
        if (typeof window.FixItNotify === 'function') {
            window.FixItNotify(message, tone);
        }
    };

    function setContainerState(containerId, state, message) {
        const container = document.getElementById(containerId);
        if (!container) return;

        if (state === 'loading') {
            container.innerHTML = `
                <div class=\"app-inline-state app-inline-state--loading\" role=\"status\">
                    <span class=\"app-skeleton\" style=\"width: 1.25rem; height: 1.25rem;\"></span>
                    <span>${message || messages.loading}</span>
                </div>
            `;
            return;
        }

        if (state === 'error') {
            container.innerHTML = `<div class="alert alert-danger">${message || messages.loadError}</div>`;
            return;
        }

        if (state === 'empty') {
            container.innerHTML = `<div class="alert alert-info">${message || messages.empty}</div>`;
        }
    }

    function showActionError(message) {
        const noticeId = 'adminSuggestionsActionNotice';
        let notice = document.getElementById(noticeId);
        if (!notice) {
            notice = document.createElement('div');
            notice.id = noticeId;
            notice.className = 'alert alert-danger my-3';
            const dashboardContainer = document.getElementById('dashboardSuggestions');
            if (dashboardContainer?.parentElement) {
                dashboardContainer.parentElement.insertBefore(notice, dashboardContainer);
            } else {
                document.body.prepend(notice);
            }
        }

        notice.textContent = message || messages.actionError;
        notify(notice.textContent, 'error');
    }

    /**
     * Fetch pending suggestions for dashboard
     */
    async function getPending(limit = 10) {
        const res = await fetch(`/api/suggestions/pending?limit=${limit}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return await res.json();
    }

    /**
     * Fetch suggestions for a specific entity (report, issue, user)
     */
    async function getForEntity(entityId, entityType) {
        const res = await fetch(`/api/suggestions/entity/${entityId}?entityType=${encodeURIComponent(entityType)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return await res.json();
    }

    /**
     * Generate suggestion for a report
     */
    async function generateReportSuggestion(reportId) {
        try {
            const res = await fetch(`/api/suggestions/report/${reportId}`, { method: 'POST' });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            if (res.status === 204) return null;
            return await res.json();
        } catch (err) {
            showActionError(messages.actionError);
            return null;
        }
    }

    /**
     * Generate suggestions for an issue
     */
    async function generateIssueSuggestions(issueId) {
        try {
            const res = await fetch(`/api/suggestions/issue/${issueId}`, { method: 'POST' });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (err) {
            showActionError(messages.actionError);
            return [];
        }
    }

    /**
     * Generate suggestion for user moderation
     */
    async function generateUserModerationSuggestion(userId) {
        try {
            const res = await fetch(`/api/suggestions/user/${userId}`, { method: 'POST' });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            if (res.status === 204) return null;
            return await res.json();
        } catch (err) {
            showActionError(messages.actionError);
            return null;
        }
    }

    /**
     * Mark suggestion as acted upon
     */
    async function actOn(suggestionId, actionTaken) {
        try {
            const res = await fetch(`/api/suggestions/${suggestionId}/act`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ actionTaken })
            });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (err) {
            showActionError(messages.actionError);
            return null;
        }
    }

    /**
     * Invalidate a suggestion
     */
    async function invalidate(suggestionId) {
        try {
            const res = await fetch(`/api/suggestions/${suggestionId}/invalidate`, { method: 'POST' });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (err) {
            showActionError(messages.actionError);
            return null;
        }
    }

    /**
     * Display a suggestion in a container
     */
    function displaySuggestion(suggestion, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const confidenceClass = suggestion.confidenceLevel === 'VeryHigh' ? 'success' :
                                 suggestion.confidenceLevel === 'High' ? 'info' :
                                 suggestion.confidenceLevel === 'Medium' ? 'warning' : 'secondary';

        const typeIcon = getTypeIcon(suggestion.type);

        const html = `
            <div class="suggestion-card suggestion-card--${confidenceClass} alert alert-${confidenceClass} d-flex align-items-start gap-3">
                <i class="bi ${typeIcon} mt-1 suggestion-card__icon"></i>
                <div class="flex-grow-1">
                    <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
                        <h5 class="mb-0">${escapeHtml(suggestion.title)}</h5>
                        <span class="badge badge-${confidenceClass}">${suggestion.confidenceScore}%</span>
                    </div>
                    <p class="mb-2 small">${escapeHtml(suggestion.description)}</p>
                    <div class="mb-3 small">
                        <strong>Recommendation:</strong> ${escapeHtml(suggestion.recommendedAction)}<br/>
                        <strong>Reasoning:</strong> ${escapeHtml(suggestion.reasoning)}
                    </div>
                    ${suggestion.supportingData?.length ? `
                        <div class="supporting-data mb-3 small">
                            ${suggestion.supportingData.map(d => `<div><i class="bi bi-check2"></i> ${escapeHtml(d)}</div>`).join('')}
                        </div>
                    ` : ''}
                    <div class="suggestion-actions d-flex gap-2 flex-wrap">
                        <button class="btn btn-sm btn-outline-primary"
                                data-suggestion-action="accept"
                                data-suggestion-id="${escapeHtml(suggestion.id)}"
                                data-recommended-action="${escapeHtml(suggestion.recommendedAction)}">
                            <i class="bi bi-check-circle"></i> Accept
                        </button>
                        <button class="btn btn-sm btn-outline-secondary"
                                data-suggestion-action="dismiss"
                                data-suggestion-id="${escapeHtml(suggestion.id)}">
                            <i class="bi bi-x-circle"></i> Dismiss
                        </button>
                    </div>
                </div>
            </div>
        `;

        container.innerHTML = html;
    }

    /**
     * Display suggestion list in container
     */
    function displaySuggestionList(suggestions, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        if (!suggestions || suggestions.length === 0) {
            container.innerHTML = '<div class="alert alert-info"><i class="bi bi-info-circle"></i> No suggestions available</div>';
            return;
        }

        const html = `
            <div class="suggestions-list">
                ${suggestions.map(s => `
                    <div class="suggestion-card-compact suggestion-card-compact--${s.confidenceLevel === 'VeryHigh' ? 'success' : s.confidenceLevel === 'High' ? 'info' : 'warning'} alert alert-light">
                        <div class="d-flex justify-content-between align-items-start gap-2">
                            <div>
                                <h6 class="mb-1">${escapeHtml(s.title)}</h6>
                                <p class="mb-1 small text-muted">${escapeHtml(s.recommendedAction)}</p>
                                <small><strong>Confidence:</strong> ${s.confidenceScore}%</small>
                            </div>
                            <span class="badge badge-${s.confidenceLevel === 'VeryHigh' ? 'success' : s.confidenceLevel === 'High' ? 'info' : 'warning'}">
                                ${s.confidenceLevel}
                            </span>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;

        container.innerHTML = html;
    }

    /**
     * Get icon for suggestion type
     */
    function getTypeIcon(type) {
        const icons = {
            'ReportAction': 'bi-exclamation-triangle',
            'IssuePriority': 'bi-arrow-up',
            'IssueDuplicateWarning': 'bi-diagram-2',
            'IssueResolution': 'bi-check-circle',
            'UserModeration': 'bi-shield-exclamation',
            'ResourceAllocation': 'bi-people',
            'default': 'bi-lightbulb'
        };
        return icons[type] || icons['default'];
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(unsafe) {
        if (!unsafe) return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    /**
     * Load and display pending suggestions on dashboard
     */
    async function loadDashboardSuggestions(containerId = 'dashboardSuggestions') {
        setContainerState(containerId, 'loading');
        try {
            const suggestions = await getPending(5);
            if (!suggestions || suggestions.length === 0) {
                setContainerState(containerId, 'empty');
                return;
            }

            displaySuggestionList(suggestions, containerId);
        } catch (err) {
            setContainerState(containerId, 'error');
        }
    }

    /**
     * Load and display suggestions for an entity
     */
    async function loadEntitySuggestions(entityId, entityType, containerId) {
        setContainerState(containerId, 'loading');
        try {
            const suggestions = await getForEntity(entityId, entityType);
            if (!suggestions || suggestions.length === 0) {
                setContainerState(containerId, 'empty');
                return;
            }

            displaySuggestionList(suggestions, containerId);
        } catch (err) {
            setContainerState(containerId, 'error');
        }
    }

    // Public API
    return {
        getPending,
        getForEntity,
        generateReportSuggestion,
        generateIssueSuggestions,
        generateUserModerationSuggestion,
        actOn,
        invalidate,
        displaySuggestion,
        displaySuggestionList,
        loadDashboardSuggestions,
        loadEntitySuggestions
    };
})();

// Auto-load dashboard suggestions on page load if container exists
document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('dashboardSuggestions')) {
        AdminSuggestions.loadDashboardSuggestions();
    }
});

document.addEventListener('click', async (event) => {
    const actionButton = event.target.closest('[data-suggestion-action]');
    if (!actionButton) return;

    const suggestionId = actionButton.getAttribute('data-suggestion-id');
    if (!suggestionId) return;

    if (actionButton.getAttribute('data-suggestion-action') === 'accept') {
        const actionTaken = actionButton.getAttribute('data-recommended-action') || '';
        const result = await AdminSuggestions.actOn(suggestionId, actionTaken);
        if (result) {
            actionButton.closest('.suggestion-card')?.remove();
        }
        return;
    }

    const result = await AdminSuggestions.invalidate(suggestionId);
    if (result) {
        actionButton.closest('.suggestion-card')?.remove();
    }
});
