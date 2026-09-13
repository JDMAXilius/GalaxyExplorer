#!/usr/bin/env python3
"""Data parity gate for the Cosmic rework.

Loads every old ScriptableObject asset under Assets/data/{experiences,destinations,bodies,moons,layouts}
and every new one under Assets/Cosmic/Data/Generated, maps old field names onto new, and reports any value
that differs or is missing. Exits non-zero on loss. No third-party dependencies.

    python3 tools/parity/check_data.py
    python3 tools/parity/check_data.py --kinds places,bodies --skip 'hd110067.*'
"""

import argparse
import os
import re
import sys

KEY = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*:(\s|$)")
OLD_ROOT = os.path.join("Assets", "data")
NEW_ROOT = os.path.join("Assets", "Cosmic", "Data", "Generated")

# Old folders that feed each new folder.
SOURCES = {
    "places": ["experiences", "destinations"],
    "bodies": ["bodies", "moons"],
    "layouts": ["layouts"],
}

# Two differences the rework means: they are reported as notes, not losses, and only in the exact shape
# described here - anything else about the same field is still a loss.
REPLACED = {
    "ContentPrefab": "the rework builds its own place prefab, named for the place id; the old "
                     "*_content_prefab and nebula_* prefabs are the legacy tree the cutover deletes",
    "Layouts": "CS-133 added solar_schematic and solar_realistic after the old pair, which is an "
               "addition to the front-loaded list, not a replacement of it",
}

DROPPED = {
    "places": {
        "Kind": "a place is a place; the dock and the map hold their own object references",
        "SceneName": "the rework has one scene and spawns Place.content instead of loading additively",
    },
    "bodies": {
        "Role": "kept, renamed: BodyInfo had none and SystemProfile.SystemBody.Role became Body.kind",
    },
    "layouts": {},
}


# ---------- Unity YAML (the subset these assets use)

def indent_of(line):
    return len(line) - len(line.lstrip(" "))


def unquote(text):
    if len(text) >= 2 and text[0] == "'" and text[-1] == "'":
        return text[1:-1].replace("''", "'")
    if len(text) >= 2 and text[0] == '"' and text[-1] == '"':
        return text[1:-1].replace('\\"', '"').replace("\\\\", "\\")
    return text


def flow_map(text):
    out = {}
    for part in text[1:-1].split(","):
        if ":" in part:
            key, _, value = part.partition(":")
            out[key.strip()] = unquote(value.strip())
    return out


def scalar(text):
    text = text.strip()
    if text == "[]":
        return []
    if text.startswith("{") and text.endswith("}"):
        return flow_map(text)
    return unquote(text)


def parse_map(lines, i, indent):
    out = {}
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        here = indent_of(line)
        body = line.strip()
        if here < indent or (here == indent and body.startswith("- ")):
            break
        if here > indent:  # stray deeper line: skip rather than guess
            i += 1
            continue
        key, _, rest = body.partition(":")
        if rest.strip():
            out[key] = scalar(rest)
            i += 1
            continue
        child, child_indent = peek(lines, i + 1)
        if child is None or child_indent < indent:
            out[key] = ""
            i += 1
        elif child_indent == indent and child.startswith("- "):
            out[key], i = parse_list(lines, i + 1, indent)
        elif child_indent > indent:
            if child.startswith("- "):
                out[key], i = parse_list(lines, i + 1, child_indent)
            else:
                out[key], i = parse_map(lines, i + 1, child_indent)
        else:
            out[key] = ""
            i += 1
    return out, i


def parse_list(lines, i, indent):
    items = []
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if indent_of(line) != indent or not line.strip().startswith("- "):
            break
        content = line.strip()[2:]
        if KEY.match(content):
            lines[i] = " " * (indent + 2) + content
            item, i = parse_map(lines, i, indent + 2)
            items.append(item)
        else:
            items.append(scalar(content))
            i += 1
    return items, i


def peek(lines, i):
    while i < len(lines):
        if lines[i].strip():
            return lines[i].strip(), indent_of(lines[i])
        i += 1
    return None, -1


def parse_asset(path):
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        lines = handle.read().splitlines()
    for i, line in enumerate(lines):
        if line.startswith("MonoBehaviour:"):
            body, _ = parse_map(lines, i + 1, 2)
            return body
    return {}


# ---------- references

def guid_index(repo):
    index = {}
    for root, dirs, files in os.walk(os.path.join(repo, "Assets")):
        dirs[:] = [d for d in dirs if d not in (".git",)]
        for name in files:
            if not name.endswith(".meta"):
                continue
            path = os.path.join(root, name)
            try:
                with open(path, "r", encoding="utf-8", errors="replace") as handle:
                    for _ in range(4):
                        line = handle.readline()
                        if line.startswith("guid: "):
                            index[line[6:].strip()] = os.path.splitext(name[:-5])[0]
                            break
            except OSError:
                continue
    return index


def ref_name(value, guids):
    if not isinstance(value, dict):
        return "" if value in ("", None) else str(value)
    guid = value.get("guid")
    if not guid:
        return ""
    if value.get("fileID", "0") == "0" and "guid" not in value:
        return ""
    return guids.get(guid, "guid:" + guid)


