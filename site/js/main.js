(function () {
  "use strict";

  var SUPPORTED_LANGS = ["ru", "en", "zh", "ja", "ko", "vi", "es"];
  var DEFAULT_LANG = "ru";
  var LS_KEY = "cthangover_lang";

  var currentLang = getInitialLang();

  function getInitialLang() {
    var stored = localStorage.getItem(LS_KEY);
    if (stored && SUPPORTED_LANGS.indexOf(stored) !== -1) return stored;
    return DEFAULT_LANG;
  }

  function getI18n(lang) {
    return I18N[lang] || I18N[DEFAULT_LANG];
  }

  function applyI18n() {
    var strings = getI18n(currentLang);

    document.documentElement.lang = currentLang;

    if (strings.hero_title) {
      document.title = strings.hero_title;
    }

    var elements = document.querySelectorAll("[data-i18n]");
    for (var i = 0; i < elements.length; i++) {
      var el = elements[i];
      var key = el.getAttribute("data-i18n");
      if (strings[key]) {
        el.textContent = strings[key];
      }
    }

    updateLangButtons();
    buildSystemsAccordion(strings);
    updateActiveNav();
  }

  function updateLangButtons() {
    var btns = document.querySelectorAll(".lang-btn");
    for (var i = 0; i < btns.length; i++) {
      var btn = btns[i];
      var lang = btn.getAttribute("data-lang");
      if (lang === currentLang) {
        btn.classList.add("active");
      } else {
        btn.classList.remove("active");
      }
    }
  }

  function buildSystemsAccordion(strings) {
    var container = document.getElementById("systemsAccordion");
    if (!container) return;

    var html = "";
    for (var i = 1; ; i++) {
      var num = i < 10 ? "0" + i : "" + i;
      var titleKey = "sys_" + num + "_title";
      var descKey = "sys_" + num + "_desc";
      var title = strings[titleKey];
      if (!title) break;
      var desc = strings[descKey] || "";

      html += '<details class="sys-details">';
      html += '<summary class="sys-summary">' + esc(title) + "</summary>";
      html += '<div class="sys-body">' + esc(desc) + "</div>";
      html += "</details>";
    }
    container.innerHTML = html;
  }

  function esc(str) {
    var div = document.createElement("div");
    div.appendChild(document.createTextNode(str));
    return div.innerHTML;
  }

  function switchLanguage(lang) {
    if (lang === currentLang) return;
    if (SUPPORTED_LANGS.indexOf(lang) === -1) return;
    currentLang = lang;
    localStorage.setItem(LS_KEY, lang);
    applyI18n();
    document.dispatchEvent(new CustomEvent("langchange", { detail: lang }));
  }

  function updateActiveNav() {
    var sections = document.querySelectorAll("section[id]");
    var links = document.querySelectorAll(".nav-links a");
    if (sections.length === 0 || links.length === 0) return;

    var scrollPos = window.scrollY + 100;
    var currentId = "";

    for (var i = 0; i < sections.length; i++) {
      var s = sections[i];
      if (s.offsetTop <= scrollPos) {
        currentId = s.getAttribute("id");
      }
    }

    for (var i = 0; i < links.length; i++) {
      var link = links[i];
      var href = link.getAttribute("href");
      if (href === "#" + currentId) {
        link.style.color = "var(--accent)";
      } else {
        link.style.color = "";
      }
    }
  }

  function initLightbox() {
    var lightbox = document.getElementById("lightbox");
    if (!lightbox) return;

    var img = lightbox.querySelector(".lightbox-img");
    var items = document.querySelectorAll(".screenshot-item img");
    var currentIndex = -1;

    function show(index) {
      if (index < 0 || index >= items.length) return;
      currentIndex = index;
      img.src = items[index].src;
      img.alt = items[index].alt;
      lightbox.classList.add("open");
      lightbox.setAttribute("aria-hidden", "false");
      document.body.style.overflow = "hidden";
    }

    function close() {
      lightbox.classList.remove("open");
      lightbox.setAttribute("aria-hidden", "true");
      document.body.style.overflow = "";
    }

    function showNext() {
      if (currentIndex < items.length - 1) show(currentIndex + 1);
    }

    function showPrev() {
      if (currentIndex > 0) show(currentIndex - 1);
    }

    for (var i = 0; i < items.length; i++) {
      items[i].parentElement.addEventListener("click", function (idx) {
        return function () { show(idx); };
      }(i));
    }

    lightbox.querySelector(".lightbox-overlay").addEventListener("click", close);
    lightbox.querySelector(".lightbox-close").addEventListener("click", close);
    lightbox.querySelector(".lightbox-next").addEventListener("click", showNext);
    lightbox.querySelector(".lightbox-prev").addEventListener("click", showPrev);

    document.addEventListener("keydown", function (e) {
      if (!lightbox.classList.contains("open")) return;
      if (e.key === "Escape") close();
      if (e.key === "ArrowRight") showNext();
      if (e.key === "ArrowLeft") showPrev();
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    var langBtns = document.querySelectorAll(".lang-btn");
    for (var i = 0; i < langBtns.length; i++) {
      langBtns[i].addEventListener("click", function (e) {
        var lang = e.currentTarget.getAttribute("data-lang");
        switchLanguage(lang);
      });
    }

    var scrollTimer;
    window.addEventListener("scroll", function () {
      if (scrollTimer) clearTimeout(scrollTimer);
      scrollTimer = setTimeout(updateActiveNav, 50);
    });

    initLightbox();

    applyI18n();
    updateActiveNav();
  });
})();
