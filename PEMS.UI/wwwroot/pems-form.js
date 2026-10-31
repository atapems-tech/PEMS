// PEMS.UI form shell (SCRUM-352). Dirty-state guard: a form with data-pems-dirty-guard asks the browser to confirm leaving the page
// once a value was changed. The browser shows its own standard text, so the library holds no message. Submitting the form is not leaving.
(function () {
    'use strict';

    function init(scope) {
        (scope || document).querySelectorAll('form[data-pems-dirty-guard]').forEach(function (form) {
            if (form.dataset.pemsInit) return;
            form.dataset.pemsInit = '1';
            var dirty = false;
            form.addEventListener('input', function () { dirty = true; });
            form.addEventListener('change', function () { dirty = true; });
            form.addEventListener('submit', function () { dirty = false; });
            form.addEventListener('reset', function () { dirty = false; });
            window.addEventListener('beforeunload', function (e) {
                if (!dirty) return;
                e.preventDefault();
                e.returnValue = '';
            });
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { init(); });
    else init();
    window.PemsForm = { init: init };
})();
