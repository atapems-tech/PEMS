// طبق «PEMS Portal UX Azure-Inspired Audit» بند A/N: این فایل قبلاً سه Handler جدا برای همون
// دکمه‌ی #sidebarToggleBtn داشت (این‌جا با کلاس‌های show/collapsed+expanded دوبار تکراری) که با
// Handler واقعی و کامل‌تر _Layout.cshtml (پشتیبانی درست از هر دو حالت دسکتاپ pems-collapsed/
// موبایل pems-opened که site.css واقعاً برایشان Style تعریف کرده) تداخل داشت - و یک موتور تمِ
// موازی مبتنی بر localStorage که هر بار رندر سمت سرور ThemeController/pems_theme Cookie را
// بی‌صدا رونویسی می‌کرد. هر دو حذف شدند؛ سایدبار و تم حالا فقط یک منبع حقیقت دارند
// (_Layout.cshtml برای سایدبار، ThemeController + Cookie برای تم).
