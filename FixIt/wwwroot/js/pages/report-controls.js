// Report flow: mirror the category pick-grid + severity segmented control onto
// the real (visually-hidden) <select> elements that power model binding + AI.
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
        var root = document.getElementById('issueCreateRoot');
        if (!root) {
            return;
        }

        function bindGroup(groupId, selectId) {
            var group = document.getElementById(groupId);
            var select = document.getElementById(selectId);
            if (!group || !select) {
                return;
            }

            var buttons = Array.prototype.slice.call(group.querySelectorAll('button[data-value]'));

            function reflect() {
                var value = select.value;
                buttons.forEach(function (btn) {
                    btn.classList.toggle('on', btn.getAttribute('data-value') === value);
                });
            }

            group.addEventListener('click', function (e) {
                var btn = e.target.closest('button[data-value]');
                if (!btn) {
                    return;
                }
                select.value = btn.getAttribute('data-value');
                // A real 'change' marks the field as user-overridden in issue-create.js.
                select.dispatchEvent(new Event('change', { bubbles: true }));
                reflect();
            });

            // AI-applied values dispatch 'fixit:sync' (no override); native change covers the rest.
            select.addEventListener('fixit:sync', reflect);
            select.addEventListener('change', reflect);

            reflect();
        }

        bindGroup('categoryPick', 'issueCategory');
        bindGroup('severitySeg', 'issuePriority');
    });
})();
