/**
 * Comments Module
 * Handles comment CRUD operations, likes/dislikes, and sorting
 */
const Comments = (() => {
    'use strict';

    const root = document.getElementById('issueDetailRoot');
    const issueId = root?.dataset.issueId || '';
    const currentUserId = root?.dataset.currentUser || '';
    const messages = {
        deleteConfirm: root?.dataset.deleteCommentConfirm || 'Delete this comment?',
        deleteOwnOnly: root?.dataset.deleteOwnOnly || 'You can only delete your own comments.',
        deleteFailed: root?.dataset.deleteCommentFailed || 'Failed to delete comment.',
        deleteError: root?.dataset.deleteCommentError || 'An error occurred while deleting the comment.',
        signInLike: root?.dataset.signInLike || 'Please sign in to like comments.',
        signInDislike: root?.dataset.signInDislike || 'Please sign in to dislike comments.',
        actionFailed: root?.dataset.commentActionFailed || 'We could not update this comment right now.',
        noComments: root?.dataset.noComments || 'No comments yet.'
    };
    const notify = (message, tone = 'info') => {
        if (typeof window.FixItNotify === 'function') {
            window.FixItNotify(message, tone);
        }
    };

    function setLoadingState(button, isLoading) {
        if (!button) return;
        button.disabled = isLoading;
        button.setAttribute('aria-busy', String(isLoading));
        button.classList.toggle('is-loading', isLoading);
    }

    function ensureEmptyState() {
        const commentsList = document.getElementById('commentsList');
        if (!commentsList) return;

        const hasComments = commentsList.querySelector('.comment-item');
        const existingState = commentsList.querySelector('[data-comments-empty]');

        if (hasComments) {
            existingState?.remove();
            return;
        }

        if (!existingState) {
            const empty = document.createElement('div');
            empty.dataset.commentsEmpty = 'true';
            empty.className = 'alert alert-info';
            empty.textContent = messages.noComments;
            commentsList.appendChild(empty);
        }
    }

    async function deleteComment(commentId, authorId, triggerButton = null) {
        if (!currentUserId || currentUserId !== authorId) {
            notify(messages.deleteOwnOnly, 'warning');
            return;
        }

        if (!window.confirm(messages.deleteConfirm)) return;

        setLoadingState(triggerButton, true);
        try {
            const response = await fetch(`/api/issues/${issueId}/comments/${commentId}`, {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json' }
            });

            if (response.ok) {
                document.querySelector(`[data-comment-id="${commentId}"]`)?.remove();
                ensureEmptyState();
            } else {
                notify(messages.deleteFailed, 'error');
            }
        } catch (error) {
            notify(messages.deleteError, 'error');
        } finally {
            setLoadingState(triggerButton, false);
        }
    }

    async function likeComment(commentId, triggerButton = null) {
        if (!currentUserId) {
            notify(messages.signInLike, 'warning');
            return;
        }

        setLoadingState(triggerButton, true);
        try {
            const response = await fetch(`/api/issues/${issueId}/comments/${commentId}/like`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            if (!response.ok) {
                notify(messages.actionFailed, 'error');
                return;
            }

            const data = await response.json();
            const element = document.querySelector(`[data-comment-id="${commentId}"]`);
            if (!element) return;

            element.querySelector('.like-count').textContent = data.data.likeCount;
            element.querySelector('.dislike-count').textContent = data.data.dislikeCount;
            element.querySelector('.comment-like-btn').classList.add('active');
            element.querySelector('.comment-dislike-btn').classList.remove('active');
        } catch (error) {
            notify(messages.actionFailed, 'error');
        } finally {
            setLoadingState(triggerButton, false);
        }
    }

    async function dislikeComment(commentId, triggerButton = null) {
        if (!currentUserId) {
            notify(messages.signInDislike, 'warning');
            return;
        }

        setLoadingState(triggerButton, true);
        try {
            const response = await fetch(`/api/issues/${issueId}/comments/${commentId}/dislike`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            if (!response.ok) {
                notify(messages.actionFailed, 'error');
                return;
            }

            const data = await response.json();
            const element = document.querySelector(`[data-comment-id="${commentId}"]`);
            if (!element) return;

            element.querySelector('.like-count').textContent = data.data.likeCount;
            element.querySelector('.dislike-count').textContent = data.data.dislikeCount;
            element.querySelector('.comment-like-btn').classList.remove('active');
            element.querySelector('.comment-dislike-btn').classList.add('active');
        } catch (error) {
            notify(messages.actionFailed, 'error');
        } finally {
            setLoadingState(triggerButton, false);
        }
    }

    function sort(sortBy) {
        const commentsList = document.getElementById('commentsList');
        if (!commentsList) return;

        const comments = Array.from(commentsList.querySelectorAll('.comment-item'));
        comments.sort((first, second) => {
            switch (sortBy) {
                case 'oldest':
                    return new Date(first.dataset.date) - new Date(second.dataset.date);
                case 'mostLiked':
                    return Number.parseInt(second.dataset.likes || '0', 10) - Number.parseInt(first.dataset.likes || '0', 10);
                default:
                    return new Date(second.dataset.date) - new Date(first.dataset.date);
            }
        });

        comments.forEach((comment) => commentsList.appendChild(comment));
    }

    function wireEvents() {
        if (!root || !issueId) return;

        document.querySelectorAll('.comment-delete-btn').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.preventDefault();
                deleteComment(button.dataset.commentId, button.dataset.authorId, button);
            });
        });

        document.querySelectorAll('.comment-like-btn').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.preventDefault();
                likeComment(button.dataset.commentId, button);
            });
        });

        document.querySelectorAll('.comment-dislike-btn').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.preventDefault();
                dislikeComment(button.dataset.commentId, button);
            });
        });

        document.getElementById('commentSortDropdown')?.addEventListener('change', (event) => {
            sort(event.target.value);
        });

        ensureEmptyState();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireEvents, { once: true });
    } else {
        wireEvents();
    }

    return { deleteComment, likeComment, dislikeComment, sort };
})();
