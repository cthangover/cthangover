(function () {
  "use strict";

  var QUICKSTART_BASE = "quickstart/";
  var SUPPORTED_LANGS = ["ru", "en", "zh", "ja", "ko", "vi", "es"];

  var container = null;
  var currentPage = null;
  var quickstartIndex = null;
  var currentLang = getInitialLang();

  function getInitialLang() {
    var stored = localStorage.getItem("cthangover_lang");
    if (stored && SUPPORTED_LANGS.indexOf(stored) !== -1) return stored;
    return "ru";
  }

  function loadIndex(callback) {
    if (quickstartIndex) {
      if (callback) callback();
      return;
    }

    fetch(QUICKSTART_BASE + "quickstart-index.json")
      .then(function (res) {
        if (!res.ok) throw new Error("HTTP " + res.status);
        return res.json();
      })
      .then(function (data) {
        quickstartIndex = data;
        if (callback) callback();
      })
      .catch(function (err) {
        console.error("Quickstart: failed to load index", err);
      });
  }

  function loadPage(name) {
    if (!container) return;
    if (!name) name = "index";
    currentPage = name;

    var key = currentLang + "/" + name;

    if (quickstartIndex && quickstartIndex[key]) {
      container.innerHTML = marked.parse(quickstartIndex[key]);
      return;
    }

    if (quickstartIndex) {
      var fallbackKey = "en/" + name;
      if (quickstartIndex[fallbackKey]) {
        container.innerHTML = marked.parse(quickstartIndex[fallbackKey]);
        return;
      }
      container.innerHTML =
        '<div class="docs-error">Page not found: <code>' +
        key +
        ".md</code></div>";
      return;
    }

    fetch(QUICKSTART_BASE + currentLang + "/" + name + ".md")
      .then(function (res) {
        if (!res.ok) throw new Error("HTTP " + res.status);
        return res.text();
      })
      .then(function (md) {
        container.innerHTML = marked.parse(md);
      })
      .catch(function () {
        fetch(QUICKSTART_BASE + "en/" + name + ".md")
          .then(function (res) { return res.text(); })
          .then(function (md) {
            container.innerHTML = marked.parse(md);
          })
          .catch(function () {
            container.innerHTML =
              '<div class="docs-error">Page not found: <code>' +
              name +
              ".md</code></div>";
          });
      });
  }

  function enterQuickstartMode(page) {
    if (!page) page = "index";
    document.body.classList.remove("docs-mode");
    document.body.classList.add("quickstart-mode");
    currentPage = null;
    loadIndex(function () {
      loadPage(page);
    });
  }

  function exitQuickstartMode() {
    document.body.classList.remove("quickstart-mode");
    currentPage = null;
    if (container) container.innerHTML = "";
    if (location.hash && location.hash.startsWith("#quickstart")) {
      history.replaceState(null, "", window.location.pathname);
    }
  }

  function walkQuickstartLinks(token) {
    if (token.type !== "link" || !token.href) return;

    var href = token.href;

    if (/\.(png|jpg|jpeg|gif|svg|webp|mp4|webm|mp3|ogg|wav|css|js)(\?.*)?$/i.test(href)) return;

    if (href.startsWith("#/docs/site/docs/")) {
      href = "site/" + href.slice(7);
    }

    if (/^(https?:|#|mailto:|data:|\/\/)/i.test(href)) return;

    var page = href;
    var isDir = page.endsWith("/");

    if (/\.md$/i.test(page)) {
      page = page.replace(/\.md$/i, "");
    }
    if (page.endsWith("/")) {
      page = page.slice(0, -1);
    }

    if (page.startsWith("site/docs/")) {
      page = page.slice(10);
    } else if (page.startsWith("docs/")) {
      page = page.slice(5);
    }

    if (page.startsWith("../") || page.startsWith("./")) {
      var baseParts = (currentPage || "index").split("/");
      baseParts.pop();
      var parts = page.split("/");
      for (var i = 0; i < parts.length; i++) {
        if (parts[i] === "..") {
          if (baseParts.length > 0) baseParts.pop();
        } else if (parts[i] !== ".") {
          baseParts.push(parts[i]);
        }
      }
      page = baseParts.join("/");
    }

    if (isDir && !page.endsWith("/index")) {
      page += "/index";
    }

    token.href = "#/docs/" + page;
  }

  function setupMarked() {
    var walkExt = { walkTokens: walkQuickstartLinks };
    marked.use(walkExt);
  }

  function handleRoute() {
    var hash = location.hash;

    if (hash.startsWith("#quickstart/")) {
      var page = hash.slice(12);
      if (!page) page = "index";
      if (!document.body.classList.contains("quickstart-mode")) {
        enterQuickstartMode(page);
      } else {
        loadPage(page);
      }
      return true;
    }

    if (hash === "#quickstart") {
      enterQuickstartMode("index");
      if (location.hash !== "#quickstart/") {
        history.replaceState(null, "", "#quickstart/");
      }
      return true;
    }

    return false;
  }

  function onDOMReady() {
    container = document.getElementById("quickstart-content");

    document.addEventListener("langchange", function (e) {
      var newLang = e.detail;
      if (SUPPORTED_LANGS.indexOf(newLang) === -1) return;
      currentLang = newLang;
      if (document.body.classList.contains("quickstart-mode") && currentPage) {
        loadPage(currentPage);
      }
    });

    var backLink = document.querySelector(".docs-back-link");
    if (backLink) {
      backLink.addEventListener("click", function (e) {
        if (document.body.classList.contains("quickstart-mode")) {
          e.preventDefault();
          e.stopImmediatePropagation();
          exitQuickstartMode();
        }
      });
    }

    container.addEventListener("click", function (e) {
      var a = e.target.closest("a");
      if (!a) return;
      var href = a.getAttribute("href");
      if (!href) return;

      if (href.startsWith("#quickstart/")) {
        e.preventDefault();
        var page = href.slice(12);
        if (!page) page = "index";
        if (location.hash !== href) {
          history.pushState(null, "", href);
        }
        if (document.body.classList.contains("quickstart-mode")) {
          loadPage(page);
        } else {
          enterQuickstartMode(page);
        }
        return;
      }

      if (href.startsWith("#/docs/")) {
        e.preventDefault();
        exitQuickstartMode();
        location.hash = href;
        return;
      }
    });

    window.addEventListener("hashchange", function () {
      handleRoute();
    });

    setupMarked();
    handleRoute();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", onDOMReady);
  } else {
    onDOMReady();
  }
})();
