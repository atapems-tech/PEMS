// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ==============================================================================
// طبق «PEMS — Engineering Documents Density/Search Consolidation» بند ۵۸: قبلاً هیچ
// نقطه‌ی مرکزی برای فعال‌سازی Tooltip های Bootstrap وجود نداشت - هر صفحه‌ای که لازم داشت
// باید خودش یک بلوک <script> جدا می‌نوشت. این‌جا فقط یک نقطه‌ی مشترک و Idempotent است
// (بررسی می‌کند که یک المان قبلاً Instance نگرفته باشد، قبل از دوباره init کردنش) - هر
// صفحه‌ای که بعداً محتوای پویا اضافه می‌کند (مثلاً بعد از باز شدن یک Modal) می‌تواند
// window.PemsTooltips.init() را دوباره صدا بزند، بدون خطر Double-Init.
// ==============================================================================
window.PemsTooltips = {
    init: function (root) {
        var scope = root || document;
        var elements = scope.querySelectorAll('[data-bs-toggle="tooltip"]');
        elements.forEach(function (el) {
            if (!bootstrap.Tooltip.getInstance(el)) {
                new bootstrap.Tooltip(el);
            }
        });
    }
};

document.addEventListener('DOMContentLoaded', function () {
    window.PemsTooltips.init();

    // طبق بند ۴۷: Ctrl+K/Cmd+K روی اولین جعبه‌ی جستجوی سراسری قابل‌مشاهده‌ی صفحه فوکوس می‌کند -
    // یک میان‌بر عمومی برای کل PEMS (نه فقط Engineering Documents)، چون data-pems-search در
    // چند صفحه‌ی دیگر هم استفاده می‌شود. اگر مرورگر/OS از قبل Ctrl+K را برای چیز دیگری استفاده
    // می‌کند (مثلاً نوار آدرس بعضی مرورگرها)، این‌جا فقط زمانی preventDefault می‌شود که واقعاً
    // یک جعبه‌ی جستجو پیدا شود - در غیر این صورت رفتار پیش‌فرض مرورگر دست‌نخورده می‌ماند.
    document.addEventListener('keydown', function (e) {
        var isShortcut = (e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K');
        if (!isShortcut) return;

        var visibleSearchBox = Array.from(document.querySelectorAll('[data-pems-search]'))
            .find(function (el) { return el.offsetParent !== null; });

        if (visibleSearchBox) {
            e.preventDefault();
            visibleSearchBox.focus();
        }
    });
});
