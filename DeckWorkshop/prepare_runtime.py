#!/usr/bin/env python3
"""Prepare an isolated duel data directory from the bundled Standard snapshot.

An optional, explicitly selected expansion adds cards without replacing released
cards or shared Lua helpers. No Lua is executed and no installed game is changed.
"""

import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import sqlite3
import zipfile


ROOT = Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def readonly(path):
    return sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)


def prepare(standard, expansion, output):
    standard, output = standard.resolve(), output.resolve()
    database = standard / "cards.cdb"
    scripts = standard / "scripts"
    if not database.is_file() or not scripts.is_dir():
        raise ValueError("Standard snapshot requires cards.cdb and scripts/.")
    if output.exists():
        raise ValueError("Output already exists; choose a new directory.")
    added = []
    overlay_scripts = {}
    receipt = {"schemaVersion": 1, "purpose": "Isolated Standard duel data; no Lua executed",
               "standardDatabaseSha256": digest(database), "expansion": None}
    # Validate the entire overlay before creating any output.
    if expansion is not None:
        expansion = expansion.resolve()
        manifest = json.loads((expansion / "provenance.json").read_text(encoding="utf-8"))
        overlay_db = expansion / "cards.cdb"
        if digest(overlay_db) != manifest["databaseSha256"]:
            raise ValueError("Expansion database hash does not match its provenance.")
        with closing(readonly(database)) as base, closing(readonly(overlay_db)) as overlay:
            for table in ("datas", "texts"):
                columns = lambda conn: conn.execute("PRAGMA table_info(" + table + ")").fetchall()
                if columns(base) != columns(overlay):
                    raise ValueError("Expansion schema differs from Standard: " + table)
                ids = {row[0] for row in overlay.execute("SELECT id FROM " + table)}
                if ids != set(manifest["cardIds"]):
                    raise ValueError("Expansion card IDs differ from its provenance: " + table)
                if any(base.execute("SELECT 1 FROM " + table + " WHERE id=?", (code,)).fetchone() for code in ids):
                    raise ValueError("Expansion collides with a released card; review the ID migration first.")
            for code in manifest["cardIds"]:
                name = "c%d.lua" % code
                source = expansion / "scripts" / name
                if digest(source) != manifest["scripts"][name]:
                    raise ValueError("Expansion script hash differs: " + name)
                if (scripts / name).exists():
                    raise ValueError("Expansion collides with a released script: " + name)
                overlay_scripts["script/" + name] = source
            added = sorted(manifest["cardIds"])
        receipt["expansion"] = {"path": str(expansion), "manifestSha256": digest(expansion / "provenance.json"),
                                "databaseSha256": digest(overlay_db), "cardIds": added}
    output.mkdir(parents=True)
    result_db = output / "locales" / "en-US" / "cards.cdb"
    result_db.parent.mkdir(parents=True)
    with closing(readonly(database)) as source, closing(sqlite3.connect(result_db)) as target:
        source.backup(target)
        if expansion is not None:
            with closing(readonly(expansion / "cards.cdb")) as overlay:
                with target:
                    for table in ("datas", "texts"):
                        rows = overlay.execute("SELECT * FROM " + table).fetchall()
                        if rows:
                            target.executemany("INSERT INTO " + table + " VALUES (" + ",".join("?" for _ in rows[0]) + ")", rows)
        if target.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
            raise ValueError("Prepared database failed integrity_check.")
        receipt["cardCount"] = target.execute("SELECT COUNT(*) FROM datas").fetchone()[0]
    entries = {"script/" + p.relative_to(scripts).as_posix(): p for p in scripts.rglob("*") if p.is_file()}
    metadata = standard / "archive-metadata"
    entries.update({p.relative_to(metadata).as_posix(): p for p in metadata.rglob("*") if p.is_file()})
    entries.update(overlay_scripts)
    script_archive = output / "script.zip"
    with zipfile.ZipFile(script_archive, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, source in sorted(entries.items()):
            # Stable metadata makes repeated preparation byte-for-byte comparable.
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, source.read_bytes())
    receipt.update({"databaseSha256": digest(result_db), "scriptsSha256": digest(script_archive),
                    "archiveFiles": len(entries)})
    (output / "data-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--standard", type=Path, default=ROOT / "Data" / "standard")
    parser.add_argument("--expansion", type=Path, help="Explicit expansion folder containing cards.cdb, scripts/, provenance.json")
    parser.add_argument("--output", type=Path, required=True, help="New directory; never overwrites an existing runtime")
    args = parser.parse_args()
    try:
        receipt = prepare(args.standard, args.expansion, args.output)
    except (OSError, ValueError, KeyError, sqlite3.Error) as error:
        parser.exit(1, "Data preparation failed: %s\n" % error)
    print("Prepared %d cards and %d archive files in %s" % (receipt["cardCount"], receipt["archiveFiles"], args.output.resolve()))
    print("Use locales/en-US/cards.cdb for WindBot and the same database + script.zip for the duel host.")


if __name__ == "__main__":
    main()
