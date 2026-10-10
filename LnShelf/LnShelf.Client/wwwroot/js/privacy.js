// The header link of the privacy page. The page is static and works without a session, so the
// link starts as "Sign in". If a session exists it becomes "Back to your shelf". A signed-out
// answer (401) or any failure leaves it as it is: the page never depends on this.
(function () {
    var link = document.querySelector("a[data-session-link]");

    if (!link || !window.fetch) {
        return;
    }

    fetch("api/auth/me", { credentials: "same-origin", headers: { Accept: "application/json" } })
        .then(function (response) {
            if (response.ok) {
                link.textContent = "Back to your shelf";
                link.setAttribute("href", "./");
            }
        })
        .catch(function () { });
})();
