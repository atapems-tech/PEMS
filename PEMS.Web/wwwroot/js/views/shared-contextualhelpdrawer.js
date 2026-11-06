    (function () {
        var drawer = document.getElementById('pemsHelpDrawer');
        if (!drawer) return;

        var loadedForKey = null;

        drawer.addEventListener('show.bs.offcanvas', function (evt) {
            var trigger = evt.relatedTarget;
            var helpKey = trigger ? trigger.getAttribute('data-help-key') : null;
            if (!helpKey || helpKey === loadedForKey) return;

            var body = document.getElementById('pemsHelpDrawerBody');
            PemsSkeleton.show(body, 'card');

            fetch(drawer.dataset.contextUrl + '?key=' + encodeURIComponent(helpKey))
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    loadedForKey = helpKey;
                    if (!data.found) {
                        body.innerHTML = '<p class="text-muted small text-center py-4">' + drawer.dataset.textNoContent + '</p>'
                            + '<a class="btn btn-sm btn-outline-secondary w-100" href="' + drawer.dataset.helpIndexUrl + '">' + drawer.dataset.textBrowse + '</a>';
                        return;
                    }
                    var related = (data.relatedArticles || []).map(function (r) {
                        return '<a class="btn btn-sm btn-outline-secondary me-1 mb-1" href="/Help/Article/' + r.code + '">' + r.title + '</a>';
                    }).join('');

                    var visualHint = data.hasVisual
                        ? '<a class="btn btn-sm btn-outline-primary w-100 mb-2" href="' + data.articleUrl + '"><i class="bi bi-diagram-3"></i> ' + drawer.dataset.textDiagram + '</a>'
                        : '';

                    body.innerHTML =
                        '<h6>' + data.title + '</h6>' +
                        '<p class="text-muted small">' + data.summary + '</p>' +
                        visualHint +
                        '<div class="pems-help-article-body small">' + data.content + '</div>' +
                        (related ? '<hr/><p class="small text-muted mb-1">' + drawer.dataset.textRelated + '</p><div>' + related + '</div>' : '') +
                        '<hr/><a class="btn btn-sm btn-pems-solid w-100" href="' + data.articleUrl + '"><i class="bi bi-box-arrow-up-right"></i> ' + drawer.dataset.textOpen + '</a>';
                })
                .catch(function () {
                    body.innerHTML = '<p class="text-danger small text-center py-4">' + drawer.dataset.textError + '</p>';
                });
        });
    })();
