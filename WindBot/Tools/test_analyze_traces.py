import json
import os
import tempfile
import unittest
from contextlib import redirect_stderr
from io import StringIO
from pathlib import Path
from unittest.mock import patch

import analyze_traces


class TraceAnalysisTests(unittest.TestCase):
    def write_capture(self, directory, rows, name="duel.jsonl"):
        path = Path(directory) / name
        path.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
        return path

    def test_completed_capture_summarizes_actions_fallbacks_and_unsupported(self):
        common = {"schema": analyze_traces.SCHEMA, "duelId": "duel-1", "agent": "TestExecutor",
                  "matchOrder": 4, "roomName": "M#123456789", "duelNumber": 2}
        rows = [
            dict(common, sequence=0, kind="start", recordedAt="2026-11-16T12:00:00.0000000Z"),
            dict(common, sequence=1, kind="decision", prompt="select_idlecmd",
                 observation={"global": {"turn": 2, "phase": 4}, "cards": [
                     {"known": True, "code": 123, "controller": 0, "location": 2, "sequence": 0}
                 ]}, offers=[{"id": 0, "type": "summon", "card": {
                     "controller": 0, "location": 2, "sequence": 0}}], selectedOfferId=0,
                 decisionSource="Summon:TestExecutor.Summon", unsupported=["shuffle-action-not-supported"]),
            dict(common, sequence=2, kind="selection", prompt="select_card",
                 observation={"global": {"turn": 2, "phase": 4}},
                 selection={"candidates": [{"id": 0, "known": True, "code": 456}]},
                 selectedCandidateIds=[0], responseSource="legal-fallback"),
            dict(common, sequence=3, kind="outcome", outcome=0),
            dict(common, sequence=4, kind="end", outcome=0, completed=True),
        ]
        with tempfile.TemporaryDirectory() as directory:
            self.write_capture(directory, rows)
            report = analyze_traces.analyze([directory])
        self.assertEqual(report["summary"]["completedCaptures"], 1)
        self.assertEqual(report["summary"]["outcomes"], {"win": 1})
        self.assertEqual(report["summary"]["actionTypes"], {"summon": 1})
        self.assertEqual(report["summary"]["responseSources"], {"legal-fallback": 1})
        self.assertEqual(report["summary"]["decisionSources"], {"Summon:TestExecutor.Summon": 1})
        self.assertEqual(report["captures"][0]["startOrder"], 1)
        self.assertEqual(report["captures"][0]["matchOrder"], 4)
        self.assertEqual(report["captures"][0]["duelNumber"], 2)
        self.assertEqual(report["decisionRows"][0]["selectedCardCode"], 123)
        self.assertEqual(report["decisionRows"][1]["selectedCandidateCodes"], "456")

    def test_rejects_non_contiguous_sequences(self):
        rows = [
            {"schema": analyze_traces.SCHEMA, "duelId": "duel-1", "agent": "A", "sequence": 0, "kind": "start"},
            {"schema": analyze_traces.SCHEMA, "duelId": "duel-1", "agent": "A", "sequence": 2, "kind": "end"},
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = self.write_capture(directory, rows)
            with self.assertRaisesRegex(ValueError, "non-contiguous sequence"):
                analyze_traces.read_capture(path)

    def test_rejects_records_after_end(self):
        rows = [dict(schema=analyze_traces.SCHEMA, duelId="one", sequence=i, kind=kind)
                for i, kind in enumerate(("start", "end", "decision"))]
        with tempfile.TemporaryDirectory() as directory:
            path = self.write_capture(directory, rows)
            with self.assertRaisesRegex(ValueError, "after end"):
                analyze_traces.read_capture(path)

    def test_outputs_cannot_replace_captures_or_each_other(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.write_capture(directory, [dict(schema=analyze_traces.SCHEMA, duelId="one",
                                                       sequence=0, kind="start")])
            data = path.read_bytes()
            report = Path(directory) / "report.json"
            for output, csv_path in ((path, report), (report, path), (report, report)):
                with self.subTest(output=output, csv=csv_path), redirect_stderr(StringIO()):
                    args = ["analyze_traces", "--traces", directory, "--output", str(output),
                            "--decisions-csv", str(csv_path)]
                    with patch("sys.argv", args), self.assertRaises(SystemExit):
                        analyze_traces.main()
                    self.assertEqual(path.read_bytes(), data)
                    self.assertFalse(report.exists())

    def test_output_link_cannot_replace_an_input_capture(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.write_capture(directory, [dict(schema=analyze_traces.SCHEMA, duelId="linked",
                                                       sequence=0, kind="start")])
            linked_output = Path(directory) / "analysis.json"
            try:
                os.link(path, linked_output)
            except OSError as error:
                self.skipTest("Hard links unavailable: " + str(error))
            with self.assertRaisesRegex(ValueError, "separate output"):
                analyze_traces.analyze([directory], outputs=[linked_output])

    def test_legacy_mislabeled_card_selection_is_normalized(self):
        common = {"schema": analyze_traces.SCHEMA, "duelId": "legacy", "agent": "A"}
        rows = [
            dict(common, sequence=0, kind="start"),
            dict(common, sequence=1, kind="decision", prompt="select_card",
                 observation={"global": {"turn": 1, "phase": 4}},
                 selection={"candidates": [{"id": 3, "known": True, "code": 456}]},
                 selectedCandidateIds=[3], responseSource="executor"),
            dict(common, sequence=2, kind="end", outcome=0, completed=True),
        ]
        with tempfile.TemporaryDirectory() as directory:
            report = analyze_traces.analyze([self.write_capture(directory, rows)])
        self.assertEqual(report["summary"]["decisions"], 0)
        self.assertEqual(report["summary"]["selections"], 1)
        self.assertEqual(report["decisionRows"][0]["kind"], "selection")
        self.assertEqual(report["decisionRows"][0]["selectedCandidateCodes"], "456")

    def test_capture_start_order_uses_utc_start_timestamp(self):
        common = {"schema": analyze_traces.SCHEMA, "agent": "A", "sequence": 0, "kind": "start"}
        with tempfile.TemporaryDirectory() as directory:
            self.write_capture(directory, [dict(
                common, duelId="later", recordedAt="2026-11-16T12:00:02.0000000Z",
                matchOrder=2, duelNumber=1,
            )], "a-later.jsonl")
            self.write_capture(directory, [dict(
                common, duelId="earlier", recordedAt="2026-11-16T12:00:01.0000000Z",
                matchOrder=1, duelNumber=1,
            )], "z-earlier.jsonl")
            captures = analyze_traces.analyze([directory])["captures"]
        self.assertEqual([capture["duelId"] for capture in captures], ["earlier", "later"])
        self.assertEqual([capture["startOrder"] for capture in captures], [1, 2])


if __name__ == "__main__":
    unittest.main()
