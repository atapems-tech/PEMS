// SCRUM-817: a form whose action needs a security-key confirmation (stepUp): the host answered the first post with the form again and this marker.
// The ceremony runs once here (pemsPerformStepUp of webauthn.js), the two answers go into the form as hidden fields and the same form is posted again.
(function () {
    "use strict";
    var marker = document.getElementById("pems-stepup-marker");
    if (!marker || typeof window.pemsPerformStepUp !== "function") return;
    var form = document.querySelector('form[action^="/f/"]');
    if (!form) return;

    function put(name, value) {
        var field = form.querySelector('input[name="' + name + '"]');
        if (!field) { field = document.createElement("input"); field.type = "hidden"; field.name = name; form.appendChild(field); }
        field.value = value;
    }

    window.pemsPerformStepUp().then(function (result) {
        if (!result || !result.success) {
            marker.removeAttribute("hidden");
            marker.className = "alert alert-danger";
            marker.textContent = (result && result.error) || "The security key confirmation did not complete.";
            return;
        }
        put("webAuthnAssertionJson", result.assertionResponseJson);
        put("webAuthnOptionsJson", result.optionsJson);
        form.submit();
    });
})();
