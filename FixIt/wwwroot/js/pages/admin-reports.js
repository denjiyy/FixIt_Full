(function () {
    'use strict';

    const onReady = window.FixItApp?.onReady || ((callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    });

    function setSummaryLoading(detailsElement) {
        const body = detailsElement.querySelector('.ai-summary-details__body');
        if (!body) return;

        body.innerHTML = `
            <div class="app-inline-state app-inline-state--loading" role="status">
                <span class="app-skeleton" style="width: 1rem; height: 1rem;"></span>
                <span>Generating summary...</span>
            </div>
        `;
    }

    function setSummaryContent(detailsElement, content, aiGenerated) {
        const body = detailsElement.querySelector('.ai-summary-details__body');
        const badge = detailsElement.querySelector('.ai-summary-badge');
        if (!body || !badge) return;

        body.textContent = content;
        badge.textContent = aiGenerated ? 'AI-generated' : 'Rule-based';
        badge.classList.toggle('is-fallback', !aiGenerated);
        detailsElement.classList.remove('d-none');
        detailsElement.open = true;
    }

    function setSummaryError(detailsElement, message) {
        const body = detailsElement.querySelector('.ai-summary-details__body');
        if (!body) return;

        body.innerHTML = `<div class="app-inline-state app-inline-state--error">${message}</div>`;
        detailsElement.classList.remove('d-none');
        detailsElement.open = true;
    }

    async function streamSummary(endpoint, onEvent) {
        const response = await fetch(endpoint, { method: 'POST' });
        if (!response.ok || !response.body) {
            throw new Error(`HTTP ${response.status}`);
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let pending = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            pending += decoder.decode(value, { stream: true });
            const lines = pending.split('\n');
            pending = lines.pop() || '';

            lines.forEach((line) => {
                const trimmed = line.trim();
                if (!trimmed) return;

                try {
                    const parsed = JSON.parse(trimmed);
                    onEvent(parsed);
                } catch {
                    // Ignore malformed chunks.
                }
            });
        }

        if (pending.trim()) {
            try {
                onEvent(JSON.parse(pending.trim()));
            } catch {
                // Ignore trailing malformed chunk.
            }
        }
    }

    async function loadReportSummary(reportId) {
        const detailsElement = document.getElementById(`report-summary-${reportId}`);
        if (!detailsElement) return;

        setSummaryLoading(detailsElement);

        let streamedText = '';
        let completed = false;
        let aiGenerated = true;

        try {
            await streamSummary(`/api/suggestions/reports/${encodeURIComponent(reportId)}/summary/stream`, (event) => {
                if (event.type === 'chunk' && typeof event.text === 'string') {
                    streamedText += event.text;
                    setSummaryContent(detailsElement, streamedText, event.aiGenerated !== false);
                }

                if (event.type === 'complete') {
                    completed = true;
                    aiGenerated = event.aiGenerated !== false;
                    const content = typeof event.content === 'string' && event.content.trim().length > 0
                        ? event.content
                        : streamedText;
                    setSummaryContent(detailsElement, content, aiGenerated);
                }

                if (event.type === 'error') {
                    throw new Error(event.message || 'Summary stream failed.');
                }
            });

            if (completed) {
                return;
            }

            if (streamedText.trim().length > 0) {
                setSummaryContent(detailsElement, streamedText, aiGenerated);
                return;
            }

            throw new Error('Empty streamed summary.');
        } catch {
            try {
                const fallbackResponse = await fetch(`/api/suggestions/reports/${encodeURIComponent(reportId)}/summary`, { method: 'POST' });
                if (!fallbackResponse.ok) {
                    throw new Error(`HTTP ${fallbackResponse.status}`);
                }

                const fallback = await fallbackResponse.json();
                if (typeof fallback?.content !== 'string' || fallback.content.trim().length === 0) {
                    throw new Error('No summary content.');
                }

                setSummaryContent(detailsElement, fallback.content, fallback.aiGenerated === true);
            } catch {
                setSummaryError(detailsElement, 'Could not generate a summary right now.');
            }
        }
    }

    onReady(async () => {
        const reportCards = document.querySelectorAll('[id^="suggestions-"]');
        if (reportCards.length && typeof window.AdminSuggestions !== 'undefined') {
            for (const card of reportCards) {
                const reportId = card.id.replace('suggestions-', '');
                const suggestion = await window.AdminSuggestions.generateReportSuggestion(reportId);
                if (!suggestion) {
                    continue;
                }

                card.classList.remove('d-none');
                window.AdminSuggestions.displaySuggestion(suggestion, card.id);
            }
        }

        document.addEventListener('click', (event) => {
            const trigger = event.target.closest('[data-summary-trigger][data-summary-type="report"]');
            if (!trigger) {
                return;
            }

            const reportId = trigger.getAttribute('data-summary-id');
            if (!reportId) {
                return;
            }

            trigger.setAttribute('disabled', 'disabled');
            loadReportSummary(reportId)
                .finally(() => trigger.removeAttribute('disabled'));
        });
    });
})();
