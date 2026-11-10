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

  function initPurchase() {
    var buy = document.getElementById("pvBuy");
    var pd = document.getElementById("pvConsentPd");
    var ads = document.getElementById("pvConsentAds");
    if (!buy || !pd || !ads) return;
    var payment = buy.getAttribute("href");
    function updatePurchase() {
      var soon = buy.classList.contains("is-soon");
      // Consents are optional and never block payment (152-FZ art. 9 p. 1).
      var disabled = soon;
      buy.setAttribute("aria-disabled", disabled ? "true" : "false");
      if (soon) return;
      if (disabled) {
        buy.removeAttribute("href");
        return;
      }
      var url = new URL(payment, document.baseURI);
      url.searchParams.set("consent_pd", pd.checked ? "2026-11-09" : "0");
      url.searchParams.set("consent_ads", ads.checked ? "2026-11-09" : "0");
      buy.href = url.href;
    }
    buy.addEventListener("click", function (event) {
      updatePurchase();
      if (buy.getAttribute("aria-disabled") === "true") event.preventDefault();
    });
    pd.addEventListener("change", updatePurchase);
    ads.addEventListener("change", updatePurchase);
    updatePurchase();
  }

  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome("private");
    updateFallback();
    initReveal();
    initPurchase();
    document.addEventListener("langchange", updateFallback);
  });
})();
