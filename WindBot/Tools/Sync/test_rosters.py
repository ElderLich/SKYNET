import codecs
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from rosters import plan_rosters, validate_rosters


def entry(title, command, description, flags="SUPPORT_MASTER_RULE_2020"):
    return f"!{title}\n{command}\n{description}\n{flags}\n"


class MaintainedRostersTests(unittest.TestCase):
    def test_all_shipped_locales_cover_the_playable_roster(self):
        windbot = Path(__file__).resolve().parents[2]
        rosters = validate_rosters(windbot)
        self.assertGreater(len(rosters), 1)
        for locale, roster in rosters.items():
            with self.subTest(locale=locale):
                self.assertEqual(rosters["en-US"].entries.keys(), roster.entries.keys())


class RosterSyncTests(unittest.TestCase):
    def setUp(self):
        scratch = TemporaryDirectory()
        self.addCleanup(scratch.cleanup)
        self.root = Path(scratch.name) / "WindBot"
        self.game = Path(scratch.name) / "game/Data"
        self.english = self.root / "BotWrapper/bot.conf"
        self.french = self.root / "BotWrapper/locales/fr-FR/bot.conf"
        for directory in (self.french.parent, self.root / "Game/AI/Decks", self.root / "Decks",
                          self.root / "Dialogs", self.game / "locales/en-US", self.game / "locales/fr-FR"):
            directory.mkdir(parents=True)
        self.file = self.root / "Game/AI/Decks/BakuraExecutor.cs"
        self.file.write_text('[Deck("Bakura", "AI_Boss")]\nclass Example {}', encoding="utf-8")
        (self.root / "Decks/AI_Boss.ydk").touch()
        for name in ("Dialog.EN", "Dialog.FR"):
            (self.root / "Dialogs" / (name + ".json")).touch()
        self.random = entry("Random", "Random=AI_LV1", "Choose from this group.")
        self.bakura = entry("Bakura", "Name=Bakura Deck='Bakura' Dialog=Dialog.EN", "Board control.")
        self.bakufr = entry("Bakura le maître", 'Name="Bakura le maître" Deck=Bakura Dialog=Dialog.FR', "Contrôle.")
        self.head = "# Solo menu\n\n"
        self.english.write_bytes((self.head + self.random + "\n" + self.bakura).encode())
        self.french.write_bytes((self.head + self.random + "\n" + self.bakufr).encode())

    def test_apply_all_locales_twice_with_external_entries_notes_and_utf16(self):
        path = self.game / "locales/fr-FR/bot.conf"
        other = entry("Externe", "Deck=GameOnly Dialog=Local", "Ce bot appartient au jeu.")
        old = self.head + self.random + "\n# Local\n" + other + "\n" + self.bakura
        path.write_bytes(codecs.BOM_UTF16_LE + old.replace("\n", "\r\n").encode("utf-16-le"))
        originals = {}
        result = plan_rosters(self.root, [self.game], originals)
        self.assertEqual(path.read_bytes(), originals[path])
        self.assertIsNone(originals[self.game / "locales/en-US/bot.conf"])
        self.assertEqual(2, len(result))
        self.assertEqual(self.english.read_bytes(), result[self.game / "locales/en-US/bot.conf"])
        merged = result[path]
        self.assertTrue(merged.startswith(codecs.BOM_UTF16_LE))
        text = merged.decode("utf-16")
        self.assertIn("# Local\r\n!Externe", text)
        self.assertLess(text.index("!Externe"), text.index("!Bakura le maître"))
        self.assertIn("Contrôle.\r\n", text)
        self.assertNotIn("Board control", text)
        for target, content in result.items():
            target.write_bytes(content)
        self.assertEqual(result, plan_rosters(self.root, [self.game]))

    def test_managed_order_missing_entries_and_target_only_trailer(self):
        target = self.game / "locales/fr-FR/bot.conf"
        target.write_bytes((self.head + self.bakufr + "\n" + entry("X", "Deck=X", "X") + "# Local footer").encode())
        result = plan_rosters(self.root, [self.game])[target]
        self.assertLess(result.index(b"!Random"), result.index(b"!Bakura"))
        self.assertLess(result.index(b"!Bakura"), result.index(b"!X"))
        self.assertTrue(result.endswith(b"# Local footer"))
        target.write_bytes(result)
        self.assertEqual(result, plan_rosters(self.root, [self.game])[target])

    def test_new_release_needs_all_languages_and_real_dialog(self):
        with self.file.open("a", encoding="utf-8") as f:
            f.write('\n[Deck("New", "AI_New", "Easy")]')
        (self.root / "Decks/AI_New.ydk").touch()
        with self.assertRaisesRegex(ValueError, "playable executor New"):
            validate_rosters(self.root)
        addition = entry("New", "Deck=New Dialog=Dialog.EN", "New.")
        with self.english.open("a", encoding="utf-8") as f:
            f.write(addition)
        with self.assertRaisesRegex(ValueError, "missing.*deck:New"):
            validate_rosters(self.root)
        with self.french.open("a", encoding="utf-8") as f:
            f.write(addition.replace("Dialog.EN", "dialog.fr"))
        with self.assertRaisesRegex(ValueError, "missing dialog dialog.fr.json"):
            validate_rosters(self.root)

    def test_custom_deck_picker_can_share_a_registration(self):
        for path, dialog in ((self.english, "Dialog.EN"), (self.french, "Dialog.FR")):
            with path.open("a", encoding="utf-8") as f:
                f.write(entry("Custom", f'Deck="Bakura" Dialog={dialog}', "Pick a file.", "SELECT_DECKFILE"))
        rosters = validate_rosters(self.root)
        self.assertEqual({"random:AI_LV1", "deck:Bakura", "custom:Bakura"}, set(rosters["fr-FR"].entries))

    def test_exclusion_removes_stale_entries_and_requires_hidden_registration(self):
        extra = self.root / "Game/AI/Decks/RaiseMoonExecutor.cs"
        extra.write_text('[Deck("RaiseMoon", "AI_RaiseMoon")]', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "excluded executor must be registered as NotFinished"):
            validate_rosters(self.root)
        extra.write_text('[Deck("RaiseMoon", "AI_RaiseMoon", "NotFinished")]', encoding="utf-8")
        target = self.game / "locales/fr-FR/bot.conf"
        stale = entry("Old", "Deck=RaiseMoon", "Old") + entry("Custom", "Deck=RaiseMoon", "Old", "SELECT_DECKFILE")
        target.write_bytes(self.french.read_bytes() + stale.encode())
        self.assertNotIn(b"RaiseMoon", plan_rosters(self.root, [self.game])[target])

    def test_duplicate_key_or_incomplete_entry_is_rejected_before_writes(self):
        target = self.game / "locales/fr-FR/bot.conf"
        target.write_bytes((self.bakufr + self.bakufr).encode())
        with self.assertRaisesRegex(ValueError, "duplicate menu entry deck:Bakura"):
            plan_rosters(self.root, [self.game])
        target.write_bytes(b"!unfinished\nDeck=A\n")
        with self.assertRaisesRegex(ValueError, "incomplete four-line bot entry"):
            plan_rosters(self.root, [self.game])
        self.assertFalse((self.game / "locales/en-US/bot.conf").exists())

    def test_untranslated_game_locale_is_reported(self):
        (self.game / "locales/it-IT").mkdir()
        with self.assertRaisesRegex(ValueError, "canonical translation.*it-IT"):
            plan_rosters(self.root, [self.game])


if __name__ == "__main__":
    unittest.main()
