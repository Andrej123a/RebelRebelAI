document.addEventListener("DOMContentLoaded", async () => {
    updateAdminMobileRailHeight();

    window.addEventListener(
        "resize",
        updateAdminMobileRailHeight
    );

    const adminNavigation = document.getElementById("adminNavigation");

    adminNavigation?.addEventListener(
        "shown.bs.collapse",
        updateAdminMobileRailHeight
    );

    adminNavigation?.addEventListener(
        "hidden.bs.collapse",
        updateAdminMobileRailHeight
    );

    if (typeof signalR === "undefined") {
        console.error("SignalR JavaScript client is not loaded.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", notification => {
        if (isNewReservationNotification(notification)) {
            updateLiveNotificationState(notification);
        }

        showLiveNotification(notification);
    });

    try {
        await connection.start();
        console.log("Live notifications connected.");
    } catch (error) {
        console.error("SignalR connection failed:", error);
    }
});

function updateAdminMobileRailHeight() {
    const rail = document.querySelector(".admin-rail");

    if (!rail) {
        return;
    }

    document.documentElement.style.setProperty(
        "--admin-mobile-rail-height",
        `${rail.offsetHeight}px`
    );
}

function updateLiveNotificationState(notification) {
    const topBadge =
        document.getElementById("notificationBadge");

    const navigationBadge =
        document.getElementById("reservationNavBadge");

    const reservationSignal =
        document.getElementById("reservationSignal");

    const signalState =
        document.getElementById("reservationSignalState");

    const currentCount = getCurrentCount(
        topBadge,
        navigationBadge
    );

    const newCount = currentCount + 1;

    const displayedCount =
        newCount > 99
            ? "99+"
            : newCount.toString();

    if (topBadge) {
        topBadge.textContent = displayedCount;
        topBadge.dataset.count = newCount.toString();
        topBadge.classList.remove("d-none");
    }

    if (navigationBadge) {
        navigationBadge.textContent = displayedCount;
        navigationBadge.dataset.count = newCount.toString();
        navigationBadge.classList.remove("d-none");

        navigationBadge.setAttribute(
            "aria-label",
            `${newCount} unread reservation notifications`
        );
    }

    if (reservationSignal) {
        reservationSignal.classList.add(
            "has-notifications"
        );

        reservationSignal.href =
            newCount === 1 && notification?.link
                ? notification.link
                : "/AdminReservations?status=Pending";
    }

    if (signalState) {
        signalState.textContent = "NEW REQUEST";
    }
}

function isNewReservationNotification(notification) {
    return notification?.link?.includes(
        "/AdminReservations/Details/"
    ) === true;
}

function getCurrentCount(
    topBadge,
    navigationBadge
) {
    const badge = topBadge || navigationBadge;

    if (!badge) {
        return 0;
    }

    const storedCount = Number.parseInt(
        badge.dataset.count,
        10
    );

    if (!Number.isNaN(storedCount)) {
        return storedCount;
    }

    const visibleCount = Number.parseInt(
        badge.textContent.trim(),
        10
    );

    return Number.isNaN(visibleCount)
        ? 0
        : visibleCount;
}

function showLiveNotification(notification) {
    document
        .getElementById("liveNotificationToast")
        ?.remove();

    const toast = document.createElement("a");

    toast.id = "liveNotificationToast";

    toast.href =
        notification.link ||
        "/AdminReservations";

    toast.style.position = "fixed";
    toast.style.top = "85px";
    toast.style.right = "25px";
    toast.style.zIndex = "9999";
    toast.style.width = "340px";
    toast.style.maxWidth = "calc(100% - 30px)";
    toast.style.padding = "18px";
    toast.style.background = "#171717";
    toast.style.border = "1px solid #f4bd00";
    toast.style.borderRadius = "14px";
    toast.style.color = "#ffffff";
    toast.style.textDecoration = "none";
    toast.style.boxShadow =
        "0 18px 45px rgba(0, 0, 0, 0.45)";

    const title = document.createElement("strong");

    title.style.display = "block";
    title.style.color = "#f4bd00";
    title.style.marginBottom = "6px";

    title.textContent =
        notification.title ||
        "New table reservation";

    const message = document.createElement("span");

    message.style.display = "block";
    message.style.fontSize = "14px";
    message.style.lineHeight = "1.5";

    message.textContent =
        notification.message || "";

    toast.append(title, message);

    document.body.appendChild(toast);

    window.setTimeout(() => {
        toast.remove();
    }, 7000);
}
