#!/usr/bin/env python3
"""
Keep the docs site (docs/) in sync with the C# components.

For every component in Components/*.cs this script:
  * reads Name, NickName, Description, SubCategory and the Register*Params calls
  * creates docs/components/<slug>.html if the component has no page yet
  * rewrites only the generated parts of an existing page (header + Inputs/Outputs grid),
    leaving any hand-written sections (workflow, physics notes, images) untouched
  * writes the "Method, assumptions and simplifications" section of pages whose component has an
    entry in Utils/MethodNotes.cs (the same text the component shows in its Info output),
    between <!-- method-notes:start --> and <!-- method-notes:end -->
  * adds a card to docs/index.html for components that have none, and refreshes
    the input/output counts and description on existing cards

Usage:
  python3 dev_scripts/gen_docs.py           # write changes
  python3 dev_scripts/gen_docs.py --check   # exit 1 if docs are out of date (used by CI)
"""
import glob
import html
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COMP_DIR = os.path.join(ROOT, "Components")
DOCS = os.path.join(ROOT, "docs")
PAGES = os.path.join(DOCS, "components")
INDEX = os.path.join(DOCS, "index.html")

STR = r'"((?:[^"\\]|\\.)*)"'
TYPE_NAMES = {
    "Point": "Point", "Vector": "Vector", "Number": "Number", "Integer": "Integer",
    "Boolean": "Boolean", "Text": "Text", "Colour": "Colour", "Curve": "Curve",
    "Mesh": "Mesh", "Brep": "Brep", "Surface": "Surface", "Geometry": "Geometry",
    "Generic": "Generic", "Plane": "Plane", "Line": "Line", "Interval": "Interval",
    "Rectangle": "Rectangle", "Box": "Box", "Circle": "Circle", "Arc": "Arc",
    "Transform": "Transform", "Matrix": "Matrix", "Path": "Path", "Group": "Group",
    "FilePath": "File Path", "Time": "Time", "Field": "Field", "Culture": "Culture",
    "Interval2D": "Interval 2D", "ComplexNumber": "Complex", "MeshFace": "Mesh Face",
}


def unescape(s):
    return s.replace('\\"', '"').replace("\\n", " ").replace("\\\\", "\\")


def slugify(name):
    # Matches the existing site convention (e.g. "Adapter: The Sculptor" -> "adapter:-the-sculptor")
    return re.sub(r"\s+", "-", name.strip().lower()).replace("/", "-")


def category_slug(sub):
    return re.sub(r"[^a-z0-9]+", "-", sub.lower()).strip("-")


def parse_params(block):
    out = []
    for m in re.finditer(r"pManager\.Add(\w+?)Parameter\(\s*" + STR + r"\s*,\s*" + STR + r"\s*,\s*" + STR, block):
        kind, name, nick, desc = m.groups()
        out.append((unescape(name), TYPE_NAMES.get(kind, kind), unescape(desc)))
    return out


def parse_component(path):
    s = open(path, encoding="utf-8-sig", errors="ignore").read()
    m = re.search(r"base\(\s*" + STR + r"\s*,\s*" + STR + r"\s*,\s*" + STR + r"\s*,\s*" + STR + r"\s*,\s*" + STR + r"\s*\)", s)
    if m:
        name, nick, desc, cat, sub = (unescape(x) for x in m.groups())
    else:
        # Special objects (value lists, toggles) set properties in the constructor
        g = lambda k: (re.search(r"this\." + k + r"\s*=\s*" + STR, s) or [None, None])[1]
        name, nick, desc, cat, sub = g("Name"), g("NickName"), g("Description"), g("Category"), g("SubCategory")
        if not name:
            return None
        name, nick, desc, sub = unescape(name), unescape(nick or ""), unescape(desc or ""), unescape(sub or "")
    if "GH_Exposure.hidden" in s:
        return None
    def section(fn):
        i = s.find(fn)
        if i < 0:
            return ""
        j = s.find("protected override", i + len(fn))
        return s[i:j if j > 0 else len(s)]
    return {
        "file": os.path.basename(path),
        "name": name, "nick": nick, "desc": desc, "sub": sub,
        "inputs": parse_params(section("RegisterInputParams")),
        "outputs": parse_params(section("RegisterOutputParams")),
    }


