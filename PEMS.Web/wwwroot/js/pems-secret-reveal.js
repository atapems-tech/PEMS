// Copy / print buttons for a secret that is shown exactly once (SCRUM-354: temporary admin password).
// Markup contract: a container with [data-pems-secret-reveal] holding [data-secret-value], [data-secret-copy] (optional data-copied-text) and [data-secret-print].
(function () {
    'use strict';

    function init(root) {
        if (root.dataset.pemsInit === '1') return;
        root.dataset.pemsInit = '1';

        var value = root.querySelector('[data-secret-value]');
        var copy = root.querySelector('[data-secret-copy]');
        var print = root.querySelector('[data-secret-print]');

        if (copy && value) {
            copy.addEventListener('click', function () {
                var text = value.textContent.trim();
                var done = function () {
                    var copied = copy.getAttribute('data-copied-text');
                    if (copied) { copy.textContent = copied; }
                };
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(text).then(done, function () { /* the value stays selectable (user-select-all) */ });
                }
            });
        }

        if (print) {
            print.addEventListener('click', function () { window.print(); });
        }
    }

    function boot() {
        document.querySelectorAll('[data-pems-secret-reveal]').forEach(init);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})();
