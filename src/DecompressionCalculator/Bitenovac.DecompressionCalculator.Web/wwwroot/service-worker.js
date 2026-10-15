// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
// Every build is a new worker; it takes over at once, since there is no cache for a waiting version to protect.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('message', event => {
    if (event.data === 'skip-waiting') {
        self.skipWaiting();
    }
});
self.addEventListener('fetch', () => { });