def io_grid(c, indent="            "):
    def col(title, items):
        lis = "\n".join(
            f'{indent}            <li><strong>{html.escape(n, quote=False)}</strong> ({t}): {html.escape(d, quote=False)}</li>' for n, t, d in items
        ) or f"{indent}            <li><em>None</em></li>"
        return (f'{indent}    <div class="io-col">\n'
                f'{indent}        <h3>{title} ({len(items)})</h3>\n'
                f'{indent}        <ul>\n{lis}\n'
                f'{indent}        </ul>\n'
                f'{indent}    </div>')
    return (f'{indent}<div class="io-grid">\n'
            f'{col("Inputs", c["inputs"])}\n{col("Outputs", c["outputs"])}\n'
            f'{indent}</div>')


def header(c):
    return (f'<h1>{html.escape(c["name"], quote=False)}</h1>\n'
            f'                <p class="doc-subtitle">{html.escape(c["desc"], quote=False)}</p>\n'
            f'                <div class="doc-meta">\n'
            f'                    <span class="badge {category_slug(c["sub"])}">{html.escape(c["sub"], quote=False)}</span>\n'
            f'                    <span>Nickname: <strong>{html.escape(c["nick"], quote=False)}</strong></span>\n'
            f'                </div>')


HEADER_RE = re.compile(r'<h1>.*?</h1>\s*<p class="doc-subtitle">.*?</p>\s*<div class="doc-meta">.*?</div>', re.S)
IO_RE = re.compile(r'[ \t]*<div class="io-grid">.*?</ul>\s*</div>\s*<div class="io-col">.*?</ul>\s*</div>\s*</div>', re.S)


def render_new_page(c, template):
    page = HEADER_RE.sub(lambda _: header(c), template, count=1)
    page = re.sub(r"<title>.*?</title>", f"<title>{html.escape(c['name'], quote=False)} - Enzyme Plugin</title>", page, count=1)
    body = (f'        <div class="doc-content">\n'
            f'            <h2>Overview</h2>\n'
            f'            <p>{html.escape(c["desc"], quote=False)}</p>\n\n'
            f'{io_grid(c)}\n'
            f'        </div>\n    </main>')
    return re.sub(r'        <div class="doc-content">.*?</main>', lambda _: body, page, count=1, flags=re.S)


def update_page(text, c):
    text = HEADER_RE.sub(lambda _: header(c), text, count=1)
    text = re.sub(r"<title>.*?</title>", f"<title>{html.escape(c['name'], quote=False)} - Enzyme Plugin</title>", text, count=1)
    m = IO_RE.search(text)
    if m:
        # Hand-written descriptions are kept as long as the port names match the code.
        # The grid is regenerated only when ports were added, removed, renamed or reordered.
        cols = re.findall(r"<h3>(?:Inputs|Outputs) \(\d+\)</h3>\s*<ul>(.*?)</ul>", m.group(0), re.S)
        names = [[html.unescape(n) for n in re.findall(r"<li><strong>(.*?)</strong>", col)] for col in cols]
        want = [[n for n, _, _ in c["inputs"]], [n for n, _, _ in c["outputs"]]]
        if names != want:
            text = text[:m.start()] + io_grid(c) + text[m.end():]
    return text


NOTES_FILE = os.path.join(ROOT, "Utils", "MethodNotes.cs")
NOTE_SECTIONS = [("Method", "Method", True), ("Assumptions", "Assumptions", False),
                 ("Simplifications", "Simplifications and limits", False),
                 ("Workflow", "Workflow", False), ("References", "References", False)]
MN_START, MN_END = "<!-- method-notes:start -->", "<!-- method-notes:end -->"
MN_RE = re.compile(r"(?:\n\n)?[ \t]*" + re.escape(MN_START) + r".*?" + re.escape(MN_END), re.S)


def parse_notes():
    """Utils/MethodNotes.cs -> {component file stem: {field: [lines]}}."""
    if not os.path.exists(NOTES_FILE):
        return {}
    src = open(NOTES_FILE, encoding="utf-8").read()
    notes = {}
    for m in re.finditer(r'\["(\w+)"\]\s*=\s*new Note\s*\{(.*?)\n            \},', src, re.S):
        key, body = m.groups()
        note = {}
        t = re.search(r"Title\s*=\s*" + STR, body)
        note["Title"] = unescape(t.group(1)) if t else key
        for f in re.finditer(r"(\w+)\s*=\s*new\[\]\s*\{(.*?)\n\s*\},", body, re.S):
            note[f.group(1)] = [unescape(x) for x in re.findall(STR, f.group(2))]
        notes[key] = note
    return notes


