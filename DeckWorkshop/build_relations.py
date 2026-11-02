#!/usr/bin/env python3
"""Build portable static relationship indexes from the imported snapshots."""

import argparse
import gzip
import hashlib
import json
from pathlib import Path
from RelatedCards.build_related_cards import CATEGORY_ORDER, RelatedCardsBuilder


ROOT = Path(__file__).resolve().parent
SOURCE_REVISION = "878b7070a91272074438e118390b25d551b2e86b"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(root):
    manifest = json.loads((root / "data-provenance.json").read_text(encoding="utf-8"))
    folder = root / "Relations"
    folder.mkdir(exist_ok=True)
    standard = RelatedCardsBuilder(root / "Data/standard/cards.cdb", root / "Data/standard/scripts", [])
    try:
        constants = dict(standard.constants)
        for name in ("standard", "rush"):
            builder = standard if name == "standard" else RelatedCardsBuilder(
                root / "Data/rush/cards.cdb", root / "Data/rush/scripts", [], constants=constants, rush=True)
            try:
                print("Building {} static relationships...".format(name), flush=True)
                cards = builder.build()
                inputs = manifest["formats"][name]
                payload = {"schemaVersion": 1, "format": name,
                           "source": {"revision": SOURCE_REVISION,
                                      "toolSha256": digest(ROOT / "RelatedCards/build_related_cards.py"),
                                      "databaseSha256": inputs["databaseSha256"],
                                      "archiveSha256": inputs["archiveSha256"]},
                           "categories": CATEGORY_ORDER, "cards": cards}
                encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
                destination = folder / (name + ".json.gz")
                temporary = folder / (name + ".json.gz.tmp")
                temporary.write_bytes(gzip.compress(encoded, mtime=0))
                temporary.replace(destination)
                edges = sum(len(ids) for categories in cards.values() for ids in categories.values())
                print("{}: {} indexed cards, {} category edges, {} compressed bytes".format(
                    name, len(cards), edges, destination.stat().st_size), flush=True)
            finally:
                if name != "standard":
                    builder.close()
    finally:
        standard.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT, help="Explicit DeckWorkshop snapshot root")
    build(parser.parse_args().root.resolve())
