"""Run fixed-opening Bakura routes with the real native duel host.

These no-shuffle/no-deck-check fixtures establish observed script-backed routes,
not tournament legality or competitive strength. The native core RNG is unknown.
Use a Debug WindBot build: assertions require its own/public movement log.
Non-Windows .exe clients require Mono and an explicitly supplied native duel
library built for that operating system; the Windows DLL cannot run on Linux.
"""
import argparse
from collections import Counter
from contextlib import closing
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
from types import SimpleNamespace

GUIDE, RHINO, OCTHROS = 10802915, 81035362, 58616392
RULER, ZORC, NECROFEAR = 100268007, 73320394, 31829185
BOARD, SANCTUARY, SENTENCE, SHIELD = 94212438, 16625614, 40771118, 8324284
MASTERY, JUDGMENT = 14386013, 41420027
CHERUBINI, MUCKRAKER = 58699500, 71607202
DARIUS, MACHINEX = 51497409, 46593546
MESSAGES = (31893528, 67287533, 94772232, 30170981)
ASH, VEILER, IMPERM, DROLL = 14558127, 97268402, 10045474, 94145021
WARWOLF, SWORDS, MIRROR_FORCE = 69247929, 72302403, 44095762
SCENARIOS = (
    {"name": "tour-guide-ruler", "hand": [GUIDE, BOARD, SANCTUARY, SHIELD, JUDGMENT],
     "route": "tour-guide", "draws": [SHIELD]},
    {"name": "zorc-rhino-ruler", "hand": [ZORC, RHINO, BOARD, SANCTUARY, SENTENCE],
     "route": "zorc-rhino", "draws": [SHIELD]},
    {"name": "board-natural-win", "hand": [BOARD, SANCTUARY, *MESSAGES[:3]],
     "route": "board", "draws": [SHIELD, SHIELD, JUDGMENT, BOARD], "passive": True},
    {"name": "sentence-accelerated-win", "hand": [BOARD, SANCTUARY, SENTENCE, *MESSAGES[:2]],
     "route": "sentence", "draws": [SENTENCE, SHIELD, SHIELD, JUDGMENT], "passive": True},
    {"name": "ash-two-starters", "hand": [GUIDE, ZORC, RHINO, BOARD, SANCTUARY],
     "route": "interruption", "draws": [SHIELD], "interruption": ASH},
    {"name": "veiler-two-starters", "hand": [GUIDE, ZORC, RHINO, BOARD, SANCTUARY],
     "route": "interruption", "draws": [SHIELD], "interruption": VEILER},
    {"name": "imperm-two-starters", "hand": [GUIDE, ZORC, RHINO, BOARD, SANCTUARY],
     "route": "interruption", "draws": [SHIELD], "interruption": IMPERM},
    {"name": "droll-two-starters", "hand": [GUIDE, ZORC, RHINO, BOARD, SANCTUARY],
     "route": "interruption", "draws": [SHIELD], "interruption": DROLL},
    {"name": "darius-machinex", "hand": [GUIDE, RHINO, RHINO, *MESSAGES[:2]],
     "route": "rank-three", "draws": [SHIELD]},
    {"name": "ruler-destroy-control-burn", "hand": [ZORC, RHINO, BOARD, SANCTUARY, SENTENCE],
     "route": "ruler-destroy", "draws": [SHIELD, SHIELD, JUDGMENT], "opponent_card": MIRROR_FORCE},
)


def digest(path):
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def read_deck(path):
    zones = {"main": [], "extra": [], "side": []}
    section = "main"
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if line in ("#main", "#extra", "!side"):
            section = line[1:]
        elif line.isdigit():
            zones[section].append(int(line))
    return zones


def write_deck(path, main, extra):
    path.write_text("#created by Destiny Board native scenario\n#main\n"
                    + "\n".join(map(str, main)) + "\n#extra\n"
                    + "\n".join(map(str, extra)) + "\n!side\n", encoding="utf-8")