def notes_block(note, indent="            "):
    cards = []
    for field, title, ordered in NOTE_SECTIONS:
        lines = note.get(field) or []
        if not lines:
            continue
        tag = "ol" if ordered else "ul"
        items = "\n".join(f"{indent}            <li>{html.escape(l, quote=False)}</li>" for l in lines)
        cards.append(
            f'{indent}    <div style="background: var(--bg-card); border: 1px solid var(--border-color); border-radius: 8px; padding: 1.25rem; margin-bottom: 1rem;">\n'
            f'{indent}        <h3 style="color: var(--accent-teal); margin-bottom: 0.5rem;">{title}</h3>\n'
            f'{indent}        <{tag} style="padding-left: 1.5rem; line-height: 1.8; font-size: 0.95rem;">\n{items}\n{indent}        </{tag}>\n'
            f'{indent}    </div>')
    return (f"{indent}{MN_START}\n"
            f'{indent}<h2 style="margin-top: 2rem;">Method, assumptions and simplifications</h2>\n'
            f'{indent}<p style="color: var(--text-secondary);">The same notes appear in the component\'s <strong>Info</strong> output.</p>\n'
            f'{indent}<div>\n' + "\n".join(cards) + f"\n{indent}</div>\n"
            f"{indent}{MN_END}\n")


def apply_notes(text, note):
    text = MN_RE.sub("", text)
    if not note:
        return text
    m = IO_RE.search(text)
    if not m:
        return text
    return text[:m.end()] + "\n\n" + notes_block(note).rstrip("\n") + text[m.end():]


def card(c, page):
    cs = category_slug(c["sub"])
    return (f'            <a href="components/{page}" class="card" data-category="{cs}">\n'
            f'                <span class="badge {cs}">{html.escape(c["sub"], quote=False)}</span>\n'
            f'                <h3>{html.escape(c["name"], quote=False)}</h3>\n'
            f'                <p>{html.escape(c["desc"], quote=False)}</p>\n'
            f'                <div class="card-meta">\n'
            f'                    <span>{len(c["inputs"])} Inputs</span>\n'
            f'                    <span>{len(c["outputs"])} Outputs</span>\n'
            f'                </div>\n'
            f'                <span class="card-link">View docs &rarr;</span>\n'
            f'            </a>')


CARD_RE = re.compile(r'[ \t]*<a href="components/([^"]+)" class="card"[^>]*>.*?</a>', re.S)


def main(check=False):
    comps = [c for c in (parse_component(p) for p in sorted(glob.glob(os.path.join(COMP_DIR, "*.cs")))) if c]
    index = open(INDEX, encoding="utf-8").read()
    original_index = index

    # Existing card -> page mapping, keyed by card title
    cards = {}
    for m in CARD_RE.finditer(index):
        t = re.search(r"<h3>(.*?)</h3>", m.group(0))
        if t:
            cards[html.unescape(t.group(1))] = m.group(1)

    template = open(os.path.join(PAGES, "sun-hours-analysis.html"), encoding="utf-8").read()
    template = MN_RE.sub("", template)
    notes = parse_notes()
    changed, warnings = [], []
    linked = set()

    for c in comps:
        page = cards.get(c["name"]) or slugify(c["name"]) + ".html"
        linked.add(page)
        path = os.path.join(PAGES, page)
        if not c["inputs"] and not c["outputs"] and not c["name"]:
            continue
        if os.path.exists(path):
            old = open(path, encoding="utf-8").read()
            new = update_page(old, c)
            if not IO_RE.search(old):
                warnings.append(f"{page}: no io-grid block found; header refreshed only")
        else:
            old, new = None, render_new_page(c, template)
        new = apply_notes(new, notes.get(os.path.splitext(c["file"])[0]))
        if new != old:
            changed.append(f"docs/components/{page}")
            if not check:
                open(path, "w", encoding="utf-8").write(new)

        new_card = card(c, page)
        if c["name"] in cards:
            index = re.sub(
                r'[ \t]*<a href="components/' + re.escape(page) + r'" class="card"[^>]*>.*?</a>',
                lambda _: new_card, index, count=1, flags=re.S)
        else:
            last = list(CARD_RE.finditer(index))[-1]
            index = index[:last.end()] + "\n" + new_card + index[last.end():]

    orphans = sorted(set(os.listdir(PAGES)) - linked)
    if index != original_index:
        changed.append("docs/index.html")
        if not check:
            open(INDEX, "w", encoding="utf-8").write(index)

    for w in warnings:
        print("warning:", w)
    for o in orphans:
        print("orphan page (no component):", o)
    if changed:
        print(("out of date: " if check else "updated: ") + ", ".join(changed))
    else:
        print(f"docs in sync ({len(comps)} components)")
    if check and (changed or orphans):
        print("Run: python3 dev_scripts/gen_docs.py")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(check="--check" in sys.argv))
