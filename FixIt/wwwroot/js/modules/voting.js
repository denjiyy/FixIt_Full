/**
 * Voting Module
 * Handles upvote/downvote interactions and UI updates
 */
const Voting = (() => {
    'use strict';

    const root = document.getElementById('issueDetailRoot');
    const issueId = root?.dataset.issueId || document.getElementById('issueId')?.value || '';
    const messages = {
        success: root?.dataset.voteSuccess || 'Vote recorded!',
        failure: root?.dataset.voteFailure || 'Failed to record vote.',
        error: root?.dataset.voteError || 'An error occurred.',
        positiveLabel: root?.dataset.positiveLabel || 'positive',
        totalLabel: root?.dataset.totalLabel || 'total'
    };

    function hydrateScoreBar() {
        const scoreBar = document.getElementById('scoreBar');
        if (scoreBar?.dataset.width) {
            scoreBar.style.width = `${scoreBar.dataset.width}%`;
        }
    }

    function updateUI(data) {
        if (!data) return;

        document.getElementById('upvoteCount').textContent = data.upvotes;
        document.getElementById('downvoteCount').textContent = data.downvotes;
        document.getElementById('statScore').textContent = data.upvotes - data.downvotes;

        const total = data.upvotes + data.downvotes;
        const pct = total > 0 ? Math.round((data.upvotes / total) * 100) : 50;
        const scoreBar = document.getElementById('scoreBar');
        if (scoreBar) {
            scoreBar.dataset.width = pct;
            scoreBar.style.width = `${pct}%`;
        }

        const upPct = document.getElementById('upvotePct');
        const totalVoteCount = document.getElementById('totalVoteCount');
        if (upPct) upPct.textContent = `${pct}% ${messages.positiveLabel}`;
        if (totalVoteCount) totalVoteCount.textContent = `${total} ${messages.totalLabel}`;
    }

    async function vote(issueIdValue, voteType) {
        const messageSlot = document.getElementById('voteMessage');

        try {
            const response = await fetch(`/api/issues/${issueIdValue}/vote`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ voteType })
            });

            if (!response.ok) {
                if (messageSlot) {
                    messageSlot.innerHTML = `<div class="alert alert-danger mt-3">${messages.failure}</div>`;
                }
                return;
            }

            const result = await response.json();
            updateUI(result.data);

            if (messageSlot) {
                messageSlot.innerHTML = `<div class="alert alert-success mt-3">${messages.success}</div>`;
                window.setTimeout(() => {
                    messageSlot.innerHTML = '';
                }, 2500);
            }

            document.querySelectorAll('.vote-btn').forEach((button) => button.classList.remove('active'));
            document.getElementById(voteType === 1 ? 'upvoteBtn' : 'downvoteBtn')?.classList.add('active');
        } catch (error) {
            if (typeof window.FixItNotify === 'function') {
                window.FixItNotify(messages.error, 'error');
            }
            if (messageSlot) {
                messageSlot.innerHTML = `<div class="alert alert-danger mt-3">${messages.error}</div>`;
            }
        }
    }

    function bindVoteButtons() {
        if (!issueId) return;

        document.querySelectorAll('[data-vote-type]').forEach((button) => {
            button.addEventListener('click', () => {
                const voteType = Number.parseInt(button.dataset.voteType || '', 10);
                if (!Number.isNaN(voteType)) {
                    vote(issueId, voteType);
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            hydrateScoreBar();
            bindVoteButtons();
        }, { once: true });
    } else {
        hydrateScoreBar();
        bindVoteButtons();
    }

    return { vote };
})();

window.vote = (voteType) => {
    const issueIdValue = document.getElementById('issueId')?.value || document.getElementById('issueDetailRoot')?.dataset.issueId;
    if (issueIdValue) {
        Voting.vote(issueIdValue, voteType);
    }
};
