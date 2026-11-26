import codecs
import hashlib
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sync_mdpro4 import Change, apply_changes, plan_sync
from Sync.rosters import DeckRename, RENAMED_DECKS


class SyncTests(unittest.TestCase):
    def setUp(self):
        workspace = tempfile.TemporaryDirectory()
        self.addCleanup(workspace.cleanup)
        self.root = Path(workspace.name).resolve()

    def test_retry_keeps_concurrent_changes_and_restores_earlier_files(self):
        first, new, last, retired = (self.root / name for name in ("first", "new", "last", "retired"))
        first.write_bytes(b"original")
        retired.write_bytes(b"old deck")
        last.write_bytes(b"other editor")
        with self.assertRaisesRegex(RuntimeError, "changed during sync"):
            apply_changes([Change(first, b"original", b"updated"), Change(new, None, b"addition"),
                           Change(retired, b"old deck", None),
                           Change(last, b"old", b"proposed")])
        self.assertEqual(retired.read_bytes(), b"old deck")
        self.assertEqual(first.read_bytes(), b"original")
        self.assertFalse(new.exists())
        self.assertEqual(last.read_bytes(), b"other editor")

    def test_both_destinations_converge_and_keep_unmanaged_assets(self):
        source = self.root / "WindBot"
        game, companion = self.root / "game", self.root / "companion"
        unity = game / "Assets/Scripts/Windbot"
        fixture = {
            "WindBot.csproj": '<Project><ItemGroup><Compile Include="Game/AI/Decks/*.cs"/></ItemGroup></Project>',
            "WindBotInfo.cs": "class WindBotInfo {}\n",
            "Game/AI/Decks/BakuraExecutor.cs": '[Deck("DestinyBoard", "AI_DestinyBoard")]\n',
            "Decks/AI_DestinyBoard.ydk": "#main\n123\n#extra\n!side\n",
            "Dialogs/Universal.EN.json": "{}\n",
            "BotWrapper/bot.conf": "!Bakura\nDeck=DestinyBoard Dialog=Universal.EN\nFiends\nAI_LV2 SUPPORT_MASTER_RULE_2020\n",
            "BotWrapper/locales/fr-FR/bot.conf":
                "!Bakura\nDeck=DestinyBoard Dialog=Universal.EN\nDémons\nAI_LV2 SUPPORT_MASTER_RULE_2020\n",
        }
        for name, content in fixture.items():
            file = source / name
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(content.encode())
        for repo in (game, companion):
            for subdir in ("Data/Windbot/Decks", "Data/Windbot/Dialogs", "Data/locales/en-US", "Data/locales/fr-FR"):
                (repo / subdir).mkdir(parents=True)
            (repo / "Data/Windbot/Decks/local.ydk").write_bytes(b"keep")
        unity.mkdir(parents=True)
        (unity / "Program.cs").write_bytes(
            b"class Program {\n    private static void Run(object args)\n    {\n"
            b"        GameClient.RunSession((WindBotInfo)args);\n    }\n}\n")
        meta = unity / "WindBotInfo.cs.meta"
        meta.write_bytes(b"existing guid")
        equivalent = companion / "Data/Windbot/Dialogs/Universal.EN.json"
        equivalent.write_bytes(codecs.BOM_UTF8 + b"{}\r\n")
        changes, _ = plan_sync(source, game, companion)
        self.assertTrue(changes)
        self.assertNotIn(equivalent, {item.path for item in changes})
        self.assertFalse((unity / "Game").exists())
        apply_changes(changes)
        self.assertEqual(plan_sync(source, game, companion)[0], [])
        self.assertEqual(meta.read_bytes(), b"existing guid")
        self.assertTrue((unity / "Game/AI/Decks/BakuraExecutor.cs.meta").is_file())
        for repo in (game, companion):
            self.assertEqual((repo / "Data/Windbot/Decks/local.ydk").read_bytes(), b"keep")
            self.assertEqual((repo / "Data/locales/fr-FR/bot.conf").read_bytes(),
                             (source / "BotWrapper/locales/fr-FR/bot.conf").read_bytes())

        # Simulate an installation from before a registration and its files were renamed.
        with patch.dict(RENAMED_DECKS, {"Former": DeckRename("DestinyBoard", hashlib.sha256(b"old").hexdigest(),
                                                            (hashlib.sha256(b"cards").hexdigest(),))}):
            (source / "Game/AI/Decks/BakuraExecutor.cs").rename(source / "Game/AI/Decks/DestinyBoardExecutor.cs")
            old_source = unity / "Game/AI/Decks/FormerExecutor.cs"
            old_meta = old_source.with_suffix(".cs.meta")
            new_meta = unity / "Game/AI/Decks/DestinyBoardExecutor.cs.meta"
            old_source.write_bytes(b"old")
            old_meta.write_bytes(b"local asset GUID")
            old_decks = [repo / "Data/Windbot/Decks/AI_Former.ydk" for repo in (game, companion)]
            for deck in old_decks:
                deck.write_bytes(b"cards")
            target = game / "Data/locales/en-US/bot.conf"
            target.write_bytes(target.read_bytes() + b"!Former\nDeck=Former\nOld deck\nAI_LV2\n")
            old_decks[0].write_bytes(b"local edit")
            with self.assertRaisesRegex(ValueError, "Locally modified retired file"):
                plan_sync(source, game, companion)
            old_decks[0].write_bytes(b"cards")
            apply_changes(plan_sync(source, game, companion)[0])
            self.assertEqual(new_meta.read_bytes(), b"local asset GUID")
            self.assertFalse(any(p.exists() for p in [old_source, old_meta] + old_decks))
            self.assertNotIn(b"Former", target.read_bytes())
            self.assertEqual(plan_sync(source, game, companion)[0], [])


if __name__ == "__main__":
    unittest.main()
