// PEMS.UI confirm and alert dialog (SCRUM-483). Replaces the browser confirm() and alert(): one shared native <dialog>, built on first use. Vanilla, offline, no inline handlers.
//   data-pems-confirm="text"          on a button, link, input or form: asks first; OK continues the click or the submit, Cancel does nothing
//   data-pems-confirm-count="#id"    replaces {n} in the text by the text of that element (a live counter)
//   data-pems-confirm-tone="danger"   colour of the OK button (primary, danger, warning...; default primary)
//   data-pems-confirm-title="text"    heading (default: the layout's data-pems-confirm-title)
//   window.PemsDialog.confirm(text, { tone, title }) -> Promise<boolean>;  window.PemsDialog.alert(text, { title }) -> Promise<void>
// Texts are inputs: the layout puts the localised title, OK and Cancel on <body data-pems-confirm-title / -ok / -cancel>.
(function () {
    'use strict';

    var dialog = null;
    var bypass = new WeakSet();

    function text(name) {
        var body = document.body;
        return (body && body.getAttribute('data-pems-confirm-' + name)) || '';
    }

    function el(tag, className, content) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (content) node.textContent = content;
        return node;
    }

    function build() {
        if (dialog) return dialog;
        dialog = el('dialog', 'pems-modal pems-modal--small pems-confirm');
        dialog.setAttribute('aria-labelledby', 'pems-confirm-title');
        dialog.setAttribute('aria-describedby', 'pems-confirm-message');
        var panel = el('div', 'pems-modal__panel');
        var header = el('header', 'pems-modal__header');
        var title = el('h2', 'pems-modal__title');
        title.id = 'pems-confirm-title';
        header.appendChild(title);
        var body = el('div', 'pems-modal__body');
        var message = el('p', 'pems-confirm__message');
        message.id = 'pems-confirm-message';
        body.appendChild(message);
        var footer = el('footer', 'pems-modal__footer');
        var cancel = el('button', 'pems-btn pems-btn--neutral pems-btn--medium');
        cancel.type = 'button';
        cancel.setAttribute('data-pems-confirm-cancel', '');
        var ok = el('button', 'pems-btn pems-btn--primary pems-btn--medium');
        ok.type = 'button';
        ok.setAttribute('data-pems-confirm-ok', '');
        footer.appendChild(cancel);
        footer.appendChild(ok);
        panel.appendChild(header);
        panel.appendChild(body);
        panel.appendChild(footer);
        dialog.appendChild(panel);
        dialog.addEventListener('click', function (e) { if (e.target === dialog) dialog.close('cancel'); });
        cancel.addEventListener('click', function () { dialog.close('cancel'); });
        ok.addEventListener('click', function () { dialog.close('ok'); });
        document.body.appendChild(dialog);
        return dialog;
    }

    // One question at a time; resolves true on OK, false on Cancel, Escape or a click on the backdrop.
    function ask(message, options, alertOnly) {
        options = options || {};
        return new Promise(function (resolve) {
            var d = build();
            d.querySelector('.pems-modal__title').textContent = options.title || text('title');
            d.querySelector('.pems-confirm__message').textContent = message || '';
            var ok = d.querySelector('[data-pems-confirm-ok]');
            var cancel = d.querySelector('[data-pems-confirm-cancel]');
            ok.textContent = text('ok');
            cancel.textContent = text('cancel');
            cancel.hidden = !!alertOnly;
            ok.className = 'pems-btn pems-btn--' + (options.tone || 'primary') + ' pems-btn--medium';
            function done() { d.removeEventListener('close', done); resolve(d.returnValue === 'ok'); }
            d.addEventListener('close', done);
            d.returnValue = '';
            if (typeof d.showModal === 'function') d.showModal(); else d.setAttribute('open', '');
            (alertOnly ? ok : cancel).focus();
        });
    }

    window.PemsDialog = {
        confirm: function (message, options) { return ask(message, options, false); },
        alert: function (message, options) { return ask(message, options, true).then(function () { }); }
    };

    function message(node) {
        var text = node.getAttribute('data-pems-confirm') || '';
        var counter = node.getAttribute('data-pems-confirm-count');
        var source = counter ? document.querySelector(counter) : null;
        return source ? text.replace('{n}', source.textContent) : text;
    }

    function options(node) {
        return { tone: node.getAttribute('data-pems-confirm-tone') || undefined, title: node.getAttribute('data-pems-confirm-title') || undefined };
    }

    // Capture phase: runs before the page's own handlers, so nothing starts (loading overlay, dirty guard) until the user agrees.
    document.addEventListener('click', function (e) {
        var node = e.target.closest ? e.target.closest('[data-pems-confirm]') : null;
        if (!node || node.tagName === 'FORM') return;
        if (bypass.has(node)) { bypass.delete(node); return; }
        if (node.disabled || node.getAttribute('aria-disabled') === 'true') return;
        e.preventDefault();
        e.stopImmediatePropagation();
        ask(message(node), options(node), false).then(function (yes) {
            if (!yes) return;
            bypass.add(node);
            node.click();
        });
    }, true);

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || form.tagName !== 'FORM' || !form.hasAttribute('data-pems-confirm')) return;
        if (bypass.has(form)) { bypass.delete(form); return; }
        var submitter = e.submitter || null;
        e.preventDefault();
        e.stopImmediatePropagation();
        ask(message(form), options(form), false).then(function (yes) {
            if (!yes) return;
            bypass.add(form);
            if (typeof form.requestSubmit === 'function') form.requestSubmit(submitter); else form.submit();
        });
    }, true);
})();
