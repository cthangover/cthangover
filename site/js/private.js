/* ============================================================
   cthangover — страница приватного каталога (private.html).
   Тексты — js/private-i18n.js; здесь шапка, плашка о
   непереведённой странице и появление блоков при прокрутке.
   ============================================================ */
(function () {
  "use strict";

  /* язык переведён целиком, если в нём есть каждый ключ блока ru */
  function isTranslated(l) {
    var d = PRIVATE_I18N[l] || {};
    for (var k in PRIVATE_I18N.ru) if (!d[k]) return false;
    return true;
  }

  function updateFallback() {
    var el = document.getElementById("pvFallback");
    if (el) el.hidden = isTranslated(lang);
  }

  function initReveal() {
    var els = document.querySelectorAll(".reveal");
    if (!("IntersectionObserver" in window)) {
      for (var i = 0; i < els.length; i++) els[i].classList.add("in");
      return;
    }
    var io = new IntersectionObserver(function (entries) {
      for (var j = 0; j < entries.length; j++) {
        if (entries[j].isIntersecting) {
          entries[j].target.classList.add("in");
          io.unobserve(entries[j].target);
        }
      }
    }, { rootMargin: "0px 0px -8% 0px" });
    for (var k = 0; k < els.length; k++) io.observe(els[k]);
  }

  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome("private");
    updateFallback();
    initReveal();
    document.addEventListener("langchange", updateFallback);
  });
})();
