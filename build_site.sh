#!/usr/bin/env bash
# Builds the GitHub Pages copy of the site: site/ -> site_github/.
#
# One source of truth: edit site/ (the RU site, served by nginx at the domain root)
# and re-run this script; site_github/ is generated output and must not be edited.
#
#   build_site.sh                    build and publish; GitHub defaults: url https://cthangover.github.io/,
#                                    repo https://github.com/cthangover
#   build_site.sh --url=<url>        another public address (e.g. a custom domain)
#   build_site.sh --repo=<url>       repository base for source links (default https://github.com/cthangover)
#   build_site.sh --out=<dir>        output folder (default site_github)
#   build_site.sh --no-push          build only, do not commit/push to cthangover.github.io
#   build_site.sh --push-url=<url>   repository to push to (default https://github.com/cthangover/cthangover.github.io.git)
#
# What changes for GitHub Pages (nginx serves site/ at the domain root):
# - root-absolute paths (/css/..., /js/..., fetch("/data/mods.json"), "/img/shots/") become relative, so the
#   site also works under <user>.github.io[/<repo>];
# - .nojekyll: without it Jekyll turns docs/**/*.md into html and parses {{ }} / {% %} in them;
# - 404.html: nginx answers old /docs/<slug> and /quickstart/<lang>/<page> addresses with index.html, whose
#   script redirects to docs.html#/docs/... ; Pages has no such fallback, its 404 page does that instead;
# - sitemap.xml keeps only real files (the /docs/... routes would be 404 on Pages);
# - canonical/og/robots point at --url, repository links at --repo (the GitHub copy must not send visitors
#   to git.cthangover.ru: the RU infrastructure is not reachable from outside the RF segment);
# - build scripts, local server, bat/ps1 and __pycache__ are left out.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$HERE/site_github"
URL="https://cthangover.github.io/"
REPO="https://github.com/cthangover"
PUSH=1
PUSH_URL="https://github.com/cthangover/cthangover.github.io.git"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --url=*) URL=$(printf '%s' "$1" | cut -d= -f2-) ;;
    --repo=*) REPO=$(printf '%s' "$1" | cut -d= -f2-) ;;
    --out=*) OUT=$(printf '%s' "$1" | cut -d= -f2-) ;;
    --push-url=*) PUSH_URL=$(printf '%s' "$1" | cut -d= -f2-) ;;
    --push) PUSH=1 ;;
    --no-push) PUSH=0 ;;
    *) sed -n '2,18p' "$0"; exit 2 ;;
  esac
  shift
