/**
 * Media Lightbox Module
 * Handles full-screen image gallery with keyboard and click navigation
 */
const MediaLightbox = (() => {
    'use strict';

    const root = document.getElementById('issueDetailRoot');
    const labels = {
        viewer: root?.dataset.lightboxLabel || 'Image viewer',
        close: root?.dataset.closeLabel || 'Close',
        previous: root?.dataset.previousLabel || 'Previous',
        next: root?.dataset.nextLabel || 'Next',
        photo: root?.dataset.photoLabel || 'Photo'
    };

    let images = [];
    let currentIndex = 0;
    let modal = null;

    function init() {
        const thumbs = document.querySelectorAll('#mediaGrid [data-media-src]');
        images = Array.from(thumbs).map((thumb) => thumb.dataset.mediaSrc).filter(Boolean);
        if (images.length === 0) return;

        thumbs.forEach((thumb, index) => {
            thumb.addEventListener('click', () => open(index));
        });

        modal = document.createElement('div');
        modal.id = 'sharedMediaModal';
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-modal', 'true');
        modal.setAttribute('aria-label', labels.viewer);
        modal.innerHTML = `
            <button class="modal-close-btn" id="lbClose" title="${labels.close}">
                <i class="bi bi-x-lg"></i>
            </button>
            <button class="modal-nav-btn modal-nav-prev" id="lbPrev" title="${labels.previous}">
                <i class="bi bi-chevron-left"></i>
            </button>
            <div class="modal-img-wrap">
                <img id="lbImg" src="" alt="">
            </div>
            <button class="modal-nav-btn modal-nav-next" id="lbNext" title="${labels.next}">
                <i class="bi bi-chevron-right"></i>
            </button>
            <div class="modal-counter" id="lbCounter"></div>
        `;
        document.body.appendChild(modal);

        document.getElementById('lbClose').addEventListener('click', close);
        document.getElementById('lbPrev').addEventListener('click', (event) => {
            event.stopPropagation();
            navigate(-1);
        });
        document.getElementById('lbNext').addEventListener('click', (event) => {
            event.stopPropagation();
            navigate(1);
        });

        modal.addEventListener('click', (event) => {
            if (event.target === modal) close();
        });

        document.addEventListener('keydown', (event) => {
            if (!modal?.classList.contains('show')) return;
            if (event.key === 'Escape') close();
            if (event.key === 'ArrowLeft') navigate(-1);
            if (event.key === 'ArrowRight') navigate(1);
        });
    }

    function open(index) {
        currentIndex = index;
        render();
        modal.classList.add('show');
        document.body.style.overflow = 'hidden';
    }

    function close() {
        if (!modal) return;
        modal.classList.remove('show');
        document.body.style.overflow = '';
    }

    function navigate(direction) {
        currentIndex = (currentIndex + direction + images.length) % images.length;
        render();
    }

    function render() {
        const image = document.getElementById('lbImg');
        const counter = document.getElementById('lbCounter');
        const previous = document.getElementById('lbPrev');
        const next = document.getElementById('lbNext');
        if (!image || !counter || !previous || !next) return;

        image.src = images[currentIndex];
        image.alt = `${labels.photo} ${currentIndex + 1}`;
        counter.textContent = `${currentIndex + 1} / ${images.length}`;
        previous.classList.toggle('hidden', images.length <= 1);
        next.classList.toggle('hidden', images.length <= 1);
    }

    return { init, open };
})();

window.openLightbox = (index) => MediaLightbox.open(index);

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => MediaLightbox.init(), { once: true });
} else {
    MediaLightbox.init();
}
