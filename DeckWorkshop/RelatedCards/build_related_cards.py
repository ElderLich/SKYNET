#!/usr/bin/env python3
"""Build isolated standard/Rush related-card graphs from installed CDBs and Lua."""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
import zipfile
from collections import defaultdict
from contextlib import closing
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Set, Tuple


CATEGORY_ORDER = ["ID", "Arch", "Level/Link/Rank", "Race", "Type", "Other", "Targets"]
MUTUAL_SOURCE_CATEGORIES = ["ID", "Arch", "Level/Link/Rank", "Race", "Type", "Other"]


def normalize_space(text: str) -> str:
    return re.sub(r"\s+", " ", text.strip())


def parse_int(value: str) -> Optional[int]:
    value = value.strip()
    if not value:
        return None
    try:
        if value.lower().startswith("0x"):
            return int(value, 16)
        return int(value)
    except ValueError:
        return None


def split_top_level(text: str, operator_word: str) -> List[str]:
    parts: List[str] = []
    depth = 0
    quote: Optional[str] = None
    start = 0
    i = 0
    needle = f" {operator_word} "
    while i < len(text):
        ch = text[i]
        if quote:
            if ch == quote and (i == 0 or text[i - 1] != "\\"):
                quote = None
            i += 1
            continue
        if ch in ("'", '"'):
            quote = ch
            i += 1
            continue
        if ch == "(":
            depth += 1
            i += 1
            continue
        if ch == ")":
            depth = max(0, depth - 1)
            i += 1
            continue
        if depth == 0 and text.startswith(needle, i):
            parts.append(text[start:i].strip())
            i += len(needle)
            start = i
            continue
        i += 1
    parts.append(text[start:].strip())
    return [part for part in parts if part]


def strip_outer_parens(text: str) -> str:
    text = text.strip()
    while text.startswith("(") and text.endswith(")"):
        depth = 0
        balanced = True
        quote: Optional[str] = None
        for index, ch in enumerate(text):
            if quote:
                if ch == quote and text[index - 1] != "\\":
                    quote = None
                continue
            if ch in ("'", '"'):
                quote = ch
                continue
            if ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
                if depth == 0 and index != len(text) - 1:
                    balanced = False
                    break
        if balanced and depth == 0:
            text = text[1:-1].strip()
        else:
            break
    return text


def split_args(text: str) -> List[str]:
    args: List[str] = []
    depth = 0
    quote: Optional[str] = None
    start = 0
    for index, ch in enumerate(text):
        if quote:
            if ch == quote and text[index - 1] != "\\":
                quote = None
            continue
        if ch in ("'", '"'):
            quote = ch
            continue
        if ch in "({[":
            depth += 1
            continue
        if ch in ")}]":
            depth = max(0, depth - 1)
            continue
        if ch == "," and depth == 0:
            args.append(text[start:index].strip())
            start = index + 1
    tail = text[start:].strip()
    if tail:
        args.append(tail)
    return args


def extract_parenthesized(text: str, start_index: int) -> Tuple[str, int]:
    depth = 1
    quote: Optional[str] = None
    i = start_index
    while i < len(text):
        ch = text[i]
        if quote:
            if ch == quote and text[i - 1] != "\\":
                quote = None
            i += 1
            continue
        if ch in ("'", '"'):
            quote = ch
            i += 1
            continue
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return text[start_index:i], i + 1
        i += 1
    raise ValueError("Unbalanced parentheses")


def strip_lua_comments_and_strings(line: str) -> str:
    output: List[str] = []
    quote: Optional[str] = None
    i = 0
    while i < len(line):
        if quote:
            if line[i] == quote and line[i - 1] != "\\":
                quote = None
            output.append(" ")
            i += 1
            continue
        if i + 1 < len(line) and line[i:i + 2] == "--":
            break
        if line[i] in ("'", '"'):
            quote = line[i]
            output.append(" ")
            i += 1
            continue
        output.append(line[i])
        i += 1
    return "".join(output)


def block_delta(line: str) -> int:
    clean = strip_lua_comments_and_strings(line)
    opens = 0
    closes = 0
    if re.search(r"\bfunction\b", clean):
        opens += len(re.findall(r"\bfunction\b", clean))
    if re.search(r"\bif\b.*\bthen\b", clean):
        opens += len(re.findall(r"\bif\b", clean))
    if re.search(r"\bfor\b.*\bdo\b", clean):
        opens += len(re.findall(r"\bfor\b", clean))
    if re.search(r"\bwhile\b.*\bdo\b", clean):
        opens += len(re.findall(r"\bwhile\b", clean))
    opens += len(re.findall(r"\brepeat\b", clean))
    closes += len(re.findall(r"\bend\b", clean))
    closes += len(re.findall(r"\buntil\b", clean))
    return opens - closes


def extract_functions(script_text: str) -> Dict[str, str]:
    lines = script_text.splitlines()
    functions: Dict[str, str] = {}
    index = 0
    while index < len(lines):
        line = lines[index]
        match = re.match(r"^\s*function\s+([A-Za-z0-9_\.]+)\s*\(", line)
        if not match:
            index += 1
            continue
        name = match.group(1)
        depth = max(1, block_delta(line))
        start = index
        index += 1
        while index < len(lines) and depth > 0:
            depth += block_delta(lines[index])
            index += 1
        body = "\n".join(lines[start:index])
        functions[name] = body
        functions[name.split(".")[-1]] = body
    return functions


def extract_return_expressions(function_body: str) -> List[str]:
    expressions: List[str] = []
    lines = function_body.splitlines()
    current: Optional[str] = None
    for raw_line in lines:
        line = strip_lua_comments_and_strings(raw_line).strip()
        if not line:
            continue
        return_index = line.find("return ")
        if current is None and return_index == -1:
            continue
        if current is None:
            current = line[return_index + len("return "):].strip()
        else:
            current += " " + line
        if current.count("(") == current.count(")") and not re.search(r"\b(and|or)$", current):
            expressions.append(normalize_space(current))
            current = None
    if current:
        expressions.append(normalize_space(current))
    return expressions


def dedupe_groups(groups: Iterable[Tuple[Tuple[str, object], ...]]) -> List[Tuple[Tuple[str, object], ...]]:
    unique: List[Tuple[Tuple[str, object], ...]] = []
    seen: Set[Tuple[Tuple[str, object], ...]] = set()
    for group in groups:
        normalized = tuple(sorted(group))
        if not normalized or normalized in seen:
            continue
        seen.add(normalized)
        unique.append(normalized)
    return unique