def inspect_log(path, names, scenario):
    lines = [re.sub(r"^\[[^\]]+\] ", "", line)
             for line in path.read_text(encoding="utf-8", errors="replace").splitlines()]
    checks, moves, activations, snapshots = [], [], [], []
    turn = 0
    field = Counter()

    def check(name, condition):
        checks.append({"name": name, "passed": bool(condition)})

    begin = lines.index("*********Bot Hand*********") if "*********Bot Hand*********" in lines else -1
    end = lines.index("*********Bot Spell*********", begin) if begin >= 0 else -1
    check("exact opening hand observed", begin >= 0 and end >= 0
          and Counter(lines[begin + 1:end]) == Counter(names[code] for code in scenario["hand"]))
    for index, line in enumerate(lines):
        if line == "(Go to Draw)":
            turn += 1
        match = re.fullmatch(r"\(0 's (.+) from (\w+) move to (\w+)\)", line)
        if match:
            name, origin, destination = match.groups()
            moves.append((index, turn, name, origin, destination))
            if origin in ("MonsterZone", "SpellZone"):
                field[(name, origin)] -= 1
            if destination in ("MonsterZone", "SpellZone"):
                field[(name, destination)] += 1
            snapshots.append((index, turn, field.copy()))
        match = re.fullmatch(r"\(([01]) 's (.+) activate effect from (\w+)\)", line)
        if match:
            activations.append((index, turn, *match.groups()))

    def moved(code, origin, destination, by_turn=1):
        return any(t <= by_turn and name == names[code] and before == origin and after == destination
                   for _, t, name, before, after in moves)

    def activated(code, player="0", by_turn=1):
        return any(t <= by_turn and owner == player and name == names[code]
                   for _, t, owner, name, _ in activations)

    route = scenario["route"]
    if route == "tour-guide":
        check("Tour Guide recruited Octhros on turn one", moved(OCTHROS, "Deck", "MonsterZone"))
        check("Cherubini Link Summoned on turn one", moved(CHERUBINI, "Extra", "MonsterZone"))
        check("Octhros sent from field to GY", moved(OCTHROS, "MonsterZone", "Grave"))
        check("Rhino sent from deck to GY", moved(RHINO, "Deck", "Grave"))
        check("Ruler searched to hand", moved(RULER, "Deck", "Hand"))
        check("Ruler summoned from hand on turn one", moved(RULER, "Hand", "MonsterZone"))
        check("Muckraker Link Summoned after the GY setup", moved(MUCKRAKER, "Extra", "MonsterZone"))
        check("Muckraker revived Ruler on turn one", moved(RULER, "Grave", "MonsterZone"))
        muckraker_at = next((i for i, t, owner, n, _ in activations
                            if t == 1 and owner == "0" and n == names[MUCKRAKER]), None)
        # These are all non-Fiends in the supplied Extra Deck. The check observes
        # the actual route after the revival resolves; it does not test legality
        # by fabricating an illegal offer to the live client.
        non_fiend_extra = {names[c] for c in (CHERUBINI, 8264361, 93854893, 63767246, 29301450)}
        check("no non-Fiend Extra summon after Muckraker revival", muckraker_at is not None
              and not any(i > muckraker_at and t == 1 and n in non_fiend_extra
                          and origin == "Extra" and target == "MonsterZone"
                          for i, t, n, origin, target in moves))
    elif route == "zorc-rhino":
        check("Zorc discarded from hand", moved(ZORC, "Hand", "Grave"))
        check("Rhino discarded from hand", moved(RHINO, "Hand", "Grave"))
        check("Rhino activated its GY effect", activated(RHINO))
        check("Ruler searched to hand", moved(RULER, "Deck", "Hand"))
        check("Ruler summoned from hand on turn one", moved(RULER, "Hand", "MonsterZone"))
    elif route in ("board", "sentence"):
        letter_names = {names[code] for code in MESSAGES}
        placed = [(index, t, name, destination) for index, t, name, origin, destination in moves
                  if name in letter_names and origin in ("Hand", "Deck", "Grave")
                  and destination in ("MonsterZone", "SpellZone")]
        winning = [(index, t) for index, t, board in snapshots
                   if board[(names[BOARD], "SpellZone")] > 0 and all(
                       board[(names[code], "MonsterZone")] + board[(names[code], "SpellZone")] > 0
                       for code in MESSAGES)]
        check("all four distinct messages observed with Destiny Board", bool(winning))
        check("authoritative duel win received", any("Duel finished against " in line and ", result: Win" in line for line in lines))
        check("messages were not consumed before the win", not any(name in letter_names
              and origin in ("MonsterZone", "SpellZone") and destination not in ("MonsterZone", "SpellZone")
              for _, _, name, origin, destination in moves))
        check("Sanctuary placed message monsters", all(any(name == names[code] and destination == "MonsterZone"
              for _, _, name, destination in placed) for code in MESSAGES))
        if route == "board":
            check("natural I/N/A/L sequence observed", [name for _, _, name, _ in placed] == [names[c] for c in MESSAGES])
            check("natural Board win reached by turn eight", bool(winning) and winning[0][1] <= 8)
            check("duel ended by turn eight", turn <= 8)
        else:
            check("Sentence activated before turn-five", activated(SENTENCE, by_turn=4))
            check("late letter L placed before A", next((i for i, _, n, _ in placed if n == names[MESSAGES[3]]), 10**9)
                  < next((i for i, _, n, _ in placed if n == names[MESSAGES[2]]), -1))
            check("accelerated Board win reached by turn four", bool(winning) and winning[0][1] <= 4)
            check("duel ended by turn four", turn <= 4)
    elif route == "interruption":
        interruption = scenario["interruption"]
        check("opponent used the configured interruption on turn one", activated(interruption, player="1"))
        check("Ruler reached the field despite interruption", moved(RULER, "Hand", "MonsterZone", by_turn=3)
              or moved(RULER, "Grave", "MonsterZone", by_turn=3))
        if interruption == DROLL:
            droll_at = next((i for i, t, owner, name, _ in activations
                             if t == 1 and owner == "1" and name == names[DROLL]), None)
            check("no deck-to-hand addition after Droll this turn", droll_at is not None
                  and not any(i > droll_at and t == 1 and origin == "Deck" and target == "Hand"
                              for i, t, _, origin, target in moves))
    elif route == "rank-three":
        check("Darius Xyz Summoned on turn one", moved(DARIUS, "Extra", "MonsterZone"))
        check("Machinex upgraded Darius on turn one", moved(MACHINEX, "Extra", "MonsterZone"))
        check("Cherubini was held with both Rhinos already drawn", not moved(CHERUBINI, "Extra", "MonsterZone"))
    elif route == "ruler-destroy":
        check("opponent activated Mirror Force", activated(MIRROR_FORCE, player="1", by_turn=3))
        check("Ruler was destroyed from the field", moved(RULER, "MonsterZone", "Grave", by_turn=3))
        check("Ruler returned itself to hand", moved(RULER, "Grave", "Hand", by_turn=3))
        return_at = next((i for i, t, n, origin, target in moves if t <= 3 and n == names[RULER]
                          and origin == "Grave" and target == "Hand"), None)
        check("Ruler took the opponent's monster at resolution", return_at is not None and any(
            i > return_at and line == "(1 's " + names[WARWOLF] + " from MonsterZone move to MonsterZone)"
            for i, line in enumerate(lines)))
        check("Ruler inflicted the half-ATK burn", return_at is not None and any(
            i > return_at and line == "(1 got damage , LifePoint left = 7000)"
            for i, line in enumerate(lines)))
    check("no retry, invalid selection or runtime error", not any(re.search(
        r"MSG_RETRY|Duel retry|Invalid card selection|Fail to select|Tick Error|Run Error|Unhandled Exception", line, re.I)
        for line in lines))
    return {"passed": all(check["passed"] for check in checks), "checks": checks,
            "observedTurns": turn, "firstThreeTurnOwnMoves": [move for move in moves if move[1] <= 3]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("bot-exe", "opponent-exe", "data-root", "server", "deck", "output-dir"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--scenarios", help="Comma-separated names; omitted runs all scenarios")
    parser.add_argument("--timeout", type=float, default=90)
    parser.add_argument("--mono", type=Path, help="Mono executable for non-Windows .exe clients; otherwise searches PATH")
    args = parser.parse_args()
    if not 10 <= args.timeout <= 300:
        parser.error("timeout must be 10..300 seconds")
    for name in ("bot_exe", "opponent_exe", "data_root", "server", "deck"):
        setattr(args, name, getattr(args, name).resolve(strict=True))
    mono = None
    if os.name != "nt" and any(path.suffix.lower() == ".exe" for path in (args.bot_exe, args.opponent_exe)):
        mono_input = args.mono or shutil.which("mono")
        if not mono_input:
            parser.error("Mono is required for .exe clients on this platform; install it or pass --mono")
        mono = Path(mono_input).resolve(strict=True)
    selected = args.scenarios.split(",") if args.scenarios else [scenario["name"] for scenario in SCENARIOS]
    if not selected or set(selected) - {scenario["name"] for scenario in SCENARIOS}:
        parser.error("Unknown scenario name")
    root = Path(__file__).resolve().parents[2]
    spec = importlib.util.spec_from_file_location("destiny_board_native_smoke", root / "SKYNET/Tools/Skynet/native_smoke.py")
    smoke = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(smoke)
    original_launch = smoke.launch

    def launch_fixture(command, cwd, log):
        # Target wins RPS so its executor's own turn-choice callback applies.
        if "Name=SkynetQA" in command:
            command = ["Hand=3" if arg == "Hand=1" else arg for arg in command]
        if mono is not None and Path(command[0]) in (args.bot_exe, args.opponent_exe) and Path(command[0]).suffix.lower() == ".exe":
            command = [str(mono), *command]
        return original_launch(command, cwd, log)

    smoke.launch = launch_fixture
    out = args.output_dir.resolve()
    out.mkdir(parents=True, exist_ok=False)
    deck = read_deck(args.deck)
    database = args.data_root / "locales/en-US/cards.cdb"
    scripts = args.data_root / "script.zip"
    with closing(sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)) as connection:
        names = dict(connection.execute("SELECT id,name FROM texts"))
    result = {"format": "destiny-board-native-checks-v1", "purpose": "Script-backed route milestones and completion, not strength",
              "harnessSha256": digest(Path(__file__).resolve()),
              "nativeSmokeSha256": digest(root / "SKYNET/Tools/Skynet/native_smoke.py"),
              "botSha256": digest(args.bot_exe), "opponentSha256": digest(args.opponent_exe),
              "serverSha256": digest(args.server), "databaseSha256": digest(database),
              "scriptsSha256": digest(scripts), "deckSha256": digest(args.deck),
              "coreSeed": None, "noShuffle": True, "noDeckCheck": True,
              "targetRps": 3, "opponentRps": 2, "training": False, "scenarios": []}
    result["platform"] = os.name
    result["mono"] = str(mono) if mono is not None else None
    for scenario in SCENARIOS:
        if scenario["name"] not in selected:
            continue
        case = out / scenario["name"]
        inputs = case / "inputs"
        inputs.mkdir(parents=True)
        shutil.copyfile(database, inputs / "cards.cdb")
        shutil.copyfile(scripts, inputs / "script.zip")
        opening = scenario["hand"] + scenario.get("draws", [])
        remaining = list(deck["main"])
        for code in opening:
            remaining.remove(code)
        target = inputs / "target.ydk"
        write_deck(target, opening + remaining, deck["extra"])
        interruption = scenario.get("interruption")
        if scenario.get("passive"):
            opponent_cards = [SWORDS] * 40
        elif interruption:
            opponent_cards = [interruption] + [WARWOLF] * 39
        else:
            opponent_cards = [scenario.get("opponent_card", SWORDS)] + [WARWOLF] * 39
        opponent = inputs / "opponent.ydk"
        write_deck(opponent, opponent_cards, [])
        settings = SimpleNamespace(server=args.server, bot_exe=args.bot_exe, opponent_exe=args.opponent_exe,
            timeout=args.timeout, policy="DestinyBoard", opponent_policy="Voiceless" if interruption else "OldSchool",
            policy_seed=20261102, train=False, policy_group="destiny-board-fixture:" + scenario["name"],
            model=None, agent=None, opponent_agent=None, shadow=False, explain=False)
        duel = smoke.run_duel(settings, case, 1, target, opponent)
        log = case / "duel-01/client-0.log"
        trace = inspect_log(log, names, scenario) if log.is_file() else {"passed": False, "checks": [{"name": "client log exists", "passed": False}]}
        row = {**scenario, "targetDeckSha256": digest(target), "opponentDeckSha256": digest(opponent),
               "duel": duel, "trace": trace, "passed": duel["status"] == "passed" and trace["passed"]}
        result["scenarios"].append(row)
        result["passed"] = all(row["passed"] for row in result["scenarios"])
        (out / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        failures = [check["name"] for check in trace["checks"] if not check["passed"]]
        print(scenario["name"] + ": " + ("PASS" if row["passed"] else "FAIL") + " " + "; ".join(failures), flush=True)
    return 0 if result.get("passed") else 1


if __name__ == "__main__":
    raise SystemExit(main())
