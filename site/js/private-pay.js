/* ============================================================
   cthangover — оплата подписки на странице private.html.

   Сайт полностью статический, серверной части у него нет — значит,
   и секретного ключа ЮKassa на фронте быть не должно. Платёж
   открывается «ссылкой на оплату» из личного кабинета ЮKassa
   (Мой магазин → Продажи → Ссылка на оплату):

     1. Ссылку на товар («Подписка на приватный каталог», 300 ₽,
        с запросом e-mail покупателя — на него приходят чек и ключ)
        вставьте в href кнопки #pvBuy в private.html.
     2. Клик открывает оплату в компактном окне поверх сайта; если
        браузер блокирует такие окна — обычным переходом новой
        вкладкой. Без JavaScript ссылка тоже работает (target="_blank").

   Примечание. Встроенный виджет ЮKassa (YooMoneyCheckoutWidget)
   требует confirmation_token, который создаётся секретным ключом
   на сервере, поэтому на статическом сайте он недоступен.
   ============================================================ */
(function () {
  "use strict";

  var buy = document.getElementById("pvBuy");
  if (!buy) return;

  buy.addEventListener("click", function (event) {
    var link = buy.getAttribute("href") || "";

    /* ссылка из личного кабинета ещё не вставлена — оплату не открываем */
    if (link.charAt(0) === "#") {
      event.preventDefault();
      return;
    }

    /* компактное окно по центру экрана; не получилось — браузер
       сам откроет новую вкладку (переход не отменяем) */
    var w = Math.min(720, screen.availWidth - 40);
    var h = Math.min(760, screen.availHeight - 80);
    var left = Math.max(0, Math.round((screen.availWidth - w) / 2));
    var top = Math.max(0, Math.round((screen.availHeight - h) / 2));
    var win = window.open(
      link,
      "yookassa_checkout",
      "width=" + w + ",height=" + h + ",left=" + left + ",top=" + top +
      ",menubar=no,toolbar=no,location=no,status=no,resizable=yes,scrollbars=yes"
    );
    if (!win) return;
    event.preventDefault();
    try { win.opener = null; } catch (e) { /* старые браузеры */ }
    win.focus();
  });
})();
