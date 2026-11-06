    function pemsToggleIssueFields() {
        var isExternal = document.getElementById('ext_unit').checked;
        var isDept = document.getElementById('dept_internal').checked;

        document.getElementById('issueDeptField').style.display = isDept ? '' : 'none';
        document.getElementById('issueExternalFields').style.display = isExternal ? '' : 'none';

        // فیلدهای نامه: برای بیرونی همیشه نشون داده بشه (اجباریه)، برای داخلی هم نشون داده
        // بشه ولی اختیاریه (سرویس خودش تشخیص می‌ده اگه موضوع خالی بود نامه نسازه)
        var showLetterFields = isExternal || isDept || document.getElementById('dcc_internal').checked;
        document.getElementById('issueLetterNumberField').style.display = showLetterFields ? '' : 'none';
        document.getElementById('issueSubjectField').style.display = showLetterFields ? '' : 'none';
        document.getElementById('issueBodyField').style.display = showLetterFields ? '' : 'none';

        // اگه بیرونیه، موضوع و گیرنده اجباری بشن
        var subjectInput = document.querySelector('#issueSubjectField input');
        var receiverInput = document.querySelector('#issueExternalFields input');
        if (subjectInput) subjectInput.required = isExternal;
        if (receiverInput) receiverInput.required = isExternal;
    }

    pemsToggleIssueFields();