def condition_category(group: Sequence[Tuple[str, object]]) -> str:
    kinds = {kind for kind, _ in group}
    if "id_any" in kinds:
        return "ID"
    if "setcode_any" in kinds:
        return "Arch"
    if any(kind.startswith("level_") or kind.startswith("rank_") or kind.startswith("link_") for kind in kinds):
        return "Level/Link/Rank"
    if any(kind.startswith("race_") for kind in kinds):
        return "Race"
    if any(kind.startswith("type_") for kind in kinds):
        return "Type"
    return "Other"


def iter_set_bits(mask: int) -> Iterable[int]:
    mask = int(mask)
    if mask < 0:
        raise ValueError("Bit masks must be normalized to their unsigned storage width")
    while mask:
        bit = mask & -mask
        yield bit
        mask ^= bit


@dataclass(frozen=True)
class CardRecord:
    id: int
    setcode: int
    type_mask: int
    race: int
    attribute: int
    attack: Optional[int]
    defense: Optional[int]
    level: Optional[int]
    rank: Optional[int]
    link: Optional[int]
    alias: int = 0


class ScriptSource:
    def __init__(self, location: Path):
        self.location = location
        self.archive: Optional[zipfile.ZipFile] = None
        self.archive_entries: Dict[str, str] = {}

        if location.is_dir():
            self.kind = "dir"
            return
        if location.is_file() and location.suffix.lower() == ".zip":
            self.kind = "zip"
            self.archive = zipfile.ZipFile(location)
            for entry_name in self.archive.namelist():
                if entry_name.endswith("/"):
                    continue
                short_name = Path(entry_name).name
                self.archive_entries.setdefault(short_name, entry_name)
            return
        raise FileNotFoundError(f"Scripts source '{location}' is neither a directory nor a .zip archive.")

    def read_text(self, name: str) -> str:
        if self.kind == "dir":
            return (self.location / name).read_text(encoding="utf-8")
        archive_name = self.archive_entries.get(name)
        if archive_name is None or self.archive is None:
            raise FileNotFoundError(f"'{name}' was not found in {self.location}")
        with self.archive.open(archive_name) as handle:
            return handle.read().decode("utf-8")

    def iter_script_names(self) -> List[str]:
        if self.kind == "dir":
            return sorted(path.name for path in self.location.glob("c*.lua"))
        return sorted(name for name in self.archive_entries if re.fullmatch(r"c\d+\.lua", name))

    def close(self) -> None:
        if self.archive is not None:
            self.archive.close()


