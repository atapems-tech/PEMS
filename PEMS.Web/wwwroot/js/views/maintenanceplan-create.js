function pemsToggleTriggerFields() {
    var t = document.getElementById('triggerTypeSelect').value;
    var meter = (t === 'Meter' || t === 'Both'), cal = (t === 'Calendar' || t === 'Both');
    document.getElementById('meterTypeWrap').style.display = meter ? '' : 'none';
    document.getElementById('meterIntervalWrap').style.display = meter ? '' : 'none';
    document.getElementById('intervalDaysWrap').style.display = cal ? '' : 'none';
    document.getElementById('nextDueDateWrap').style.display = cal ? '' : 'none';
}
document.getElementById('triggerTypeSelect').addEventListener('change', pemsToggleTriggerFields);
pemsToggleTriggerFields();
