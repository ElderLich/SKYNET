#!/usr/bin/env python3
# -*- coding: utf-8 -*-

r"""
Compile deck entries into a single .txt:
- Parse IDs from Deck.txt (C#-style const lines).
- Map EDOPRO card id -> cid via data.json (cid -> {cid, id}), where "id" may be int OR list[int].
- Read English JSON: <translations_output>/<cid>.json
- Read Lua script:   <scripts>/c<ID>.lua (recursively searched)
- NEW: Optional pre-release fallback:
    - Look up id in a pre-release .cdb texts table (id, name, desc)
    - If JSON is missing (or cid not found), emit a minimal JSON from the CDB: {id,name,desc}
    - Optionally search pre-release scripts tree; if CDB hit, prefer pre-release scripts first.

Usage (defaults match your original paths + your pre-release paths):
    python Pull.py
Or override any path:
    python Pull.py --deck Deck.txt ^
        --translations "D:\Modding\Official MD Translations\01. English\output" ^
        --data "D:\Modding\Card Replace Database\data.json" ^
        --scripts "D:\Modding\Deck Compiler\script" ^
        --pr-scripts "D:\Modding\Deck Compiler\pre-release\script" ^
        --pr-cdb "D:\Modding\Deck Compiler\pre-release\test-release.cdb" ^
        --out compiled_deck_cards.txt

Options:
    --keep-dupes               Keep duplicate IDs from Deck.txt
    --suppress-missing-lua     Never warn when a Lua file is missing
    --pr-cdb ""                Disable pre-release cdb lookup
    --pr-scripts ""            Disable pre-release script lookup
"""

import argparse
import json
import re
import sqlite3
from pathlib import Path
from datetime import datetime

# Matches: public const int RedEyesBlackDragon = 74677427;
CONST_LINE_RE = re.compile(r'([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(\d+)\s*;')

def parse_args():
    p = argparse.ArgumentParser(description="Compile card JSON + Lua script into a single .txt from a deck list.")
    p.add_argument("--deck", default="Deck.txt", help="Path to Deck.txt (default: Deck.txt in current folder)")

    p.add_argument("--translations", default=r"D:\Modding\Official MD Translations\01. English\output",
                   help="Folder with English JSONs named <cid>.json")

    p.add_argument("--data", default=r"D:\Modding\Card Replace Database\data.json",
                   help="Path to data.json (maps cid -> {cid, id}, where id may be int or list[int])")

    p.add_argument("--scripts", default=r"D:\Modding\Deck Compiler\script",
                   help="Primary folder tree with Lua scripts (files named c<ID>.lua)")

    # NEW: pre-release paths (optional)
    p.add_argument("--pr-scripts", default=r"D:\Modding\Deck Compiler\pre-release\script",
                   help='Optional pre-release Lua scripts root. Set to "" to disable.')

    p.add_argument("--pr-cdb", default=r"D:\Modding\Deck Compiler\pre-release\test-release.cdb",
                   help='Optional pre-release .cdb to read texts table from. Set to "" to disable.')

    p.add_argument("--out", default="compiled_deck_cards.txt", help="Output .txt file path")
    p.add_argument("--keep-dupes", action="store_true", help="Keep duplicate IDs if they appear multiple times in Deck.txt")
    p.add_argument("--suppress-missing-lua", action="store_true", help="Silence missing-Lua warnings entirely")
    return p.parse_args()

def parse_deck_ids(deck_path: Path, keep_dupes: bool):
    text = deck_path.read_text(encoding="utf-8", errors="ignore")
    found = CONST_LINE_RE.findall(text)  # -> list[(const_name, id_str)]
    ids = []
    seen = set()
    for const_name, id_str in found:
        card_id = int(id_str)
        if not keep_dupes:
            if card_id in seen:
                continue
            seen.add(card_id)
        ids.append((const_name, card_id))
    return ids

def coerce_int(x):
    if isinstance(x, int):
        return x
    if isinstance(x, str):
        x = x.strip()
        if x.isdigit():
            return int(x)
    return None