class ScriptExpressionParser:
    IGNORED_CALLS = (
        "IsAbleToHand",
        "IsCanBeSpecialSummoned",
        "IsSSetable",
        "IsAbleToDeck",
        "IsAbleToGrave",
        "IsAbleToRemove",
        "IsAbleToRemoveAsCost",
        "IsDiscardable",
        "IsCanOverlay",
        "IsCanBeRitualMaterial",
        "IsCanBeFusionMaterial",
        "IsFaceup",
        "IsFaceupEx",
        "IsFacedown",
        "IsLocation",
        "IsControler",
        "IsPreviousControler",
        "IsPreviousLocation",
        "IsReason",
        "IsReasonCard",
        "IsReasonEffect",
        "IsRelateToEffect",
        "IsImmuneToEffect",
        "IsSummonType",
        "IsPosition",
        "CheckActivateEffect",
        "GetFieldID",
        "GetSequence",
        "GetControler",
        "GetPreviousTypeOnField",
        "GetReasonCard",
        "GetOverlayCount",
        "GetLocationCountFromEx",
        "CheckRemoveOverlayCard",
    )

    def __init__(self, constants: Dict[str, int], functions: Dict[str, str],
                 code_lists: Optional[Dict[str, Tuple[int, ...]]] = None):
        self.constants = constants
        self.functions = functions
        self.code_lists = code_lists or {}
        self.type_tuner = constants.get("TYPE_TUNER", 0)

    def parse_named_mask(self, expr: str, prefixes: Sequence[str]) -> Optional[int]:
        mask = 0
        for token in split_args(expr.replace("+", ",")):
            token = strip_outer_parens(token.strip())
            if not token:
                continue
            if not any(token.startswith(prefix) for prefix in prefixes):
                value = parse_int(token)
                if value is None:
                    return None
                mask |= value
                continue
            if token not in self.constants:
                return None
            mask |= self.constants[token]
        return mask

    def parse_code_values(self, expr: str) -> Optional[Tuple[int, ...]]:
        values: List[int] = []
        for token in split_args(expr):
            token = strip_outer_parens(token)
            unpack = re.fullmatch(r"table\.unpack\((\w+)\)", token)
            if unpack:
                token = unpack.group(1)
            if token in self.code_lists:
                values.extend(self.code_lists[token])
                continue
            indexed = re.fullmatch(r"(\w+)\[(\d+)\]", token)
            if indexed and indexed.group(1) in self.code_lists:
                items = self.code_lists[indexed.group(1)]
                index = int(indexed.group(2)) - 1
                if index < 0 or index >= len(items):
                    return None
                values.append(items[index])
                continue
            if token.startswith("{") and token.endswith("}"):
                items = self.parse_code_values(token[1:-1])
                if items is None:
                    return None
                values.extend(items)
                continue
            value = parse_int(strip_outer_parens(token))
            if value is None:
                return None
            values.append(value)
        return tuple(dict.fromkeys(values))

    def parse_filter_reference(self, expr: str) -> List[Tuple[Tuple[str, object], ...]]:
        expr = normalize_space(strip_outer_parens(expr))
        if not expr or expr in {"nil", "aux.TRUE", "true"}:
            return []
        direct = self.parse_direct_filter(expr)
        if direct is not None:
            return direct
        body = self.functions.get(expr) or self.functions.get(expr.split(".")[-1])
        if body is None:
            return []
        return self.parse_function_body(body)

    def parse_direct_filter(self, expr: str) -> Optional[List[Tuple[Tuple[str, object], ...]]]:
        if re.fullmatch(r"(?:RD|RushDuel)\.Filter\(.+\)", expr):
            args = split_args(expr[expr.find("(") + 1:-1])
            if not args:
                return []
            if args[0].startswith("Card."):
                return self.build_direct_groups(args[0][5:], ",".join(args[1:]))
            return self.parse_filter_reference(args[0])
        if re.fullmatch(r"(?:aux|Auxiliary)\.FilterBoolFunction(?:Ex)?\(.+\)", expr):
            inner = expr[expr.find("(") + 1:-1]
            args = split_args(inner)
            if len(args) < 2:
                return []
            func_name = args[0].replace("Card.", "")
            raw_values = ",".join(args[1:])
            return self.build_direct_groups(func_name, raw_values)
        if re.fullmatch(r"(?:aux|Auxiliary)\.NecroValleyFilter\(.+\)", expr):
            inner = expr[expr.find("(") + 1:-1]
            args = split_args(inner)
            if not args:
                return []
            return self.parse_filter_reference(args[0])
        if re.fullmatch(r"(?:aux|Auxiliary)\.NOT\(.+\)", expr):
            inner = expr[expr.find("(") + 1:-1]
            groups = self.parse_direct_filter(inner)
            if groups is None:
                return []
            negated: List[Tuple[Tuple[str, object], ...]] = []
            for group in groups:
                converted: List[Tuple[str, object]] = []
                for kind, value in group:
                    if kind == "type_any":
                        converted.append(("type_not", value))
                    elif kind == "race_any":
                        converted.append(("race_not", value))
                    elif kind == "attr_any":
                        converted.append(("attr_not", value))
                if converted:
                    negated.append(tuple(converted))
            return negated
        if re.fullmatch(r"(?:aux|Auxiliary)\.NonTuner\(.+\)", expr):
            inner = expr[expr.find("(") + 1:-1]
            args = split_args(inner)
            if len(args) < 2:
                return []
            func_name = args[0].replace("Card.", "")
            raw_values = ",".join(args[1:])
            groups = self.build_direct_groups(func_name, raw_values) or []
            results: List[Tuple[Tuple[str, object], ...]] = []
            for group in groups:
                results.append(tuple(list(group) + [("type_not", self.type_tuner)]))
            return results
        return None

    def build_direct_groups(self, func_name: str, raw_values: str) -> List[Tuple[Tuple[str, object], ...]]:
        if func_name in {"IsSetCard", "IsFusionSetCard", "IsLinkSetCard"}:
            values = self.parse_code_values(raw_values)
            return [tuple([("setcode_any", values)])] if values else []
        if func_name == "IsCode":
            values = self.parse_code_values(raw_values)
            return [tuple([("id_any", values)])] if values else []
        if func_name in {"IsRace", "IsFusionRace", "IsLinkRace"}:
            mask = self.parse_named_mask(raw_values, ("RACE_",))
            return [tuple([("race_any", mask)])] if mask else []
        if func_name in {"IsAttribute", "IsFusionAttribute", "IsLinkAttribute"}:
            mask = self.parse_named_mask(raw_values, ("ATTRIBUTE_",))
            return [tuple([("attr_any", mask)])] if mask else []
        if func_name in {"IsType", "IsFusionType", "IsLinkType", "IsXyzType"}:
            mask = self.parse_named_mask(raw_values, ("TYPE_",))
            return [tuple([("type_any", mask)])] if mask else []
        return []

    def parse_atom(self, atom: str) -> Optional[List[Tuple[Tuple[str, object], ...]]]:
        atom = normalize_space(strip_outer_parens(atom))
        if not atom:
            return []

        direct = self.parse_direct_filter(atom)
        if direct is not None:
            return direct

        if any(name in atom for name in self.IGNORED_CALLS):
            return [tuple()]

        if atom.startswith("not "):
            inner = atom[4:].strip()
            groups = self.parse_atom(inner) or []
            negated: List[Tuple[Tuple[str, object], ...]] = []
            for group in groups:
                converted: List[Tuple[str, object]] = []
                for kind, value in group:
                    if kind == "type_any":
                        converted.append(("type_not", value))
                    elif kind == "race_any":
                        converted.append(("race_not", value))
                    elif kind == "attr_any":
                        converted.append(("attr_not", value))
                if converted:
                    negated.append(tuple(converted))
            return negated

        rush_code = re.fullmatch(r"(?:RD|RushDuel)\.IsLegendCode\(\w+\s*,\s*(.+)\)", atom)
        if rush_code:
            values = self.parse_code_values(rush_code.group(1))
            return [(("id_any", values),)] if values else []

        # These helpers add a live Maximum-mode check to the printed DEF test.
        # Related cards describe static compatibility, not a current duel state.
        rush_defense = re.fullmatch(r"(?:RD|RushDuel)\.IsDefense(Below|Above)?\(\w+\s*,\s*(\d+)\)", atom)
        if rush_defense:
            suffix = {None: "eq", "Below": "max", "Above": "min"}[rush_defense.group(1)]
            return [(("defense_" + suffix, int(rush_defense.group(2))),)]

        code_list_match = re.search(r"aux\.IsCodeListed\(\w+\s*,\s*([^)]+)\)", atom)
        if code_list_match:
            values = self.parse_code_values(code_list_match.group(1))
            return [tuple([("id_any", values)])] if values else []

        setname_match = re.search(r"aux\.IsSetNameMonsterListed\(\w+\s*,\s*([^)]+)\)", atom)
        if setname_match:
            values = self.parse_code_values(setname_match.group(1))
            return [tuple([("setcode_any", values)])] if values else []

        type_band = re.search(r"bit\.band\([^)]*GetType\(\)\s*,\s*([A-Z0-9_+]+)\)\s*==\s*([A-Z0-9_+]+)", atom)
        if type_band:
            mask = self.parse_named_mask(type_band.group(2), ("TYPE_",))
            if not mask:
                return []
            required = tuple(("type_any", bit) for bit in iter_set_bits(mask))
            return [required] if required else []

        comparisons = [
            (r":IsLevelBelow\((\d+)\)", "level_max"),
            (r":IsLevelAbove\((\d+)\)", "level_min"),
            (r":IsLevel\((\d+)\)", "level_eq"),
            (r":IsRankBelow\((\d+)\)", "rank_max"),
            (r":IsRankAbove\((\d+)\)", "rank_min"),
            (r":IsRank\((\d+)\)", "rank_eq"),
            (r":IsLinkBelow\((\d+)\)", "link_max"),
            (r":IsLinkAbove\((\d+)\)", "link_min"),
            (r":IsLink\((\d+)\)", "link_eq"),
            (r":IsAttackBelow\((\d+)\)", "attack_max"),
            (r":IsAttackAbove\((\d+)\)", "attack_min"),
            (r":IsAttack\((\d+)\)", "attack_eq"),
            (r":IsDefenseBelow\((\d+)\)", "defense_max"),
            (r":IsDefenseAbove\((\d+)\)", "defense_min"),
            (r":IsDefense\((\d+)\)", "defense_eq"),
        ]
        for pattern, kind in comparisons:
            match = re.search(pattern, atom)
            if match:
                return [tuple([(kind, int(match.group(1)))])]

        simple_calls = [
            (r":IsCode\(([^)]+)\)", "id_any", ("",)),
            (r":Is(?:Fusion|Link)?SetCard\(([^)]+)\)", "setcode_any", ("",)),
            (r":Is(?:Fusion|Link)?Race\(([^)]+)\)", "race_any", ("RACE_",)),
            (r":Is(?:Fusion|\w*|Link)Attribute\(([^)]+)\)", "attr_any", ("ATTRIBUTE_",)),
            (r":Is(?:Fusion|Link|Xyz)?Type\(([^)]+)\)", "type_any", ("TYPE_",)),
        ]
        for pattern, kind, prefixes in simple_calls:
            match = re.search(pattern, atom)
            if not match:
                continue
            if kind in {"id_any", "setcode_any"}:
                values = self.parse_code_values(match.group(1))
                return [tuple([(kind, values)])] if values else []
            mask = self.parse_named_mask(match.group(1), prefixes)
            return [tuple([(kind, mask)])] if mask else []

        return [tuple()]

    def parse_expression(self, expr: str) -> List[Tuple[Tuple[str, object], ...]]:
        expr = normalize_space(strip_outer_parens(expr))
        if not expr:
            return []

        or_parts = split_top_level(expr, "or")
        if len(or_parts) > 1:
            groups: List[Tuple[Tuple[str, object], ...]] = []
            for part in or_parts:
                groups.extend(self.parse_expression(part))
            return dedupe_groups(groups)

        and_parts = split_top_level(expr, "and")
        groups: List[Tuple[Tuple[str, object], ...]] = [tuple()]
        for part in and_parts:
            part_groups = self.parse_atom(part)
            if part_groups is None or not part_groups:
                continue
            combined: List[Tuple[Tuple[str, object], ...]] = []
            for base in groups:
                for addition in part_groups:
                    combined.append(tuple(list(base) + list(addition)))
            groups = combined
        return dedupe_groups(groups)

    def parse_function_body(self, function_body: str) -> List[Tuple[Tuple[str, object], ...]]:
        groups: List[Tuple[Tuple[str, object], ...]] = []
        for expression in extract_return_expressions(function_body):
            groups.extend(self.parse_expression(expression))
        return dedupe_groups(groups)


