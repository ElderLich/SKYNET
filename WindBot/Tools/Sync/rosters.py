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
        if level.lower() not in ("test", "notfinished") and not any(
                key in english.entries for key in ("deck:" + name, "custom:" + name)):
            raise ValueError(f"{english.path}: playable executor {name} needs an English entry and translations")
    for roster in sources.values():
        missing = sorted(english.entries.keys() - roster.entries.keys())
        extra = sorted(roster.entries.keys() - english.entries.keys())
        if missing or extra:
            raise ValueError(f"{roster.path}: menu differs from English; missing {missing}, extra {extra}")
        for key, entry in roster.entries.items():
            reference = english.entries[key]
            if not entry.lines[2].strip():
                raise ValueError(f"{roster.path}: missing localized description for {key}")
            if entry.flags != reference.flags:
                raise ValueError(f"{roster.path}: menu flags for {key} differ from English")
            gameplay_keys = (entry.arguments.keys() | reference.arguments.keys()) - {"name", "dialog"}
            if any(entry.arguments.get(k) != reference.arguments.get(k) for k in gameplay_keys):
                raise ValueError(f"{roster.path}: launch arguments for {key} differ from English")
            if key.startswith("random:"):
                continue
            name = entry.arguments["deck"]
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
    """Keep destination-only entries and annotations before their next shared entry."""
    notes = Counter(line for entry in source.entries.values() for line in entry.before if line.strip())
    notes.update(line for line in source.tail if line.strip())
    pending = []
    extras = {}

    def keep_notes(lines):
        for line in lines:
            if line.strip():
                if notes[line] > 0:
                    notes[line] -= 1
                else:
                    pending.append(line)

    for key, entry in destination.entries.items():
        keep_notes(entry.before)
        if key in source.entries:
            extras[key] = pending
            pending = []
        else:
            pending.extend(entry.lines)
            pending.append("")
    keep_notes(destination.tail)
    lines = []
    for key, entry in source.entries.items():
        lines.extend(entry.before)
        lines.extend(extras.get(key, ()))
        lines.extend(entry.lines)
    if pending:
        lines.append("")
        if pending[-1] == "":
            pending.pop()
        lines.extend(pending)
    lines.extend(source.tail)
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
