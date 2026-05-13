(() => {
    const toast = document.getElementById('notificationToast');
    const titleTarget = document.getElementById('notificationToastTitle');
    const bodyTarget = document.getElementById('notificationToastBody');

    const showToast = notification => {
        if (!toast || !titleTarget || !bodyTarget) return;

        titleTarget.textContent = notification?.title || 'Nouvelle notification';
        bodyTarget.textContent = notification?.body || '';
        toast.hidden = false;

        if (notification?.link) {
            toast.style.cursor = 'pointer';
            toast.onclick = () => {
                window.location.href = notification.link;
            };
        }

        window.setTimeout(() => {
            toast.hidden = true;
            toast.onclick = null;
            toast.style.cursor = 'default';
        }, 5200);
    };

    const incrementNotificationBadge = () => {
        const link = document.querySelector('.icon-link-btn');
        if (!link) return;

        let badge = link.querySelector('.icon-badge');
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'icon-badge';
            badge.textContent = '0';
            link.appendChild(badge);
        }

        badge.textContent = String((Number(badge.textContent) || 0) + 1);
    };

    if (window.signalR) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/notificationsHub')
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveNotification', notification => {
            incrementNotificationBadge();
            showToast(notification);
        });

        connection.start().catch(() => {
            // The app remains usable if realtime notifications are unavailable.
        });
    }
})();
