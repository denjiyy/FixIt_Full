/**
 * AI Analysis Module
 * Handles AI analysis polling, display, and error states
 */
const AIAnalysis = (() => {
    'use strict';

    let checkInterval = null;
    let checkAttempts = 0;
    const MAX_ATTEMPTS = 60;
    const POLL_INTERVAL = 5000;

    async function load(issueId) {
        try {
            const res = await fetch(`/api/analysis/analyze/${issueId}`);
            const data = await res.json();
            if (data.success && data.analysis) {
                display(data.analysis);
                clearInterval(checkInterval);
                return true;
            }
            return false;
        } catch {
            return false;
        }
    }

    function display(a) {
        const el = document.getElementById('analysisPlaceholder');
        if (!el) return;

        const sColor = a.estimatedSeverity >= 8 ? '#ef4444' : a.estimatedSeverity >= 5 ? '#f59e0b' : '#10b981';
        const sBg = a.estimatedSeverity >= 8 ? 'var(--danger)' : a.estimatedSeverity >= 5 ? 'var(--warning)' : 'var(--success)';
        const sLightBg = a.estimatedSeverity >= 8 ? 'var(--danger-light)' : a.estimatedSeverity >= 5 ? 'var(--warning-light)' : 'var(--success-light)';

        const kwHtml = (a.keywords?.length) ? `
            <div class="mb-4">
                <div style="font-size:var(--text-xs);font-weight:var(--font-semibold);text-transform:uppercase;letter-spacing:.06em;color:var(--gray-400);margin-bottom:var(--space-2);">
                    <i class="bi bi-key-fill me-1"></i>Keywords
                </div>
                <div class="d-flex flex-wrap gap-2">
                    ${a.keywords.slice(0, 6).map(k => `<span class="badge" style="background:var(--gray-100);color:var(--gray-700);border:1px solid var(--gray-200);font-size:var(--text-xs);font-weight:var(--font-medium);text-transform:none;letter-spacing:0;">${k}</span>`).join('')}
                    ${a.keywords.length > 6 ? `<span class="badge" style="background:var(--gray-100);color:var(--gray-400);border:1px solid var(--gray-200);">+${a.keywords.length - 6} more</span>` : ''}
                </div>
            </div>` : '';

        const tagHtml = (a.suggestedTags?.length) ? `
            <div class="mb-4">
                <div style="font-size:var(--text-xs);font-weight:var(--font-semibold);text-transform:uppercase;letter-spacing:.06em;color:var(--gray-400);margin-bottom:var(--space-2);">
                    <i class="bi bi-hash me-1"></i>Suggested Tags
                </div>
                <div class="d-flex flex-wrap gap-2">
                    ${a.suggestedTags.slice(0, 5).map(t => `<a href="/tags/${t}" class="badge" style="background:var(--primary-50);color:var(--primary-700);border:1px solid var(--primary-200);text-decoration:none;font-weight:var(--font-medium);text-transform:none;letter-spacing:0;font-size:var(--text-xs);">#${t}</a>`).join('')}
                    ${a.suggestedTags.length > 5 ? `<span class="badge" style="background:var(--gray-100);color:var(--gray-400);border:1px solid var(--gray-200);">+${a.suggestedTags.length - 5} more</span>` : ''}
                </div>
            </div>` : '';

        const reasonHtml = a.reasoning ? `
            <div class="ai-reasoning mb-4">
                <div class="label"><i class="bi bi-chat-right-quote-fill"></i> Analysis Notes</div>
                ${a.reasoning}
            </div>` : '';

        el.innerHTML = `
            <div class="ai-card-header">
                <div class="ai-card-header-left"><i class="bi bi-stars"></i> AI Analysis</div>
                <span class="badge bg-success" style="font-size:var(--text-xs);"><i class="bi bi-check-circle-fill me-1"></i>Complete</span>
            </div>
            <div class="ai-card-body">
                <div class="row g-3 mb-4">
                    <div class="col-sm-6">
                        <div class="ai-metric">
                            <div class="ai-metric-icon" style="background:var(--info-light);color:var(--info);"><i class="bi bi-tag-fill"></i></div>
                            <div>
                                <div class="ai-metric-label">Category</div>
                                <div class="ai-metric-value">${a.category}</div>
                                <div class="ai-metric-sub">${a.confidenceScore}% confidence</div>
                            </div>
                        </div>
                    </div>
                    <div class="col-sm-6">
                        <div class="ai-metric">
                            <div class="ai-metric-icon" style="background:${sLightBg};color:${sColor};"><i class="bi bi-exclamation-triangle-fill"></i></div>
                            <div>
                                <div class="ai-metric-label">Severity</div>
                                <div class="ai-metric-value">${a.estimatedSeverity} <span style="font-size:var(--text-sm);color:var(--gray-400);">/ 10</span></div>
                                <div class="severity-track"><div class="severity-fill" style="width:${a.estimatedSeverity * 10}%;background:${sBg};"></div></div>
                            </div>
                        </div>
                    </div>
                </div>
                ${kwHtml}${tagHtml}${reasonHtml}
                <div style="font-size:var(--text-xs);color:var(--gray-400);border-top:1px solid var(--gray-100);padding-top:var(--space-3);margin-top:var(--space-2);">
                    <i class="bi bi-clock me-1"></i>
                    Analyzed ${new Date(a.analyzedAt).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                </div>
            </div>`;
    }

    function showError() {
        const el = document.getElementById('analysisPlaceholder');
        if (!el) return;
        el.innerHTML = `
            <div class="ai-card-header">
                <div class="ai-card-header-left"><i class="bi bi-stars"></i> AI Analysis</div>
                <span class="badge" style="background:var(--warning-light);color:var(--warning-dark);border:1px solid #fde68a;font-size:var(--text-xs);"><i class="bi bi-exclamation-triangle me-1"></i>Unavailable</span>
            </div>
            <div class="ai-card-body">
                <div style="text-align:center;padding:var(--space-8);">
                    <div style="width:56px;height:56px;background:var(--warning-light);border-radius:var(--radius-xl);display:flex;align-items:center;justify-content:center;margin:0 auto var(--space-4);font-size:1.5rem;color:var(--warning);">
                        <i class="bi bi-exclamation-triangle-fill"></i>
                    </div>
                    <div style="font-weight:var(--font-semibold);color:var(--gray-700);margin-bottom:var(--space-1);">Analysis Unavailable</div>
                    <div style="font-size:var(--text-sm);color:var(--gray-400);margin-bottom:var(--space-4);">Could not generate AI analysis at this time.</div>
                    <button type="button" class="btn btn-primary btn-sm" onclick="AIAnalysis.retry()">
                        <i class="bi bi-arrow-clockwise me-1"></i>Retry
                    </button>
                </div>
            </div>`;
    }

    function startPolling(issueId) {
        checkAttempts = 0;
        clearInterval(checkInterval);
        load(issueId).then(found => {
            if (!found) {
                checkInterval = setInterval(async () => {
                    checkAttempts++;
                    const ok = await load(issueId);
                    if (ok || checkAttempts >= MAX_ATTEMPTS) {
                        clearInterval(checkInterval);
                        if (!ok) showError();
                    }
                }, POLL_INTERVAL);
            }
        });
    }

    return {
        startPolling,
        retry: (issueId) => {
            const el = document.getElementById('analysisPlaceholder');
            if (el) {
                el.innerHTML = `
                    <div class="ai-card-header">
                        <div class="ai-card-header-left"><i class="bi bi-stars"></i> AI Analysis</div>
                        <span class="badge" style="background:var(--warning-light);color:var(--warning-dark);border:1px solid #fde68a;font-size:var(--text-xs);"><i class="bi bi-hourglass-split me-1"></i>Pending</span>
                    </div>
                    <div class="ai-card-body">
                        <div class="ai-loading">
                            <div class="ai-pulse-ring"><i class="bi bi-stars"></i></div>
                            <div style="font-weight:var(--font-semibold);color:var(--gray-700);margin-bottom:var(--space-1);">Generating AI Analysis...</div>
                            <div style="font-size:var(--text-sm);color:var(--gray-400);">This may take 15–30 seconds</div>
                        </div>
                    </div>`;
            }
            fetch(`/api/analysis/analyze/${issueId}`, { method: 'POST' }).catch(() => {});
            startPolling(issueId);
        }
    };
})();

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        const issueId = document.querySelector('[data-issue-id]')?.dataset.issueId || document.getElementById('issueId')?.value;
        if (issueId?.trim() && document.getElementById('analysisPlaceholder')) {
            AIAnalysis.startPolling(issueId);
        }
    });
} else {
    const issueId = document.querySelector('[data-issue-id]')?.dataset.issueId || document.getElementById('issueId')?.value;
    if (issueId?.trim() && document.getElementById('analysisPlaceholder')) {
        AIAnalysis.startPolling(issueId);
    }
}
