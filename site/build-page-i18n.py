"""Build the Russian block of page dictionaries from the pages' own markup.

The Russian text of private.html, offer.html and privacy.html is written in
the HTML (it is what a visitor without JavaScript and a payment aggregator's
checker see). This script reads every element with data-i18n="<prefix>..."
or data-i18n-tpl="<prefix>..." and rewrites only the `ru: {...}` block of the
page's js/<page>-i18n.js, so the dictionary never drifts from the page.
Translations (en, es, zh, ja, ko, vi blocks) are kept as they are.

data-i18n     -> plain text, no markup inside.
data-i18n-tpl -> a template rendered by renderTpl() in js/i18n.js:
    <span|a data-seller="x">…</span|a>  -> {x}
    <b>…</b>                            -> **…**
    <a href="/url">text</a>             -> [text](/url)
Any other tag inside is an error.

Usage: python build-page-i18n.py   (also run by update_indexes.bat)
"""
import html
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PAGES = [
    ("private.html", "js/private-i18n.js", "PRIVATE_I18N", "pv_", "страницы приватного каталога"),
    ("offer.html", "js/offer-i18n.js", "OFFER_I18N", "of_", "публичной оферты"),
    ("privacy.html", "js/privacy-i18n.js", "PRIVACY_I18N", "pp_", "политики обработки персональных данных"),
]
LANGS = ["en", "es", "zh", "ja", "ko", "vi"]

RU_BLOCK = re.compile(r"(\nru: \{\n)(.*?)(\n\},\n)", re.S)


def to_template(inner, where):
    t = re.sub(r'<(\w+) data-seller="(\w+)"[^>]*>[^<]*</\1>', r"{\2}", inner)
    t = re.sub(r"<b>([^<]*)</b>", r"**\1**", t)
    t = re.sub(r'<a href="((?:/|https://)[^"]*)">([^<]*)</a>', r"[\2](\1)", t)
    if "<" in t:
        raise SystemExit(f"{where}: unsupported markup in a template: {t}")
    return html.unescape(t)


def collect(page, prefix):
    text = (ROOT / page).read_text(encoding="utf-8")
    element = re.compile(
        r'<(?P<tag>\w+)\b[^>]*\bdata-i18n(?P<tpl>-tpl)?="(?P<key>' + re.escape(prefix)
        + r'\w+)"[^>]*>(?P<inner>.*?)</(?P=tag)>', re.S)
    keys = {}
    for m in element.finditer(text):
        key, inner = m["key"], m["inner"]
        where = f"{page}: {key}"
        if m["tpl"]:
            value = to_template(inner, where)
        else:
            if "<" in inner:
                raise SystemExit(f"{where}: data-i18n must hold plain text, use data-i18n-tpl")
            value = html.unescape(inner)
        value = " ".join(value.split())
        if key in keys and keys[key] != value:
            raise SystemExit(f"{where}: the same key has different texts on the page")
        keys[key] = value
    if not keys:
        raise SystemExit(f"{page}: no {prefix}* keys found")
    return keys


def new_file(var, title):
    blocks = ",\n".join(f"{lang}: {{}}" for lang in LANGS)
    return f"""/* ============================================================
   cthangover — тексты {title}.
   Ключи добавляются в общий словарь I18N, переключатель языков тот же.

   Блок ru собирает build-page-i18n.py из разметки страницы:
   русский текст правьте в HTML, не здесь.
   Перевод: скопируйте блок ru в объект нужного языка и переведите
   значения. Не переводите {{поля}} реквизитов, адреса в [текст](/адрес)
   и звёздочки **жирного**; их можно переставлять внутри фразы.
   Пока в языке нет хотя бы одного ключа, страница показывает плашку
   и русский текст.
   ============================================================ */
var {var} = {{
ru: {{
  _: ""
}},
{blocks}
}};

(function () {{
  for (var l in {var}) {{
    if (!I18N[l]) continue;
    for (var k in {var}[l]) I18N[l][k] = {var}[l][k];
  }}
}})();
"""


def main():
    for page, js_path, var, prefix, title in PAGES:
        keys = collect(page, prefix)
        path = ROOT / js_path
        source = path.read_text(encoding="utf-8") if path.exists() else new_file(var, title)
        body = ",\n".join(f"  {k}: {json.dumps(v, ensure_ascii=False)}" for k, v in keys.items())
        if not RU_BLOCK.search(source):
            raise SystemExit(f"{js_path}: no `ru: {{ ... }},` block")
        result = RU_BLOCK.sub(lambda m: m[1] + body + m[3], source, count=1)
        if result != source:
            with path.open("w", encoding="utf-8", newline="\n") as f:
                f.write(result)
        stale = sorted(set(re.findall(r"\b(" + re.escape(prefix) + r"\w+):", result)) - set(keys))
        note = f", keys in translations but not on the page: {', '.join(stale)}" if stale else ""
        print(f"OK  {js_path}  ({len(keys)} keys from {page}{note})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
