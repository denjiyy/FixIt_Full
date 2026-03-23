/**
 * Comments Module
 * Handles comment CRUD operations, likes/dislikes, and sorting
 */
const Comments = (() => {
    'use strict';

    const issueId = document.getElementById('issueId')?.value || document.querySelector('[data-issue-id]')?.dataset.issueId;
    const currentUserId = document.body.dataset.currentUserId || document.querySelector('[data-current-user]')?.dataset.currentUser || '';

    async function deleteComment(commentId, authorId) {
        if (!currentUserId || currentUserId !== authorId) {
            alert('You can only delete your own comments');
            return;
        }
        if (!confirm('Are you sure you want to delete this comment?')) return;

        try {
            const response = await fetch(`/api/issues/${issueId}/comments/${commentId}`, {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json' }
            });
            if (response.ok) {
                const commentEl = document.querySelector(`[data-comment-id="${commentId}"]`);
                if (commentEl) {
                    commentEl.style.opacity = '0.5';
                    commentEl.querySelector('.comment-text').textContent = '[deleted]';
                    commentEl.querySelector('.comment-delete-btn').style.display = 'none';
                }
            } else {
                alert('Failed to delete comment');
            }
        } catch (error) {
            console.error('Error deleting comment:', error);
            alert('An error occurred while deleting the comment');
        }
    }

    async function likeComment(commentId) {
        if (!currentUserId) {
            alert('Please sign in to like comments');
            return;
        }
        try {
            const response = await fetch(`/api/issues/${issueId}/comments/${commentId}/like`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' }
            });
            if (response.ok) {
                const data = await response.json();
                const el = document.querySelector(`[data-comment-id="${commentId}"]`);
                if (el) {
                    el.querySelector('.like-count').textContent = data.data.likeCount;
                    el.querySelector('.dislike-count').textContent = data.data.dislikeCount;
                    el.querySelector('.comment-like-btn').classList.add('active');
                    el.querySelector('.comment-dislike-btn').classList.remove('active');
                }
            }
        } catch (e) {
            console.error(e);
        }
    }

    async function dislikeComment(commentId) {
        if (!currentUserId) {
            alert('Please sign in to dislike comments');
            return;
        }
        try {
            const response = await fetch(`/api/issues/${issueId}/comments/${commentId}/dislike`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' }
            });
            if (response.ok) {
                const data = await response.json();
                const el = document.querySelector(`[data-comment-id="${commentId}"]`);
                if (el) {
                    el.querySelector('.like-count').textContent = data.data.likeCount;
                    el.querySelector('.dislike-count').textContent = data.data.dislikeCount;
                    el.querySelector('.comment-like-btn').classList.remove('active');
                    el.querySelector('.comment-dislike-btn').classList.add('active');
                }
            }
        } catch (e) {
            console.error(e);
        }
    }

    function sort(sortBy) {
        const commentsList = document.getElementById('commentsList');
        if (!commentsList) return;
        const comments = Array.from(commentsList.querySelectorAll('.comment-item'));
        comments.sort((a, b) => {
            switch (sortBy) {
                case 'oldest':
                    return new Date(a.dataset.date) - new Date(b.dataset.date);
                case 'mostLiked':
                    return parseInt(b.dataset.likes) - parseInt(a.dataset.likes);
                default:
                    return new Date(b.dataset.date) - new Date(a.dataset.date);
            }
        });
        comments.forEach(c => commentsList.appendChild(c));
    }

    // Wire up event listeners
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => wireEvents());
    } else {
        wireEvents();
    }

    function wireEvents() {
        document.querySelectorAll('.comment-delete-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                deleteComment(btn.dataset.commentId, btn.dataset.authorId);
            });
        });

        document.querySelectorAll('.comment-like-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                likeComment(btn.dataset.commentId);
            });
        });

        document.querySelectorAll('.comment-dislike-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                dislikeComment(btn.dataset.commentId);
            });
        });

        const sortDropdown = document.getElementById('commentSortDropdown');
        if (sortDropdown) {
            sortDropdown.addEventListener('change', (e) => sort(e.target.value));
        }
    }

    return { deleteComment, likeComment, dislikeComment, sort };
})();
