(function () {
    'use strict';

    const onReady = (callback) => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    };

    const delegate = (root, eventName, selector, handler) => {
        root.addEventListener(eventName, (event) => {
            const target = event.target.closest(selector);
            if (!target || !root.contains(target)) {
                return;
            }

            handler(event, target);
        });
    };

    const safeParseJson = (elementId, fallback = null) => {
        const element = document.getElementById(elementId);
        if (!element) {
            return fallback;
        }

        try {
            return JSON.parse(element.textContent || '{}');
        } catch {
            return fallback;
        }
    };

    window.FixItApp = window.FixItApp || {};
    window.FixItApp.onReady = onReady;
    window.FixItApp.delegate = delegate;
    window.FixItApp.safeParseJson = safeParseJson;

    onReady(() => {
        initAppToasts();
        initPublicShell();
        initAdminShell();
        initScrollTop();
        initActiveNavigation();
        initSmoothAnchors();
        initProgressWidths();
        initConfirmActions();
        initHistoryActions();
        initAutoSubmitControls();
    });

    function initAppToasts() {
        let toastRegion = document.getElementById('appToastRegion');
        if (!toastRegion) {
            toastRegion = document.createElement('div');
            toastRegion.id = 'appToastRegion';
            toastRegion.className = 'app-toast-region';
            toastRegion.setAttribute('aria-live', 'polite');
            toastRegion.setAttribute('aria-atomic', 'false');
            document.body.appendChild(toastRegion);
        }

        window.FixItNotify = (message, variant = 'info', timeoutMs = 4200) => {
            if (!message) {
                return;
            }

            const tone = ['success', 'error', 'warning', 'info'].includes(variant) ? variant : 'info';
            const toast = document.createElement('div');
            toast.className = `app-toast app-toast--${tone}`;
            toast.setAttribute('role', 'status');

            const icon = document.createElement('i');
            icon.className = tone === 'success'
                ? 'bi bi-check-circle-fill'
                : tone === 'error'
                    ? 'bi bi-exclamation-octagon-fill'
                    : tone === 'warning'
                        ? 'bi bi-exclamation-triangle-fill'
                        : 'bi bi-info-circle-fill';
            icon.setAttribute('aria-hidden', 'true');

            const text = document.createElement('span');
            text.textContent = message;

            const closeButton = document.createElement('button');
            closeButton.type = 'button';
            closeButton.className = 'app-toast__close';
            closeButton.setAttribute('aria-label', 'Dismiss notification');
            closeButton.innerHTML = '<i class="bi bi-x-lg" aria-hidden="true"></i>';
            closeButton.addEventListener('click', () => toast.remove());

            toast.appendChild(icon);
            toast.appendChild(text);
            toast.appendChild(closeButton);
            toastRegion.appendChild(toast);

            window.setTimeout(() => {
                toast.style.opacity = '0';
                toast.style.transform = 'translateY(-4px)';
                window.setTimeout(() => toast.remove(), 160);
            }, Math.max(timeoutMs, 1500));
        };
    }

    function initPublicShell() {
        const header = document.getElementById('shellHeader');
        if (!header) {
            return;
        }

        const syncHeaderState = () => {
            header.classList.toggle('is-scrolled', window.scrollY > 10);
        };

        syncHeaderState();
        window.addEventListener('scroll', syncHeaderState, { passive: true });
    }

    function initAdminShell() {
        const sidebar = document.getElementById('adminSidebar');
        if (!sidebar) {
            return;
        }

        const toggleButtons = document.querySelectorAll('[data-admin-toggle]');
        const closeButtons = document.querySelectorAll('[data-admin-close]');
        const overlay = document.querySelector('.admin-overlay');
        const root = document.body;

        const setOpen = (open) => {
            root.classList.toggle('admin-sidebar-open', open);
            if (overlay) {
                overlay.hidden = !open;
            }
            toggleButtons.forEach((button) => {
                button.setAttribute('aria-expanded', String(open));
            });
        };

        toggleButtons.forEach((button) => {
            button.addEventListener('click', () => {
                setOpen(!root.classList.contains('admin-sidebar-open'));
            });
        });

        closeButtons.forEach((button) => {
            button.addEventListener('click', () => setOpen(false));
        });

        window.addEventListener('resize', () => {
            if (window.innerWidth >= 1200) {
                setOpen(false);
            }
        });
    }

    function initScrollTop() {
        const button = document.getElementById('scrollTopBtn');
        if (!button) {
            return;
        }

        const syncState = () => {
            button.classList.toggle('is-visible', window.scrollY > 480);
        };

        syncState();
        window.addEventListener('scroll', syncState, { passive: true });
        button.addEventListener('click', () => {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    function initActiveNavigation() {
        const currentPath = (document.body.dataset.currentPath || window.location.pathname || '/')
            .toLowerCase()
            .replace(/\/$/, '') || '/';

        const candidates = document.querySelectorAll('.app-nav-link, .admin-nav__link');
        candidates.forEach((link) => {
            const href = (link.getAttribute('href') || '').toLowerCase().replace(/\/$/, '');
            if (!href || href === '#') {
                return;
            }

            const matches = href === '/'
                ? currentPath === '/'
                : currentPath === href || currentPath.startsWith(href + '/');

            if (matches) {
                link.classList.add('is-active');
                link.setAttribute('aria-current', 'page');
            }
        });
    }

    function initSmoothAnchors() {
        document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
            anchor.addEventListener('click', (event) => {
                const href = anchor.getAttribute('href');
                if (!href || href === '#' || href === '#!') {
                    return;
                }

                const target = document.querySelector(href);
                if (!target) {
                    return;
                }

                event.preventDefault();
                target.scrollIntoView({
                    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                    block: 'start'
                });
            });
        });
    }

    function initProgressWidths() {
        document.querySelectorAll('[data-progress-width]').forEach((element) => {
            const width = Number.parseFloat(element.dataset.progressWidth || '');
            if (!Number.isNaN(width)) {
                element.style.width = `${width}%`;
            }
        });
    }

    function initConfirmActions() {
        delegate(document, 'click', '[data-confirm-message]', (event, trigger) => {
            const message = trigger.getAttribute('data-confirm-message');
            if (!message) {
                return;
            }

            if (!window.confirm(message)) {
                event.preventDefault();
                event.stopPropagation();
            }
        });
    }

    function initHistoryActions() {
        delegate(document, 'click', '[data-history-back]', (event) => {
            event.preventDefault();
            window.history.back();
        });
    }

    function initAutoSubmitControls() {
        delegate(document, 'change', '[data-auto-submit="change"]', (_, trigger) => {
            const form = trigger.closest('form');
            if (!form) {
                return;
            }

            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit();
            } else {
                form.submit();
            }
        });
    }
})();
