        (function () {
            var projectSelect = document.querySelector('select[name="projectId"]');
            var searchBox = document.getElementById('docPickerSearch');
            var rows = document.querySelectorAll('.doc-picker-row');
            var counter = document.getElementById('docPickerCounter');
            var recipientRows = document.getElementById('recipientRows');
            var template = document.getElementById('recipientRowTemplate');
            var addBtn = document.getElementById('addRecipientBtn');

            function addRecipientRow() {
                var clone = template.content.cloneNode(true);
                clone.querySelector('.remove-recipient-btn').addEventListener('click', function (e) {
                    e.target.closest('.recipient-row').remove();
                });
                recipientRows.appendChild(clone);
            }

            addBtn.addEventListener('click', addRecipientRow);
            addRecipientRow();

            function applyFilter() {
                var projectId = projectSelect.value;
                var term = searchBox.value.trim().toLowerCase();
                var visibleCount = 0, selectedCount = 0;

                rows.forEach(function (row) {
                    var matchesProject = !projectId || row.getAttribute('data-project-id') === projectId;
                    var matchesSearch = !term || row.getAttribute('data-search').indexOf(term) !== -1;
                    var show = matchesProject && matchesSearch;

                    row.style.display = show ? '' : 'none';
                    if (show) visibleCount++;

                    if (row.querySelector('.doc-picker-checkbox').checked) selectedCount++;
                });

                counter.textContent = visibleCount + ' shown, ' + selectedCount + ' selected';
            }

            projectSelect.addEventListener('change', applyFilter);
            searchBox.addEventListener('input', applyFilter);
            rows.forEach(function (row) {
                row.querySelector('.doc-picker-checkbox').addEventListener('change', applyFilter);
            });

            applyFilter();
        })();
