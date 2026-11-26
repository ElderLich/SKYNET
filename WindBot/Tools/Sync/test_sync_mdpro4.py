import codecs
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from sync_mdpro4 import Change, apply_changes, plan_sync


class SyncTests(unittest.TestCase):
    def setUp(self):
        workspace = tempfile.TemporaryDirectory()
        self.addCleanup(workspace.cleanup)
        self.root = Path(workspace.name).resolve()

    def test_retry_keeps_concurrent_changes_and_restores_earlier_files(self):
        first, new, last = (self.root / name for name in ("first", "new", "last"))
        first.write_bytes(b"original")
        last.write_bytes(b"other editor")
        with self.assertRaisesRegex(RuntimeError, "changed during sync"):
            apply_changes([Change(first, b"original", b"updated"), Change(new, None, b"addition"),
                           Change(last, b"old", b"proposed")])
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
            "BotWrapper/bot.conf": "!Bakura\nDeck=DestinyBoard Dialog=Universal.EN\nFiends\nSUPPORT_MASTER_RULE_2020\n",
            "BotWrapper/locales/fr-FR/bot.conf":
                "!Bakura\nDeck=DestinyBoard Dialog=Universal.EN\nDémons\nSUPPORT_MASTER_RULE_2020\n",
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


if __name__ == "__main__":
    unittest.main()
