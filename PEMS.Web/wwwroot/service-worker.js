// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۱۴: پایه‌ی حداقلی PWA برای تجربه‌ی موبایل قابل‌نصب.
// عمداً بدون Cache-first برای درخواست‌های API/فرم‌های حساس - PEMS یک سیستم ایمنی‌محور است و
// نباید داده‌ی قدیمی/آفلاین برای تصمیمات Permit نمایش دهد.
self.addEventListener("install", () => {
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    event.waitUntil(self.clients.claim());
});

self.addEventListener("fetch", () => {
    // بدون Intercept - همه‌ی درخواست‌ها مستقیم به شبکه می‌روند.
});
