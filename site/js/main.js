/* ============================================================
   cthangover — главная страница: сборка данных из data/mods.json
   (каталог модов, статистика, DSL-сэмпл) + перевод интерфейса.
   ============================================================ */
(function () {
  "use strict";

  /* --- совместимость со старыми адресами: /docs/... и /quickstart/... --- */
  (function legacyRedirect(){
    var p = location.pathname;
    var mDocs = p.match(/^\/docs\/(.*)$/i);
    var mQs = p.match(/^\/quickstart(?:\/(.*))?$/i);
    try {
      if (mDocs) {
        location.replace("/docs.html#/docs/" + mDocs[1]);
        return;
      }
      if (mQs) {
        location.replace("/quickstart.html" + (mQs[1] ? "#/quickstart/" + mQs[1] : ""));
      }
    } catch (e) {}
  })();

  /* ---------- данные ---------- */
  var DATA = null;

  /* числа до загрузки data/mods.json (и при file://) — те же, что в разметке */
  var FALLBACK_STATS = { mods: 28, dslCommands: 29 };

  function stat(k) {
    return (DATA && DATA.stats && DATA.stats[k]) || FALLBACK_STATS[k];
  }

  var LAYERS = [
    { k: "arch_l1t", d: "arch_l1d", rn: "I" },
    { k: "arch_l2t", d: "arch_l2d", rn: "II" },
    { k: "arch_l3t", d: "arch_l3d", rn: "III" },
    { k: "arch_l4t", d: "arch_l4d", rn: "IV", hot: true }
  ];

  var TILES = [
    ["f1t","f1d"],["f2t","f2d"],["f3t","f3d"],["f4t","f4d"],
    ["f5t","f5d"],["f6t","f6d"],["f7t","f7d"],["f8t","f8d"]
  ];
  var RN2 = ["i","ii","iii","iv","v","vi","vii","viii"];

  var CAT_KEYS = { engine:"cat_engine", gameplay:"cat_gameplay", story:"cat_story",
                   library:"cat_library", ui:"cat_ui", tools:"cat_tools" };
  var BATTLE_DESC = { turn_battle_base:"turn_d", card_battle:"card_d", ff_battle:"ff_d" };
  var BATTLE_TITLE = { turn_battle_base:"turn_t", card_battle:"card_t", ff_battle:"ff_t" };
  var BATTLE_ENGINE = "turn_battle_base";

  /* ---------- рендер ---------- */
  function renderLayers() {
    var el = document.getElementById("layers");
    if (!el) return;
    var html = "";
    for (var i = 0; i < LAYERS.length; i++) {
      var L = LAYERS[i];
      if (i > 0) html += '<div class="larrow">→</div>';
      html += '<div class="layer' + (L.hot ? " hot" : "") + '"><span class="rn">' + L.rn +
              '</span><h3>' + esc(t(L.k)) + '</h3><p>' + esc(t(L.d)) + '</p></div>';
    }
    el.innerHTML = html;
  }

  function renderTiles() {
    var el = document.getElementById("tiles");
    if (!el) return;
    var html = "";
    for (var i = 0; i < TILES.length; i++) {
      html += '<div class="tile"><span class="no">' + RN2[i] + '</span><h3>' +
              esc(t(TILES[i][0])) + '</h3><p>' + esc(t(TILES[i][1], { n: stat("dslCommands") })) + '</p></div>';
    }
    el.innerHTML = html;
  }

  function renderBattle() {
    var el = document.getElementById("battleGrid");
    if (!el) return;
    var battles = (DATA && DATA.battle) || [
      { id: "turn_battle_base", version: "2.0.0", deps: [] },
      { id: "card_battle", version: "1.0.0", deps: [] },
      { id: "ff_battle", version: "1.0.0", deps: [] }
    ];
    var html = "";
    for (var i = 0; i < battles.length; i++) {
      var b = battles[i];
      var titleKey = BATTLE_TITLE[b.id] || null;
      var descKey = BATTLE_DESC[b.id] || null;
      var deps = (b.deps || []).join(" · ");
      html += '<div class="bcard' + (b.id === BATTLE_ENGINE ? " engine" : "") + '">' +
              '<span class="id">' + (titleKey ? esc(t(titleKey)) : esc(b.id)) + '</span>' +
              '<span class="v">v' + esc(b.version) + '</span>' +
              '<p>' + (descKey ? esc(t(descKey)) : esc(b.desc || "")) + '</p>' +
              (deps ? '<div class="base">' + esc(t("depends")) + ': ' + esc(deps) + '</div>' : '') +
              '</div>';
    }
    el.innerHTML = html;
  }

  function colorScenario(text) {
    var lines = text.split("\n");
    var out = "";
    for (var i = 0; i < lines.length; i++) {
      var raw = lines[i];
      var L = esc(raw);
      if (/^\s*(#|\/\/)/.test(raw)) L = '<span class="cm">' + L + '</span>';
      else if (/^\s*(scene|priority|condition):/.test(raw)) L = L.replace(/^(\s*\w+:)/, '<span class="kw">$1</span>');
      else if (/^\s*:/.test(raw)) L = '<span class="lb">' + L + '</span>';
      else if (raw.indexOf('"') !== -1) L = L.replace(/&quot;([^&]*)&quot;/g, '<span class="st">&quot;$1&quot;</span>');
      out += L + "\n";
    }
    return out;
  }

  function renderDsl() {
    var code = document.getElementById("dslCode");
    if (!code) return;
    if (DATA && DATA.dslSample) {
      document.getElementById("dslFile").textContent = DATA.dslSample.file;
      code.innerHTML = colorScenario(DATA.dslSample.text);
    } else {
      code.innerHTML = '<span class="cm"># data/mods.json not loaded — run build-mods-catalog.py</span>';
    }
  }

  function renderMods() {
    var el = document.getElementById("modGrid");
    if (!el) return;
    if (!DATA || !DATA.mods) return;
    var html = "";
    for (var i = 0; i < DATA.mods.length; i++) {
      var m = DATA.mods[i];
      var chips = "";
      for (var c = 0; c < m.cat.length; c++) {
        var key = CAT_KEYS[m.cat[c]];
        chips += '<span class="chip">' + esc(key ? t(key) : m.cat[c]) + '</span>';
      }
      if (m.test) chips += '<span class="chip test">' + esc(t("cat_test")) + '</span>';
      var deps = (m.deps || []).join(" · ");
      html += '<div class="mod"><div class="row1"><span class="id">' + esc(m.id) +
              '</span><span class="v">v' + esc(m.version) + '</span><span class="chips">' + chips +
              '</span></div><p>' + esc(m.desc) + '</p>' +
              (deps ? '<div class="deps">← ' + esc(deps) + '</div>' : '') +
              '</div>';
    }
    el.innerHTML = html;
  }

  function setText(id, v) {
    var e = document.getElementById(id);
    if (e) e.textContent = v;
  }

  /* числа в разметке — статичный фолбэк; заголовок каталога переводится
     здесь, а не через data-i18n, потому что в нём число модов */
  function renderStats() {
    setText("modsTitle", t("mods_title", { n: stat("mods") }));
    if (!DATA || !DATA.stats) return;
    var s = DATA.stats;
    setText("statDsl", s.dslCommands);
    setText("statMods", s.mods);
    setText("statLoc", Math.round((s.coreLoc + s.modLoc) / 1000) + "K");
    setText("statBattle", (DATA.battle && DATA.battle.length) || 3);
  }

  function renderDynamic() {
    renderLayers();
    renderTiles();
    renderBattle();
    renderDsl();
    renderMods();
    renderStats();
  }

  /* ---------- init ---------- */
  document.addEventListener("DOMContentLoaded", function () {
    initHeaderChrome("home");
    renderDynamic();
    document.addEventListener("langchange", renderDynamic);

    fetch("/data/mods.json")
      .then(function (r) { if (!r.ok) throw new Error("HTTP " + r.status); return r.json(); })
      .then(function (data) {
        DATA = data;
        renderDynamic();
      })
      .catch(function () {
        /* file:// или нет data/mods.json — остаётся статический fallback */
      });
  });
})();
