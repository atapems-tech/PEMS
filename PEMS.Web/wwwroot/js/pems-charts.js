// طبق «PEMS Global Three-Theme Design System» (بند ۱۸): پیش از این، هر صفحه‌ی Chart.js
// (Home/Index و ۵ صفحه‌ی Report) رنگ‌های خودش را جداگانه hard-code کرده بود - نه فقط
// تکراری، بلکه در تم Dark/Turquoise هم عیناً همون رنگ‌های تم روشن باقی می‌ماند. این فایل
// همان مقادیر را یک‌بار از توکن‌های CSS (site.css: --pems-chart-*) می‌خواند و به هر صفحه
// می‌دهد - نه یک کتابخانه‌ی نمودار تازه، فقط منبع رنگ مشترک و theme-aware.
(function () {
    function cssVar(name, fallback) {
        var v = getComputedStyle(document.documentElement).getPropertyValue(name);
        v = v && v.trim();
        return v || fallback;
    }

    var primary = cssVar('--pems-chart-primary', '#3b82f6');
    var secondary = cssVar('--pems-chart-secondary', '#a855f7');
    var tertiary = cssVar('--pems-chart-tertiary', '#14b8a6');
    var positive = cssVar('--pems-chart-positive', '#22c55e');
    var negative = cssVar('--pems-chart-negative', '#ef4444');
    var warning = cssVar('--pems-chart-warning', '#f59e0b');
    var neutral = cssVar('--pems-chart-neutral', '#94a3b8');
    var accent = cssVar('--pems-chart-accent', '#d98c2b');

    window.PemsChartColors = {
        primary: primary,
        secondary: secondary,
        tertiary: tertiary,
        positive: positive,
        negative: negative,
        warning: warning,
        neutral: neutral,
        accent: accent,
        // ترتیب رایج برای doughnut/pie چندبخشی - همون ترتیبی که قبلاً در هر صفحه جداگانه
        // hard-code شده بود (neutral, warning, positive, negative, primary, secondary, tertiary)
        categorical: [neutral, warning, positive, negative, primary, secondary, tertiary],
        gridColor: cssVar('--pems-surface-border', '#edf0f4'),
        textColor: cssVar('--pems-text-muted', '#5c6f85')
    };
})();
