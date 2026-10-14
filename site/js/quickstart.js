/* ============================================================
   cthangover — быстрые старты: три трека × 7 языков из
   quickstart/quickstart-index.json (build-quickstart-index.py).
   Фолбэк честный: нет перевода — показываем en с бейджем.
   ============================================================ */
(function () {
  "use strict";

  var INDEX_URL = "/quickstart/quickstart-index.json";

  var contentEl = document.getElementById("qsContent");
  if (!contentEl) return;

  var fileEl = document.getElementById("qsFile");
  var fallbackEl = document.getElementById("qsFallback");
  var pillsEl = document.getElementById("qsPills");
  var tabsEl = document.getElementById("qsTabs");

  var index = null;
  var loaded = false;
  var curTab = "index";

  function t(k){ var d = I18N[lang] || I18N.en; return d[k] || I18N.en[k] || k; }

  function loadIndex(cb) {
    if (loaded) { cb(); return; }
    fetch(INDEX_URL)
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (data) { index = data; loaded = true; cb(); })
      .catch(function () {
        try {
          var xhr = new XMLHttpRequest();
          xhr.open("GET", INDEX_URL, true);
          xhr.onload = function () {
            if (xhr.status === 0 || xhr.status === 200) {
              try { index = JSON.parse(xhr.responseText); } catch (e) { index = {}; }
            } else index = {};
            loaded = true;
            cb();
          };
          xhr.onerror = function () { index = {}; loaded = true; cb(); };
          xhr.send();
        } catch (e) { index = {}; loaded = true; cb(); }
      });
  }

  function renderPills() {
    var html = "";
    for (var i = 0; i < LANGS.length; i++) {
      html += '<button data-l="' + LANGS[i][0] + '" class="' +
              (LANGS[i][0] === lang ? "sel" : "") + '">' +
              LANGS[i][0].toUpperCase() + "</button>";
    }
    pillsEl.innerHTML = html;
    var bs = pillsEl.querySelectorAll("button");
    for (var j = 0; j < bs.length; j++) {
      (function (b) {
        b.addEventListener("click", function () { setLang(b.getAttribute("data-l")); });
      })(bs[j]);
    }
  }

  function mdFor(tab, use) {
    if (!index) return null;
    if (tab === "index") return index[use + "/index"];
    return index[use + "/" + tab];
  }

  function renderPanel() {
    if (!loaded) {
      contentEl.innerHTML = '<div class="docs-error">…</div>';
      return;
    }
    var md = mdFor(curTab, lang);
    var has = !!md;
    var use = has ? lang : "en";
    if (!has) md = mdFor(curTab, use);

    fallbackEl.style.display = has ? "none" : "";
    if (!md) {
      fileEl.textContent = "quickstart/" + use + "/" + curTab + ".md";
      contentEl.innerHTML = '<div class="docs-error">' +
        esc(t("not_found")) + ': <code>quickstart/' + esc(use) + "/" + esc(curTab) + ".md</code></div>";
      return;
    }
    fileEl.textContent = "quickstart/" + use + "/" + curTab + ".md";
    contentEl.innerHTML = marked.parse(qsPreprocess(md));
    bindLinks();
  }

  function bindLinks() {
    var links = contentEl.querySelectorAll("a[href]");
    for (var i = 0; i < links.length; i++) {
      links[i].addEventListener("click", function (e) {
        var href = this.getAttribute("href") || "";
        var m = href.match(/^#quickstart\/(\w+)$/) || href.match(/^#\/quickstart\/\w+\/(\w+)$/);
        if (m) {
          e.preventDefault();
          setTab(m[1]);
          window.scrollTo({ top: 0, behavior: "smooth" });
        }
      });
    }
  }

  function setTab(tab) {
    if (["index", "player", "modder", "programmer"].indexOf(tab) === -1) tab = "index";
    curTab = tab;
    var tabs = tabsEl.querySelectorAll("button");
    for (var i = 0; i < tabs.length; i++) {
      tabs[i].classList.toggle("on", tabs[i].getAttribute("data-tab") === tab);
    }
    var tracks = document.querySelectorAll(".track");
    for (var k = 0; k < tracks.length; k++) {
      tracks[k].classList.toggle("on",
        tracks[k].getAttribute("data-tab") === tab && tab !== "index");
    }
    renderPanel();
    var hash = "#/quickstart/" + (tab === "index" ? "" : tab + "/");
    if (location.hash !== hash) history.replaceState(null, "", hash);
  }

  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome("quickstart");

    CTRender.configureMarked({
      base: "",
      href: function (page) {
        if (page.startsWith("/quickstart/")) return "#/quickstart/" + page.slice(12);
        return "#/quickstart/" + page;
      },
    });

    var m = (location.hash || "").match(/^#\/quickstart(?:\/(\w+))?/);
    var p = (location.search || "").match(/[?&]tab=(\w+)/);
    curTab = (p && p[1]) || (m && m[1]) || "index";

    var tbs = tabsEl.querySelectorAll("button");
    for (var i = 0; i < tbs.length; i++) {
      (function (b) {
        b.addEventListener("click", function () { setTab(b.getAttribute("data-tab")); });
      })(tbs[i]);
    }
    var trs = document.querySelectorAll(".track");
    for (var k = 0; k < trs.length; k++) {
      (function (b) {
        b.addEventListener("click", function () { setTab(b.getAttribute("data-tab")); });
      })(trs[k]);
    }

    window.addEventListener("hashchange", function () {
      var hm = (location.hash || "").match(/^#\/quickstart(?:\/(\w+))?/);
      if (hm) setTab(hm[1] || "index");
    });

    document.addEventListener("langchange", function () {
      renderPills();
      renderPanel();
    });

    renderPills();
    setTab(curTab);
    loadIndex(renderPanel);
  });
})();
