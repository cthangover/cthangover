/* ============================================================
   cthangover — быстрые старты: три трека × 7 языков из
   quickstart/quickstart-index.json (build-quickstart-index.py).
   Маршрут: #/quickstart/<lang>/<page> (тот же вид, что в sitemap:
   /quickstart/<lang>/<page> редиректится сюда с главной);
   короткие #/quickstart/<page> и ?tab=<page> тоже понимаются.
   Фолбэк честный: нет перевода — показываем en с бейджем.
   ============================================================ */
(function () {
  "use strict";

  var INDEX_URL = "/quickstart/quickstart-index.json";
  var TABS = ["index", "player", "modder", "programmer"];

  var contentEl = document.getElementById("qsContent");
  if (!contentEl) return;

  var fileEl = document.getElementById("qsFile");
  var fallbackEl = document.getElementById("qsFallback");
  var pillsEl = document.getElementById("qsPills");
  var tabsEl = document.getElementById("qsTabs");

  var index = null;
  var curTab = "index";

  /* убрать html-блоки быстрых стартов: карточки-ссылки дублируют треки
     страницы, а мета-строку (<div class="qs-meta"><span>…) превратить в курсив */
  function preprocess(md) {
    var lines = md.split("\n"), out = [], inCards = false, inMeta = false, metaBuf = [];
    for (var i = 0; i < lines.length; i++) {
      var L = lines[i];
      if (/<div class="quickstart-cards">/.test(L)) { inCards = true; continue; }
      if (inCards) { if (/<\/div>/.test(L)) inCards = false; continue; }
      if (/<div class="qs-meta">/.test(L)) { inMeta = true; metaBuf = []; continue; }
      if (inMeta) {
        var m = L.match(/<span>([^<]*)<\/span>/);
        if (m) { metaBuf.push(m[1]); continue; }
        if (/<\/div>/.test(L)) { inMeta = false; out.push("*" + metaBuf.join(" · ") + "*"); continue; }
      }
      out.push(L);
    }
    return out.join("\n");
  }

  /* {lang, tab} из адреса; lang === null — язык в адресе не указан */
  function parseRoute() {
    var route = { lang: null, tab: null };
    var m = (location.hash || "").match(/^#\/quickstart\/?([\w\/]*)/);
    var segs = m ? m[1].split("/").filter(Boolean) : [];
    if (segs.length && I18N[segs[0]]) route.lang = segs.shift();
    if (segs.length) route.tab = segs[0];
    var p = (location.search || "").match(/[?&]tab=(\w+)/);
    if (!route.tab && p) route.tab = p[1];
    return route;
  }

  function writeRoute() {
    var hash = "#/quickstart/" + lang + "/" + (curTab === "index" ? "" : curTab);
    if (location.hash !== hash) history.replaceState(null, "", hash);
  }

  function renderPills() {
    var html = "";
    for (var i = 0; i < LANGS.length; i++) {
      html += '<button data-l="' + LANGS[i][0] + '" title="' + esc(LANGS[i][1]) + '" class="' +
              (LANGS[i][0] === lang ? "sel" : "") + '">' +
              LANGS[i][0].toUpperCase() + "</button>";
    }
    pillsEl.innerHTML = html;
    var bs = pillsEl.querySelectorAll("button");
    for (var j = 0; j < bs.length; j++) {
      bs[j].addEventListener("click", function () { setLang(this.getAttribute("data-l")); });
    }
  }

  function renderPanel() {
    if (!index) {
      contentEl.innerHTML = '<div class="docs-error">…</div>';
      return;
    }
    var use = index[lang + "/" + curTab] ? lang : "en";
    var md = index[use + "/" + curTab];
    var path = "quickstart/" + use + "/" + curTab + ".md";

    fileEl.textContent = path;
    fallbackEl.style.display = use === lang ? "none" : "";
    if (!md) {
      contentEl.innerHTML = '<div class="docs-error">' +
        esc(t("not_found")) + ": <code>" + esc(path) + "</code></div>";
      return;
    }
    contentEl.innerHTML = marked.parse(preprocess(md));
    bindLinks();
  }

  /* ссылки между треками внутри md: "#quickstart/<page>" или "#/quickstart/<lang>/<page>" */
  function bindLinks() {
    var links = contentEl.querySelectorAll("a[href]");
    for (var i = 0; i < links.length; i++) {
      links[i].addEventListener("click", function (e) {
        var href = this.getAttribute("href") || "";
        var m = href.match(/^#\/?quickstart\/(?:\w+\/)?(\w+)\/?$/);
        if (m && TABS.indexOf(m[1]) !== -1) {
          e.preventDefault();
          setTab(m[1]);
          window.scrollTo({ top: 0, behavior: "smooth" });
        }
      });
    }
  }

  function setTab(tab) {
    if (TABS.indexOf(tab) === -1) tab = "index";
    curTab = tab;
    var tabs = tabsEl.querySelectorAll("button");
    for (var i = 0; i < tabs.length; i++) {
      tabs[i].classList.toggle("on", tabs[i].getAttribute("data-tab") === tab);
    }
    var tracks = document.querySelectorAll(".track");
    for (var k = 0; k < tracks.length; k++) {
      tracks[k].classList.toggle("on", tracks[k].getAttribute("data-tab") === tab);
    }
    renderPanel();
    writeRoute();
  }

  function applyRoute() {
    var r = parseRoute();
    if (r.lang) setLang(r.lang);
    setTab(r.tab || "index");
  }

  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome("quickstart");

    CTRender.configureMarked({
      base: "",
      href: function (page, isDir, isDocs) {
        if (isDocs) return "/docs.html#/docs/" + page + (isDir ? "/index" : "");
        return "#/quickstart/" + page.replace(/^\/?quickstart\//, "");
      },
    });

    var buttons = document.querySelectorAll("#qsTabs button, .track");
    for (var i = 0; i < buttons.length; i++) {
      buttons[i].addEventListener("click", function () { setTab(this.getAttribute("data-tab")); });
    }

    window.addEventListener("hashchange", applyRoute);
    document.addEventListener("langchange", function () {
      renderPills();
      renderPanel();
      writeRoute();
    });

    renderPills();
    applyRoute();
    CTRender.loadJson(INDEX_URL,
      function (data) { index = data; renderPanel(); },
      function () { index = {}; renderPanel(); });
  });
})();
