// Community leaderboard: period segmented control (weekly / monthly / all-time).
(function () {
    'use strict';

    function ready(cb) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', cb, { once: true });
        } else {
            cb();
        }
    }

    ready(function () {
        var root = document.getElementById('communityRoot');
        var seg = document.getElementById('communityPeriod');
        if (!root || !seg) {
            return;
        }

        var panels = Array.prototype.slice.call(root.querySelectorAll('.board__panel'));

        function setPeriod(period) {
            panels.forEach(function (panel) {
                panel.hidden = panel.getAttribute('data-period') !== period;
            });
            Array.prototype.forEach.call(seg.querySelectorAll('button[data-period]'), function (btn) {
                btn.classList.toggle('on', btn.getAttribute('data-period') === period);
            });
            try {
                window.localStorage.setItem('fixit.leaderboardPeriod', period);
            } catch (e) {
                /* ignore storage failures */
            }
        }

        seg.addEventListener('click', function (e) {
            var btn = e.target.closest('button[data-period]');
            if (!btn) {
                return;
            }
            setPeriod(btn.getAttribute('data-period'));
        });

        var saved = 'monthly';
        try {
            saved = window.localStorage.getItem('fixit.leaderboardPeriod') || 'monthly';
        } catch (e) {
            saved = 'monthly';
        }
        // Only honour a saved period if its panel actually exists.
        if (!root.querySelector('.board__panel[data-period="' + saved + '"]')) {
            saved = 'monthly';
        }
        setPeriod(saved);
    });
})();
