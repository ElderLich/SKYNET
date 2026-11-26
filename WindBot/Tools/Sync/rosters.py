"""Plan localized MDPro4 Solo menu updates from the maintained WindBot rosters."""

import codecs
import re
from collections import Counter
from dataclasses import dataclass
from pathlib import Path


ARGUMENT = re.compile(r"""(?<!\S)(\w+)=(?:'([^']*)'|"([^"]*)"|((?!['"])\S+))(?=\s|$)""")
DECK_ATTRIBUTE = re.compile(
    r'^\s*\[\s*Deck\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"'
    r'(?:\s*,\s*"([^"]+)")?\s*\)\s*\]', re.MULTILINE
)

# Unfinished executors may still be launched by developers using an explicit Deck=.
SOLO_EXCLUSIONS = {"RaiseMoon": "Unfinished executor"}
RUSH_START = "# Rush Support Start"
RUSH_END = "# Rush Support End"


@dataclass(frozen=True)
class DeckRename:
    new_name: str
    old_executor_hash: str
    old_deck_hashes: tuple


# SHA-256 of the published UTF-8 text with LF endings. Both deck header revisions shipped.
RENAMED_DECKS = {
    "SirokamiDarkMagician": DeckRename(
        "DarkMagicianTheDragonKnight",
        "9cbd50dae2562a7ea3422a940a8c1516a622a19819513c302aac74740b24afc5",
        ("c4dc78a9aef4ca220180a78f25cf8551ce0b76e396c77a788a4d663d483894fb",
         "6e5ad85fdfb75978c636041fb3aa0357483e9c534656160f314989c18a7f095d")),
}


@dataclass
class Entry:
    before: list
    lines: list
    arguments: dict
    flags: frozenset


@dataclass
class Roster:
    path: Path
    raw: bytes
    entries: dict
    tail: list
    encoding: str
    bom: bytes
    newline: str
    final_newline: bool


def _arguments(command):
    result = {}
    end = 0
    for match in ARGUMENT.finditer(command):
        if command[end:match.start()].strip():
            raise ValueError("invalid command near " + repr(command[end:match.start()]))
        name = match[1].lower()
        if name in result:
            raise ValueError("duplicate command argument " + match[1])
        result[name] = next(v for v in match.groups()[1:] if v is not None)
        end = match.end()
    if command[end:].strip():
        raise ValueError("invalid command near " + repr(command[end:]))
    return result


def _read(path, raw=None):
    if raw is None:
        raw = path.read_bytes()
    # File.ReadAllLines in SoloBotCatalog recognizes Unicode byte-order marks.
    formats = ((codecs.BOM_UTF32_LE, "utf-32-le"), (codecs.BOM_UTF32_BE, "utf-32-be"),
               (codecs.BOM_UTF8, "utf-8"), (codecs.BOM_UTF16_LE, "utf-16-le"),
               (codecs.BOM_UTF16_BE, "utf-16-be"))
    bom, encoding = next((p for p in formats if raw.startswith(p[0])), (b"", "utf-8"))
    try:
        text = raw[len(bom):].decode(encoding)
    except UnicodeError as exc:
        raise ValueError(f"{path}: invalid {encoding} roster") from exc
    if "\0" in text:
        raise ValueError(f"{path}: unexpected NUL in roster")
    endings = Counter(re.findall(r"\r\n|\r|\n", text))
    newline = endings.most_common(1)[0][0] if endings else "\n"
    lines = text.splitlines()
    entries = {}
    before = []
    i = 0
    while i < len(lines):
        heading = lines[i].strip()
        if not heading.startswith("!"):
            before.append(lines[i])
            i += 1
            continue
        context = f"{path}:{i + 1}"
        if not heading[1:].strip() or i + 3 >= len(lines):
            raise ValueError(f"{context}: incomplete four-line bot entry")
        fields = lines[i:i + 4]
        try:
            args = _arguments(fields[1])
        except ValueError as exc:
            raise ValueError(f"{context}: {exc}") from exc
        flags = frozenset(fields[3].split())
        if fields[3].lstrip().startswith(("!", "#")) or any("=" in f for f in flags):
            raise ValueError(f"{context}: invalid flags line")
        deck, random = args.get("deck"), args.get("random")
        if bool(deck) == bool(random):
            raise ValueError(f"{context}: expected one Deck= or Random= argument")
        # Match SoloBotCatalog.ReadLocalized, including the bring-your-own-deck variant.
        key = "random:" + random if random else ("custom:" if "SELECT_DECKFILE" in flags else "deck:") + deck
        if key in entries:
            raise ValueError(f"{context}: duplicate menu entry {key}")
        entries[key] = Entry(before, fields, args, flags)
        before = []
        i += 4
    return Roster(path, raw, entries, before, encoding, bom, newline, text.endswith(("\n", "\r")))


