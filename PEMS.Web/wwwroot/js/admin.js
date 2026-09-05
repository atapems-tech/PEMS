document.addEventListener("DOMContentLoaded", function () {
    var toggleBtn = document.getElementById("sidebarToggleBtn");
    var sidebar = document.getElementById("pemsSidebar");

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener("click", function () {
            sidebar.classList.toggle("show");
        });

        document.addEventListener("click", function (e) {
            if (window.innerWidth > 991) return;

            if (!sidebar.contains(e.target) && !toggleBtn.contains(e.target)) {
                sidebar.classList.remove("show");
            }
        });
    }
});
// باز و بسته کردن سایدبار
document.addEventListener('DOMContentLoaded', function () {
    const toggleBtn = document.getElementById('sidebarToggleBtn');
    const sidebar = document.getElementById('pemsSidebar');
    const content = document.querySelector('.pems-content');

    if (toggleBtn && sidebar && content) {
        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('collapsed');
            content.classList.toggle('expanded');
        });
    }
});

// ۱. باز و بسته کردن سایدبار در دسکتاپ
document.addEventListener('DOMContentLoaded', function () {
    const toggleBtn = document.getElementById('sidebarToggleBtn');
    const sidebar = document.querySelector('.pems-sidebar');
    const content = document.querySelector('.pems-content');

    if (toggleBtn && sidebar && content) {
        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('collapsed');
            content.classList.toggle('expanded');
        });
    }
});

// ۲. سیستم تغییر تم (Theme Engine)
document.addEventListener('DOMContentLoaded', function () {
    const themeSwitcher = document.querySelectorAll('.theme-switch');
    
    // خواندن تم ذخیره شده از مرورگر
    const savedTheme = localStorage.getItem('pems-theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);

    themeSwitcher.forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.preventDefault();
            const selectedTheme = this.getAttribute('data-theme');
            document.documentElement.setAttribute('data-theme', selectedTheme);
            localStorage.setItem('pems-theme', selectedTheme); // ذخیره در مرورگر
        });
    });
});
