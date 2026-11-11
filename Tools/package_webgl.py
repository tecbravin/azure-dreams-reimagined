"""Prepare a Unity Gzip WebGL build for Cloudflare Pages static hosting."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import re
import shutil
import tempfile

MIB = 1024 * 1024
PART_BYTES = 24 * MIB
FILE_LIMIT = 25 * MIB

BOOTSTRAP = """      async function startUnity() {
        if (!navigator.serviceWorker || !window.DecompressionStream)
          throw new Error("This build requires HTTPS (or localhost) and a modern browser.");
        await navigator.serviceWorker.register("unity-data-worker.js");
        if (!navigator.serviceWorker.controller) {
          await new Promise((resolve, reject) => {
            const workers = navigator.serviceWorker;
            const ready = () => {
              if (!workers.controller) return;
              clearTimeout(timeout);
              workers.removeEventListener("controllerchange", ready);
              resolve();
            };
            const timeout = setTimeout(() => {
              workers.removeEventListener("controllerchange", ready);
              reject(new Error("Unity data worker did not start."));
            }, 15000);
            workers.addEventListener("controllerchange", ready);
            ready();
          });
        }
        document.body.appendChild(script);
      }
      startUnity().catch(error => unityShowBanner(error.message, "error"));"""


def unpacked_size(path):
    with gzip.open(path, "rb") as stream:
        return sum(len(block) for block in iter(lambda: stream.read(MIB), b""))


def package(source, destination):
    if destination.exists() or destination.is_relative_to(source) or source.is_relative_to(destination):
        raise ValueError("Choose a new output folder outside the original build.")
    index = (source / "index.html").read_text(encoding="utf-8")
    match = re.search(r'dataUrl:\s*buildUrl\s*\+\s*"/([^/"]+\.data\.gz)"', index)
    if not match or index.count("document.body.appendChild(script);") != 1:
        raise ValueError("Expected a Unity Default WebGL build with Gzip compression.")
    archive = source / "Build" / match[1]
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()[:16]
    virtual_name = f"{archive.name[:-8]}.{digest}.unitydata"
    excluded = {archive.relative_to(source).as_posix()}
    destination.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(dir=destination.parent) as work:
        site = Path(work) / "site"
        skipped_bytes = 0
        for path in source.rglob("*"):
            if not path.is_file():
                continue
            relative = path.relative_to(source)
            if relative.as_posix() in excluded:
                continue
            if relative.parts[:2] == ("StreamingAssets", "Extracted") or (
                    relative.parts[0] == "StreamingAssets" and path.suffix == ".meta"):
                skipped_bytes += path.stat().st_size
                continue
            target = site / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)

        parts = []
        with archive.open("rb") as stream:
            for block in iter(lambda: stream.read(PART_BYTES), b""):
                name = f"{virtual_name}.{len(parts):03}.bin"
                (site / "Build" / name).write_bytes(block)
                parts.append(name)
        manifest = {"parts": parts, "uncompressedBytes": unpacked_size(archive)}
        (site / "Build" / (virtual_name + ".json")).write_text(json.dumps(manifest), encoding="utf-8")
        index = index[:match.start(1)] + virtual_name + index[match.end(1):]
        index = index.replace("      document.body.appendChild(script);", BOOTSTRAP)
        index = index.replace("        arguments: [],", '        arguments: [],\n        cacheControl: () => "no-store",')
        (site / "index.html").write_text(index, encoding="utf-8")
        shutil.copyfile(Path(__file__).with_name("cloudflare-unity-data.js"), site / "unity-data-worker.js")

        headers = site / "_headers"
        rules = [headers.read_text(encoding="utf-8").rstrip()] if headers.exists() else []
        for file in sorted((site / "Build").glob("*.gz")):
            media = "application/wasm" if file.name.endswith(".wasm.gz") else "application/javascript"
            rules.append(f"/Build/{file.name}\n  Content-Type: {media}\n  Content-Encoding: gzip")
        rules.extend([
            f"/Build/{virtual_name}.*\n  Cache-Control: public, max-age=31536000, immutable",
            "/unity-data-worker.js\n  Cache-Control: no-cache",
        ])
        headers.write_text("\n\n".join(rules) + "\n", encoding="utf-8")

        files = [p for p in site.rglob("*") if p.is_file()]
        largest = max(files, key=lambda p: p.stat().st_size)
        if largest.stat().st_size > FILE_LIMIT:
            raise ValueError(f"Cloudflare limit exceeded by {largest.relative_to(site)}.")
        print(f"{archive.stat().st_size / MIB:.2f} MiB in {len(parts)} parts")
        print(f"{skipped_bytes / MIB:.2f} MiB of unused extraction files and metadata omitted")
        print(f"Largest file: {largest.relative_to(site)} ({largest.stat().st_size / MIB:.2f} MiB)")
        site.rename(destination)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("build", type=Path, help="Unity WebGL output containing index.html")
    parser.add_argument("output", type=Path, help="new folder to publish")
    args = parser.parse_args()
    package(args.build.resolve(), args.output.resolve())
