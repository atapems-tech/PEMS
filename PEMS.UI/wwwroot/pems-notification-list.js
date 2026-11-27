// SCRUM-841: fills the list of the bell dropdown (component notification-list) from its source: a list of { notificationId, title, message, created, isRead, openUrl }.
// The markup comes from the notif-item and notif-empty templates (pems-template.js); the dismiss and mark-all buttons are served by notifications-hub.js.
(function () {
    "use strict";

    function empty(box, text) {
        box.replaceChildren(window.PemsTemplate.fill(window.PemsTemplate.clone("notif-empty"), { text: text }));
    }

    function load(root) {
        var box = root.querySelector("[data-notification-items]");
        if (!box) return Promise.resolve();
        var dismiss = root.getAttribute("data-dismiss") || "";
        return fetch(root.getAttribute("data-source"), { cache: "no-store", credentials: "same-origin", headers: { "Accept": "application/json" } })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(function (items) {
                if (!items || !items.length) { empty(box, root.getAttribute("data-no-new")); return; }
                box.replaceChildren.apply(box, items.map(function (n) {
                    var row = window.PemsTemplate.clone("notif-item");
                    return window.PemsTemplate.fill(row, {
                        row: { attrs: { "data-notif-id": String(n.notificationId) }, addClass: n.isRead ? "" : "bg-light" },
                        link: { attrs: { href: n.openUrl } },
                        dot: n.isRead ? null : {},
                        title: n.title, message: n.message, date: n.created,
                        dismiss: { attrs: { "data-notif-id": String(n.notificationId), title: dismiss, "aria-label": dismiss } }
                    });
                }));
            })
            .catch(function () { empty(box, root.getAttribute("data-error")); });
    }

    window.PemsNotificationList = { load: function () { return Promise.all(Array.prototype.map.call(document.querySelectorAll("[data-pems-notification-list]"), load)); } };
})();