# ---------- comparisons

def as_text(value):
    return "" if value is None else str(value).strip()


def same_text(old, new):
    return as_text(old) == as_text(new)


def same_number(old, new):
    try:
        return abs(float(as_text(old) or 0) - float(as_text(new) or 0)) < 1e-4
    except ValueError:
        return same_text(old, new)


def same_vector(old, new):
    if not isinstance(old, dict) or not isinstance(new, dict):
        return same_text(old, new)
    return all(same_number(old.get(axis, 0), new.get(axis, 0)) for axis in ("x", "y", "z"))


def listed(value):
    return value if isinstance(value, list) else []


def clip(text, start, width):
    piece = text[start:start + width]
    return ("..." if start else "") + piece + ("..." if start + width < len(text) else "")


def show_pair(old, new, width=70):
    a, b = as_text(old).replace("\n", " "), as_text(new).replace("\n", " ")
    if len(a) <= width and len(b) <= width:
        return a, b
    i = 0
    while i < min(len(a), len(b)) and a[i] == b[i]:
        i += 1
    start = max(0, i - 20)
    return clip(a, start, width), clip(b, start, width)


class Report:
    def __init__(self, verbose):
        self.verbose = verbose
        self.losses = []
        self.checked = 0

    def loss(self, subject, message):
        self.losses.append("%s: %s" % (subject, message))
        print("LOSS  %s: %s" % (subject, message))

    def ok(self, subject, field):
        self.checked += 1
        if self.verbose:
            print("ok    %s: %s" % (subject, field))

    def note(self, subject, message):
        self.checked += 1
        print("note  %s: %s" % (subject, message))


def compare_scalar(report, subject, field, old, new, kind):
    if kind == "number":
        equal = same_number(old, new)
    elif kind == "vector":
        equal = same_vector(old, new)
    else:
        equal = same_text(old, new)
    if equal:
        report.ok(subject, field)
    else:
        a, b = show_pair(old, new)
        report.loss(subject, "%s old=[%s] new=[%s]" % (field, a, b))


def compare_texts(report, subject, field, old, new):
    old_items, new_items = listed(old), listed(new)
    if len(old_items) != len(new_items):
        report.loss(subject, "%s count old=%d new=%d" % (field, len(old_items), len(new_items)))
        return
    for index, (a, b) in enumerate(zip(old_items, new_items)):
        compare_scalar(report, subject, "%s[%d]" % (field, index), a, b, "text")


def compare_refs(report, subject, field, old, new, guids, old_is_text=False):
    old_items = [as_text(v) if old_is_text else ref_name(v, guids) for v in listed(old)]
    new_items = [ref_name(v, guids) for v in listed(new)]
    if len(old_items) != len(new_items):
        report.loss(subject, "%s count old=%d (%s) new=%d (%s)"
                    % (field, len(old_items), ",".join(old_items), len(new_items), ",".join(new_items)))
        return
    for index, (a, b) in enumerate(zip(old_items, new_items)):
        compare_scalar(report, subject, "%s[%d]" % (field, index), a, b, "text")


def compare_rows(report, subject, field, old, new, fields, guids):
    old_rows, new_rows = listed(old), listed(new)
    if len(old_rows) != len(new_rows):
        report.loss(subject, "%s count old=%d new=%d" % (field, len(old_rows), len(new_rows)))
        return
    for index, (old_row, new_row) in enumerate(zip(old_rows, new_rows)):
        for old_key, new_key, kind in fields:
            label = "%s[%d].%s" % (field, index, old_key)
            old_value = old_row.get(old_key, "")
            new_value = new_row.get(new_key, "")
            if kind == "ref_from_id":
                compare_scalar(report, subject, label, as_text(old_value), ref_name(new_value, guids), "text")
            else:
                compare_scalar(report, subject, label, old_value, new_value, kind)


STATS = [("Label", "label", "text"), ("Value", "value", "text"),
         ("Unit", "unit", "text"), ("Exponent", "exponent", "number")]

SLOTS = [("BodyId", "body", "ref_from_id"), ("LocalPosition", "localPosition", "vector"),
         ("LocalEuler", "localEuler", "vector"), ("Scale", "scale", "number")]


