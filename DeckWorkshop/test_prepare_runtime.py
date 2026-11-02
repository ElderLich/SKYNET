"""Safety/identity checks for the standalone runtime data exporter."""

import hashlib
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
import zipfile

from prepare_runtime import prepare


class RuntimeDataTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.standard = self.root / "standard"
        self.expansion = self.root / "expansion"
        for folder, code in ((self.standard, 1), (self.expansion, 2)):
            (folder / "scripts").mkdir(parents=True)
            conn = sqlite3.connect(folder / "cards.cdb")
            try:
                conn.execute("CREATE TABLE datas (id INTEGER PRIMARY KEY, type INTEGER)")
                conn.execute("CREATE TABLE texts (id INTEGER PRIMARY KEY, name TEXT)")
                conn.execute("INSERT INTO datas VALUES (?, 33)", (code,))
                conn.execute("INSERT INTO texts VALUES (?, ?)", (code, "Card " + str(code)))
                conn.commit()
            finally:
                conn.close()
            (folder / "scripts" / ("c%d.lua" % code)).write_bytes(b"-- source\r\n")
        (self.standard / "scripts" / "utility.lua").write_bytes(b"-- base helper\r\n")
        (self.standard / "archive-metadata").mkdir()
        (self.standard / "archive-metadata" / "LICENSE").write_bytes(b"attribution\n")
        sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
        self.manifest = {"cardIds": [2], "databaseSha256": sha(self.expansion / "cards.cdb"),
                         "scripts": {"c2.lua": sha(self.expansion / "scripts" / "c2.lua")}}
        self.write_manifest()

    def write_manifest(self):
        (self.expansion / "provenance.json").write_text(json.dumps(self.manifest), encoding="utf-8")

    def test_adds_only_selected_cards_and_preserves_helpers_and_attribution(self):
        # A package helper must never silently override the pinned base helper.
        (self.expansion / "scripts" / "utility.lua").write_bytes(b"unreviewed helper")
        original = (self.standard / "cards.cdb").read_bytes()
        receipt = prepare(self.standard, self.expansion, self.root / "out")
        self.assertEqual(receipt["cardCount"], 2)
        conn = sqlite3.connect(self.root / "out/locales/en-US/cards.cdb")
        try:
            self.assertEqual(conn.execute("SELECT id,name FROM texts ORDER BY id").fetchall(), [(1, "Card 1"), (2, "Card 2")])
        finally:
            conn.close()
        with zipfile.ZipFile(self.root / "out/script.zip") as archive:
            self.assertEqual(archive.read("script/c2.lua"), b"-- source\r\n")
            self.assertEqual(archive.read("script/utility.lua"), b"-- base helper\r\n")
            self.assertEqual(archive.read("LICENSE"), b"attribution\n")
        self.assertEqual((self.standard / "cards.cdb").read_bytes(), original)

    def test_rejects_tampering_before_creating_output(self):
        (self.expansion / "scripts" / "c2.lua").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "script hash"):
            prepare(self.standard, self.expansion, self.root / "out")
        self.assertFalse((self.root / "out").exists())

    def test_rejects_released_id_collision(self):
        conn = sqlite3.connect(self.standard / "cards.cdb")
        try:
            conn.execute("INSERT INTO datas VALUES (2,33)")
            conn.execute("INSERT INTO texts VALUES (2,'Released')")
            conn.commit()
        finally:
            conn.close()
        with self.assertRaisesRegex(ValueError, "collides with a released card"):
            prepare(self.standard, self.expansion, self.root / "out")

    def test_refuses_existing_output(self):
        output = self.root / "out"
        output.mkdir()
        (output / "keep.txt").write_text("existing runtime")
        with self.assertRaisesRegex(ValueError, "Output already exists"):
            prepare(self.standard, self.expansion, output)
        self.assertEqual((output / "keep.txt").read_text(), "existing runtime")

    def test_export_is_deterministic_and_standard_only_is_supported(self):
        first = prepare(self.standard, None, self.root / "first")
        second = prepare(self.standard, None, self.root / "second")
        self.assertEqual(first["cardCount"], 1)
        self.assertEqual(first["scriptsSha256"], second["scriptsSha256"])
        self.assertEqual(first["databaseSha256"], second["databaseSha256"])


if __name__ == "__main__":
    unittest.main()
