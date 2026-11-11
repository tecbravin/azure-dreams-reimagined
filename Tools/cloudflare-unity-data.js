/* Reassemble Unity's compressed data without buffering all parts in the page. */
self.addEventListener("install", event => event.waitUntil(self.skipWaiting()));
self.addEventListener("activate", event => event.waitUntil(self.clients.claim()));
self.addEventListener("fetch", event => {
    const url = new URL(event.request.url);
    const buildPath = new URL("Build/", self.registration.scope).pathname;
    if (event.request.method === "GET" && url.origin === self.location.origin &&
        url.pathname.startsWith(buildPath) && url.pathname.endsWith(".unitydata")) {
        event.respondWith(loadUnityData(url));
    }
});

async function loadUnityData(url) {
    const response = await fetch(new URL(url.pathname + ".json", url));
    if (!response.ok) throw new Error("Unity data manifest could not be loaded");
    const manifest = await response.json();
    let part = 0;
    let reader;
    const compressed = new ReadableStream({
        async pull(controller) {
            while (part < manifest.parts.length) {
                if (!reader) {
                    const file = await fetch(new URL(manifest.parts[part], url));
                    if (!file.ok || !file.body) throw new Error("Unity data part could not be loaded");
                    reader = file.body.getReader();
                }
                const chunk = await reader.read();
                if (!chunk.done) {
                    controller.enqueue(chunk.value);
                    return;
                }
                reader.releaseLock();
                reader = undefined;
                part++;
            }
            controller.close();
        },
        async cancel(reason) {
            if (reader) await reader.cancel(reason);
        }
    });

    return new Response(compressed.pipeThrough(new DecompressionStream("gzip")), {
        headers: {
            "Content-Type": "application/octet-stream",
            "Content-Length": String(manifest.uncompressedBytes),
            "Cache-Control": "no-store"
        }
    });
}
