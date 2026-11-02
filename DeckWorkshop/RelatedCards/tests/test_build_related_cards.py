import contextlib
import io
import json
import sqlite3
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

from DeckWorkshop.RelatedCards import build_related_cards as related


CONSTANTS = """TYPE_MONSTER = 1
TYPE_SPELL = 2
TYPE_TRAP = 4
TYPE_NORMAL = 16
TYPE_EFFECT = 32
TYPE_FUSION = 64
TYPE_XYZ = 0x800000
TYPE_LINK = 0x4000000
TYPE_TOKEN = 0x4000
RACE_DRAGON = 8192
ATTRIBUTE_LIGHT = 16
"""


def write_database(path, ids, aliases=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    with contextlib.closing(sqlite3.connect(path)) as connection, connection:
        connection.execute("CREATE TABLE datas (id, setcode, type, level, race, attribute, atk, def, alias)")
        connection.executemany("INSERT INTO datas VALUES (?,0,33,4,8192,16,1000,1000,?)",
                               [(card_id, (aliases or {}).get(card_id, 0)) for card_id in ids])


def write_archive(path, scripts):
    with zipfile.ZipFile(path, "w") as archive:
        for name, source in scripts.items():
            archive.writestr("script/" + name, source)


class RelatedCardsTests(unittest.TestCase):
    def setUp(self):
        scratch = Path(__file__).resolve().parents[3] / "Library/Codex_QA/RelatedCards20261025"
        scratch.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=scratch)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.standard_db = self.root / "Data/locales/en-US/cards.cdb"
        self.rush_db = self.standard_db.with_name("rush_cards.cdb")
        write_database(self.standard_db, [100, 101])
        write_database(self.rush_db, [200, 201, 202, 203, 204, 205], {204: 200})
        write_archive(self.root / "Data/script.zip", {
            "constant.lua": CONSTANTS,
            "c100.lua": "aux.AddCodeList(c,101)",
        })
        write_archive(self.root / "Data/rush_scripts.zip", {
            "RDBase.lua": "RACE_GALAXY = 0x40000000",
            "RDLegend.lua": "TYPE_LEGEND = 8",
            "RDMaximum.lua": "TYPE_MAXIMUM = 0x8000",
            "c200.lua": "local list={201,202}\nRD.AddMaximumProcedure(c,3500,list[1],list[2])",
            "c203.lua": "local list={201,202}\nRD.AddFusionProcedure(c,list[1],list[2])",
            "c204.lua": "RD.AlternateCard(200)\n",
            "c205.lua": """function cm.material(c)
    return c:IsRace(RACE_DRAGON) and c:IsLevel(4)
end
RD.SelectAndDoAction(HINTMSG_ATOHAND,RD.Filter(cm.material,e,tp),tp,LOCATION_GRAVE,0,1,1,nil,action)
""",
        })

    def run_main(self, *args):
        with patch.object(related, "find_repo_root", return_value=self.root), \
                patch.object(sys, "argv", ["build_related_cards.py", *args]), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return related.main()

    def read_output(self):
        return json.loads((self.root / "Assets/StreamingAssets/RelatedCards.json").read_text())

    def test_default_merges_independent_formats_and_rush_materials(self):
        self.assertEqual(self.run_main(), 0)
        result = self.read_output()
        self.assertEqual(result["100"]["ID"], [101])
        self.assertTrue({201, 202, 204}.issubset(result["200"]["ID"]))
        self.assertTrue({200, 203, 204}.issubset(result["201"]["ID"]))
        self.assertIn(201, result["205"]["Targets"])
        self.assertIn(205, result["201"]["Level/Link/Rank"])
        for card_id, categories in result.items():
            self.assertEqual(list(categories), related.CATEGORY_ORDER)
            for ids in categories.values():
                self.assertEqual(len(ids), len(set(ids)))
                self.assertNotIn(int(card_id), ids)
                self.assertTrue(all((int(card_id) < 200) == (value < 200) for value in ids))

    def test_standard_only_does_not_require_rush_inputs(self):
        self.rush_db.unlink()
        self.assertEqual(self.run_main("--format", "standard"), 0)
        self.assertEqual(set(self.read_output()), {"100", "101"})

    def test_missing_rush_input_does_not_replace_output(self):
        self.run_main()
        before = self.read_output()
        self.rush_db.unlink()
        with self.assertRaises(SystemExit):
            self.run_main()
        self.assertEqual(self.read_output(), before)

    def test_overlapping_formats_are_rejected(self):
        with contextlib.closing(sqlite3.connect(self.rush_db)) as connection, connection:
            connection.execute("UPDATE datas SET id=100 WHERE id=200")
        with self.assertRaises(SystemExit):
            self.run_main()
        self.assertFalse((self.root / "Assets/StreamingAssets/RelatedCards.json").exists())

    def test_literal_code_tables_and_unresolved_values(self):
        parser = related.ScriptExpressionParser({}, {}, {"list": (201, 202)})
        self.assertEqual(parser.parse_code_values("list"), (201, 202))
        self.assertEqual(parser.parse_code_values("list[2],201"), (202, 201))
        self.assertEqual(parser.parse_code_values("table.unpack(list),203"), (201, 202, 203))
        self.assertEqual(parser.parse_code_values("{201,202},203"), (201, 202, 203))
        self.assertIsNone(parser.parse_code_values("list[0]"))
        self.assertIsNone(parser.parse_code_values("list[3]"))
        self.assertIsNone(parser.parse_code_values("list[index]"))
        self.assertIsNone(parser.parse_code_values("Duel.GetMatchingGroup(...)"))

    def test_unreferenced_filters_do_not_add_relations(self):
        archive = self.root / "Data/script.zip"
        write_archive(archive, {"constant.lua": CONSTANTS, "c100.lua": """function s.unusedfilter(c)
return c:IsCode(101)
end
"""})
        self.run_main("--format", "standard")
        self.assertEqual(self.read_output(), {})

    def test_alias_is_a_valid_exact_code_target(self):
        card = related.CardRecord(204, 0, 33, 8192, 16, 1000, 1000, 4, None, None, 200)
        matcher = related.CardMatcher({204: card}, {})
        self.assertEqual(matcher.ids_for_condition(("id_any", (200,))), {204})

    def test_rush_predicates_keep_static_defense_and_named_code(self):
        parser = related.ScriptExpressionParser({}, {}, {"list": (201, 202)})
        self.assertEqual(parser.parse_atom("RD.IsDefense(c,1000)"), [(("defense_eq", 1000),)])
        self.assertEqual(parser.parse_atom("RD.IsDefenseBelow(c,1000)"), [(("defense_max", 1000),)])
        self.assertEqual(parser.parse_atom("RD.IsLegendCode(c,list[2])"), [(("id_any", (202,)),)])

    def test_signed_cyborg_race_is_normalized_and_matches_bit_31(self):
        with contextlib.closing(sqlite3.connect(self.rush_db)) as connection, connection:
            connection.execute("UPDATE datas SET race=-2147483648 WHERE id=201")
        builder = related.RelatedCardsBuilder(self.rush_db, self.root / "Data/rush_scripts.zip", [], constants={})
        self.addCleanup(builder.close)
        self.assertEqual(builder.cards[201].race, 0x80000000)
        matcher = related.CardMatcher(builder.cards, {})
        self.assertEqual(matcher.ids_for_condition(("race_any", 0x80000000)), {201})
        with self.assertRaisesRegex(ValueError, "unsigned storage width"):
            list(related.iter_set_bits(-2147483648))

    def test_alternate_cycles_fail_instead_of_recursing(self):
        write_archive(self.root / "Data/rush_scripts.zip", {
            "c200.lua": "RD.AlternateCard(204)",
            "c204.lua": "RD.AlternateCard(200)",
        })
        builder = related.RelatedCardsBuilder(self.rush_db, self.root / "Data/rush_scripts.zip", [], constants={})
        self.addCleanup(builder.close)
        with self.assertRaisesRegex(ValueError, "Cyclic alternate-card"):
            builder.parse_script_groups("c200.lua")


if __name__ == "__main__":
    unittest.main()
