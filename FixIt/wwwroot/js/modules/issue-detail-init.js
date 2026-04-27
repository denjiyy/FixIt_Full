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
        const root = document.getElementById('issueDetailRoot');
        if (!root) return;

        document.querySelectorAll('.severity-fill[data-width], #scoreBar[data-width]').forEach((element) => {
            const width = Number.parseFloat(element.dataset.width || '');
            if (!Number.isNaN(width)) {
                element.style.width = `${width}%`;
            }
        });

        document.querySelectorAll('[data-video-preview]').forEach((video) => {
            video.addEventListener('error', () => {
                const wrapper = video.closest('.detail-video-card');
                if (!wrapper) return;

                const fallback = document.createElement('div');
                fallback.className = 'alert alert-warning mb-0';
                fallback.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${root.dataset.videoPreviewUnavailable || 'Video preview unavailable.'}`;
                wrapper.replaceWith(fallback);
            }, { once: true });
        });
    });
})();
