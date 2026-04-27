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
        const root = document.getElementById('issuesIndexRoot');
        if (!root) return;

        const form = document.getElementById('issuesFilterForm');
        const searchInput = document.getElementById('searchQuery');
        const sortInput = document.getElementById('sortInput');
        const statusInput = document.getElementById('status');
        const priorityInput = document.getElementById('priority');
        const categoryInput = document.getElementById('categoryInput');
        const fromInput = document.getElementById('fromInput');
        const toInput = document.getElementById('toInput');
        const filterPanel = document.querySelector('[data-filter-panel]');
        const filterToggle = document.querySelector('[data-filter-toggle]');
        const aiQueryInput = document.getElementById('naturalSearchQuery');
        const aiFilterButton = document.getElementById('aiFilterButton');
        const aiFilterStatus = document.getElementById('aiFilterStatus');

        if (!form || !searchInput || !sortInput || !filterPanel || !filterToggle) return;

        const messages = {
            aiLoading: root.dataset.aiFilterLoading || 'Interpreting your request with AI...',
            aiError: root.dataset.aiFilterError || 'AI filter translation failed. Applying basic text search instead.',
            aiApplied: root.dataset.aiFilterApplied || 'AI filters applied to the current view.',
            aiLabel: root.dataset.aiFilterLabel || 'AI-generated filters'
        };

        let debounceTimer = null;

        function setAiStatus(state, message) {
            if (!aiFilterStatus) {
                return;
            }

            if (!message) {
                aiFilterStatus.className = 'issues-toolbar__ai-status';
                aiFilterStatus.innerHTML = '';
                return;
            }

            if (state === 'loading') {
                aiFilterStatus.className = 'issues-toolbar__ai-status app-inline-state app-inline-state--loading';
                aiFilterStatus.innerHTML = `<span class="app-skeleton" style="width: 1rem; height: 1rem;"></span><span>${message}</span>`;
                return;
            }

            if (state === 'error') {
                aiFilterStatus.className = 'issues-toolbar__ai-status app-inline-state app-inline-state--error';
                aiFilterStatus.textContent = message;
                return;
            }

            aiFilterStatus.className = 'issues-toolbar__ai-status app-inline-state';
            aiFilterStatus.innerHTML = `<i class="bi bi-stars" aria-hidden="true"></i><span>${message}</span>`;
        }

        function submitForm() {
            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit();
            } else {
                form.submit();
            }
        }

        async function applyNaturalLanguageFilters() {
            if (!aiQueryInput || !aiFilterButton) {
                return;
            }

            const query = aiQueryInput.value.trim();
            if (!query) {
                return;
            }

            aiFilterButton.disabled = true;
            setAiStatus('loading', messages.aiLoading);

            try {
                const response = await fetch('/api/analysis/issue-search/translate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ query })
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const result = await response.json();

                if (typeof result.searchQuery === 'string') {
                    searchInput.value = result.searchQuery;
                }

                if (statusInput && result.status !== null && result.status !== undefined) {
                    statusInput.value = String(result.status);
                } else if (statusInput && result.status === null) {
                    statusInput.value = '';
                }

                if (priorityInput && result.priority !== null && result.priority !== undefined) {
                    priorityInput.value = String(result.priority);
                } else if (priorityInput && result.priority === null) {
                    priorityInput.value = '';
                }

                if (categoryInput) {
                    categoryInput.value = typeof result.category === 'string' ? result.category : '';
                }

                if (fromInput) {
                    fromInput.value = typeof result.from === 'string' ? result.from : '';
                }

                if (toInput) {
                    toInput.value = typeof result.to === 'string' ? result.to : '';
                }

                const label = result.aiGenerated === true ? `${messages.aiLabel}. ${messages.aiApplied}` : messages.aiApplied;
                setAiStatus('ok', label);
                submitForm();
            } catch {
                setAiStatus('error', messages.aiError);
                searchInput.value = query;
                submitForm();
            } finally {
                aiFilterButton.disabled = false;
            }
        }

        searchInput.addEventListener('input', () => {
            window.clearTimeout(debounceTimer);
            debounceTimer = window.setTimeout(() => {
                if (searchInput.value.trim().length === 0 || searchInput.value.trim().length >= 3) {
                    submitForm();
                }
            }, 450);
        });

        ['status', 'priority'].forEach((id) => {
            const field = document.getElementById(id);
            if (!field) return;

            field.addEventListener('change', () => {
                submitForm();
            });
        });

        document.querySelectorAll('[data-sort-value]').forEach((button) => {
            button.addEventListener('click', () => {
                sortInput.value = button.getAttribute('data-sort-value') || 'newest';
                submitForm();
            });
        });

        if (aiFilterButton) {
            aiFilterButton.addEventListener('click', applyNaturalLanguageFilters);
        }

        if (aiQueryInput) {
            aiQueryInput.addEventListener('keydown', (event) => {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    applyNaturalLanguageFilters();
                }
            });
        }

        const syncToggle = () => {
            const isMobile = window.innerWidth < 768;
            const isOpen = isMobile ? filterPanel.classList.contains('is-open') : true;
            filterToggle.setAttribute('aria-expanded', String(isOpen));
        };

        filterToggle.addEventListener('click', () => {
            filterPanel.classList.toggle('is-open');
            syncToggle();
        });

        window.addEventListener('resize', syncToggle);
        syncToggle();
    });
})();
