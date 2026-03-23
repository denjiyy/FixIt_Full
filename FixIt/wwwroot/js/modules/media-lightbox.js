/**
 * Media Lightbox Module
 * Handles full-screen image gallery with keyboard and click navigation
 */
const MediaLightbox = (() => {
    'use strict';

    let images = [];
    let currentIndex = 0;
    let modal = null;

    function init() {
        // Collect all image srcs from the grid
        const thumbs = document.querySelectorAll('#mediaGrid .media-thumb');
        images = Array.from(thumbs).map(t => t.dataset.mediaSrc);

        if (images.length === 0) return;

        // Build modal and append to body (NOT inside any transformed container)
        modal = document.createElement('div');
        modal.id = 'sharedMediaModal';
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-modal', 'true');
        modal.setAttribute('aria-label', 'Image viewer');
        modal.innerHTML = `
            <button class="modal-close-btn" id="lbClose" title="Close (Esc)">
                <i class="bi bi-x-lg"></i>
            </button>
            <button class="modal-nav-btn modal-nav-prev" id="lbPrev" title="Previous">
                <i class="bi bi-chevron-left"></i>
            </button>
            <div class="modal-img-wrap">
                <img id="lbImg" src="" alt="Full-size photo">
            </div>
            <button class="modal-nav-btn modal-nav-next" id="lbNext" title="Next">
                <i class="bi bi-chevron-right"></i>
            </button>
            <div class="modal-counter" id="lbCounter"></div>
        `;
        document.body.appendChild(modal);

        // Event listeners
        document.getElementById('lbClose').addEventListener('click', close);
        document.getElementById('lbPrev').addEventListener('click', (e) => { e.stopPropagation(); navigate(-1); });
        document.getElementById('lbNext').addEventListener('click', (e) => { e.stopPropagation(); navigate(1); });

        // Click outside image closes modal
        modal.addEventListener('click', (e) => {
            if (e.target === modal) close();
        });

        // Keyboard navigation
        document.addEventListener('keydown', (e) => {
            if (!modal?.classList.contains('show')) return;
            if (e.key === 'Escape') close();
            if (e.key === 'ArrowLeft') navigate(-1);
            if (e.key === 'ArrowRight') navigate(1);
        });
    }

    function open(index) {
        currentIndex = index;
        render();
        modal.classList.add('show');
        document.body.style.overflow = 'hidden';
    }

    function close() {
        modal.classList.remove('show');
        document.body.style.overflow = '';
    }

    function navigate(dir) {
        currentIndex = (currentIndex + dir + images.length) % images.length;
        render();
    }

    function render() {
        const img = document.getElementById('lbImg');
        const counter = document.getElementById('lbCounter');
        const prev = document.getElementById('lbPrev');
        const next = document.getElementById('lbNext');

        // Reset animation by cloning the img-wrap
        const wrap = img.parentElement;
        wrap.style.animation = 'none';
        wrap.offsetHeight; // reflow
        wrap.style.animation = '';

        img.src = images[currentIndex];
        img.alt = `Photo ${currentIndex + 1} of ${images.length}`;
        counter.textContent = `${currentIndex + 1} / ${images.length}`;

        // Hide nav arrows when only one image
        const single = images.length <= 1;
        prev.classList.toggle('hidden', single);
        next.classList.toggle('hidden', single);
    }

    return { init, open };
})();

// Attach to window for use in onclick handlers
window.openLightbox = (index) => MediaLightbox.open(index);

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => MediaLightbox.init());
} else {
    MediaLightbox.init();
}
