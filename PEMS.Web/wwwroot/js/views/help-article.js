        var askButton = document.getElementById('helpAiAskBtn');
askButton.addEventListener('click', async function () {
            var question = document.getElementById('helpAiQuestion').value.trim();
            if (!question) return;
            var answerBox = document.getElementById('helpAiAnswer');
            answerBox.hidden = false;
            answerBox.innerHTML = '<span class="text-muted">' + askButton.dataset.thinking + '</span>';

            var form = new FormData();
            form.append('articleCode', askButton.dataset.code);
            form.append('question', question);
            form.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');

            try {
                var res = await fetch(askButton.dataset.url, { method: 'POST', body: form });
                var data = await res.json();
                answerBox.innerHTML = '<div class="p-2 rounded bg-body-tertiary">' + (data.answer || '').replace(/\n/g, '<br/>') + '</div>';
            } catch (e) {
                answerBox.innerHTML = '<span class="text-danger">' + askButton.dataset.error + '</span>';
            }
        });
