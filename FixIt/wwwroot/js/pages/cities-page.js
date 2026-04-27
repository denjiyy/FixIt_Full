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
        document.querySelectorAll('.city-card__image').forEach((image) => {
            image.addEventListener('error', () => {
                image.closest('.city-card__media')?.classList.add('is-fallback');
            }, { once: true });
        });
    });
})();
