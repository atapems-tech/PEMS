(function () {
    var form = document.getElementById('refreshForm');
    var btn = document.getElementById('refreshBtn');
    form && form.addEventListener('submit', function () {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Scanning...';
    });
})();
(function () {
    var input = document.getElementById('moduleSearch');
    var cards = [].slice.call(document.querySelectorAll('.module-card'));
    input && input.addEventListener('input', function () {
        var q = this.value.trim().toLowerCase();
        cards.forEach(function (c) {
            var show = !q || c.getAttribute('data-module').indexOf(q) !== -1;
            c.style.display = show ? '' : 'none';
        });
    });
})();