def _is_rush(entry):
    return any(re.fullmatch(r"AI_RD[1-9]\d*", flag) for flag in entry.flags | {entry.arguments.get("random", "")})


def _validate_sections(roster):
    markers = []
    for index, entry in enumerate(roster.entries.values()):
        markers.extend((line.strip(), index) for line in entry.before if line.strip() in (RUSH_START, RUSH_END))
    count = len(roster.entries)
    markers.extend((line.strip(), count) for line in roster.tail if line.strip() in (RUSH_START, RUSH_END))
    rush = [i for i, entry in enumerate(roster.entries.values()) if _is_rush(entry)]
    if not (markers or rush):
        return
    if not rush or markers != [(RUSH_START, rush[0]), (RUSH_END, count)] or rush != list(range(rush[0], count)):
        raise ValueError(f"{roster.path}: put all Rush bots between {RUSH_START} and {RUSH_END} at the end")


def validate_rosters(windbot_root):
    """Read all canonical locales and verify menu parity and installed resources."""
    wrapper = windbot_root / "BotWrapper"
    sources = {"en-US": _read(wrapper / "bot.conf")}
    for locale in sorted((wrapper / "locales").iterdir()):
        if not locale.is_dir() or locale.name.startswith("."):
            continue
        if locale.name == "en-US":
            raise ValueError(f"{locale}: English is maintained in {wrapper / 'bot.conf'}")
        sources[locale.name] = _read(locale / "bot.conf")

    registrations = {}
    for source in sorted((windbot_root / "Game/AI/Decks").glob("*.cs")):
        for name, deck_file, level in DECK_ATTRIBUTE.findall(source.read_text(encoding="utf-8-sig")):
            if name in registrations:
                raise ValueError(f"{source}: duplicate executor registration {name}")
            registrations[name] = (deck_file, level)
    deck_files = {p.name for p in (windbot_root / "Decks").glob("*.ydk")}
    dialog_files = {p.name for p in (windbot_root / "Dialogs").glob("*.json")}
    english = sources["en-US"]
    if not english.entries:
        raise ValueError(f"{english.path}: empty roster")
    for name, (_, level) in registrations.items():
        if name in RENAMED_DECKS:
            raise ValueError(f"{name}: use renamed registration {RENAMED_DECKS[name].new_name}")
        if name in SOLO_EXCLUSIONS and level.lower() != "notfinished":
            raise ValueError(f"{name}: an excluded executor must be registered as NotFinished")
        if level.lower() not in ("test", "notfinished", "manual") and not any(
                key in english.entries for key in ("deck:" + name, "custom:" + name)):
            raise ValueError(f"{english.path}: playable executor {name} needs an English entry and translations")
    for roster in sources.values():
        _validate_sections(roster)
        missing = sorted(english.entries.keys() - roster.entries.keys())
        extra = sorted(roster.entries.keys() - english.entries.keys())
        if missing or extra:
            raise ValueError(f"{roster.path}: menu differs from English; missing {missing}, extra {extra}")
        if list(roster.entries) != list(english.entries):
            raise ValueError(f"{roster.path}: menu order differs from English")
        for key, entry in roster.entries.items():
            reference = english.entries[key]
            if not entry.lines[2].strip():
                raise ValueError(f"{roster.path}: missing localized description for {key}")
            if entry.flags != reference.flags:
                raise ValueError(f"{roster.path}: menu flags for {key} differ from English")
            if key.startswith("deck:") and not any(re.fullmatch(r"AI_(LV[1-9]\d*|RD[1-9]\d*|WILD|ANTI_META)", flag)
                                                   for flag in entry.flags):
                raise ValueError(f"{roster.path}: missing Solo difficulty or specialty group for {key}")
            gameplay_keys = (entry.arguments.keys() | reference.arguments.keys()) - {"name", "dialog"}
            if any(entry.arguments.get(k) != reference.arguments.get(k) for k in gameplay_keys):
                raise ValueError(f"{roster.path}: launch arguments for {key} differ from English")
            if key.startswith("random:"):
                continue
            name = entry.arguments["deck"]
            if name in SOLO_EXCLUSIONS:
                raise ValueError(f"{roster.path}: excluded Solo executor {name}")
            if name not in registrations:
                raise ValueError(f"{roster.path}: unknown executor {name} (names are case-sensitive)")
            deck_file = registrations[name][0] + ".ydk"
            if not key.startswith("custom:") and deck_file not in deck_files:
                raise ValueError(f"{roster.path}: missing registered deck file {deck_file} for {name}")
            dialog = entry.arguments.get("dialog", "default") + ".json"
            if dialog not in dialog_files:
                raise ValueError(f"{roster.path}: missing dialog {dialog} for {name} (names are case-sensitive)")
    return sources


