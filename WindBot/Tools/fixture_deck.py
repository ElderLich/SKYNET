"""Read YDK decks and write fixed-order native duel fixtures."""


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
    lines = ["#created by WindBot native scenario", "#main"]
    lines.extend(map(str, main))
    lines.append("#extra")
    lines.extend(map(str, extra))
    lines.append("!side")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
