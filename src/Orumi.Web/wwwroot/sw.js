// Solo guarda la página y sus recursos para poder abrirla sin red. La API nunca se cachea.
const VERSION = 'orumi-captura-v4';
const RECURSOS = ['./', 'index.html', 'app.js', 'ui.css', 'captura.css', 'iconos.js', 'tema.js', 'icono.svg', 'manifest.webmanifest'];

self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(VERSION).then((c) => c.addAll(RECURSOS)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', (e) => {
  e.waitUntil(caches.keys().then((ks) => Promise.all(ks.filter((k) => k !== VERSION).map((k) => caches.delete(k)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', (e) => {
  const url = new URL(e.request.url);
  // El panel (/panel) tiene su propia página y no se guarda aquí.
  if (e.request.method !== 'GET' || url.origin !== location.origin || url.pathname.startsWith('/api/') || url.pathname.startsWith('/panel')) return;
  // Red primero (siempre la última versión); si no hay red, la copia guardada.
  e.respondWith(fetch(e.request).then((r) => {
    const copia = r.clone();
    caches.open(VERSION).then((c) => c.put(e.request, copia));
    return r;
  }).catch(() => caches.match(e.request).then((r) => r || caches.match('index.html'))));
});