def compare_place(report, subject, old, new, guids):
    for old_key, new_key, kind in [("Id", "id", "text"), ("DisplayName", "title", "text"),
                                   ("SecondLine", "subtitle", "text"), ("Environment", "room", "number")]:
        compare_scalar(report, subject, old_key, old.get(old_key, ""), new.get(new_key, ""), kind)
    place_id = as_text(new.get("id", "")) or as_text(old.get("Id", ""))
    for old_key, new_key in [("DockThumbnail", "thumbnail"), ("ContentPrefab", "content"),
                             ("Narration", "narration"), ("Ambience", "ambience")]:
        old_ref = ref_name(old.get(old_key, ""), guids)
        new_ref = ref_name(new.get(new_key, ""), guids)
        if old_key == "ContentPrefab" and place_id and new_ref == place_id and old_ref != new_ref:
            report.note(subject, "ContentPrefab old=[%s] new=[%s]: %s" % (old_ref, new_ref, REPLACED["ContentPrefab"]))
            continue
        compare_scalar(report, subject, old_key, old_ref, new_ref, "text")
    old_panel = old.get("Panel", {}) if isinstance(old.get("Panel"), dict) else {}
    new_panel = new.get("panel", {}) if isinstance(new.get("panel"), dict) else {}
    compare_scalar(report, subject, "Panel.Title", old_panel.get("Title", ""), new_panel.get("title", ""), "text")
    compare_scalar(report, subject, "Panel.Instruction",
                   old_panel.get("Instruction", ""), new_panel.get("instruction", ""), "text")
    compare_texts(report, subject, "Panel.Paragraphs", old_panel.get("Paragraphs", []),
                  new_panel.get("paragraphs", []))
    old_layouts = [ref_name(v, guids) for v in listed(old.get("Layouts", []))]
    new_layouts = [ref_name(v, guids) for v in listed(new.get("layouts", []))]
    if len(new_layouts) > len(old_layouts) and new_layouts[:len(old_layouts)] == old_layouts:
        report.note(subject, "Layouts old=%d (%s) new=%d (%s): %s"
                    % (len(old_layouts), ",".join(old_layouts), len(new_layouts), ",".join(new_layouts),
                       REPLACED["Layouts"]))
    else:
        compare_refs(report, subject, "Layouts", old.get("Layouts", []), new.get("layouts", []), guids)


def compare_body(report, subject, old, new, guids):
    for old_key, new_key in [("Id", "id"), ("DisplayName", "title"),
                             ("Subtitle", "subtitle"), ("Paragraph", "paragraph")]:
        compare_scalar(report, subject, old_key, old.get(old_key, ""), new.get(new_key, ""), "text")
    for old_key, new_key in [("Orbits", "orbits"), ("Narration", "narration"), ("Ambience", "ambience")]:
        compare_scalar(report, subject, old_key,
                       ref_name(old.get(old_key, ""), guids), ref_name(new.get(new_key, ""), guids), "text")
    compare_rows(report, subject, "Stats", old.get("Stats", []), new.get("stats", []), STATS, guids)
    compare_refs(report, subject, "Moons", old.get("Moons", []), new.get("moons", []), guids)


def compare_layout(report, subject, old, new, guids):
    for old_key, new_key, kind in [("Id", "id", "text"), ("DisplayName", "title", "text"),
                                   ("SecondLine", "subtitle", "text"),
                                   ("TransitionSeconds", "transitionSeconds", "number")]:
        compare_scalar(report, subject, old_key, old.get(old_key, ""), new.get(new_key, ""), kind)
    compare_rows(report, subject, "Slots", old.get("Slots", []), new.get("slots", []), SLOTS, guids)


COMPARE = {"places": compare_place, "bodies": compare_body, "layouts": compare_layout}


def old_assets(repo, folders, skip):
    found = {}
    for folder in folders:
        directory = os.path.join(repo, OLD_ROOT, folder)
        if not os.path.isdir(directory):
            continue
        for name in sorted(os.listdir(directory)):
            if not name.endswith(".asset"):
                continue
            asset_id = name[: -len(".asset")]
            if skip and skip.match(asset_id):
                continue
            found[asset_id] = os.path.join(directory, name)
    return found


def main():
    parser = argparse.ArgumentParser(description="Old-to-new data parity check.")
    parser.add_argument("--repo", default=os.getcwd())
    parser.add_argument("--kinds", default="places,bodies,layouts")
    parser.add_argument("--skip", default=None, help="regex of old asset ids to ignore")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    repo = os.path.abspath(args.repo)
    kinds = [k.strip() for k in args.kinds.split(",") if k.strip()]
    skip = re.compile(args.skip) if args.skip else None
    guids = guid_index(repo)
    report = Report(args.verbose)

    for kind in kinds:
        if kind not in SOURCES:
            print("unknown kind: %s" % kind)
            return 2
        olds = old_assets(repo, SOURCES[kind], skip)
        new_dir = os.path.join(repo, NEW_ROOT, kind)
        print("\n== %s: %d old asset(s) against %s" % (kind, len(olds), os.path.relpath(new_dir, repo)))
        for asset_id, old_path in olds.items():
            subject = "%s/%s" % (kind, asset_id)
            new_path = os.path.join(new_dir, asset_id + ".asset")
            if not os.path.isfile(new_path):
                report.loss(subject, "no new asset at %s" % os.path.relpath(new_path, repo))
                continue
            COMPARE[kind](report, subject, parse_asset(old_path), parse_asset(new_path), guids)
        for old_key, why in sorted(DROPPED.get(kind, {}).items()):
            print("note  %s.%s dropped: %s" % (kind, old_key, why))

    print("\n%d value(s) checked, %d loss(es)" % (report.checked, len(report.losses)))
    return 1 if report.losses else 0


if __name__ == "__main__":
    sys.exit(main())
