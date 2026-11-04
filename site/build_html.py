"""Refresh generated HTML values without requiring JavaScript at page load."""
import hashlib
import html
from pathlib import Path
import re


def update_html(path, values, scripts=()):
    """Replace unique text-only elements by id and version local scripts by content."""
    path = Path(path)
    with path.open(encoding="utf-8", newline="") as source:
        text = source.read()
    original = text
    for element_id, value in values.items():
        pattern = re.compile(r'(<(?P<tag>\w+)\b[^>]*\bid="' + re.escape(element_id)
                             + r'"[^>]*>)[^<]*(</(?P=tag)>)')
        text, count = pattern.subn(lambda m: m[1] + html.escape(str(value)) + m[3], text)
        if count != 1:
            raise ValueError(f"{path}: expected one element #{element_id}, found {count}")
    for script in scripts:
        version = hashlib.sha256((path.parent / script).read_bytes()).hexdigest()[:12]
        pattern = re.compile(r'(<script\b[^>]*\bsrc="/' + re.escape(script)
                             + r')(?:\?[^\"]*)?(\")')
        text, count = pattern.subn(lambda m: m[1] + '?v=' + version + m[2], text)
        if count != 1:
            raise ValueError(f"{path}: expected one script {script}, found {count}")
    if text != original:
        with path.open("w", encoding="utf-8", newline="") as target:
            target.write(text)
