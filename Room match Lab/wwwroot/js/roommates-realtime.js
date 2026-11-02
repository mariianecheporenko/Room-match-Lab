(() => {
    const body = document.body;
    const userId = body.dataset.currentUserId;
    if (!userId || !window.signalR) return;

    const userEmail = (body.dataset.currentUserEmail || "").toLowerCase();
    const status = document.getElementById("landlord-live-status");
    const housingLinkId = "addHousingNavLink";

    function updateHousingLink(isAllowed) {
        let link = document.getElementById(housingLinkId);
        if (isAllowed && !link) {
            link = document.createElement("a");
            link.id = housingLinkId;
            link.className = "nav-link add-housing-btn";
            link.href = "/HousingsMvc/Create";
            link.textContent = "Add housing";
            document.querySelector(".navbar-nav")?.append(link);
        } else if (!isAllowed && link && body.dataset.currentRole !== "Admin") {
            link.remove();
        }
    }

    async function refreshToken() {
        if (!window.RoomMatesAuth?.getToken()) return;
        try {
            const response = await window.RoomMatesAuth.fetch("/api/auth/refresh", { method: "POST" });
            if (!response.ok) return;
            const result = await response.json();
            window.RoomMatesAuth.setToken(result.token);
        } catch (error) {
            console.warn("Could not refresh the session after a landlord status change.", error);
        }
    }

    function handleStatusUpdate(event) {
        const sameUser = String(event.userId || "").toLowerCase() === userId.toLowerCase() ||
            (event.email || "").toLowerCase() === userEmail;
        if (!sameUser) return;

        body.dataset.currentRole = event.role;
        body.dataset.currentApproved = String(event.isApproved);
        const canAddHousing = event.role === "Admin" || (event.role === "Landlord" && event.isApproved);
        updateHousingLink(canAddHousing);

        const profileHasInlineStatus = document.getElementById("pendingApplicationAlert");
        if (!(profileHasInlineStatus && event.role === "Landlord")) {
            if (event.role === "Landlord" && event.isApproved) {
                status.textContent = "Ваш акаунт власника житла успішно підтверджено! Тепер ви можете додавати житло.";
                status.className = "alert alert-success";
            } else if (event.role === "Student") {
                status.textContent = "Права власника житла для вашого акаунта скасовано.";
                status.className = "alert alert-danger";
            } else {
                status.textContent = "Ваша заявка на статус власника житла очікує на підтвердження адміністратора.";
                status.className = "alert alert-warning";
            }
        }

        window.dispatchEvent(new CustomEvent("landlordStatusUpdated", { detail: event }));
        refreshToken();
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/bookingHub")
        .withAutomaticReconnect()
        .build();
    connection.on("LandlordStatusUpdated", handleStatusUpdate);
    const start = () => connection.start().catch(error => {
        console.error("Live landlord status updates failed; retrying in five seconds.", error);
        window.setTimeout(start, 5000);
    });
    start();
})();
