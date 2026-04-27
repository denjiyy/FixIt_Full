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
        document.addEventListener('click', (event) => {
            const trigger = event.target.closest('[data-user-id][data-user-email]');
            if (!trigger) {
                return;
            }

            const userIdInput = document.getElementById('userId');
            const userEmailInput = document.getElementById('userEmail');
            if (!userIdInput || !userEmailInput) {
                return;
            }

            userIdInput.value = trigger.getAttribute('data-user-id') || '';
            userEmailInput.value = trigger.getAttribute('data-user-email') || '';
        });
    });
})();
