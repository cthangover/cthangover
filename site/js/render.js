/* ============================================================
   cthangover — общий рендер Markdown (marked + highlight.js + mermaid).
   Используется docs.js и quickstart.js.
   ============================================================ */
(function () {
  "use strict";

  var mermaidReady = false;

  function ensureMermaid() {
    if (typeof mermaid === "undefined" || mermaidReady) return;
    mermaidReady = true;
    mermaid.initialize({
      startOnLoad: false,
      theme: "dark",
      securityLevel: "loose",
      logLevel: 4,
      themeVariables: {
        primaryColor: "#171923",
        primaryTextColor: "#e8e5de",
        primaryBorderColor: "#d9a441",
        lineColor: "#8b8894",
        secondaryColor: "#0e0f15",
        tertiaryColor: "#101118",
        background: "#0d0e14",
        mainBkg: "#171923",
        nodeBorder: "#2f3242",
        clusterBkg: "#101118",
        clusterBorder: "#2a2c38",
        titleColor: "#f0c169",
        edgeLabelBackground: "#171923",
        nodeTextColor: "#e8e5de",
        labelTextColor: "#c9c5bc",
        textColor: "#e8e5de",
        fontSize: "14px",
      },
      flowchart: { useMaxWidth: false, htmlLabels: true, curve: "basis" },
    });
  }

  function escapeCode(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function createRenderer() {
    return {
      code: function (token) {
        var lang = token.lang || "";
        var text = token.text || "";

        if (lang === "mermaid" && typeof mermaid !== "undefined") {
          ensureMermaid();
          try {
            var mmId = "mermaid-" + Math.random().toString(36).slice(2, 10);
            return (
              '<div class="mermaid-wrap"><div id="' + mmId +
              '" class="mermaid">' + escapeCode(text) + "</div></div>\n"
            );
          } catch (e) {
            return "<pre><code>" + escapeCode(text) + "</code></pre>\n";
          }
        }

        if (lang && typeof hljs !== "undefined" && hljs.getLanguage(lang)) {
          try {
            var res = hljs.highlight(text, { language: lang });
            return (
              '<pre><code class="hljs language-' + lang + '">' +
              res.value + "</code></pre>\n"
            );
          } catch (e) {}
        }

        return (
          "<pre><code" + (lang ? ' class="language-' + lang + '"' : "") +
          ">" + escapeCode(text) + "</code></pre>\n"
        );
      },
    };
  }

  /* относительные ссылки внутри md -> хэш-маршруты/абсолютные пути.
     opts.href(page, isDir, isDocs) -> новый href или null (оставить как есть);
     isDocs — ссылка явно указывает в docs/ ("docs/…", "/site/docs/…") */
  function configureMarked(opts) {
    var use = { renderer: createRenderer() };
    if (opts && typeof opts.href === "function") {
      use.walkTokens = function (token) {
        if (token.type !== "link" || !token.href) return;
        var href = token.href;

        var hash = "";
        var hIdx = href.indexOf("#");
        if (hIdx !== -1 && !/^(https?:|mailto:|data:)/i.test(href)) {
          hash = href.slice(hIdx);
          href = href.slice(0, hIdx);
          if (!href) return; /* чистый якорь — не трогаем */
        }

        if (/^(https?:|mailto:|data:|\/\/)/i.test(href)) return;
        if (/\.(png|jpg|jpeg|gif|svg|webp|mp4|webm|mp3|ogg|wav|css|js)(\?.*)?$/i.test(href)) return;

        var siteAbsolute = false;
        var mAbs = href.match(/^\/?(?:site\/)?docs\/(.+)$/i);
        if (mAbs) {
          siteAbsolute = true;
          href = mAbs[1];
        } else if (href.charAt(0) === "/") {
          return; /* другой абсолютный путь сайта — не трогаем */
        }

        var base = typeof opts.base === "function" ? (opts.base() || "") : (opts.base || "");
        var baseParts = base.split("/").filter(Boolean);
        if (baseParts.length) baseParts.pop(); /* каталог текущей страницы */

        var page = href.replace(/\.md$/i, "");
        var isDir = page.endsWith("/");
        if (page.endsWith("/")) page = page.slice(0, -1);

        if (!siteAbsolute) {
          var parts = page.split("/");
          if (parts[0] === ".." || parts[0] === ".") {
            for (var i = 0; i < parts.length; i++) {
              if (parts[i] === "..") { if (baseParts.length > 0) baseParts.pop(); }
              else if (parts[i] !== ".") baseParts.push(parts[i]);
            }
            page = baseParts.join("/");
          } else if (baseParts.length) {
            page = baseParts.concat(parts).join("/");
          }
        }

        var mapped = opts.href(page, isDir, siteAbsolute);
        if (mapped) token.href = mapped + hash;
      };
    }
    marked.use(use);
  }

  /* проставить id заголовкам после вставки HTML (независимо от API marked) */
  function addHeadingIds(container) {
    var heads = container.querySelectorAll("h1, h2, h3, h4");
    for (var i = 0; i < heads.length; i++) heads[i].id = "h-" + i;
    return heads;
  }

  /* mermaid: отрисовать диаграммы и включить zoom/pan (порт старого поведения) */
  function renderMermaid() {
    if (typeof mermaid === "undefined") return;
    ensureMermaid();
    try {
      var run = mermaid.run({ querySelector: ".mermaid" });
      if (run && typeof run.catch === "function") run.catch(function () {});
    } catch (e) {}

    var setups = document.querySelectorAll(".mermaid-wrap:not([data-zoom-ready])");
    for (var i = 0; i < setups.length; i++) initZoomPan(setups[i]);
  }

  function initZoomPan(wrapper) {
    wrapper.setAttribute("data-zoom-ready", "1");
    var viewport = wrapper.querySelector(".mermaid");
    if (!viewport) return;
    var scale = 1, panX = 0, panY = 0, isPanning = false, startX = 0, startY = 0;

    function update() {
      viewport.style.transformOrigin = "0 0";
      viewport.style.transform = "translate(" + panX + "px," + panY + "px) scale(" + scale + ")";
      wrapper.classList.toggle("is-zoomed", scale !== 1);
    }
    wrapper.addEventListener("wheel", function (e) {
      e.preventDefault();
      scale = Math.max(0.3, Math.min(5, scale + (e.deltaY > 0 ? -0.1 : 0.1)));
      update();
    }, { passive: false });
    wrapper.addEventListener("dblclick", function () { scale = 1; panX = 0; panY = 0; update(); });
    wrapper.addEventListener("mousedown", function (e) {
      if (scale === 1) return;
      isPanning = true; startX = e.clientX - panX; startY = e.clientY - panY;
      e.preventDefault();
    });
    window.addEventListener("mousemove", function (e) {
      if (!isPanning) return;
      panX = e.clientX - startX; panY = e.clientY - startY; update();
    });
    window.addEventListener("mouseup", function () { isPanning = false; });
  }

  /* JSON-индекс контента. fetch не работает при открытии с file:// —
     тогда пробуем XHR (статус 0 там означает успех). onFail() без аргументов. */
  function loadJson(url, onOk, onFail) {
    fetch(url)
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(onOk)
      .catch(function () {
        var xhr = new XMLHttpRequest();
        xhr.open("GET", url, true);
        xhr.onload = function () {
          if (xhr.status !== 0 && xhr.status !== 200) { onFail(); return; }
          var data;
          try { data = JSON.parse(xhr.responseText); } catch (e) { onFail(); return; }
          onOk(data);
        };
        xhr.onerror = onFail;
        xhr.send();
      });
  }

  window.CTRender = {
    configureMarked: configureMarked,
    addHeadingIds: addHeadingIds,
    renderMermaid: renderMermaid,
    loadJson: loadJson,
  };
})();
