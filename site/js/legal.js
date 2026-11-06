/* ============================================================
   cthangover — страницы документов (offer.html, privacy.html).
   Словарь страницы — <body data-dict="OFFER_I18N">: объект из
   js/<page>-i18n.js. Здесь шапка и плашка #docFallback, пока у
   выбранного языка нет хотя бы одного ключа блока ru.
   ============================================================ */
(function () {
  "use strict";

  function dict() {
    var name = document.body.getAttribute("data-dict");
    return (name && window[name]) || { ru: {} };
  }

  function isTranslated(l) {
    var all = dict(), d = all[l] || {};
    for (var k in all.ru) if (!d[k]) return false;
    return true;
  }

  function updateFallback() {
    var el = document.getElementById("docFallback");
    if (el) el.hidden = isTranslated(lang);
  }

  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome(document.body.getAttribute("data-page") || "");
    updateFallback();
    document.addEventListener("langchange", updateFallback);
  });
})();
