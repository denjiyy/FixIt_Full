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
        const form = document.querySelector('form');
        const loginButton = document.getElementById('loginBtn');

        form?.addEventListener('submit', () => {
            if (loginButton) {
                loginButton.disabled = true;
            }
        });

        document.querySelectorAll('.form-control').forEach((input) => {
            input.addEventListener('focus', () => {
                input.classList.remove('is-invalid');
            });
        });
    });
})();
