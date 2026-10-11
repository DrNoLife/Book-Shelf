// The app's service worker, and it does one thing: show the release notifications the server
// pushes (feature-spec §4.5). No fetch handler and no cache, on purpose: Blazor's bundle is
// fingerprinted and served fresh by nginx, and a worker that cached it would serve yesterday's
// app after a deploy. Served with Cache-Control: no-cache (nginx.conf) so a new version of
// this file is picked up on the next visit.

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

// Shown when a push arrives without a payload this file can read. Every push must show
// something: Safari revokes the subscription of a site that pushes silently.
const fallback = {
    title: 'LN Shelf',
    body: 'Something changed on your shelf.',
    url: '/',
    tag: 'lnshelf-releases'
};

self.addEventListener('push', event => {
    let message = fallback;
    try {
        const data = event.data ? event.data.json() : null;
        if (data && typeof data.title === 'string' && typeof data.body === 'string') {
            message = {
                title: data.title,
                body: data.body,
                url: typeof data.url === 'string' ? data.url : fallback.url,
                tag: typeof data.tag === 'string' ? data.tag : fallback.tag
            };
        }
    } catch {
        // Not JSON: the fallback says something happened, which is still true.
    }

    event.waitUntil(self.registration.showNotification(message.title, {
        body: message.body,
        tag: message.tag,
        icon: '/icon-192.png',
        data: { url: message.url }
    }));
});

// The browser or its push service replaced this device's subscription (Firefox and FCM rotate
// endpoints now and then). Without telling the server, it would keep pushing to the old
// endpoint, get a 410, delete the row, and the device would go quiet with nobody the wiser.
// The old row is left for that same 410 to remove. Same origin, so the session cookie goes
// along; a signed-out browser gets a 401 and simply stays off.
self.addEventListener('pushsubscriptionchange', event => {
    event.waitUntil((async () => {
        let subscription = event.newSubscription;
        if (!subscription) {
            const options = event.oldSubscription && event.oldSubscription.options;
            if (!options || !options.applicationServerKey) return;
            subscription = await self.registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: options.applicationServerKey
            });
        }

        const json = subscription.toJSON();
        await fetch('/api/notifications/subscriptions', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth })
        });
    })().catch(() => {
        // Nothing to retry with here; Profile shows this device as off until it is turned on again.
    }));
});

// Opens the page the notification is about, on this origin only: a payload naming another
// site is treated as naming the shelf.
self.addEventListener('notificationclick', event => {
    event.notification.close();

    const requested = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin);
    const target = requested.origin === self.location.origin ? requested.href : self.location.origin + '/';

    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = windows.find(client => new URL(client.url).origin === self.location.origin);

        if (open) {
            try {
                await open.focus();
                if ('navigate' in open) {
                    await open.navigate(target);
                }
                return;
            } catch {
                // An uncontrolled window cannot be navigated from here; a new one is opened instead.
            }
        }

        await self.clients.openWindow(target);
    })());
});
