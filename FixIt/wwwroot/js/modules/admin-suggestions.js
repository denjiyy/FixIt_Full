/**
 * Admin Suggestions Module
 * Handles fetching, displaying, and acting on AI suggestions for admin tasks
 */
const AdminSuggestions = (() => {
    'use strict';

    /**
     * Fetch pending suggestions for dashboard
     */
    async function getPending(limit = 10) {
        try {
            const res = await fetch(`/api/suggestions/pending?limit=${limit}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (err) {
            console.error('Error fetching pending suggestions:', err);
            return [];
        }
    }

    /**
     * Fetch suggestions for a specific entity (report, issue, user)
     */
    async function getForEntity(entityId, entityType) {
        try {
            const res = await fetch(`/api/suggestions/entity/${entityId}?entityType=${encodeURIComponent(entityType)}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (err) {
            console.error(`Error fetching suggestions for ${entityType} ${entityId}:`, err);
            return [];
        }
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
            console.error(`Error generating report suggestion for ${reportId}:`, err);
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
            console.error(`Error generating issue suggestions for ${issueId}:`, err);
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
            console.error(`Error generating user moderation suggestion for ${userId}:`, err);
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
            console.error(`Error marking suggestion ${suggestionId} as acted:`, err);
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
            console.error(`Error invalidating suggestion ${suggestionId}:`, err);
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
            <div class="suggestion-card alert alert-${confidenceClass} d-flex align-items-start gap-3" style="border-left: 4px solid var(--${confidenceClass});">
                <i class="bi ${typeIcon} mt-1" style="font-size: 1.25rem;"></i>
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
                        <div class="supporting-data mb-3 small" style="background: rgba(0,0,0,0.05); padding: 0.75rem; border-radius: 0.375rem;">
                            ${suggestion.supportingData.map(d => `<div><i class="bi bi-check2"></i> ${escapeHtml(d)}</div>`).join('')}
                        </div>
                    ` : ''}
                    <div class="suggestion-actions d-flex gap-2 flex-wrap">
                        <button class="btn btn-sm btn-outline-primary" onclick="AdminSuggestions.actOn('${suggestion.id}', '${suggestion.recommendedAction}')">
                            <i class="bi bi-check-circle"></i> Accept
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="AdminSuggestions.invalidate('${suggestion.id}')">
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
                    <div class="suggestion-card-compact alert alert-light border-left" style="border-left: 4px solid var(--primary); margin-bottom: 0.75rem; padding: 0.75rem;">
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
        const suggestions = await getPending(5);
        displaySuggestionList(suggestions, containerId);
    }

    /**
     * Load and display suggestions for an entity
     */
    async function loadEntitySuggestions(entityId, entityType, containerId) {
        const suggestions = await getForEntity(entityId, entityType);
        displaySuggestionList(suggestions, containerId);
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
