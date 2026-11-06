    async function registerNow() {
        const nickname = document.getElementById('nickname').value;
        const token = document.querySelector('#registerForm input[name="__RequestVerificationToken"]').value;
        const alertBox = document.getElementById('pems-passkey-alert');
        alertBox.innerHTML = '';

        const result = await pemsRegisterPasskey(nickname, token);

        if (result.success) {
            location.reload();
        } else {
            alertBox.innerHTML = '<pems-alert tone="danger" css="py-2 px-3 small">' + (result.error || 'ثبت ناموفق بود.') + '</pems-alert>';
        }
    }