def _merge(source, destination):
    """Keep local entries with their format and preserve comments during upgrades."""
    notes = Counter(line for entry in source.entries.values() for line in entry.before if line.strip())
    notes.update(line for line in source.tail if line.strip())
    has_rush = any(line.strip() == RUSH_END for line in source.tail)
    pending = {False: [], True: []}
    extras = {}

    def keep_notes(lines, output):
        for line in lines:
            if line.strip():
                if has_rush and line.strip() in (RUSH_START, RUSH_END):
                    continue
                if notes[line] > 0:
                    notes[line] -= 1
                else:
                    output.append(line)

    for key, entry in destination.entries.items():
        group = has_rush and _is_rush(source.entries.get(key, entry))
        keep_notes(entry.before, pending[group])
        if key in source.entries:
            extras[key] = pending[group]
            pending[group] = []
        elif entry.arguments.get("deck") not in SOLO_EXCLUSIONS and entry.arguments.get("deck") not in RENAMED_DECKS:
            pending[group].extend(entry.lines)
            pending[group].append("")
    footer = []
    keep_notes(destination.tail, footer)
    lines = []

    def add_notes(block):
        for line in block:
            if has_rush and line.strip() in (RUSH_START, RUSH_END):
                lines.extend(pending[line.strip() == RUSH_END])
            lines.append(line)

    for key, entry in source.entries.items():
        add_notes(entry.before)
        lines.extend(extras.get(key, ()))
        lines.extend(entry.lines)
    if not has_rush and pending[False]:
        lines.append("")
        lines.extend(pending[False])
        if lines[-1] == "" and not footer:
            lines.pop()
    add_notes(source.tail)
    lines.extend(footer)
    original = destination.raw[len(destination.bom):].decode(destination.encoding)
    if lines == original.splitlines():
        return destination.raw
    text = destination.newline.join(lines)
    if destination.final_newline:
        text += destination.newline
    return destination.bom + text.encode(destination.encoding)


def plan_rosters(windbot_root, data_roots, originals=None):
    """Plan desired bytes and optionally expose the same snapshots used for merging."""
    sources = validate_rosters(windbot_root)
    plan = {}
    for root in data_roots:
        locales = root / "locales"
        if not locales.is_dir():
            raise ValueError(f"{locales}: expected the game's Data/locales directory")
        for locale in locales.iterdir():
            if locale.is_dir() and not locale.name.startswith(".") and locale.name not in sources:
                raise ValueError(f"{locale}: add a canonical translation in WindBot/BotWrapper/locales/{locale.name}")
        for language, source in sources.items():
            target = locales / language / "bot.conf"
            try:
                raw = target.read_bytes()
            except FileNotFoundError:
                raw = None
            if originals is not None:
                originals[target] = raw
            plan[target] = source.raw if raw is None else _merge(source, _read(target, raw))
    return plan
