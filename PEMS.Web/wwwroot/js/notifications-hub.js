// طبق درخواست: اعلان‌های لحظه‌ای بدون Refresh صفحه (SignalR)
(function () {
    var userId = document.body.getAttribute('data-user-id');
    if (!userId || typeof signalR === 'undefined') return;

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveNotification', function (title, url) {
        showLiveToast(title, false);

        // شمارنده‌ی زنگ اعلان بالای صفحه را هم به‌روزرسانی کن، اگر وجود داشت
        var badge = document.getElementById('notifBadge');
        if (badge) {
            var current = parseInt(badge.textContent || '0', 10) || 0;
            badge.textContent = current + 1;
            badge.classList.remove('d-none');
        }
    });

    connection.start()
        .then(function () { return connection.invoke('JoinUserGroup', userId); })
        .catch(function () { /* اتصال زنده اختیاری است - نبودش نباید کار کاربر را مختل کند */ });
})();
