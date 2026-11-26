import tempfile
import unittest
from pathlib import Path

from unity_source import plan_unity_sources


class UnitySourceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.source = Path(temporary.name) / "standalone"
        self.target = Path(temporary.name) / "Windbot"
        self.source.mkdir()
        self.target.mkdir()
        (self.source / "Game").mkdir()
        (self.source / "WindBot.csproj").write_text(
            '<Project><ItemGroup><Compile Include="Game\\AI.cs"/></ItemGroup></Project>', encoding="utf-8")
        (self.source / "Game/AI.cs").write_bytes(b"class AI {}\r\n")
        (self.source / "Game/Ignored.cs").write_text("// unregistered", encoding="utf-8")
        (self.source / "WindBotInfo.cs").write_text("class WindBotInfo {}", encoding="utf-8")
        (self.target / "Program.cs").write_bytes(
            b"class Program {\n    private static void Run(object o)\n    {\n"
            b"        GameClient.RunSession((WindBotInfo)o);\n    }\n}\n")

    def test_repeat_sync_preserves_unity_source_and_guids(self):
        (self.target / "Game").mkdir()
        ai = self.target / "Game/AI.cs"
        ai.write_bytes(b"\xef\xbb\xbfclass AI {}\n")
        meta = ai.with_suffix(".cs.meta")
        meta.write_bytes(b"guid: installed\n")
        snapshot = {}
        first = dict(plan_unity_sources(self.source, self.target, snapshot))
        self.assertNotIn(meta, first)
        self.assertNotIn(self.target / "Game/Ignored.cs", first)
        self.assertEqual(first[ai], ai.read_bytes())
        self.assertEqual(snapshot[self.target / "Program.cs"], first[self.target / "Program.cs"])
        for target, content in first.items():
            target.write_bytes(content)
        repeat = dict(plan_unity_sources(self.source, self.target))
        self.assertTrue(all(p.read_bytes() == data for p, data in repeat.items()))
        self.assertEqual(meta.read_bytes(), b"guid: installed\n")
        self.assertTrue((self.target / "Game.meta").is_file())

    def test_custom_worker_is_preserved(self):
        target = self.target / "Program.cs"
        custom = target.read_bytes().replace(b"GameClient.RunSession((WindBotInfo)o);", b"RunMySession(o);")
        target.write_bytes(custom)
        with self.assertRaisesRegex(ValueError, "Custom session worker"):
            list(plan_unity_sources(self.source, self.target))
        self.assertEqual(target.read_bytes(), custom)


if __name__ == "__main__":
    unittest.main()