def load_id_to_cid_map(data_json_path: Path):
    """
    data.json example:
      "4088":  { "cid": 4088,  "id": [74677422, 74677423, ...] }
      "20548": { "cid": 20548, "id": 15216188 }
    Build: id -> cid (for every id listed)
    """
    obj = json.loads(data_json_path.read_text(encoding="utf-8"))
    id_to_cid = {}
    for _k, val in obj.items():
        try:
            cid = coerce_int(val.get("cid"))
            if cid is None:
                continue
            vid = val.get("id")
            if isinstance(vid, list):
                for item in vid:
                    iv = coerce_int(item)
                    if iv is not None:
                        id_to_cid[iv] = cid
            else:
                iv = coerce_int(vid)
                if iv is not None:
                    id_to_cid[iv] = cid
        except Exception:
            continue
    return id_to_cid

def safe_read_text(path: Path):
    if not path.exists():
        return None
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(errors="ignore")

def find_lua_script_text(card_id: int, scripts_dir: Path):
    """Return (text, path) or (None, None)."""
    if scripts_dir is None:
        return None, None
    direct = scripts_dir / f"c{card_id}.lua"
    if direct.exists():
        return safe_read_text(direct), direct
    for p in scripts_dir.rglob(f"c{card_id}.lua"):
        return safe_read_text(p), p
    return None, None

def find_lua_script_text_multi(card_id: int, script_roots):
    """
    Try each scripts root in order, return first hit.
    script_roots: list[Path|None]
    """
    for root in script_roots:
        if root is None:
            continue
        txt, p = find_lua_script_text(card_id, root)
        if txt is not None:
            return txt, p
    return None, None

def open_cdb(cdb_path: Path):
    """
    Open a YGOPro/EDOPro .cdb (sqlite). Returns connection or None.
    """
    if cdb_path is None:
        return None
    if not cdb_path.exists():
        print(f"[WARN] Pre-release CDB not found: {cdb_path}")
        return None

    conn = sqlite3.connect(str(cdb_path))
    conn.row_factory = sqlite3.Row

    # Be tolerant to weird encodings (usually UTF-8, but this avoids hard crashes).
    conn.text_factory = lambda b: b.decode("utf-8", "ignore") if isinstance(b, (bytes, bytearray)) else str(b)
    return conn

def get_texts_row(conn, card_id: int):
    """
    Query texts table for (id, name, desc). Returns dict or None.
    """
    if conn is None:
        return None
    cur = conn.cursor()
    try:
        cur.execute('SELECT id, name, "desc" AS desc FROM texts WHERE id=?', (card_id,))
    except sqlite3.OperationalError:
        # fallback if quoting differs
        try:
            cur.execute("SELECT id, name, desc FROM texts WHERE id=?", (card_id,))
        except sqlite3.OperationalError:
            return None
    row = cur.fetchone()
    if not row:
        return None
    return {"id": row["id"], "name": row["name"], "desc": row["desc"]}

def minimal_json_from_cdb(texts_row):
    """
    Build a minimal JSON object for consistent output when normal JSON is unavailable.
    """
    obj = {
        "id": int(texts_row["id"]),
        "name": texts_row.get("name") or "",
        "desc": texts_row.get("desc") or "",
    }
    return json.dumps(obj, ensure_ascii=False, indent=2)

def compile_entry_block(idx, const_name, card_id, cid, card_json_text, lua_text, lua_path):
    # Pull "name" from JSON for nicer headers (best-effort)
    display_name = None
    try:
        data = json.loads(card_json_text) if card_json_text else {}
        display_name = data.get("name")
    except Exception:
        pass

    header_name = display_name or const_name or "(unknown)"
    header = f"===== CARD {idx} | {header_name} | id={card_id} | cid={cid if cid is not None else 'N/A'} ====="
    parts = [header]

    parts.append("\n--- CARD JSON ---")
    parts.append(card_json_text if card_json_text is not None else "(missing JSON)")

    parts.append("\n--- LUA SCRIPT ---")
    if lua_text is not None:
        parts.append(lua_text)
    else:
        parts.append("(missing Lua script)")

    parts.append("===== END CARD =====\n")
    return "\n".join(parts)

