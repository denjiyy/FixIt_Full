// Back-button handler for the 404 page. Extracted from an inline <script>
// block so the page contributes less to CSP script-src surface area.
document.addEventListener('DOMContentLoaded', function () {
    const backButton = document.querySelector('[data-history-back]');
    if (backButton) {
        backButton.addEventListener('click', function () {
            if (window.history.length > 1) {
                window.history.back();
            } else {
                window.location.href = '/';
            }
        });
    }
});
