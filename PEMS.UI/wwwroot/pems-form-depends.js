// PEMS.UI dependent select (SCRUM-826): <select data-pems-depends-on="parentField" data-pems-depends-source="data-source-key" data-pems-depends-arg="argName"> is written by the form field component.
// The select is rendered with the full list (what the server validates against). When the parent field has a value the choices are the rows of GET /d/{source}?f.{arg}={value} ({ items: [{ value, label }] });
// without a value the full list is back. The chosen value stays when it is still a choice. No markup is written here other than <option> elements.
(function () {
    'use strict';

    function rowsUrl(source, arg, value) {
        return '/d/' + encodeURIComponent(source) + '?pageSize=500&f.' + encodeURIComponent(arg) + '=' + encodeURIComponent(value);
    }

    function setOptions(select, items, keep) {
        var blank = select.querySelector('option[value=""]');
        select.textContent = '';
        if (blank) select.appendChild(blank);
        items.forEach(function (it) {
            var o = document.createElement('option');
            o.value = it.value; o.textContent = it.label;
            select.appendChild(o);
        });
        if (keep && Array.prototype.some.call(select.options, function (o) { return o.value === keep; })) select.value = keep;
        else select.value = '';
        select.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function attach(select) {
        if (select.dataset.pemsDependsInit) return;
        select.dataset.pemsDependsInit = '1';
        var form = select.form || document;
        var parent = form.querySelector('[name="' + select.getAttribute('data-pems-depends-on') + '"]');
        var source = select.getAttribute('data-pems-depends-source');
        var arg = select.getAttribute('data-pems-depends-arg');
        if (!parent || !source || !arg) return;
        var full = Array.prototype.filter.call(select.options, function (o) { return o.value !== ''; }).map(function (o) { return { value: o.value, label: o.textContent }; });
        var token = 0;
        function refresh(initial) {
            var chosen = select.value;
            var value = parent.value;
            var mine = ++token;
            if (!value) { setOptions(select, full, chosen); return; }
            fetch(rowsUrl(source, arg, value), { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
                .then(function (data) { if (mine === token) setOptions(select, (data && data.items) || [], chosen); })
                .catch(function () { if (mine === token) setOptions(select, full, chosen); });
        }
        parent.addEventListener('change', function () { refresh(false); });
        refresh(true);
    }

    function scan(scope) { (scope || document).querySelectorAll('select[data-pems-depends-on]').forEach(attach); }
    window.PemsFormDepends = { scan: scan };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { scan(); }); else scan();
})();