class CardMatcher:
    def __init__(self, cards: Dict[int, CardRecord], constants: Dict[str, int]):
        self.cards = cards
        self.all_ids: Set[int] = set(cards.keys())
        self.condition_cache: Dict[Tuple[str, object], Set[int]] = {}
        self.type_index: Dict[int, Set[int]] = defaultdict(set)
        self.race_index: Dict[int, Set[int]] = defaultdict(set)
        self.attribute_index: Dict[int, Set[int]] = defaultdict(set)
        self.level_index: Dict[int, Set[int]] = defaultdict(set)
        self.rank_index: Dict[int, Set[int]] = defaultdict(set)
        self.link_index: Dict[int, Set[int]] = defaultdict(set)
        self.code_index: Dict[int, Set[int]] = defaultdict(set)
        for card in cards.values():
            self.code_index[card.alias or card.id].add(card.id)
            self.code_index[card.id].add(card.id)
            for bit in iter_set_bits(card.type_mask):
                self.type_index[bit].add(card.id)
            for bit in iter_set_bits(card.race):
                self.race_index[bit].add(card.id)
            for bit in iter_set_bits(card.attribute):
                self.attribute_index[bit].add(card.id)
            if card.level is not None:
                self.level_index[card.level].add(card.id)
            if card.rank is not None:
                self.rank_index[card.rank].add(card.id)
            if card.link is not None:
                self.link_index[card.link].add(card.id)

    def ids_for_mask(self, index: Dict[int, Set[int]], mask: int) -> Set[int]:
        matched: Set[int] = set()
        for bit in iter_set_bits(mask):
            matched.update(index.get(bit, set()))
        return matched

    @staticmethod
    def compare_numeric(kind: str, card_value: int, value: int) -> bool:
        if kind.endswith("_eq"):
            return card_value == value
        if kind.endswith("_min"):
            return card_value >= value
        if kind.endswith("_max"):
            return card_value <= value
        return False

    @staticmethod
    def if_set_card(setcode_to_analyse: int, setcode_from_card: int) -> bool:
        settype = setcode_to_analyse & 0x0FFF
        setsubtype = setcode_to_analyse & 0xF000
        current = setcode_from_card
        while current:
            segment = current & 0xFFFF
            if (segment & 0x0FFF) == settype and (segment & 0xF000 & setsubtype) == setsubtype:
                return True
            current >>= 16
        return False

    def ids_for_condition(self, condition: Tuple[str, object]) -> Set[int]:
        if condition in self.condition_cache:
            return self.condition_cache[condition]

        kind, value = condition
        matched: Set[int]
        if kind == "id_any":
            matched = set().union(*(self.code_index.get(candidate, set()) for candidate in value))
        elif kind == "setcode_any":
            matched = {
                card.id
                for card in self.cards.values()
                if any(self.if_set_card(setcode, card.setcode) for setcode in value)
            }
        elif kind in {"type_any", "type_not"}:
            matched = self.ids_for_mask(self.type_index, int(value))
        elif kind in {"race_any", "race_not"}:
            matched = self.ids_for_mask(self.race_index, int(value))
        elif kind in {"attr_any", "attr_not"}:
            matched = self.ids_for_mask(self.attribute_index, int(value))
        elif kind.startswith("level_"):
            matched = {
                card_id
                for level, ids in self.level_index.items()
                if self.compare_numeric(kind, level, int(value))
                for card_id in ids
            }
        elif kind.startswith("rank_"):
            matched = {
                card_id
                for rank, ids in self.rank_index.items()
                if self.compare_numeric(kind, rank, int(value))
                for card_id in ids
            }
        elif kind.startswith("link_"):
            matched = {
                card_id
                for link, ids in self.link_index.items()
                if self.compare_numeric(kind, link, int(value))
                for card_id in ids
            }
        elif kind.startswith("attack_"):
            matched = {
                card.id
                for card in self.cards.values()
                if card.attack is not None and self.compare_numeric(kind, card.attack, int(value))
            }
        elif kind.startswith("defense_"):
            matched = {
                card.id
                for card in self.cards.values()
                if card.defense is not None and self.compare_numeric(kind, card.defense, int(value))
            }
        else:
            matched = set()

        self.condition_cache[condition] = matched
        return matched

    def match_group(self, group: Sequence[Tuple[str, object]]) -> Set[int]:
        candidates: Optional[Set[int]] = None
        negative_sets: List[Set[int]] = []
        for condition in group:
            kind, _ = condition
            ids = self.ids_for_condition(condition)
            if kind.endswith("_not"):
                negative_sets.append(ids)
                continue
            candidates = set(ids) if candidates is None else candidates & ids
            if not candidates:
                return set()
        if candidates is None:
            candidates = set(self.all_ids)
        for ids in negative_sets:
            candidates -= ids
            if not candidates:
                return set()
        return candidates


