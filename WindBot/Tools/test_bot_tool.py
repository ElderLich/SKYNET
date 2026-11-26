"""Portable launcher regressions; these do not launch a duel client or server."""

import json
from contextlib import closing, redirect_stderr
import io
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest

import bot_tool
import fixture_deck


class PortableToolTests(unittest.TestCase):
    def test_deck_sources_are_registered_executors(self):
        decks = bot_tool.ROOT / "Game" / "AI" / "Decks"
        for source in decks.rglob("*.cs"):
            with self.subTest(source=source.name):
                self.assertEqual(source.parent, decks)
                self.assertTrue(source.name.endswith("Executor.cs"))
                self.assertRegex(source.read_text(encoding="utf-8-sig"), r"\[Deck\s*\(")

    def test_registered_suites_have_harness_sources(self):
        for suite, relative_path in bot_tool.SUITES.items():
            with self.subTest(suite=suite):
                self.assertTrue((bot_tool.ROOT / "Tools" / relative_path).is_file(), relative_path)

    def test_fixture_deck_preserves_zone_order_and_duplicate_cards(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "fixture.ydk"
            path.write_text("#created by fixture\n#main\n9\n3\n9\n#extra\n8\n!side\n4\n", encoding="utf-8-sig")
            zones = fixture_deck.read_deck(path)
            self.assertEqual({"main": [9, 3, 9], "extra": [8], "side": [4]}, zones)
            fixture_deck.write_deck(path, zones["main"], zones["extra"])
            self.assertEqual({"main": [9, 3, 9], "extra": [8], "side": []}, fixture_deck.read_deck(path))

    def test_selected_database_is_read_only_and_missing_database_is_not_created(self):
        with tempfile.TemporaryDirectory() as temporary:
            database = Path(temporary) / "cards with spaces.cdb"
            with self.assertRaises(sqlite3.OperationalError):
                bot_tool.inspect_database(database)
            self.assertFalse(database.exists())
            with closing(sqlite3.connect(database)) as connection:
                connection.executescript("CREATE TABLE datas (id INTEGER); CREATE TABLE texts (id INTEGER); INSERT INTO datas VALUES(1); INSERT INTO texts VALUES(1);")
            before = database.read_bytes()
            self.assertEqual(1, bot_tool.inspect_database(database))
            self.assertEqual(before, database.read_bytes())

    def test_native_failure_is_not_replaced_with_another_database(self):
        with tempfile.TemporaryDirectory() as temporary:
            database = Path(temporary) / "wrong-schema.cdb"
            with closing(sqlite3.connect(database)):
                pass
            with self.assertRaises(sqlite3.OperationalError):
                bot_tool.inspect_database(database)

    def test_argv_preserves_spaces_and_shell_metacharacters(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "args.json"
            arguments = ['value with spaces', '$HOME; echo accidental', '`literal`', 'HostInfo=a=b']
            code = "import json,pathlib,sys; pathlib.Path(sys.argv[1]).write_text(json.dumps(sys.argv[2:]))"
            self.assertEqual(0, bot_tool.execute([sys.executable, "-c", code, str(output)] + arguments, temporary, 10))
            self.assertEqual(arguments, json.loads(output.read_text()))

    def test_timeout_reaps_only_the_owned_child(self):
        with tempfile.TemporaryDirectory() as temporary:
            code = "import time; time.sleep(30)"
            with self.assertRaises(subprocess.TimeoutExpired):
                bot_tool.execute([sys.executable, "-c", code], temporary, 0.1)

    def test_exit_status_is_preserved(self):
        self.assertEqual(7, bot_tool.execute([sys.executable, "-c", "raise SystemExit(7)"], bot_tool.ROOT, 10))

    def test_run_requires_explicit_host_database_and_deck(self):
        with redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            bot_tool.parser().parse_args(["run"])


if __name__ == "__main__":
    unittest.main()
