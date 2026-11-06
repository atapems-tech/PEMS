document.querySelectorAll('.adjust-stock-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        document.getElementById('adjustWarehouseId').value = btn.dataset.warehouseId;
        document.getElementById('adjustWarehouseLabel').textContent = '(' + btn.dataset.warehouseCode + ')';
        document.getElementById('adjustNewOnHand').value = btn.dataset.onhand;
        new bootstrap.Modal(document.getElementById('adjustStockModal')).show();
    });
});
document.querySelectorAll('.transfer-stock-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        document.getElementById('transferFromWarehouseId').value = btn.dataset.warehouseId;
        document.getElementById('transferFromWarehouseLabel').textContent = '(' + btn.dataset.warehouseCode + ')';
        document.getElementById('transferAvailable').textContent = btn.dataset.available;
        new bootstrap.Modal(document.getElementById('transferStockModal')).show();
    });
});

// طبق «PEMS — Warehouse and Inventory Barcode Integration»: اسکن فقط <select name="warehouseId">
// موجود را از قبل پر می‌کند - ReceiveStock همون مسیر/سرویس موجود (IMaterialStockService) را طی می‌کند.
document.getElementById('receiveWarehouseScanBtn')?.addEventListener('click', async function () {
    var input = document.getElementById('receiveWarehouseScanInput');
    var select = document.getElementById('receiveWarehouseSelect');
    var code = (input.value || '').trim();
    if (!code) return;

    try {
        var res = await fetch('/Barcode/ResolveJson?code=' + encodeURIComponent(code));
        var data = await res.json();

        if (!data.success) { showLiveToast('Barcode not resolved: ' + (data.errorMessage || data.errorCode), true); return; }
        if (data.targetType !== 'Warehouse') { showLiveToast('This barcode belongs to a ' + data.targetType + ', not a Warehouse.', true); return; }

        var matched = false;
        for (var i = 0; i < select.options.length; i++) {
            if (select.options[i].value == data.targetId) { select.value = select.options[i].value; matched = true; break; }
        }
        if (!matched) { showLiveToast('Resolved to ' + (data.label || ('Warehouse #' + data.targetId)) + ', but it is not in this list.', true); return; }

        input.value = '';
    } catch (e) {
        showLiveToast('Could not resolve barcode.', true);
    }
});