class RelatedCardsBuilder:
    FILTER_CALL_ARGUMENTS: Tuple[Tuple[str, int], ...] = (
        (r"Duel\.IsExistingMatchingCard\s*\(", 0),
        (r"Duel\.GetMatchingGroup(?:Count)?\s*\(", 0),
        (r"Duel\.GetFirstMatchingCard\s*\(", 0),
        (r"Duel\.SelectMatchingCard\s*\(", 1),
        (r"Duel\.IsExistingTarget\s*\(", 0),
        (r"Duel\.SelectTarget\s*\(", 1),
        (r"Duel\.CheckReleaseGroup(?:Ex)?\s*\(", 1),
        (r"Duel\.SelectReleaseGroup(?:Ex)?\s*\(", 1),
        (r"Duel\.SelectTribute\s*\(", 1),
        (r"(?:RD|RushDuel)\.Select(?:Group)?AndDoAction\s*\(", 1),
        (r"(?:RD|RushDuel)\.CanSelect(?:Group)?AndDoAction\s*\(", 2),
        (r"(?:RD|RushDuel)\.Select(?:Group)?And(?:SpecialSummon|Set)\s*\(", 0),
        (r"(?:RD|RushDuel)\.CanSelect(?:Group)?And(?:SpecialSummon|Set)\s*\(", 1),
        (r"(?:RD|RushDuel)\.SelectAndToHandOrSpecialSummon\s*\(", 1),
        (r"(?:RD|RushDuel)\.CanSelectAndToHandOrSpecialSummon\s*\(", 2),
        (r"(?:RD|RushDuel)\.CreateRitualEffect\s*\(", 2),
        (r"(?:RD|RushDuel)\.CreateRitualEffect\s*\(", 3),
        (r"(?:RD|RushDuel)\.AddFusionProcedure(?:Rep|SP)\s*\(", 3),
    )
    MAX_AUTO_GROUP_MATCHES = 3000

    def __init__(self, cards_cdb: Path, scripts_dir: Path, manual_files: Sequence[Path],
                 constants: Optional[Dict[str, int]] = None, rush: bool = False):
        self.cards_cdb = cards_cdb
        self.scripts_dir = scripts_dir
        self.script_source = ScriptSource(scripts_dir)
        self.manual_files = list(manual_files)
        try:
            self.constants = dict(constants) if constants is not None else self.load_constants()
            if rush:
                for name in ("RDBase.lua", "RDLegend.lua", "RDMaximum.lua"):
                    self.constants.update(self.load_constants(name))
            self.cards = self.load_cards()
        except BaseException:
            self.script_source.close()
            raise
        self.manual_warnings: List[str] = []
        self.manual_override_ids: Set[int] = set()

    def load_constants(self, name: str = "constant.lua") -> Dict[str, int]:
        constants: Dict[str, int] = {}
        pattern = re.compile(r"^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(0x[0-9A-Fa-f]+|\d+)")
        for line in self.script_source.read_text(name).splitlines():
            match = pattern.match(line)
            if not match:
                continue
            constants[match.group(1)] = int(match.group(2), 0)
        return constants

    def load_cards(self) -> Dict[int, CardRecord]:
        cards: Dict[int, CardRecord] = {}
        with closing(sqlite3.connect(self.cards_cdb.resolve().as_uri() + "?mode=ro", uri=True)) as connection:
            cursor = connection.execute("SELECT id, setcode, type, level, race, attribute, atk, def, alias FROM datas")
            type_spell = self.constants.get("TYPE_SPELL", 0)
            type_trap = self.constants.get("TYPE_TRAP", 0)
            type_link = self.constants.get("TYPE_LINK", 0)
            type_xyz = self.constants.get("TYPE_XYZ", 0)
            for row in cursor:
                card_id, setcode, type_mask, level_raw, race, attribute, atk, defense, alias = row
                # SQLite can expose legacy CDB bit fields as signed integers.
                # In particular Cyborg's bit 31 must not sign-extend forever in
                # Python's unbounded bit iterator. Match the host Card fields.
                setcode &= 0xFFFFFFFFFFFFFFFF
                type_mask &= 0xFFFFFFFF
                race &= 0xFFFFFFFF
                attribute &= 0xFFFFFFFF
                level = None
                rank = None
                link = None
                normalized_attack = atk
                normalized_defense = defense
                normalized_race = race
                normalized_attribute = attribute
                if type_mask & (type_spell | type_trap):
                    normalized_attack = None
                    normalized_defense = None
                    normalized_race = 0
                    normalized_attribute = 0
                elif type_mask & type_link:
                    link = level_raw & 0xFF
                    normalized_defense = None
                elif type_mask & type_xyz:
                    rank = level_raw & 0xFF
                else:
                    level = level_raw & 0xFF
                if level is None and rank is None and link is None and not (type_mask & (type_spell | type_trap)):
                    level = level_raw & 0xFF
                cards[card_id] = CardRecord(
                    id=card_id,
                    setcode=setcode,
                    type_mask=type_mask,
                    race=normalized_race,
                    attribute=normalized_attribute,
                    attack=normalized_attack,
                    defense=normalized_defense,
                    level=level,
                    rank=rank,
                    link=link,
                    alias=alias,
                )
        return cards

    def find_calls(self, script_text: str, call_pattern: str) -> List[List[str]]:
        calls: List[List[str]] = []
        for match in re.finditer(call_pattern, script_text):
            args_text, _ = extract_parenthesized(script_text, match.end())
            calls.append(split_args(args_text))
        return calls

    @staticmethod
    def condition_family(kind: str) -> str:
        if kind == "id_any":
            return "id"
        if kind == "setcode_any":
            return "setcode"
        if kind.startswith(("level_", "rank_", "link_")):
            return "level"
        if kind.startswith(("attack_", "defense_")):
            return "stats"
        if kind.startswith("race_"):
            return "race"
        if kind.startswith("attr_"):
            return "attribute"
        if kind.startswith("type_"):
            return "type"
        return kind

    def has_specific_type_signal(self, group: Sequence[Tuple[str, object]]) -> bool:
        generic_type_mask = (
            self.constants.get("TYPE_MONSTER", 0)
            | self.constants.get("TYPE_SPELL", 0)
            | self.constants.get("TYPE_TRAP", 0)
            | self.constants.get("TYPE_TOKEN", 0)
        )
        for kind, value in group:
            if kind != "type_any":
                continue
            if int(value) & ~generic_type_mask:
                return True
        return False

    @staticmethod
    def is_probable_filter_expression(expr: str, functions: Dict[str, str]) -> bool:
        expr = normalize_space(strip_outer_parens(expr))
        if not expr:
            return False
        if expr in {"nil", "true", "false", "aux.TRUE", "aux.FALSE", "Auxiliary.TRUE", "Auxiliary.FALSE"}:
            return True
        if re.fullmatch(r"(?:aux|Auxiliary)\.(?:FilterBoolFunction(?:Ex)?|NOT|NonTuner|NecroValleyFilter)\(.+\)", expr):
            return True
        if re.fullmatch(r"(?:RD|RushDuel)\.Filter\(.+\)", expr):
            return True
        short_name = expr.split(".")[-1]
        return expr in functions or short_name in functions

    def is_useful_script_group(self, group: Sequence[Tuple[str, object]]) -> bool:
        if not group:
            return False

        positive_kinds = [kind for kind, _ in group if not kind.endswith("_not")]
        if not positive_kinds:
            return False

        exact_kinds = {
            "id_any",
            "setcode_any",
            "level_eq",
            "rank_eq",
            "link_eq",
            "attack_eq",
            "defense_eq",
        }
        if any(kind in exact_kinds for kind in positive_kinds):
            return True

        positive_families = {self.condition_family(kind) for kind in positive_kinds}
        if len(positive_families) >= 2:
            return True
        if positive_families in ({"level"}, {"stats"}) and len(positive_kinds) >= 2:
            return True
        if positive_families == {"type"}:
            return self.has_specific_type_signal(group)
        return False

    def get_card_setcode_queries(self, card_id: int) -> Tuple[int, ...]:
        card = self.cards.get(card_id)
        if card is None:
            return tuple()
        values: List[int] = []
        current = card.setcode
        while current:
            segment = current & 0xFFFF
            if segment:
                values.append(segment)
                base_segment = segment & 0x0FFF
                if base_segment and base_segment != segment:
                    values.append(base_segment)
            current >>= 16
        return tuple(dict.fromkeys(values))

    def parse_script_groups(self, script_name: str, visiting: Optional[Set[str]] = None) -> List[Tuple[Tuple[str, object], ...]]:
        visiting = set() if visiting is None else visiting
        if script_name in visiting:
            raise ValueError(f"Cyclic alternate-card script: {script_name}")
        visiting.add(script_name)
        script_text = self.script_source.read_text(script_name)
        # Alternate-art wrappers delegate their complete gameplay to this exact ID.
        alternate = re.fullmatch(r"\s*(?:RD|RushDuel)\.AlternateCard\((\d+)\)\s*", script_text)
        if alternate:
            canonical = int(alternate.group(1))
            groups = self.parse_script_groups(f"c{canonical}.lua", visiting)
            return dedupe_groups(groups + [(('id_any', (canonical,)),)])
        # Resolve only literal tables. Never execute Lua or guess dynamic lists.
        code_lists: Dict[str, Tuple[int, ...]] = {}
        literal_parser = ScriptExpressionParser(self.constants, {})
        for match in re.finditer(r"\blocal\s+(\w+)\s*=\s*\{([^{}]*)\}", script_text):
            values = literal_parser.parse_code_values(match.group(2))
            if values:
                code_lists[match.group(1)] = values
        functions = extract_functions(script_text)
        parser = ScriptExpressionParser(self.constants, functions, code_lists)
        groups: List[Tuple[Tuple[str, object], ...]] = []

        for args in self.find_calls(script_text, r"(?:aux|Auxiliary)\.AddXyzProcedure\s*\("):
            if len(args) >= 3:
                base_groups = parser.parse_filter_reference(args[1])
                level = parse_int(args[2])
                if level is not None:
                    if base_groups:
                        for group in base_groups:
                            groups.append(tuple(list(group) + [("level_eq", level)]))
                    else:
                        groups.append(tuple([("level_eq", level)]))
            if len(args) >= 5:
                groups.extend(parser.parse_filter_reference(args[4]))

        for args in self.find_calls(script_text, r"(?:aux|Auxiliary)\.AddLinkProcedure\s*\("):
            if len(args) >= 2:
                groups.extend(parser.parse_filter_reference(args[1]))

        for args in self.find_calls(script_text, r"(?:aux|Auxiliary)\.AddSynchroProcedure\w*\s*\("):
            for index in (1, 2):
                if len(args) > index:
                    groups.extend(parser.parse_filter_reference(args[index]))

        for args in self.find_calls(script_text, r"(?:aux|Auxiliary)\.AddRitualProc\w*\s*\("):
            for index in (1, 3):
                if len(args) > index:
                    groups.extend(parser.parse_filter_reference(args[index]))

        for pattern in (r"(?:aux|Auxiliary)\.AddFusionProc\w*\s*\(", r"Fusion\.AddProc\w*\s*\("):
            for args in self.find_calls(script_text, pattern):
                for expr in args[1:]:
                    if self.is_probable_filter_expression(expr, functions):
                        groups.extend(parser.parse_filter_reference(expr))

        for args in self.find_calls(script_text, r"(?:RD|RushDuel)\.AddFusionProcedure\s*\("):
            for expr in args[1:]:
                values = parser.parse_code_values(expr)
                if values:
                    groups.append((("id_any", values),))
                elif self.is_probable_filter_expression(expr, functions):
                    groups.extend(parser.parse_filter_reference(expr))

        for args in self.find_calls(script_text, r"(?:RD|RushDuel)\.AddMaximumProcedure\s*\("):
            if len(args) >= 4:
                values = parser.parse_code_values(",".join(args[2:4]))
                if values:
                    groups.append((("id_any", values),))

        for args in self.find_calls(script_text, r"(?:RD|RushDuel)\.EnableChangeCode\s*\("):
            if len(args) >= 2:
                values = parser.parse_code_values(args[1])
                if values:
                    groups.append((("id_any", values),))

        for pattern, filter_index in self.FILTER_CALL_ARGUMENTS:
            for args in self.find_calls(script_text, pattern):
                if len(args) <= filter_index:
                    continue
                expr = args[filter_index]
                if not self.is_probable_filter_expression(expr, functions):
                    continue
                groups.extend(parser.parse_filter_reference(expr))

        for args in self.find_calls(script_text, r"(?:aux|Auxiliary|RD|RushDuel)\.AddCodeList\s*\("):
            values = parser.parse_code_values(",".join(args[1:]))
            if values:
                groups.append(tuple([("id_any", values)]))

        for match in re.finditer(r"aux\.IsCodeListed\(\w+\s*,\s*([^)]+)\)", script_text):
            values = parser.parse_code_values(match.group(1))
            if values:
                groups.append(tuple([("id_any", values)]))

        return [group for group in dedupe_groups(groups) if self.is_useful_script_group(group)]

    def parse_manual_groups(self, manual_file: Path) -> Dict[int, List[Tuple[Tuple[str, object], ...]]]:
        result: Dict[int, List[Tuple[Tuple[str, object], ...]]] = {}
        parser = ScriptExpressionParser(self.constants, {})
        for line_number, raw_line in enumerate(manual_file.read_text(encoding="utf-8").splitlines(), start=1):
            line = raw_line.strip()
            if not line:
                continue
            match = re.match(r"^(\d+)\s+(.+)$", line)
            if not match:
                self.manual_warnings.append(f"{manual_file}:{line_number}: could not parse line")
                continue
            card_id = int(match.group(1))
            groups_text = re.findall(r"\[([^\]]+)\]", match.group(2))
            if not groups_text:
                self.manual_warnings.append(f"{manual_file}:{line_number}: missing [] groups")
                continue
            parsed_groups: List[Tuple[Tuple[str, object], ...]] = []
            for group_text in groups_text:
                try:
                    normalized = group_text.replace("AND", "and").replace("OR", "or")
                    parsed_groups.extend(parser.parse_expression(normalized))
                except Exception:
                    self.manual_warnings.append(f"{manual_file}:{line_number}: failed to parse group '{group_text}'")
            if parsed_groups:
                result[card_id] = dedupe_groups(parsed_groups)
        return result

    def collect_source_groups(self) -> Dict[int, List[Tuple[Tuple[str, object], ...]]]:
        source_groups: Dict[int, List[Tuple[Tuple[str, object], ...]]] = {}
        self.manual_override_ids.clear()
        for script_name in self.script_source.iter_script_names():
            match = re.fullmatch(r"c(\d+)\.lua", script_name)
            if not match:
                continue
            card_id = int(match.group(1))
            groups = self.parse_script_groups(script_name)
            setcode_queries = self.get_card_setcode_queries(card_id)
            if setcode_queries:
                groups.append(tuple([("setcode_any", setcode_queries)]))
                groups = dedupe_groups(groups)
            if groups:
                source_groups[card_id] = groups

        for manual_file in self.manual_files:
            if manual_file.exists():
                for card_id, groups in self.parse_manual_groups(manual_file).items():
                    source_groups[card_id] = groups
                    self.manual_override_ids.add(card_id)

        return source_groups

    def get_sort_rank(self, card_id: int) -> Tuple[int, int]:
        card = self.cards.get(card_id)
        if card is None:
            return (999, card_id)
        order = [
            self.constants.get("TYPE_MONSTER", 0),
            self.constants.get("TYPE_EFFECT", 0),
            self.constants.get("TYPE_PENDULUM", 0),
            self.constants.get("TYPE_FUSION", 0),
            self.constants.get("TYPE_RITUAL", 0),
            self.constants.get("TYPE_SYNCHRO", 0),
            self.constants.get("TYPE_XYZ", 0),
            self.constants.get("TYPE_LINK", 0),
            self.constants.get("TYPE_SPELL", 0),
            self.constants.get("TYPE_TRAP", 0),
        ]
        rank = 999
        for index, bit in enumerate(order):
            if bit and card.type_mask & bit:
                rank = max(rank if rank != 999 else index, index)
        return (rank, card_id)

    def build(self) -> Dict[str, Dict[str, List[int]]]:
        source_groups = self.collect_source_groups()
        matcher = CardMatcher(self.cards, self.constants)
        # A card can satisfy many equivalent script conditions. Deduplicate while
        # collecting and reversing edges, rather than multiplying temporary lists.
        related: Dict[int, Dict[str, Set[int]]] = defaultdict(
            lambda: {category: set() for category in CATEGORY_ORDER})
        type_token = self.constants.get("TYPE_TOKEN", 0)

        for source_id, groups in source_groups.items():
            is_manual_override = source_id in self.manual_override_ids
            for group in groups:
                category = condition_category(group)
                matched_targets = matcher.match_group(group)
                if (
                    not is_manual_override
                    and category not in {"ID", "Arch"}
                    and len(matched_targets) > self.MAX_AUTO_GROUP_MATCHES
                ):
                    continue
                for target_id in matched_targets:
                    if target_id == source_id:
                        continue
                    target_card = self.cards.get(target_id)
                    if target_card is None or (type_token and target_card.type_mask & type_token):
                        continue
                    related[target_id][category].add(source_id)

        for target_id, categories in list(related.items()):
            for category in MUTUAL_SOURCE_CATEGORIES:
                for source_id in list(categories.get(category, [])):
                    if source_id == target_id:
                        continue
                    reverse = related[source_id]
                    if category in {"ID", "Arch"}:
                        reverse[category].add(target_id)
                    else:
                        reverse["Targets"].add(target_id)

        cleaned: Dict[str, Dict[str, List[int]]] = {}
        for card_id, categories in related.items():
            card = self.cards.get(card_id)
            if card is None or (type_token and card.type_mask & type_token):
                continue
            ordered: Dict[str, List[int]] = {}
            has_any = False
            for category in CATEGORY_ORDER:
                seen: Set[int] = set()
                filtered = []
                for related_id in categories.get(category, []):
                    if related_id == card_id or related_id not in self.cards or related_id in seen:
                        continue
                    seen.add(related_id)
                    filtered.append(related_id)
                filtered.sort(key=self.get_sort_rank)
                ordered[category] = filtered
                has_any = has_any or bool(filtered)
            if has_any:
                cleaned[str(card_id)] = ordered

        return dict(sorted(cleaned.items(), key=lambda item: int(item[0])))

    def close(self) -> None:
        self.script_source.close()


