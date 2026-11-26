"""Project the normal WindBot's compiled Game sources into the MDPro4 player."""

from pathlib import Path, PurePosixPath
import hashlib
import re
import uuid
import xml.etree.ElementTree as ET


def _session_entrypoint(path, raw):
    # The embedded launcher owns locale/expansion databases, Solo startup and file lookup.
    # Only its worker body belongs to the portable GameClient session lifecycle.
    text = raw.decode("utf-8")
    method = re.search(
        r"^(?P<indent>[ \t]*)private static void Run\(object (?P<arg>\w+)\)\s*\{"
        r"(?P<body>.*?)^(?P=indent)\}", text, re.MULTILINE | re.DOTALL,
    )
    if method is None:
        raise ValueError(f"Cannot locate the WindBot session worker in {path}")
    if re.search(r"^\s*GameClient\.RunSession\(", method["body"], re.MULTILINE):
        return raw
    # This is a one-time migration of MDPro4's original worker. Preserve a separately
    # customized worker until its owner has routed it through the common session.
    original = method.group().replace("\r\n", "\n").encode("utf-8")
    if hashlib.sha256(original).hexdigest() != "2772e6d74ec2da968015ded7d0112f2e2c1bc4d8792b77aae105b847d426acca":
        raise ValueError(f"Custom session worker in {path}. Keep its integration logic and call "
                         "GameClient.RunSession((WindBotInfo)o) for the duel loop.")
    indent = method["indent"]
    eol = "\r\n" if "\r\n" in method.group() else "\n"
    body = eol + indent + f"    GameClient.RunSession((WindBotInfo){method['arg']});" + eol
    return (text[:method.start("body")] + body + text[method.end("body"):]).encode("utf-8")


def _game_sources(windbot):
    project = ET.parse(windbot / "WindBot.csproj")
    sources = set()
    for item in project.iterfind(".//{*}Compile"):
        pattern = PurePosixPath(item.get("Include", "").replace("\\", "/"))
        if pattern.parts[:1] != ("Game",):
            continue
        if ".." in pattern.parts or pattern.suffix != ".cs":
            raise ValueError(f"Unexpected WindBot Game compile item: {pattern}")
        matches = list(windbot.glob(pattern.as_posix()))
        if not matches:
            raise FileNotFoundError(f"Cannot find WindBot compiler input: {pattern}")
        sources.update(p.relative_to(windbot) for p in matches if p.is_file())
    if not sources:
        raise ValueError(f"No Game sources included by {windbot / 'WindBot.csproj'}")
    return sorted(sources)


def _meta(relative, folder):
    # Stable GUIDs let two fresh targets import the same asset. Existing GUIDs stay game-owned.
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "https://github.com/ElderLich/SKYNET/WindBot/" + relative.as_posix()).hex
    importer = ("folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" if folder else
                "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
                "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n")
    return (f"fileFormatVersion: 2\nguid: {guid}\n{importer}"
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("ascii")


def plan_unity_sources(windbot: Path, unity_windbot: Path, originals=None, former_paths=None):
    """Yield (absolute target, bytes) for compiled C#, its bridge and missing Unity metadata."""
    relatives = _game_sources(windbot) + [Path("WindBotInfo.cs"), Path("Program.cs")]
    folders = set()
    for relative in relatives:
        target = unity_windbot / relative
        existing = target.read_bytes() if target.is_file() else None
        if originals is not None:
            originals[target] = existing
        if relative == Path("Program.cs"):
            if existing is None:
                raise FileNotFoundError(f"Embedded MDPro4 launcher not found: {target}")
            contents = _session_entrypoint(target, existing)
        else:
            contents = (windbot / relative).read_bytes()
            if existing is not None and existing != contents:
                # Unity and standalone checkouts can use different BOM/line-ending conventions.
                if (existing.decode("utf-8-sig").replace("\r\n", "\n")
                        == contents.decode("utf-8-sig").replace("\r\n", "\n")):
                    contents = existing
        yield target, contents
        meta = target.with_name(target.name + ".meta")
        if not meta.exists():
            if originals is not None:
                originals[meta] = None
            former = (former_paths or {}).get(relative, relative)
            former_bytes = None
            if former != relative:
                former_meta = (unity_windbot / former).with_suffix(".cs.meta")
                try:
                    former_bytes = former_meta.read_bytes()
                except FileNotFoundError:
                    pass
                if originals is not None:
                    originals[former_meta] = former_bytes
            yield meta, former_bytes if former_bytes is not None else _meta(former, False)
        folders.update(p for p in relative.parents if p != Path("."))
    for relative in sorted(folders):
        target = unity_windbot / relative
        meta = target.with_name(target.name + ".meta")
        if not meta.exists():
            if originals is not None:
                originals[meta] = None
            yield meta, _meta(relative, True)
