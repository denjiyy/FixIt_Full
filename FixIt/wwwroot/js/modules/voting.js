/**
 * Voting Module
 * Handles upvote/downvote interactions and UI updates
 */
const Voting = (() => {
    'use strict';

    function updateUI(d) {
        if (!d) return;
        document.getElementById('upvoteCount').textContent = d.upvotes;
        document.getElementById('downvoteCount').textContent = d.downvotes;
        document.getElementById('statScore').textContent = d.upvotes - d.downvotes;

        const total = d.upvotes + d.downvotes;
        const pct = total > 0 ? Math.round(d.upvotes / total * 100) : 50;
        const scoreBar = document.getElementById('scoreBar');
        if (scoreBar) scoreBar.style.width = pct + '%';

        const upPctEl = document.getElementById('upvotePct');
        const totalEl = document.getElementById('totalVoteCount');
        if (upPctEl) upPctEl.textContent = pct + '% positive';
        if (totalEl) totalEl.textContent = total + ' total';
    }

    async function vote(issueId, voteType) {
        try {
            const res = await fetch(`/api/issues/${issueId}/vote`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ voteType })
            });

            const msg = document.getElementById('voteMessage');
            if (res.ok) {
                const data = await res.json();
                updateUI(data.data);
                msg.innerHTML = '<div class="alert alert-success"><i class="bi bi-check-circle-fill me-1"></i>Vote recorded!</div>';
                setTimeout(() => msg.innerHTML = '', 2500);
                document.querySelectorAll('.vote-btn').forEach(b => b.classList.remove('active'));
                document.getElementById(voteType === 1 ? 'upvoteBtn' : 'downvoteBtn')?.classList.add('active');
            } else {
                msg.innerHTML = '<div class="alert alert-danger"><i class="bi bi-x-circle-fill me-1"></i>Failed to record vote.</div>';
            }
        } catch {
            document.getElementById('voteMessage').innerHTML = '<div class="alert alert-danger">An error occurred.</div>';
        }
    }

    return { vote };
})();

// Attach to window for use in onclick handlers
window.vote = (voteType) => {
    const issueId = document.getElementById('issueId')?.value || document.querySelector('[data-issue-id]')?.dataset.issueId;
    if (issueId) Voting.vote(issueId, voteType);
};