def find_repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def default_cards_cdb(repo_root: Path, locale: str) -> Path:
    return repo_root / "Data" / "locales" / locale / "cards.cdb"


def default_scripts_source(repo_root: Path) -> Path:
    candidates = [
        repo_root / "Data" / "script.zip",
        repo_root / "Data" / "script",
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    return candidates[0]


def normalize_output_path(path: Path) -> Path:
    if path.suffix.lower() == ".json":
        return path
    return path / "RelatedCards.json"


def main() -> int:
    repo_root = find_repo_root()
    parser = argparse.ArgumentParser(description="Build MDPro4 RelatedCards.json from isolated standard and Rush catalogs.")
    parser.add_argument("--format", choices=("all", "standard", "rush"), default="all",
                        help="Catalogs to include (default: all); standard preserves single-CDB workflows")
    parser.add_argument("--locale", default="en-US", help="Locale folder to use under Data/locales (default: en-US)")
    parser.add_argument("--cards-cdb", type=Path, help="Path to cards.cdb (defaults to Data/locales/<locale>/cards.cdb)")
    parser.add_argument(
        "--scripts-dir",
        "--scripts-source",
        dest="scripts_dir",
        type=Path,
        help="Path to extracted Lua scripts directory or script.zip archive (defaults to Data/script.zip)",
    )
    parser.add_argument(
        "--manual-conditions",
        type=Path,
        action="append",
        default=[],
        help="Optional override file in the old [Condition] format. Later files win.",
    )
    parser.add_argument(
        "--rush-cards-cdb", type=Path,
        help="Rush database (defaults to Data/locales/<locale>/rush_cards.cdb)",
    )
    parser.add_argument(
        "--rush-scripts-source", type=Path,
        help="Rush Lua folder or archive (defaults to Data/rush_scripts.zip)",
    )
    parser.add_argument(
        "--rush-manual-conditions", type=Path, action="append", default=[],
        help="Optional Rush-only overrides in the old [Condition] format. Later files win.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        help="Output file or directory (defaults to Assets/StreamingAssets/RelatedCards.json)",
    )
    args = parser.parse_args()

    args.cards_cdb = args.cards_cdb or default_cards_cdb(repo_root, args.locale)
    args.scripts_dir = args.scripts_dir or default_scripts_source(repo_root)
    args.rush_cards_cdb = args.rush_cards_cdb or repo_root / "Data" / "locales" / args.locale / "rush_cards.cdb"
    args.rush_scripts_source = args.rush_scripts_source or repo_root / "Data" / "rush_scripts.zip"
    args.output = normalize_output_path(args.output or (repo_root / "Assets" / "StreamingAssets"))

    if not args.cards_cdb.exists():
        parser.error(f"cards.cdb was not found at '{args.cards_cdb}'")
    if not args.scripts_dir.exists():
        parser.error(f"scripts source was not found at '{args.scripts_dir}'")

    if args.format != "standard":
        for path in (args.rush_cards_cdb, args.rush_scripts_source):
            if not path.exists():
                parser.error(f"Rush input was not found at '{path}'; use --format standard to build only standard cards")

    builders = []
    try:
        standard = RelatedCardsBuilder(args.cards_cdb, args.scripts_dir, args.manual_conditions)
        builders.append(standard)
        related_cards = standard.build() if args.format != "rush" else {}
        if args.format != "rush":
            print(f"Standard: {len(standard.cards)} cards, {len(related_cards)} related-card entries")
        if args.format != "standard":
            rush = RelatedCardsBuilder(args.rush_cards_cdb, args.rush_scripts_source,
                                       args.rush_manual_conditions, constants=standard.constants, rush=True)
            builders.append(rush)
            overlapping = standard.cards.keys() & rush.cards.keys()
            if overlapping:
                parser.error(f"Standard and Rush databases share card IDs: {sorted(overlapping)[:10]}")
            rush_related = rush.build()
            # Match each format independently; a shared attribute must not connect
            # cards from incompatible formats in the merged runtime file.
            related_cards.update(rush_related)
            print(f"Rush: {len(rush.cards)} cards, {len(rush_related)} related-card entries")
            print(f"Using Rush inputs: {args.rush_cards_cdb}, {args.rush_scripts_source}")
        related_cards = dict(sorted(related_cards.items(), key=lambda item: int(item[0])))
    finally:
        for builder in builders:
            builder.close()

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(related_cards, indent=2), encoding="utf-8")

    print(f"Using cards.cdb: {args.cards_cdb}")
    print(f"Using scripts: {args.scripts_dir}")
    print(f"Writing output: {args.output}")
    print(f"Wrote {len(related_cards)} related-card entries ({args.output.stat().st_size:,} bytes) to {args.output}")
    for builder in builders:
        if builder.manual_warnings:
            print("Warnings while reading manual condition files:")
            for warning in builder.manual_warnings[:50]:
                print(f"  - {warning}")
            if len(builder.manual_warnings) > 50:
                print(f"  - ... {len(builder.manual_warnings) - 50} more")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
