// SCRUM-428: item rows of the New MR form. Rows come from <template id="mrItemTemplate">; the part-number select is enhanced by the
// PEMS.UI searchable select (data-pems-select-url → MaterialRequisition/PartLookup). Choosing a registered part fills an empty
// description and unit; a typed part number is marked "not catalogued". The server re-checks the part and stores the link.
(function () {
    'use strict';
    var container = document.querySelector('[data-mr-items]');
    var template = document.getElementById('mrItemTemplate');
    if (!container || !template || container.dataset.pemsInit) return;
    container.dataset.pemsInit = '1';

    function onPartChange(row, select) {
        var ts = select.tomselect;
        var value = select.value;
        var option = ts && value ? ts.options[value] : null;
        var registered = !!(option && Object.prototype.hasOwnProperty.call(option, 'description'));
        var flag = row.querySelector('[data-mr-not-catalogued]');
        if (flag) flag.hidden = !value || registered;
        if (!registered) return;
        var desc = row.querySelector('[name="itemDescriptions"]');
        var unit = row.querySelector('[name="itemUnits"]');
        if (desc && !desc.value && option.description) desc.value = option.description;
        if (unit && !unit.value && option.unit) unit.value = option.unit;
    }

    function addRow() {
        var row = template.content.firstElementChild.cloneNode(true);
        container.appendChild(row);
        var select = row.querySelector('select[name="itemParts"]');
        // the searchable select initialises added selects itself (MutationObserver); make sure it is ready before wiring
        if (window.PemsSelect && !select.tomselect) window.PemsSelect.init(select);
        select.addEventListener('change', function () { onPartChange(row, select); });
    }

    document.getElementById('addItemBtn').addEventListener('click', addRow);
    addRow();
})();
