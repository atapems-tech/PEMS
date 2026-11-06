function pemsToggleStorageFields() {
    var type = document.getElementById('providerTypeSelect').value;
    var isNetwork = (type === 'NetworkShare' || type === 'Ftp' || type === 'Sftp');
    var isFtpSftp = (type === 'Ftp' || type === 'Sftp');
    var isFtp = (type === 'Ftp');
    document.querySelectorAll('.network-field').forEach(function (el) { el.style.display = isNetwork ? '' : 'none'; });
    document.querySelectorAll('.ftp-sftp-field').forEach(function (el) { el.style.display = isFtpSftp ? '' : 'none'; });
    document.querySelectorAll('.ftp-field').forEach(function (el) { el.style.display = isFtp ? '' : 'none'; });
}
document.addEventListener('DOMContentLoaded', pemsToggleStorageFields);
