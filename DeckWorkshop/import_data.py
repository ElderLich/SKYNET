#!/usr/bin/env python3
"""Import the explicitly selected card/script snapshots without executing scripts."""

import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import sqlite3
import stat
import tempfile
import zipfile


ROOT = Path(__file__).resolve().parent
FORMATS = (("standard", "cards.cdb", "script.zip"),
           ("rush", "rush_cards.cdb", "rush_scripts.zip"))
COMPILER_FILES = ("Pull.py", "Deck.txt", "Deck - MF.txt")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def record(path, relative_to):
    return {"path": path.relative_to(relative_to).as_posix(),
            "bytes": path.stat().st_size, "sha256": sha256(path)}


def extract_archive(source, target):
    """Retain license/metadata members, flatten only the common script/ prefix."""
    seen = set()
    with zipfile.ZipFile(source) as archive:
        members = archive.infolist()
        if len(members) > 30000 or sum(item.file_size for item in members) > 200 * 1024 * 1024:
            raise ValueError("Script archive exceeds the supported snapshot size.")
        for item in members:
            member = PurePosixPath(item.filename.replace("\\", "/"))
            if member.is_absolute() or ".." in member.parts or any(":" in part for part in member.parts):
                raise ValueError("Unsafe archive member: " + item.filename)
            if stat.S_ISLNK(item.external_attr >> 16):
                raise ValueError("Symbolic links are not supported in snapshots.")
            if item.is_dir():
                continue
            relative = (Path("scripts", *member.parts[1:]) if member.parts[0] == "script"
                        else Path("archive-metadata", *member.parts))
            key = relative.as_posix().casefold()
            if key in seen:
                raise ValueError("Duplicate archive destination: " + str(relative))
            seen.add(key)
            destination = target / relative
            if not destination.resolve().is_relative_to(target.resolve()):
                raise ValueError("Archive destination escapes snapshot.")
            destination.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(item) as incoming, destination.open("wb") as outgoing:
                shutil.copyfileobj(incoming, outgoing)


def import_snapshots(data_root, compiler_root, output):
    output.mkdir(parents=True, exist_ok=True)
    owned = ("Data", "LegacyCompiler", "data-provenance.json")
    if any((output / name).exists() for name in owned):
        raise ValueError("Snapshot already exists. Verify it, or import to a new --output directory for review.")
    provenance = {"schemaVersion": 1, "locale": "en-US", "excluded": ["edison.cdb"],
                  "method": "Byte-preserving local import; Lua is never executed.",
                  "formats": {}, "legacyCompiler": {"source": str(compiler_root), "files": []}}
    # Build a complete snapshot before publishing any imported directory.
    with tempfile.TemporaryDirectory(prefix=".import-", dir=output) as temporary:
        stage = Path(temporary)
        for key, database_name, archive_name in FORMATS:
            source_db = data_root / "locales" / "en-US" / database_name
            source_archive = data_root / archive_name
            destination = stage / "Data" / key
            destination.mkdir(parents=True)
            copied_db = destination / "cards.cdb"
            shutil.copyfile(source_db, copied_db)
            with closing(sqlite3.connect(copied_db.as_uri() + "?mode=ro", uri=True)) as connection:
                count = connection.execute("SELECT count(*) FROM texts").fetchone()[0]
                connection.execute("SELECT id, type FROM datas LIMIT 1").fetchone()
            extract_archive(source_archive, destination)
            files = [record(item, stage) for item in sorted(destination.rglob("*")) if item.is_file()]
            provenance["formats"][key] = {
                "databaseSource": str(source_db), "archiveSource": str(source_archive),
                "archiveSha256": sha256(source_archive), "databaseSha256": sha256(copied_db),
                "cardCount": count, "luaCount": sum(item["path"].endswith(".lua") for item in files),
                "files": files,
            }
        legacy = stage / "LegacyCompiler"
        legacy.mkdir()
        for name in COMPILER_FILES:
            shutil.copyfile(compiler_root / name, legacy / name)
            provenance["legacyCompiler"]["files"].append(record(legacy / name, stage))
        (stage / "data-provenance.json").write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
        for name in owned:
            # Create published files under the destination's permissions. On Windows,
            # renaming out of Python's private TemporaryDirectory preserves its ACL,
            # which can make a checkout unreadable to its ordinary desktop owner.
            if (stage / name).is_dir():
                shutil.copytree(stage / name, output / name, copy_function=shutil.copyfile)
            else:
                shutil.copyfile(stage / name, output / name)
    return provenance


def verify(output):
    provenance = json.loads((output / "data-provenance.json").read_text(encoding="utf-8"))
    files = provenance["legacyCompiler"]["files"] + [
        item for dataset in provenance["formats"].values() for item in dataset["files"]]
    errors = []
    expected = set()
    for item in files:
        path = output / item["path"]
        if not path.resolve().is_relative_to(output.resolve()):
            raise ValueError("Manifest path escapes snapshot.")
        expected.add(path.resolve())
        if not path.is_file() or path.stat().st_size != item["bytes"] or sha256(path) != item["sha256"]:
            errors.append(item["path"])
    actual = {item.resolve() for folder in ("Data", "LegacyCompiler")
              for item in (output / folder).rglob("*") if item.is_file()}
    errors.extend(str(item.relative_to(output.resolve())) + " (unrecorded)" for item in actual - expected)
    if errors:
        raise ValueError("Snapshot verification failed: " + ", ".join(errors[:20]))
    print("Verified {} byte-preserved files; Edison excluded.".format(len(files)))
    return provenance


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("import", "verify"))
    parser.add_argument("--data-root", type=Path, help="Explicit MDPro4_Data/Data directory")
    parser.add_argument("--compiler-root", type=Path, help="Explicit original Deck Compiler directory")
    parser.add_argument("--output", type=Path, default=ROOT)
    args = parser.parse_args()
    try:
        if args.command == "verify":
            provenance = verify(args.output.resolve())
        else:
            if not args.data_root or not args.compiler_root:
                parser.error("import requires --data-root and --compiler-root")
            provenance = import_snapshots(args.data_root.resolve(), args.compiler_root.resolve(), args.output.resolve())
        for key, value in provenance["formats"].items():
            print("{}: {} cards, {} Lua scripts".format(key, value["cardCount"], value["luaCount"]))
    except (OSError, ValueError, sqlite3.Error, zipfile.BadZipFile) as error:
        parser.exit(1, "Import error: {}\n".format(error))


if __name__ == "__main__":
    main()
