/* Part Catalogue: search existing parts + MPN duplicate check (read-only; never merges anything).
   Server is the authority; this only shows candidates. DOM is built with textContent (no innerHTML). */
(function () {
    'use strict';

    function init() {
        var root = document.getElementById('pcDedup');
        if (!root || root.dataset.pemsInit === '1') return;
        root.dataset.pemsInit = '1';

        var out = document.getElementById('pcDedupResult');
        var queryInput = document.getElementById('pcDedupQuery');
        var searchBtn = document.getElementById('pcDedupSearch');
        var checkBtn = document.getElementById('pcDedupCheck');
        var txt = {
            none: root.dataset.txtNone || '',
            ambiguous: root.dataset.txtAmbiguous || '',
            inflight: root.dataset.txtInflight || '',
            error: root.dataset.txtError || ''
        };

        function clear() { while (out.firstChild) out.removeChild(out.firstChild); }

        function note(message, cls) {
            var p = document.createElement('div');
            p.className = cls || 'text-muted';
            p.textContent = message;
            out.appendChild(p);
        }

        function render(hits) {
            if (!hits.length) { note(txt.none); return; }
            var ul = document.createElement('ul');
            ul.className = 'list-unstyled mb-0';
            hits.forEach(function (h) {
                var li = document.createElement('li');
                li.className = 'border-bottom py-1';
                var code = document.createElement('strong');
                code.className = 'vs-num';
                code.textContent = h.partNumber || '';
                li.appendChild(code);
                var meta = document.createElement('span');
                meta.className = 'text-muted ms-2';
                meta.textContent = [h.matchKind, h.matchedOn, h.manufacturerName, h.manufacturerPartNumber]
                    .filter(Boolean).join(' · ');
                li.appendChild(meta);
                if (h.description) {
                    var d = document.createElement('div');
                    d.textContent = h.description;
                    li.appendChild(d);
                }
                ul.appendChild(li);
            });
            out.appendChild(ul);
        }

        function getJson(url) {
            return fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (r) { if (!r.ok) throw new Error(String(r.status)); return r.json(); });
        }

        function val(id) { var el = document.getElementById(id); return el ? el.value.trim() : ''; }

        searchBtn.addEventListener('click', function () {
            clear();
            var q = queryInput.value.trim();
            if (q.length < 2) return;
            getJson(root.dataset.urlExisting + '?q=' + encodeURIComponent(q))
                .then(render)
                .catch(function () { note(txt.error, 'text-danger'); });
        });

        queryInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); searchBtn.click(); }
        });

        checkBtn.addEventListener('click', function () {
            clear();
            var params = new URLSearchParams();
            params.set('manufacturerPartNumber', val('pc_ManufacturerPartNumber'));
            params.set('supplierPartNumber', val('pc_SupplierPartNumber'));
            var mfrId = document.querySelector('input[name="ManufacturerId"]');
            if (mfrId && mfrId.value) params.set('manufacturerId', mfrId.value);
            params.set('newManufacturerName', val('pc_NewManufacturerName'));
            if (root.dataset.requestId) params.set('excludeRequestId', root.dataset.requestId);
            getJson(root.dataset.urlDuplicates + '?' + params.toString())
                .then(function (res) {
                    if (res.isAmbiguous) note(txt.ambiguous, 'text-warning');
                    render(res.candidates || []);
                    if (res.inFlightRequestsWithSameMpn > 0) note(txt.inflight + ' ' + res.inFlightRequestsWithSameMpn, 'text-warning');
                })
                .catch(function () { note(txt.error, 'text-danger'); });
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
