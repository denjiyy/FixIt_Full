(function () {
    'use strict';

    const onReady = window.FixItApp?.onReady || ((callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    });

    function wireCounter(inputId, outputId, maxLength) {
        const input = document.getElementById(inputId);
        const output = document.getElementById(outputId);
        if (!input || !output) return;

        const update = () => {
            output.textContent = String(input.value.length);
            output.parentElement?.classList.toggle('is-warning', input.value.length >= maxLength * 0.85);
        };

        input.addEventListener('input', update);
        update();
    }

    onReady(() => {
        if (!document.getElementById('issueEditRoot')) return;
        wireCounter('Input_Title', 'charCount', 200);
        wireCounter('Input_Description', 'descCharCount', 5000);
    });
})();
