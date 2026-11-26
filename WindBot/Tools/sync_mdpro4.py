#!/usr/bin/env python3
"""Publish WindBot to the MDPro4 Unity checkout and its companion Data checkout."""

import argparse
import codecs
from dataclasses import dataclass
import hashlib
import os
from pathlib import Path
import sys
import tempfile
from typing import Optional

from Sync.rosters import RENAMED_DECKS, plan_rosters
from Sync.unity_source import plan_unity_sources


WINDBOT = Path(__file__).resolve().parents[1]
CHECKOUTS = WINDBOT.parent.parent


@dataclass
class Change:
    path: Path
    before: Optional[bytes]
    after: Optional[bytes]


def require_folder(path):
    if not path.is_dir():
        raise ValueError("Directory not found: " + str(path))
    return path


def file_content(path):
    try:
        return path.read_bytes()
    except FileNotFoundError:
        return None


def comparable_text(content):
    if content.startswith(codecs.BOM_UTF8):
        content = content[len(codecs.BOM_UTF8):]
    return content.replace(b"\r\n", b"\n")


def resource_files(windbot, data_roots):
    for folder, suffix in (("Decks", ".ydk"), ("Dialogs", ".json")):
        source = require_folder(windbot / folder)
        files = sorted(source.rglob("*" + suffix))
        if not files:
            raise ValueError("Empty WindBot resource directory: " + str(source))
        for file in files:
            content = file.read_bytes()
            for data in data_roots:
                yield data / "Windbot" / folder / file.relative_to(source), content


def executor_file(name):
    return Path("Game/AI/Decks") / (name + "Executor.cs")


def retired_files(windbot, unity, data_roots, originals):
    for old, rename in RENAMED_DECKS.items():
        source = unity / executor_file(old)
        old_deck = Path("Decks") / ("AI_" + old + ".ydk")
        new_deck = Path("Decks") / ("AI_" + rename.new_name + ".ydk")
        paths = [(source, (rename.old_executor_hash,))]
        paths.extend((root / "Windbot" / old_deck, rename.old_deck_hashes) for root in data_roots)
        for path, hashes in paths:
            content = file_content(path)
            if content is None:
                continue
            if not (windbot / new_deck).is_file() or not (windbot / executor_file(rename.new_name)).is_file():
                raise ValueError("Missing replacement for retired executor " + old)
            if hashlib.sha256(comparable_text(content)).hexdigest() not in hashes:
                raise ValueError("Locally modified retired file; reconcile it before syncing: " + str(path))
            originals[path] = content
            yield path, None
        meta = source.with_suffix(".cs.meta")
        new_meta = (unity / executor_file(rename.new_name)).with_suffix(".cs.meta")
        old_content = originals.get(meta) if meta in originals else file_content(meta)
        if old_content is not None:
            if not (windbot / executor_file(rename.new_name)).is_file():
                raise ValueError("Missing replacement for retired Unity asset " + old)
            existing = file_content(new_meta)
            if existing is not None and comparable_text(existing) != comparable_text(old_content):
                raise ValueError("Conflicting metadata for renamed Unity asset: " + str(new_meta))
            originals[meta] = old_content
            yield meta, None


def plan_sync(windbot, game, companion):
    game, companion = game.resolve(), companion.resolve()
    if game == companion:
        raise ValueError("Choose separate game and companion Data checkouts.")
    unity = require_folder(game / "Assets/Scripts/Windbot")
    data_roots = [require_folder(root / "Data") for root in (game, companion)]
    allowed = [unity]
    for data in data_roots:
        require_folder(data / "locales")
        for folder in ("Decks", "Dialogs"):
            allowed.append(require_folder(data / "Windbot" / folder))
        allowed.append(data / "locales")

    originals = {}
    former_paths = {executor_file(rename.new_name): executor_file(old) for old, rename in RENAMED_DECKS.items()}
    outputs = list(plan_unity_sources(windbot, unity, originals=originals, former_paths=former_paths))
    outputs.extend(resource_files(windbot, data_roots))
    outputs.extend(plan_rosters(windbot, data_roots, originals=originals).items())
    outputs.extend(retired_files(windbot, unity, data_roots, originals))
    changes = []
    seen = set()
    for path, content in outputs:
        if path != path.resolve() or not any(parent in path.parents for parent in allowed):
            raise ValueError("Unexpected sync destination: " + str(path))
        if path in seen:
            raise ValueError("Repeated sync destination: " + str(path))
        seen.add(path)
        before = originals[path] if path in originals else file_content(path)
        if before == content:
            continue
        # Game-owned files can already contain the same text with another BOM or line ending.
        if before is not None and content is not None and comparable_text(before) == comparable_text(content):
            continue
        changes.append(Change(path, before, content))
    return sorted(changes, key=lambda change: (change.after is None, str(change.path))), sum(path.suffix != ".meta" for path in seen)


def replace_file(path, content):
    if content is None:
        path.unlink()
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=".windbot-", delete=False) as output:
            temp = Path(output.name)
            output.write(content)
        os.chmod(temp, path.stat().st_mode if path.exists() else 0o644)
        os.replace(temp, path)
    finally:
        if temp is not None and temp.exists():
            temp.unlink()


def apply_changes(changes):
    applied = []
    try:
        for change in changes:
            if change.path.resolve() != change.path or file_content(change.path) != change.before:
                raise RuntimeError("Destination changed during sync: " + str(change.path))
            replace_file(change.path, change.after)
            applied.append(change)
    except (OSError, RuntimeError, KeyboardInterrupt) as cause:
        failures = []
        for change in reversed(applied):
            try:
                if file_content(change.path) != change.after:
                    raise RuntimeError("changed since writing")
                replace_file(change.path, change.before)
            except (OSError, RuntimeError) as exc:
                failures.append(str(change.path) + " (" + str(exc) + ")")
        if failures:
            raise RuntimeError("Sync failed. Could not restore: " + ", ".join(failures)) from cause
        raise


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", type=Path, default=CHECKOUTS / "MDPro_3",
                        help="MDPro4 Unity checkout (default: sibling MDPro_3)")
    parser.add_argument("--data-root", type=Path, default=CHECKOUTS / "MDPro4_Data",
                        help="Companion checkout containing Data (default: sibling MDPro4_Data)")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true", help="List pending writes without applying them")
    mode.add_argument("--check", action="store_true", help="Verify both destinations; exit 1 for pending writes")
    args = parser.parse_args(argv)
    try:
        changes, total = plan_sync(WINDBOT, args.game_root, args.data_root)
        for change in changes:
            action = "REMOVE" if change.after is None else "ADD" if change.before is None else "UPDATE"
            print(f"{action:7}{change.path}")
        if args.check or args.dry_run:
            print(f"{len(changes)} pending changes. Checked {total} source, resource and roster files.")
            return int(args.check and bool(changes))
        apply_changes(changes)
        print(f"Synced {len(changes)} changes. Checked {total} source, resource and roster files.")
        return 0
    except (ValueError, OSError, RuntimeError) as exc:
        print("WindBot sync failed: " + str(exc), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
