document.getElementById('implStatus').addEventListener('change', function () {
    document.getElementById('complianceWarning').style.display = this.value === 'ComplianceEvidenceAvailable' ? '' : 'none';
});
