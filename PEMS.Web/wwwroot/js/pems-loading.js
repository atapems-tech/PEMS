// طبق «PEMS Premium Three-Theme UI/UX Redesign» (بند ۲۶): کمک‌کننده‌ی سبک و اختیاری برای
// نمایش/پنهان‌سازی Overlay سراسری Loading (_PemsLoadingOverlay.cshtml). عمداً به هیچ رویداد
// موجودی (فرم/AJAX/ناوبری) خودکار وصل نشده - هر صفحه‌ای که واقعاً لازمش دارد آگاهانه صدا
// می‌زند، تا هیچ رفتار async موجودی بی‌صدا تغییر نکند.
(function () {
    function overlay() { return document.getElementById('pemsGlobalLoading'); }

    window.PemsLoading = {
        show: function (label) {
            var el = overlay();
            if (!el) return;
            if (label) {
                var lbl = el.querySelector('.pems-loading-label');
                if (lbl) lbl.textContent = label;
            }
            el.hidden = false;
        },
        hide: function () {
            var el = overlay();
            if (el) el.hidden = true;
        }
    };
})();
