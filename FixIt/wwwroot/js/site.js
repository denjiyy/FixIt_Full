// ===================================================
// FixIt - Enhanced JavaScript Interactions
// ===================================================

(function() {
    'use strict';

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        initSmoothScrolling();
        initAnimateOnScroll();
        initTooltips();
        initCardHoverEffects();
        initFormValidationFeedback();
        initLoadingStates();
    });

    // ===================================================
    // Smooth Scrolling for Anchor Links
    // ===================================================
    function initSmoothScrolling() {
        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', function (e) {
                const href = this.getAttribute('href');
                if (href !== '#' && href !== '#!') {
                    e.preventDefault();
                    const target = document.querySelector(href);
                    if (target) {
                        target.scrollIntoView({
                            behavior: 'smooth',
                            block: 'start'
                        });
                    }
                }
            });
        });
    }

    // ===================================================
    // Animate Elements on Scroll
    // ===================================================
    function initAnimateOnScroll() {
        const observerOptions = {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        };

        const observer = new IntersectionObserver(function(entries) {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('aos-animate');
                }
            });
        }, observerOptions);

        // Add animation classes to cards and sections
        document.querySelectorAll('.city-card, .issue-card, .profile-card, .feature-card').forEach(el => {
            el.classList.add('aos-element');
            observer.observe(el);
        });
    }

    // Add CSS for animations
    const style = document.createElement('style');
    style.textContent = `
        .aos-element {
            opacity: 0;
            transform: translateY(30px);
            transition: opacity 0.6s ease-out, transform 0.6s ease-out;
        }
        
        .aos-element.aos-animate {
            opacity: 1;
            transform: translateY(0);
        }

        /* Stagger animation delays */
        .aos-element:nth-child(1) { transition-delay: 0.1s; }
        .aos-element:nth-child(2) { transition-delay: 0.2s; }
        .aos-element:nth-child(3) { transition-delay: 0.3s; }
        .aos-element:nth-child(4) { transition-delay: 0.4s; }
        .aos-element:nth-child(5) { transition-delay: 0.5s; }
        .aos-element:nth-child(6) { transition-delay: 0.6s; }
    `;
    document.head.appendChild(style);

    // ===================================================
    // Initialize Bootstrap Tooltips
    // ===================================================
    function initTooltips() {
        // Check if Bootstrap is loaded
        if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
            const tooltipTriggerList = [].slice.call(
                document.querySelectorAll('[data-bs-toggle="tooltip"], [title]')
            );
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }
    }

    // ===================================================
    // Card Hover Effects
    // ===================================================
    function initCardHoverEffects() {
        document.querySelectorAll('.city-card, .issue-card').forEach(card => {
            card.addEventListener('mouseenter', function() {
                this.style.zIndex = '10';
            });
            
            card.addEventListener('mouseleave', function() {
                this.style.zIndex = '1';
            });
        });
    }

    // ===================================================
    // Form Validation Feedback
    // ===================================================
    function initFormValidationFeedback() {
        // Add real-time validation feedback
        document.querySelectorAll('input, textarea, select').forEach(field => {
            field.addEventListener('blur', function() {
                if (this.hasAttribute('required') || this.hasAttribute('data-val-required')) {
                    if (!this.value.trim()) {
                        this.classList.add('is-invalid');
                        this.classList.remove('is-valid');
                    } else {
                        this.classList.add('is-valid');
                        this.classList.remove('is-invalid');
                    }
                }
            });

            field.addEventListener('input', function() {
                if (this.classList.contains('is-invalid') && this.value.trim()) {
                    this.classList.remove('is-invalid');
                    this.classList.add('is-valid');
                }
            });
        });
    }

    // ===================================================
    // Loading States for Buttons
    // ===================================================
    function initLoadingStates() {
        document.querySelectorAll('form').forEach(form => {
            form.addEventListener('submit', function(e) {
                const submitBtn = this.querySelector('button[type="submit"], input[type="submit"]');
                if (submitBtn && !submitBtn.disabled) {
                    const originalText = submitBtn.innerHTML;
                    submitBtn.disabled = true;
                    submitBtn.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Processing...';
                    
                    // Add spinning animation
                    const spinStyle = document.createElement('style');
                    spinStyle.textContent = `
                        @keyframes spin {
                            from { transform: rotate(0deg); }
                            to { transform: rotate(360deg); }
                        }
                        .spin {
                            display: inline-block;
                            animation: spin 1s linear infinite;
                        }
                    `;
                    if (!document.querySelector('#spin-style')) {
                        spinStyle.id = 'spin-style';
                        document.head.appendChild(spinStyle);
                    }
                }
            });
        });
    }

    // ===================================================
    // Utility Functions
    // ===================================================

    // Show toast notification
    window.showToast = function(message, type = 'info') {
        const toastContainer = getOrCreateToastContainer();
        const toast = createToastElement(message, type);
        toastContainer.appendChild(toast);
        
        // Trigger animation
        setTimeout(() => toast.classList.add('show'), 100);
        
        // Auto remove
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    };

    function getOrCreateToastContainer() {
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 9999;
                display: flex;
                flex-direction: column;
                gap: 10px;
            `;
            document.body.appendChild(container);
        }
        return container;
    }

    function createToastElement(message, type) {
        const toast = document.createElement('div');
        const colors = {
            success: 'linear-gradient(135deg, #10b981, #059669)',
            error: 'linear-gradient(135deg, #ef4444, #dc2626)',
            warning: 'linear-gradient(135deg, #f59e0b, #d97706)',
            info: 'linear-gradient(135deg, #3b82f6, #2563eb)'
        };
        const icons = {
            success: 'check-circle-fill',
            error: 'x-circle-fill',
            warning: 'exclamation-triangle-fill',
            info: 'info-circle-fill'
        };
        
        toast.style.cssText = `
            background: ${colors[type] || colors.info};
            color: white;
            padding: 1rem 1.5rem;
            border-radius: 12px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
            min-width: 300px;
            opacity: 0;
            transform: translateX(400px);
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
            display: flex;
            align-items: center;
            gap: 0.75rem;
            font-weight: 500;
        `;
        
        toast.classList.add('toast-notification');
        toast.innerHTML = `
            <i class="bi bi-${icons[type] || icons.info}" style="font-size: 1.5rem;"></i>
            <span>${message}</span>
        `;
        
        toast.classList.add('show');
        return toast;
    }

    // Add show class styles
    const toastStyle = document.createElement('style');
    toastStyle.textContent = `
        .toast-notification.show {
            opacity: 1 !important;
            transform: translateX(0) !important;
        }
    `;
    document.head.appendChild(toastStyle);

    // Confirm before delete
    window.confirmDelete = function(message) {
        return confirm(message || 'Are you sure you want to delete this item?');
    };

    // Copy to clipboard
    window.copyToClipboard = function(text) {
        navigator.clipboard.writeText(text).then(() => {
            showToast('Copied to clipboard!', 'success');
        }).catch(() => {
            showToast('Failed to copy', 'error');
        });
    };

    // ===================================================
    // Page Load Performance
    // ===================================================
    window.addEventListener('load', function() {
        // Log page load time
        const perfData = performance.timing;
        const pageLoadTime = perfData.loadEventEnd - perfData.navigationStart;
        console.log(`Page loaded in ${pageLoadTime}ms`);
    });

    // ===================================================
    // Error Handling
    // ===================================================
    window.addEventListener('error', function(e) {
        console.error('Global error:', e.message);
    });

})();