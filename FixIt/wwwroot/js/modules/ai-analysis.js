/**
 * AI Analysis Module
 * Handles AI analysis polling, display, and error states
 */
const AIAnalysis = (() => {
    'use strict';

    const root = document.getElementById('issueDetailRoot');
    const locale = root?.dataset.locale || document.documentElement.lang || undefined;
    const i18n = {
        title: root?.dataset.analysisTitle || 'AI Analysis',
        pending: root?.dataset.analysisPending || 'Pending',
        complete: root?.dataset.analysisComplete || 'Complete',
        unavailable: root?.dataset.analysisUnavailable || 'Analysis unavailable',
        errorCopy: root?.dataset.analysisErrorCopy || 'Could not generate analysis at this time.',
        retry: root?.dataset.analysisRetry || 'Retry',
        generatingTitle: root?.dataset.analysisGeneratingTitle || 'Generating analysis...',
        generatingCopy: root?.dataset.analysisGeneratingCopy || 'This may take a few seconds.',
        categoryLabel: root?.dataset.analysisCategoryLabel || 'Category',
        severityLabel: root?.dataset.analysisSeverityLabel || 'Severity',
        keywordsLabel: root?.dataset.analysisKeywordsLabel || 'Keywords',
        suggestedTagsLabel: root?.dataset.analysisSuggestedTagsLabel || 'Suggested Tags',
        notesLabel: root?.dataset.analysisNotesLabel || 'Analysis Notes',
        similarIssuesLabel: root?.dataset.analysisSimilarIssuesLabel || 'Similar Issues',
        moreLabel: root?.dataset.analysisMoreLabel || 'more',
        confidenceSuffix: root?.dataset.analysisConfidenceSuffix || 'confidence',
        analyzedLabel: root?.dataset.analysisAnalyzedLabel || 'Analyzed'
    };

    let checkInterval = null;
    let checkAttempts = 0;
    const maxAttempts = 60;
    const pollInterval = 5000;

    function hydrateProgress(container = document) {
        container.querySelectorAll('.severity-fill[data-width]').forEach((bar) => {
            const width = Number.parseFloat(bar.dataset.width || '');
            if (!Number.isNaN(width)) {
                bar.style.width = `${width}%`;
            }
        });
    }

    function formatAnalyzedDate(value) {
        return new Date(value).toLocaleString(locale, {
            year: 'numeric',
            month: 'long',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    async function load(issueId) {
        try {
            const response = await fetch(`/api/analysis/analyze/${issueId}`);
            const data = await response.json();
            if (data.success && data.analysis) {
                display(data.analysis);
                window.clearInterval(checkInterval);
                return true;
            }
            return false;
        } catch {
            return false;
        }
    }

    function display(analysis) {
        const placeholder = document.getElementById('analysisPlaceholder');
        if (!placeholder) return;

        const severityToneClass = analysis.estimatedSeverity >= 8
            ? 'detail-metric__icon--danger'
            : analysis.estimatedSeverity >= 5
                ? 'detail-metric__icon--warning'
                : 'detail-metric__icon--success';
        const severityFillClass = analysis.estimatedSeverity >= 8
            ? 'detail-progress__bar--danger'
            : analysis.estimatedSeverity >= 5
                ? 'detail-progress__bar--warning'
                : 'detail-progress__bar--success';

        const keywordMarkup = analysis.keywords?.length
            ? `
                <div class="detail-chip-block">
                    <span class="detail-chip-block__label">${i18n.keywordsLabel}</span>
                    <div class="detail-chip-list">
                        ${analysis.keywords.slice(0, 6).map((keyword) => `<span class="detail-chip">${keyword}</span>`).join('')}
                        ${analysis.keywords.length > 6 ? `<span class="detail-chip">+${analysis.keywords.length - 6} ${i18n.moreLabel}</span>` : ''}
                    </div>
                </div>
            `
            : '';

        const suggestedTagMarkup = analysis.suggestedTags?.length
            ? `
                <div class="detail-chip-block">
                    <span class="detail-chip-block__label">${i18n.suggestedTagsLabel}</span>
                    <div class="detail-chip-list">
                        ${analysis.suggestedTags.slice(0, 5).map((tag) => `<a href="/tags/${tag}" class="detail-chip detail-chip--link">#${tag}</a>`).join('')}
                        ${analysis.suggestedTags.length > 5 ? `<span class="detail-chip">+${analysis.suggestedTags.length - 5} ${i18n.moreLabel}</span>` : ''}
                    </div>
                </div>
            `
            : '';

        const notesMarkup = analysis.reasoning
            ? `
                <div class="detail-note">
                    <strong>${i18n.notesLabel}</strong>
                    <p>${analysis.reasoning}</p>
                </div>
            `
            : '';

        const duplicateMarkup = analysis.potentialDuplicates?.length
            ? `
                <div class="detail-duplicates">
                    <span class="detail-chip-block__label">${i18n.similarIssuesLabel}</span>
                    ${analysis.potentialDuplicates.slice(0, 3).map((duplicate) => `
                        <a href="/issues/${duplicate.issueId}" class="detail-duplicate-card">
                            <div>
                                <strong>${duplicate.issueTitle}</strong>
                                <span>${duplicate.reason}</span>
                            </div>
                            <em>${duplicate.similarityScore}%</em>
                        </a>
                    `).join('')}
                </div>
            `
            : '';

        placeholder.innerHTML = `
            <div class="detail-card__header detail-card__header--split">
                <h2>${i18n.title}</h2>
                <span class="detail-status detail-status--success">${i18n.complete}</span>
            </div>
            <div class="detail-card__body">
                <div class="detail-metric-grid">
                    <article class="detail-metric">
                        <div class="detail-metric__icon">
                            <i class="bi bi-tag-fill"></i>
                        </div>
                        <div>
                            <span>${i18n.categoryLabel}</span>
                            <strong>${analysis.category}</strong>
                            <small>${analysis.confidenceScore}% ${i18n.confidenceSuffix}</small>
                        </div>
                    </article>
                    <article class="detail-metric">
                        <div class="detail-metric__icon ${severityToneClass}">
                            <i class="bi bi-exclamation-triangle-fill"></i>
                        </div>
                        <div>
                            <span>${i18n.severityLabel}</span>
                            <strong>${analysis.estimatedSeverity} / 10</strong>
                            <div class="detail-progress">
                                <div class="detail-progress__bar ${severityFillClass} severity-fill" data-width="${analysis.estimatedSeverity * 10}"></div>
                            </div>
                        </div>
                    </article>
                </div>
                ${keywordMarkup}
                ${suggestedTagMarkup}
                ${notesMarkup}
                ${duplicateMarkup}
                <div class="detail-analysis-meta">
                    <i class="bi bi-clock-history"></i>
                    <span>${i18n.analyzedLabel} ${formatAnalyzedDate(analysis.analyzedAt)}</span>
                </div>
            </div>
        `;

        hydrateProgress(placeholder);
    }

    function showError() {
        const placeholder = document.getElementById('analysisPlaceholder');
        if (!placeholder) return;

        placeholder.innerHTML = `
            <div class="detail-card__header detail-card__header--split">
                <h2>${i18n.title}</h2>
                <span class="detail-status detail-status--warning">${i18n.unavailable}</span>
            </div>
            <div class="detail-card__body">
                <div class="analysis-error analysis-error--visible">
                    <div class="analysis-error-icon">
                        <i class="bi bi-exclamation-triangle-fill"></i>
                    </div>
                    <h3>${i18n.unavailable}</h3>
                    <p class="analysis-error-copy">${i18n.errorCopy}</p>
                    <button type="button" class="btn btn-primary btn-sm" data-ai-retry>
                        <i class="bi bi-arrow-clockwise me-1"></i>${i18n.retry}
                    </button>
                </div>
            </div>
        `;
    }

    function startPolling(issueId) {
        checkAttempts = 0;
        window.clearInterval(checkInterval);

        load(issueId).then((found) => {
            if (found) return;

            checkInterval = window.setInterval(async () => {
                checkAttempts += 1;
                const loaded = await load(issueId);
                if (loaded || checkAttempts >= maxAttempts) {
                    window.clearInterval(checkInterval);
                    if (!loaded) {
                        showError();
                    }
                }
            }, pollInterval);
        });
    }

    function retry(issueId) {
        const resolvedIssueId = issueId || root?.dataset.issueId || document.getElementById('issueId')?.value;
        const placeholder = document.getElementById('analysisPlaceholder');
        if (placeholder) {
            placeholder.innerHTML = `
                <div class="detail-card__header detail-card__header--split">
                    <h2>${i18n.title}</h2>
                    <span class="detail-status detail-status--warning">${i18n.pending}</span>
                </div>
                <div class="detail-card__body">
                    <div class="detail-analysis-loading">
                        <div class="detail-analysis-loading__icon">
                            <i class="bi bi-stars"></i>
                        </div>
                        <h3>${i18n.generatingTitle}</h3>
                        <p>${i18n.generatingCopy}</p>
                    </div>
                </div>
            `;
        }

        if (!resolvedIssueId) return;
        fetch(`/api/analysis/analyze/${resolvedIssueId}`, { method: 'POST' }).catch(() => {});
        startPolling(resolvedIssueId);
    }

    return { startPolling, retry };
})();

window.AIAnalysis = AIAnalysis;

document.addEventListener('click', (event) => {
    const retryButton = event.target.closest('[data-ai-retry]');
    if (!retryButton) return;

    const issueId = retryButton.getAttribute('data-issue-id') || undefined;
    AIAnalysis.retry(issueId);
});

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        const root = document.getElementById('issueDetailRoot');
        if (!root) return;

        if (root.dataset.issueId && document.getElementById('analysisPlaceholder')) {
            AIAnalysis.startPolling(root.dataset.issueId);
        }
    }, { once: true });
} else {
    const root = document.getElementById('issueDetailRoot');
    if (root?.dataset.issueId && document.getElementById('analysisPlaceholder')) {
        AIAnalysis.startPolling(root.dataset.issueId);
    }
}
