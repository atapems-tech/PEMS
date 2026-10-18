// Organization Chart (Position hierarchy). Read-only: loads one JSON DTO from Admin/Organization/ChartData and hands it to the
// vendored d3-org-chart library (layout, zoom, pan, expand/collapse are all the library's). No data is ever written from here.
(function () {
    "use strict";
    var root = document.getElementById("pemsOpcRoot");
    if (!root || !window.d3 || !d3.OrgChart) return;

    var T = function (k) { return root.getAttribute("data-t-" + k) || k; };
    var canvas = document.getElementById("pemsOpcCanvas");
    var stateEl = document.getElementById("pemsOpcState");
    var panelEl = document.getElementById("pemsOpcPanel");
    var warnEl = document.getElementById("pemsOpcWarnings");
    var chart = null, byId = {}, nodes = [], selectedId = null;

    function esc(v) {
        return String(v == null ? "" : v).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; });
    }
    function showState(msg) { stateEl.textContent = msg; stateEl.hidden = false; canvas.hidden = true; }
    function hideState() { stateEl.hidden = true; canvas.hidden = false; }

    function nodeHtml(d) {
        var n = d.data || d;
        if (n.nodeKind === "container") return '<div class="pems-opc-node kind-container">' + esc(T("container")) + " (" + n.childCount + ")</div>";
        var cls = "pems-opc-node kind-" + esc(n.nodeKind) + (n.isVacant ? " is-vacant" : "") + (n.id === selectedId ? " is-selected" : "");
        var holder = n.isVacant ? '<div class="pems-opc-vacant">' + esc(T("vacant")) + "</div>"
            : '<div class="pems-opc-emp">' + esc(n.employeeName) + "</div>";
        var badge = (n.nodeKind === "orphan" || n.nodeKind === "cycle" || (n.issueCodes && n.issueCodes.length))
            ? ' <span class="pems-opc-badge" title="' + esc((n.issueCodes || []).join(", ")) + '">&#9888; ' + esc(T(n.nodeKind === "cycle" ? "cycle" : n.nodeKind === "orphan" ? "orphan" : "issue")) + "</span>" : "";
        return '<div class="' + cls + '" role="button" tabindex="0" aria-label="' + esc(n.positionTitle) + '">' +
            '<div><div class="pems-opc-label">' + esc(T("position")) + badge + "</div>" +
            '<div class="pems-opc-title">' + esc(n.positionTitle) + "</div></div>" + holder +
            '<div class="pems-opc-unit">' + esc(n.departmentName || n.teamName || "") + "</div></div>";
    }

    function showDetails(n) {
        if (!n || n.nodeKind === "container") { panelEl.innerHTML = '<p class="text-muted small mb-0">' + esc(T("select")) + "</p>"; return; }
        var parent = n.parentId && byId[n.parentId] && byId[n.parentId].nodeKind !== "container" ? byId[n.parentId] : null;
        var reports = nodes.filter(function (x) { return x.parentId === n.id; });
        var li = reports.map(function (r) { return "<li>" + esc(r.positionTitle) + " — " + esc(r.isVacant ? T("vacant") : r.employeeName) + "</li>"; }).join("");
        var kind = { root: T("validRoot"), position: "", orphan: T("orphan"), cycle: T("cycle") }[n.nodeKind] || "";
        panelEl.innerHTML =
            "<h2 class='h6 mb-2'>" + esc(n.positionTitle) + (n.positionCode ? " <small class='text-muted'>[" + esc(n.positionCode) + "]</small>" : "") + "</h2><dl>" +
            "<dt>" + esc(T("employee")) + "</dt><dd>" + (n.isVacant ? esc(T("vacant")) : esc(n.employeeName) + (n.holderIsPrimary ? "" : " (" + esc(T("secondary")) + ")")) + "</dd>" +
            (n.assignmentFrom ? "<dt>" + esc(T("assignment")) + "</dt><dd>" + esc(n.assignmentFrom) + " → " + esc(n.assignmentTo || "—") + "</dd>" : "") +
            "<dt>" + esc(T("unit")) + "</dt><dd>" + esc(n.departmentName || "—") + "</dd>" +
            (n.teamName ? "<dt>" + esc(T("team")) + "</dt><dd>" + esc(n.teamName) + "</dd>" : "") +
            "<dt>" + esc(T("parent")) + "</dt><dd>" + (parent ? esc(parent.positionTitle) : "—") + (kind ? " <span class='text-muted'>(" + esc(kind) + ")</span>" : "") + "</dd>" +
            "<dt>" + esc(T("reports")) + " (" + reports.length + ")</dt><dd>" + (li ? "<ul>" + li + "</ul>" : "—") + "</dd>" +
            ((n.issueCodes && n.issueCodes.length) ? "<dt>" + esc(T("issues")) + "</dt><dd>" + esc(n.issueCodes.join(", ")) + "</dd>" : "") +
            "</dl><div class='mt-3'><a class='btn btn-outline-secondary btn-sm' href='" + esc(root.getAttribute("data-manage-url")) + "'>" + esc(T("manage")) + "</a></div>";
    }

    function select(id) {
        selectedId = id;
        showDetails(byId[id]);
        if (chart) chart.render();
    }

    function build(dto) {
        nodes = dto.nodes || [];
        byId = {}; nodes.forEach(function (n) { byId[n.id] = n; });
        if (!nodes.length) { showState(T("empty")); return; }
        hideState();

        var warns = (dto.warnings || []).map(function (w) { return esc(T(w.code === "HIERARCHY_CYCLE" ? "cycle" : "orphan")) + ": " + w.count; });
        warnEl.hidden = warns.length === 0;
        warnEl.innerHTML = warns.join(" · ");

        chart = new d3.OrgChart()
            .container("#pemsOpcCanvas")
            .data(nodes.map(function (n) { return Object.assign({}, n, { parentId: n.parentId || null }); }))
            .svgHeight(canvas.clientHeight || 520)
            .nodeWidth(function (d) { return (d.data.nodeKind === "container") ? 150 : 230; })
            .nodeHeight(function (d) { return (d.data.nodeKind === "container") ? 44 : 118; })
            .childrenMargin(function () { return 50; })
            .compactMarginBetween(function () { return 24; })
            .compactMarginPair(function () { return 40; })
            .neighbourMargin(function () { return 24; })
            .initialExpandLevel(2)
            .nodeContent(nodeHtml)
            .buttonContent(function (o) {
                var n = o.node;
                return '<div class="pems-opc-btn">' + (n.children ? "−" : "+") + " " + (n.data._directSubordinates || "") + "</div>";
            })
            .onNodeClick(function (d) { var n = d.data || d; select(n.id); })
            .render();
        chart.fit();
    }

    function bind(id, fn) { var el = document.getElementById(id); if (el) el.addEventListener("click", fn); }
    bind("pemsOpcFit", function () { if (chart) chart.fit(); });
    bind("pemsOpcZoomIn", function () { if (chart) chart.zoomIn(); });
    bind("pemsOpcZoomOut", function () { if (chart) chart.zoomOut(); });
    bind("pemsOpcExpand", function () { if (chart) { chart.expandAll(); chart.fit(); } });
    bind("pemsOpcCollapse", function () { if (chart) { chart.collapseAll(); chart.setExpanded(nodes[0].id, true).render(); chart.fit(); } });

    var search = document.getElementById("pemsOpcSearch");
    if (search) search.addEventListener("keydown", function (e) {
        if (e.key !== "Enter" || !chart) return;
        var q = search.value.trim().toLowerCase();
        if (!q) { chart.clearHighlighting(); return; }
        var hit = nodes.find(function (n) {
            return n.nodeKind !== "container" && ((n.positionTitle || "").toLowerCase().indexOf(q) >= 0 || (n.employeeName || "").toLowerCase().indexOf(q) >= 0 ||
                (n.departmentName || "").toLowerCase().indexOf(q) >= 0 || (n.positionCode || "").toLowerCase().indexOf(q) >= 0);
        });
        if (hit) { chart.clearHighlighting(); chart.setHighlighted(hit.id).render(); chart.setCentered(hit.id).render(); select(hit.id); }
        else search.classList.add("is-invalid");
    });
    if (search) search.addEventListener("input", function () { search.classList.remove("is-invalid"); });

    showState(T("loading"));
    fetch(root.getAttribute("data-url"), { credentials: "same-origin", headers: { Accept: "application/json" } })
        .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
        .then(build)
        .catch(function () { showState(T("error")); });
})();
