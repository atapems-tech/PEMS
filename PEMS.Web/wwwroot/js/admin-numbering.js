// SCRUM-40: live preview on Admin > Numbering. Asks the server to format the unsaved pattern (no number is consumed) and shows
// the result, or the validation message, in the row. Debounced; one request per row at a time.
(function () {
    "use strict";
    var table = document.getElementById("numberingTable");
    if (!table || table.dataset.pemsInit) return;
    table.dataset.pemsInit = "1";
    var url = table.dataset.previewUrl;

    function field(row, name) {
        var form = row.querySelector("form");
        return document.querySelector('[form="' + form.id + '"][name="' + name + '"]') || form.querySelector('[name="' + name + '"]');
    }

    function refresh(row) {
        var params = new URLSearchParams({
            documentType: field(row, "DocumentType").value,
            pattern: field(row, "Pattern").value,
            padWidth: field(row, "PadWidth").value,
            calendar: field(row, "Calendar").value,
            resetPeriod: field(row, "ResetPeriod").value
        });
        var out = row.querySelector("[data-num-preview]");
        var err = row.querySelector("[data-num-error]");
        fetch(url + "?" + params.toString(), { headers: { "Accept": "application/json" } })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) {
                out.textContent = data.number || "";
                err.textContent = data.error || "";
            })
            .catch(function () { out.textContent = ""; err.textContent = table.dataset.errNetwork || ""; });
    }

    table.querySelectorAll("[data-numbering-row]").forEach(function (row) {
        var timer = null;
        row.querySelectorAll("[data-num-input]").forEach(function (input) {
            input.addEventListener("input", function () {
                clearTimeout(timer);
                timer = setTimeout(function () { refresh(row); }, 300);
            });
        });
    });
})();