done
URL=$(printf '%s' "$URL" | sed 's#/*$#/#')
case "$OUT" in
  /*) ;;
  *) OUT="$HERE/$OUT" ;;
esac

rm -rf "$OUT"
mkdir -p "$OUT"

# Everything is generated except the site sources that are not web content.
(cd "$HERE/site" && find . -type f \
  ! -name '*.py' ! -name '*.pyc' ! -name '*.bat' ! -name '*.ps1' \
  ! -name '.gdignore' ! -name '.gitkeep' ! -name 'git.sitemap.xml' \
  ! -path '*/__pycache__/*' -print0) |
  (cd "$HERE/site" && xargs -0 cp --parents -t "$OUT")

# Root-absolute -> relative. HTML attributes: "/" -> "./", "/x" -> "x" (protocol-relative "//" untouched).
for f in "$OUT"/*.html; do
  sed -i -E 's#(href|src)="/"#\1="./"#g; s#(href|src)="/([^/"])#\1="\2#g' "$f"
done
# JS string literals that start with a site folder or page ("/docs.html#...", "/img/shots/", "/data/mods.json").
for f in "$OUT"/js/*.js; do
  sed -i -E 's#(["'"'"'])/(docs|quickstart|img|data|css|js|examples)([/.])#\1\2\3#g' "$f"
done

# Public address: canonical/og links of the pages and docs.js, robots.txt.
if [ "$URL" != "https://cthangover.ru/" ]; then
  sed -i "s#https://cthangover.ru/#$URL#g" "$OUT"/*.html "$OUT"/js/*.js "$OUT"/robots.txt
fi

# Repository links: never point a GitHub Pages visitor at the Gitea server.
if [ -n "$REPO" ]; then
  find "$OUT" -type f \( -name "*.html" -o -name "*.js" -o -name "*.json" -o -name "*.xml" -o -name "*.txt" -o -name "*.md" \) -print0 |
    while IFS= read -r -d "" f; do
      sed -i -e "s|https://git.cthangover.ru/cthangover/cthangover/src/branch/master/site/docs|https://github.com/cthangover/cthangover/tree/master/site/docs|g" -e "s|https://git.cthangover.ru/cthangover/cthangover/src/branch/master/LICENSE|https://github.com/cthangover/cthangover/blob/master/LICENSE|g" -e "s|https://git.cthangover.ru/cthangover/mod_live2d/raw/branch/master/|https://raw.githubusercontent.com/cthangover/mod_live2d/master/|g" -e "s|https://git.cthangover.ru/cthangover|$REPO|g" "$f"
    done
fi

# Sitemap: the real pages only.
{
  echo '<?xml version="1.0" encoding="UTF-8"?>'
  echo '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">'
  for p in "" docs.html quickstart.html; do
    echo "  <url><loc>$URL$p</loc></url>"
  done
  echo '</urlset>'
} > "$OUT/sitemap.xml"

# 404 page: old path addresses -> hash routes (the base is whatever precedes /docs or /quickstart, so it works
# under /<repo>/ too); anything else gets a link home.
cat > "$OUT/404.html" <<'EOF'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex">
<title>cthangover - page not found</title>
<script>
(function () {
  var p = location.pathname;
  var m = p.match(/^(.*?)\/(docs|quickstart)(?:\/(.*?))?\/?$/i);
  if (!m) return;
  var base = m[1] + "/";
  location.replace(m[2].toLowerCase() === "docs"
    ? base + "docs.html#/docs/" + (m[3] || "index")
    : base + "quickstart.html" + (m[3] ? "#/quickstart/" + m[3] : ""));
})();
</script>
<style>
  body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #0e0f15; color: #e8e5de;
         font: 16px/1.5 system-ui, sans-serif; }
  a { color: #d9a441; }
</style>
</head>
<body>
<main><h1>404</h1><p>Page not found. <a id="home" href="./">Go to the home page</a></p></main>
<script>
  /* on <user>.github.io/<repo>/ the home page is /<repo>/ */
  if (/\.github\.io$/i.test(location.hostname)) {
    var seg = location.pathname.split("/")[1];
    if (seg && !/\./.test(seg)) document.getElementById("home").href = "/" + seg + "/";
  }
</script>
</body>
</html>
EOF

touch "$OUT/.nojekyll"

cat > "$OUT/README.md" <<'EOF'
# cthangover site - GitHub Pages build

Generated from the site/ folder by build_site.sh; do not edit the files here, edit site/ and rebuild.

Publish: push the contents of this folder as the root of the repository (or of a gh-pages branch), then
Settings -> Pages -> Deploy from a branch -> / (root).
EOF

if [ "$PUSH" -eq 1 ]; then
  TMP=$(mktemp -d)
  git clone --quiet "$PUSH_URL" "$TMP/repo"
  find "$TMP/repo" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
  (cd "$OUT" && tar -cf - .) | (cd "$TMP/repo" && tar -xf -)
  # Windows files come from /mnt/c with 777 permissions; normalize so commits do not churn modes.
  find "$TMP/repo" -type f -exec chmod 644 {} +
  (
    cd "$TMP/repo"
    git add -A
    if git diff --cached --quiet; then
      echo "site: no changes to publish"
    else
      git -c user.name=iisaev -c user.email=vpcoder@ya.ru commit -m "Site: publish GitHub Pages build"
      git push origin HEAD
      echo "site: published"
    fi
  )
  rm -rf "$TMP"
fi

echo "built $OUT ($(find "$OUT" -type f | wc -l) files, url $URL, repo $REPO)"