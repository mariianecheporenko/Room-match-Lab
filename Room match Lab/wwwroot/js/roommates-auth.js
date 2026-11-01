(() => {
    const tokenKey = "roommates.jwt";

    function clearToken() {
        localStorage.removeItem(tokenKey);
        updateControls();
    }

    function getToken() {
        const token = localStorage.getItem(tokenKey);
        if (!token) return null;

        try {
            const segments = token.split(".");
            if (segments.length !== 3) throw new Error("Malformed JWT");

            let payload = segments[1].replace(/-/g, "+").replace(/_/g, "/");
            while (payload.length % 4) payload += "=";
            const claims = JSON.parse(window.atob(payload));
            if (typeof claims.exp === "number" && claims.exp * 1000 <= Date.now()) {
                clearToken();
                return null;
            }
        } catch {
            clearToken();
            return null;
        }

        return token;
    }

    function setToken(token) {
        if (!token) throw new Error("A JWT token is required.");
        localStorage.setItem(tokenKey, token);
        updateControls();
    }

    function updateControls() {
        const signedIn = Boolean(getToken());
        const loginLink = document.getElementById("authLoginLink");
        const logoutButton = document.getElementById("authLogoutButton");
        if (loginLink) loginLink.hidden = signedIn;
        if (logoutButton) {
            logoutButton.hidden = !signedIn;
            logoutButton.onclick = signOut;
        }
    }

    async function signOut() {
        try {
            await window.fetch("/api/auth/logout", {
                method: "POST",
                credentials: "same-origin"
            });
        } catch {
            // Clear the browser token even if the server cannot be reached.
        }
        clearToken();
        window.location.assign("/ProfilesMVC/Create");
    }

    async function fetchWithAuth(resource, options = {}) {
        const requestUrl = new URL(
            typeof resource === "string" || resource instanceof URL ? resource : resource.url,
            window.location.href);
        const headers = new Headers(options.headers || {});
        const token = getToken();

        if (token && requestUrl.origin === window.location.origin)
            headers.set("Authorization", `Bearer ${token}`);

        const response = await window.fetch(resource, { ...options, headers });
        if (response.status === 401 && requestUrl.origin === window.location.origin)
            clearToken();
        return response;
    }

    window.RoomMatesAuth = { clearToken, fetch: fetchWithAuth, getToken, setToken, signOut };
    document.addEventListener("DOMContentLoaded", updateControls);
    window.addEventListener("storage", updateControls);
})();