def main():
    args = parse_args()

    deck_path = Path(args.deck)
    translations_dir = Path(args.translations)
    data_json_path = Path(args.data)
    scripts_dir = Path(args.scripts)

    pr_scripts_dir = Path(args.pr_scripts) if str(args.pr_scripts).strip() else None
    pr_cdb_path = Path(args.pr_cdb) if str(args.pr_cdb).strip() else None

    out_path = Path(args.out)

    if not deck_path.exists():
        raise FileNotFoundError(f"Deck file not found: {deck_path}")
    if not data_json_path.exists():
        raise FileNotFoundError(f"data.json not found: {data_json_path}")
    if not translations_dir.exists():
        raise FileNotFoundError(f"Translations folder not found: {translations_dir}")
    if not scripts_dir.exists():
        raise FileNotFoundError(f"Lua scripts folder not found: {scripts_dir}")
    if pr_scripts_dir is not None and not pr_scripts_dir.exists():
        print(f"[WARN] Pre-release scripts folder not found: {pr_scripts_dir} (will ignore)")
        pr_scripts_dir = None

    deck_entries = parse_deck_ids(deck_path, keep_dupes=args.keep_dupes)
    if not deck_entries:
        print("No IDs found in Deck.txt. Make sure lines look like '... = 12345678;'.")
        return

    id_to_cid = load_id_to_cid_map(data_json_path)

    pr_conn = open_cdb(pr_cdb_path)

    compiled_blocks = []
    missing_any = False

    for idx, (const_name, card_id) in enumerate(deck_entries, start=1):
        cid = id_to_cid.get(card_id)

        # Try pre-release texts row early (cheap per-card query)
        pr_texts = get_texts_row(pr_conn, card_id)

        card_json_text = None
        card_type_lower = None

        # JSON by cid (normal flow)
        if cid is not None:
            json_path = translations_dir / f"{cid}.json"
            card_json_text = safe_read_text(json_path)
            if card_json_text is None:
                missing_any = True
                print(f"[WARN] JSON not found for cid={cid} at {json_path}")
            else:
                try:
                    j = json.loads(card_json_text)
                    t = j.get("type")
                    if isinstance(t, str):
                        card_type_lower = t.lower()
                except Exception:
                    pass
        else:
            # no cid mapping
            missing_any = True
            print(f"[WARN] cid not found in data.json for id={card_id}")

        # NEW: fallback to pre-release CDB if JSON missing (or cid missing)
        if (card_json_text is None) and (pr_texts is not None):
            card_json_text = minimal_json_from_cdb(pr_texts)
            # This is expected for pre-release-only cards
            print(f"[INFO] Using pre-release CDB texts for id={card_id} (name='{pr_texts.get('name','')}')")

        # Lua search rule (no dupes / no shadowing):
        # Always prefer the normal scripts folder.
        # Only use pre-release scripts if the card script is not found in normal.
        lua_text, lua_path = find_lua_script_text(card_id, scripts_dir)

        if lua_text is None and pr_scripts_dir is not None:
            lua_text, lua_path = find_lua_script_text(card_id, pr_scripts_dir)


        # Missing Lua handling:
        # - allow suppression via flag
        # - also suppress for known "normal" monsters (from JSON only)
        lua_missing_ok = args.suppress_missing_lua or (card_type_lower == "normal")

        if lua_text is None:
            if lua_missing_ok:
                print(f"[INFO] Lua script not found for id={card_id} (suppressed)")
            else:
                missing_any = True
                searched = ", ".join([str(p) for p in (scripts_dir, pr_scripts_dir) if p is not None])
                print(f"[WARN] Lua script not found for id={card_id} under: {searched} (recursive search)")

        compiled_blocks.append(
            compile_entry_block(idx, const_name, card_id, cid, card_json_text, lua_text, lua_path)
        )

    if pr_conn is not None:
        pr_conn.close()

    header = [
        "########################################",
        "# Compiled Deck Cards",
        f"# Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"# Deck file: {deck_path}",
        f"# Translations: {translations_dir}",
        f"# data.json: {data_json_path}",
        f"# Lua scripts (root): {scripts_dir}",
        f"# Pre-release scripts (root): {pr_scripts_dir if pr_scripts_dir is not None else '(disabled)'}",
        f"# Pre-release CDB: {pr_cdb_path if pr_cdb_path is not None else '(disabled)'}",
        "########################################\n",
    ]

    out_text = "\n".join(header + compiled_blocks)
    out_path.write_text(out_text, encoding="utf-8")
    print(f"Done. Wrote {out_path.resolve()}")
    if missing_any:
        print("Some entries were missing JSON and/or Lua. See warnings above and the output file placeholders.")

if __name__ == "__main__":
    main()
