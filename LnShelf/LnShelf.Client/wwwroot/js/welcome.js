// Carries the page a signed-out visitor came from through the login, so a deep link still
// lands where it was meant to. The API accepts local URLs only; this check keeps the link
// itself honest and ignores anything that is not a same-origin path.
(function () {
    var returnUrl = new URLSearchParams(window.location.search).get("returnUrl");

    if (!returnUrl || returnUrl.charAt(0) !== "/" || returnUrl.charAt(1) === "/" || returnUrl.charAt(1) === "\\") {
        return;
    }

    var href = "api/auth/login?returnUrl=" + encodeURIComponent(returnUrl);

    document.querySelectorAll("a[data-sign-in]").forEach(function (link) {
        link.setAttribute("href", href);
    });
})();
