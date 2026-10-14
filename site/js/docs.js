/* ============================================================
   cthangover — страница документации.
   Контент: docs/docs-index.json (собирается build-docs-index.py
   из docs/** (md). Маршруты: #/docs/<slug>, ?p=<slug>,
   поддерживаются старые адреса /docs/<slug> (через редирект
   на главной). Поиск — полнотекстовый по JSON-индексу.
   ============================================================ */
(function () {
  "use strict";

  var INDEX_URL = "/docs/docs-index.json";
  var GIT_BASE = "https://git.cthangover.ru/cthangover/cthangover/src/branch/master/site/docs/";

  var container = document.getElementById("docs-content");
  if (!container) return;

  var treeEl = document.getElementById("docsTree");
  var tocEl = document.getElementById("tocBody");
  var searchInput = document.getElementById("docsSearch");
  var searchClear = document.getElementById("searchClear");
  var crumbsEl = document.getElementById("crumbs");
  var metaEl = document.getElementById("docMeta");
  var pagerPrev = document.getElementById("pagerPrev");
  var pagerNext = document.getElementById("pagerNext");
  var pagerPrevT = document.getElementById("pagerPrevT");
  var pagerNextT = document.getElementById("pagerNextT");

  var canonicalLink = document.querySelector('link[rel="canonical"]');
  var defaultTitle = document.title;

  var docsIndex = {};
  var docsTree = [];
  var flat = [];            /* [{slug,title}] в порядке дерева — для prev/next */
  var loaded = false;
  var currentPage = null;
  var searchTimer = null;

  function esc(s) {
    var d = document.createElement("div");
    d.appendChild(document.createTextNode(String(s)));
    return d.innerHTML;
  }

  function t(k){ var d = I18N[lang] || I18N.en; return d[k] || I18N.en[k] || k; }

  /* ---------------- загрузка индекса ---------------- */
  function loadIndex(callback) {
    if (loaded) { callback(); return; }
    fetch(INDEX_URL)
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (data) {
        docsIndex = data;
        docsTree = data._tree || [];
        buildFlat(docsTree);
        loaded = true;
        renderTree(currentFilter());
        callback();
      })
      .catch(function () {
        if (location.protocol === "file:") {
          tryXhr(INDEX_URL, function (text) {
            try {
              docsIndex = JSON.parse(text);
              docsTree = docsIndex._tree || [];
              buildFlat(docsTree);
              loaded = true;
              renderTree(currentFilter());
            } catch (e) {
              docsIndex = {};
              loaded = true;
            }
            callback();
          }, callback);
          return;
        }
        container.innerHTML =
          '<div class="docs-error">Failed to load ' + esc(INDEX_URL) + "</div>";
        loaded = true;
        callback();
      });
  }

  function tryXhr(url, ok, fail) {
    try {
      var xhr = new XMLHttpRequest();
      xhr.open("GET", url, true);
      xhr.onload = function () {
        if (xhr.status === 0 || xhr.status === 200) ok(xhr.responseText);
        else fail();
      };
      xhr.onerror = function () { fail(); };
      xhr.send();
    } catch (e) { fail(); }
  }

  /* ---------------- дерево / плоский список ---------------- */
  function buildFlat(nodes) {
    flat = [];
    (function walk(list) {
      for (var i = 0; i < list.length; i++) {
        var n = list[i];
        if (n.slug) flat.push({ slug: n.slug, title: n.title });
        if (n.children) walk(n.children);
      }
    })(nodes);
  }

  function titleOf(slug) {
    for (var i = 0; i < flat.length; i++) {
      if (flat[i].slug === slug) return flat[i].title;
    }
    var parts = slug.split("/");
    return parts[parts.length - 1];
  }

  function matchText(text, query) {
    var lower = text.toLowerCase();
    var words = query.toLowerCase().split(/\s+/);
    for (var i = 0; i < words.length; i++) {
      if (words[i] && lower.indexOf(words[i]) === -1) return false;
    }
    return true;
  }

  function filterTree(nodes, query) {
    var result = [];
    for (var i = 0; i < nodes.length; i++) {
      var n = nodes[i];
      var self = n.slug ? matchText(docsIndex[n.slug] || "", query) : false;
      var kids = n.children ? filterTree(n.children, query) : [];
      if (self || kids.length) {
        var copy = { title: n.title, slug: n.slug };
        if (kids.length) copy.children = kids;
        result.push(copy);
      }
    }
    return result;
  }

  function renderTree(query) {
    if (!treeEl) return;
    var nodes = docsTree;
    if (query) nodes = filterTree(docsTree, query);

    if (!nodes.length) {
      treeEl.innerHTML = '<div class="empty">' + esc(query ? "∅" : "…") + "</div>";
      return;
    }

    var html = "";
    (function walk(list) {
      for (var i = 0; i < list.length; i++) {
        var n = list[i];
        if (!n.slug) {
          html += '<div class="grp">' + esc(n.title) + "</div>";
        } else {
          var on = n.slug === currentPage ? " on" : "";
          html += '<a href="#/docs/' + n.slug + '" class="' + on.trim() + '" data-slug="' + n.slug + '">' + esc(n.title) + "</a>";
        }
        if (n.children) walk(n.children);
      }
    })(nodes);

    treeEl.innerHTML = html;
    var links = treeEl.querySelectorAll("a");
    for (var j = 0; j < links.length; j++) {
      links[j].addEventListener("click", function (e) {
        e.preventDefault();
        loadPage(this.getAttribute("data-slug"));
      });
    }
  }

  function currentFilter() {
    return searchInput ? searchInput.value.trim() : "";
  }

  /* ---------------- маршруты ---------------- */
  function parseRoute() {
    var m;
    var stripFrag = function (s) { return s.replace(/#.*$/, ""); };
    if ((m = location.hash.match(/^#\/docs\/(.+)$/))) return stripFrag(m[1]).replace(/\/$/, "");
    if (location.hash && location.hash.length > 1 && location.hash.indexOf("#/") !== 0) {
      return stripFrag(location.hash.slice(1)).replace(/\/$/, "");
    }
    if ((m = location.search.match(/[?&]p=([^&]+)/))) {
      try { return decodeURIComponent(m[1]); } catch (e) { return m[1]; }
    }
    var pm = location.pathname.match(/^\/docs\/(.+?)\/?$/i);
    if (pm) return pm[1];
    return "index";
  }

  function normalizeSlug(slug) {
    slug = (slug || "index").replace(/^\/+|\/+$/g, "");
    if (slug === "" || slug === "docs") slug = "index";
    slug = slug.replace(/^docs\//, "");
    if (!docsIndex[slug] && docsIndex[slug + "/index"]) slug = slug + "/index";
    return slug;
  }

  /* ---------------- загрузка страницы ---------------- */
  function loadPage(slug) {
    var frag = "";
    var fIdx = slug.indexOf("#");
    if (fIdx !== -1) { frag = slug.slice(fIdx + 1); slug = slug.slice(0, fIdx); }
    slug = normalizeSlug(slug);
    if (!loaded) {
      container.innerHTML = '<div class="docs-error">…</div>';
      loadIndex(function () { loadPage(slug); });
      return;
    }

    var md = docsIndex[slug];
    if (!md) {
      container.innerHTML =
        '<div class="docs-error">' + esc(t("not_found")) + ': <code>' + esc(slug) + "</code></div>";
      return;
    }

    currentPage = slug;
    container.innerHTML = marked.parse(md);
    var heads = CTRender.addHeadingIds(container);
    CTRender.renderMermaid();
    buildToc(heads);
    updateChrome(slug);
    bindInternalLinks();

    var newHash = "#/docs/" + slug;
    if (location.hash !== newHash) {
      history.replaceState(null, "", newHash);
    }
    var title = titleOf(slug);
    document.title = title + " — cthangover";
    if (canonicalLink) {
      canonicalLink.setAttribute("href", "https://cthangover.ru/docs/" + slug);
    }
    window.scrollTo(0, 0);
    if (frag) {
      var target = document.getElementById(frag);
      if (target) target.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }

  function bindInternalLinks() {
    var links = container.querySelectorAll("a[href]");
    for (var i = 0; i < links.length; i++) {
      links[i].addEventListener("click", function (e) {
        var href = this.getAttribute("href");
        if (href && href.indexOf("#/docs/") === 0) {
          e.preventDefault();
          loadPage(href.slice(7));
        }
      });
    }
  }

  function updateChrome(slug) {
    var parts = slug.split("/");
    if (parts[parts.length - 1] === "index") parts.pop();
    var crumbs = "docs";
    for (var i = 0; i < parts.length; i++) crumbs += " / <b>" + esc(parts[i]) + "</b>";
    crumbsEl.innerHTML = crumbs;

    var note = lang === "en" ? "" : t("not_translated");
    metaEl.innerHTML =
      '<span class="f">▣ docs/' + slug + ".md</span>" +
      '<span class="g">' + t("built_from") + " build-docs-index.py</span>" +
      "<span>EN</span>" +
      (note ? '<span class="r">' + esc(note) + "</span>" : "");

    var idx = -1;
    for (var j = 0; j < flat.length; j++) if (flat[j].slug === slug) idx = j;
    if (idx > 0) {
      pagerPrev.classList.remove("invisible");
      pagerPrev.onclick = function (e) { e.preventDefault(); loadPage(flat[idx - 1].slug); window.scrollTo(0, 0); };
      pagerPrevT.textContent = flat[idx - 1].title;
    } else pagerPrev.classList.add("invisible");
    if (idx >= 0 && idx < flat.length - 1) {
      pagerNext.classList.remove("invisible");
      pagerNext.onclick = function (e) { e.preventDefault(); loadPage(flat[idx + 1].slug); window.scrollTo(0, 0); };
      pagerNextT.textContent = flat[idx + 1].title;
    } else pagerNext.classList.add("invisible");

    renderTree(currentFilter());
  }

  /* ---------------- TOC + scrollspy ---------------- */
  function buildToc(heads) {
    var out = "";
    var n = 0;
    for (var i = 0; i < heads.length; i++) {
      if (heads[i].tagName === "H1") continue;
      var cls = heads[i].tagName === "H3" || heads[i].tagName === "H4" ? "l3" : "";
      out += '<a href="#' + heads[i].id + '" class="' + cls + '" data-i="' + i + '">' + esc(heads[i].textContent) + "</a>";
      n++;
    }
    tocEl.innerHTML = out || '<span class="none">—</span>';
    if (!n) return;
    var links = tocEl.querySelectorAll("a");
    for (var j = 0; j < links.length; j++) {
      (function (a, k) {
        a.addEventListener("click", function (e) {
          e.preventDefault();
          heads[k].scrollIntoView({ behavior: "smooth", block: "start" });
        });
      })(links[j], +links[j].getAttribute("data-i"));
    }
    window.onscroll = function () {
      var cur = -1;
      for (var q = 0; q < heads.length; q++) {
        if (heads[q].getBoundingClientRect().top < 130) cur = q;
      }
      for (var w = 0; w < links.length; w++) {
        links[w].classList.toggle("spy", +links[w].getAttribute("data-i") === cur);
      }
    };
  }

  /* ---------------- поиск ---------------- */
  function bindSearch() {
    if (!searchInput) return;
    searchInput.addEventListener("input", function () {
      if (searchClear) searchClear.classList.toggle("visible", searchInput.value.length > 0);
      clearTimeout(searchTimer);
      searchTimer = setTimeout(function () { renderTree(currentFilter()); }, 180);
    });
    if (searchClear) {
      searchClear.addEventListener("click", function () {
        searchInput.value = "";
        searchClear.classList.remove("visible");
        renderTree("");
        searchInput.focus();
      });
    }
    document.addEventListener("keydown", function (e) {
      if (e.key === "/" && document.activeElement !== searchInput && !/^(INPUT|TEXTAREA)$/.test(document.activeElement.tagName)) {
        e.preventDefault();
        searchInput.focus();
      }
    });
  }

  /* ---------------- init ---------------- */
  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome("docs");
    bindSearch();
    CTRender.configureMarked({
      base: function () { return currentPage || "index"; },
      href: function (page) {
        var variants = [];
        var dir = (currentPage || "index").split("/").slice(0, -1).join("/");
        if (dir && page.indexOf(dir + "/") === 0) {
          var rest = page.slice(dir.length + 1);
          variants.push(rest);
          var parent = dir.split("/").slice(0, -1).join("/");
          if (parent) variants.push(parent + "/" + rest);
        }
        var segs = page.split("/");
        if (segs.length > 2) variants.push(segs.slice(1).join("/")); /* автор промахнулся на один "../" */
        variants.push(page);
        var cands = [];
        for (var v = 0; v < variants.length; v++) {
          cands.push(variants[v]);
          if (!/\/index$/.test(variants[v])) cands.push(variants[v] + "/index");
        }
        for (var i = 0; i < cands.length; i++) {
          if (docsIndex[cands[i]]) return "#/docs/" + cands[i];
        }
        return "#/docs/" + page;
      },
    });;;
    window.addEventListener("hashchange", function () { loadPage(parseRoute()); });
    document.addEventListener("langchange", function () {
      if (currentPage) updateChrome(currentPage);
    });
    window.addEventListener("load", function () {
      if (currentPage) CTRender.renderMermaid();
    });
    loadPage(parseRoute());
  });
})();
