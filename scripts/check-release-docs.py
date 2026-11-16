"""Check GitHub-relative documentation links, case-sensitive paths and static SVGs.

--screenshots-from can verify pending UI-owned images in a separate checkout without
copying them; those paths are still reported as pending integration.
"""
import argparse
import os
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DOCS = [ROOT / name for name in ("README.md", "CHANGELOG.md", "docs/SETTINGS.md", "docs/perf/README.md")]


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.urls = []

    def handle_starttag(self, tag, attrs):
        self.urls.extend(value for key, value in attrs if key in ("href", "src") and value)


def exact_file(path):
    # resolve() on Windows can repair the case, hiding links that fail on GitHub.
    path = Path(os.path.normpath(str(path if path.is_absolute() else Path.cwd() / path)))
    current = Path(path.anchor)
    for part in path.parts[1:]:
        if not current.is_dir() or part not in {item.name for item in current.iterdir()}:
            return False
        current /= part
    return current.is_file()


def anchors(path):
    counts, result = Counter(), set()
    for heading in re.findall(r"^#{1,6}\s+(.+?)\s*#*\s*$", path.read_text(encoding="utf-8"), re.M):
        slug = re.sub(r"[^\w\- ]", "", heading.lower()).replace(" ", "-")
        suffix = "" if counts[slug] == 0 else f"-{counts[slug]}"
        result.add(slug + suffix); counts[slug] += 1
    return result


def check(screenshots):
    failures, pending, checked = [], set(), set()
    for document in DOCS:
        text = document.read_text(encoding="utf-8")
        body = re.sub(r"```.*?```", "", text, flags=re.S)
        html = Links(); html.feed(body)
        urls = html.urls + re.findall(r"!?\[[^\]]*\]\(([^\s)]+)\)", body)
        for url in urls:
            parsed = urlsplit(url)
            if parsed.scheme or parsed.netloc:
                continue
            target = Path(os.path.normpath(str(document.parent / unquote(parsed.path)))) if parsed.path else document
            checked.add(str(target))
            if not exact_file(target):
                relative = target.relative_to(ROOT).as_posix() if target.is_relative_to(ROOT) else str(target)
                if screenshots and relative.startswith("docs/images/") and exact_file(screenshots / target.name):
                    with (screenshots / target.name).open("rb") as source:
                        if source.read(8) != b"\x89PNG\r\n\x1a\n": failures.append(f"Invalid PNG: {target.name}")
                    pending.add(relative)
                else:
                    failures.append(f"{document.relative_to(ROOT)}: missing or wrong-case path {parsed.path}")
            elif parsed.fragment and target.suffix == ".md" and unquote(parsed.fragment) not in anchors(target):
                failures.append(f"{document.relative_to(ROOT)}: missing anchor {url}")
    for path in [ROOT / "readme-banner.svg", *sorted((ROOT / "docs/perf").glob("*.svg"))]:
        svg = ET.parse(path)
        for element in svg.iter():
            if element.tag.split("}")[-1] in {"script", "filter", "linearGradient", "radialGradient", "foreignObject", "image"}:
                failures.append(f"{path.name}: unsupported active/decorative SVG element {element.tag}")
            for key, value in element.attrib.items():
                if key.split("}")[-1] == "href" and not value.startswith("#"):
                    failures.append(f"{path.name}: external SVG resource {value}")
    if re.search(r"\b[0-9a-fA-F]{64}\b", (ROOT / "README.md").read_text(encoding="utf-8")):
        failures.append("README contains a concrete checksum instead of the requested release TBD")
    for path in sorted(pending): print(f"PENDING: {path} (PNG verified in UI checkout; not copied)")
    for failure in failures: print("FAIL:", failure)
    print(f"Checked {len(checked)} unique local targets and static SVG structure; {len(pending)} UI paths pending integration.")
    return 1 if failures else 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--screenshots-from", type=Path)
    raise SystemExit(check(parser.parse_args().screenshots_from))
