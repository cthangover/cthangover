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

  /* build-mods-catalog.py обновляет числа в HTML; они же служат запасными значениями. */
  var FALLBACK_STATS = {
    mods: Number(document.getElementById("statMods").textContent),
    dslCommands: Number(document.getElementById("statDsl").textContent)
  };

  function stat(k) {
    return DATA && DATA.stats && DATA.stats[k] != null ? DATA.stats[k] : FALLBACK_STATS[k];
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

  /* Скриншоты галереи: site/make-shots.ps1 снимает их через игровые сценарии захвата,
     shots-to-img.py кладёт в img/shots/<suite>__<name>.jpg (+ .thumb.jpg). */
  var GAL_CATS = [
    ["all", "gal_all"], ["story", "gal_story"], ["battle", "gal_battle"],
    ["rpg", "gal_rpg"], ["stas", "gal_stas"], ["ui", "gal_ui"], ["tools", "gal_tools"]
  ];
  /* первые ALL_COUNT кадров — подборка вкладки «All», остальные видны во вкладках категорий */
  var ALL_COUNT = 12;
  var SHOTS = [
    { f: "site_shots__novel_wolf", c: "story", k: "s_novel_wolf", big: true },
    { f: "stas_case__01b_office_native128", c: "stas", k: "s_stas_office", big: true },
    { f: "card_played__battle_round4", c: "battle", k: "s_card_battle", big: true },
    { f: "ff_played__battle_round4", c: "battle", k: "s_ff_battle" },
    { f: "stas_case__04_mill_investigation", c: "stas", k: "s_stas_mill", big: true },
    { f: "stas_case__02_capital_gates", c: "stas", k: "s_stas_gates" },
    { f: "stas_world__market", c: "stas", k: "s_stas_market" },
    { f: "rpg_world__rpg_village", c: "rpg", k: "s_rpg_village", big: true },
    { f: "site_shots__panel_inventory", c: "ui", k: "s_inventory" },
    { f: "stas_case__06_journal_all_clues", c: "stas", k: "s_stas_journal" },
    { f: "trade__trade_trade", c: "ui", k: "s_trade" },
    { f: "rpg_world__rpg_plus_prologue", c: "story", k: "s_rp_prologue" },
    { f: "stas_world__riverside", c: "stas", k: "s_stas_river" },
    { f: "stas_case__01c_office_death_portrait", c: "stas", k: "s_stas_death" },
    { f: "stas_world__bureau", c: "stas", k: "s_stas_bureau" },
    { f: "site_shots__panel_map", c: "ui", k: "s_map" },
    { f: "rpg_world__rpg_plus_sheet", c: "rpg", k: "s_rp_sheet" },
    { f: "site_shots__novel_menu", c: "story", k: "s_novel_menu" },
    { f: "site_shots__panel_characters", c: "ui", k: "s_characters" },
    { f: "tools_windows__scenario_editor", c: "tools", k: "s_scenario_editor" },
    { f: "rpg_world__rpg_forest", c: "rpg", k: "s_forest" },
    { f: "menu_flow__game_picker", c: "ui", k: "s_picker" },
    { f: "site_shots__location_home_outside", c: "story", k: "s_home_outside" },
    { f: "rpg_world__rpg_house", c: "rpg", k: "s_house" },
    { f: "site_shots__panel_skills", c: "ui", k: "s_skills" },
    { f: "tools_windows__scene_editor", c: "tools", k: "s_scene_editor" },
    { f: "tools_windows__rpg_map_editor", c: "tools", k: "s_rpg_editor" },
    { f: "tools_windows__map_editor", c: "tools", k: "s_map_editor" },
    { f: "tools_windows__light_editor", c: "tools", k: "s_light_editor" },
    { f: "tools_windows__schema_data_editor", c: "tools", k: "s_data_editor" }
  ];
  var galCat = "all";
  var galVisible = [];
  var lbIndex = 0;
  var modCat = null;
  var modTabNames = [];

  var CAT_KEYS = { story:"cat_story", characters:"cat_characters", gameplay:"cat_gameplay",
    visual:"cat_visual", audio:"cat_audio", ui:"cat_ui", locale:"cat_locale", library:"cat_library",
    engine:"cat_engine", overhaul:"cat_overhaul", tools:"cat_tools", editor:"cat_editor", debug:"cat_debug", other:"cat_other" };
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

  function catLabel(category) {
    var identity = category.toLowerCase();
    var key = Object.prototype.hasOwnProperty.call(CAT_KEYS, identity) ? CAT_KEYS[identity] : null;
    var translated = key ? t(key) : category;
    return translated === key ? category : translated;
  }

  function renderModTabs() {
    var el = document.getElementById("modTabs");
    if (!el || !DATA || !DATA.mods) return;
    // Category text may also be "all", "constructor" or "__proto__".
    var counts = Object.create(null);
    var names = Object.create(null);
    var order = [];
    for (var i = 0; i < DATA.mods.length; i++) {
      var seen = Object.create(null);
      for (var c = 0; c < DATA.mods[i].cat.length; c++) {
        var name = DATA.mods[i].cat[c];
        var identity = name.toLowerCase();
        if (!(identity in counts)) { counts[identity] = 0; names[identity] = name; order.push(identity); }
        if (!seen[identity]) { counts[identity]++; seen[identity] = true; }
      }
    }
    order.sort(function (a, b) {
      if (a === "other") return 1;
      if (b === "other") return -1;
      var left = catLabel(names[a]).toLowerCase(), right = catLabel(names[b]).toLowerCase();
      return left < right ? -1 : left > right ? 1 : 0;
    });
    modTabNames = order.map(function (identity) { return names[identity]; });
    var html = '<button data-all="1" class="' + (modCat === null ? "on" : "") + '">' +
      esc(t("gal_all")) + '<span class="c">' + DATA.mods.length + '</span></button>';
    for (var o = 0; o < order.length; o++) {
      var identity = order[o];
      html += '<button data-category-index="' + o + '" class="' +
        (modCat !== null && modCat.toLowerCase() === identity ? "on" : "") + '">' +
        esc(catLabel(names[identity])) + '<span class="c">' + counts[identity] + '</span></button>';
    }
    el.innerHTML = html;
  }

  function renderMods() {
    var el = document.getElementById("modGrid");
    if (!el) return;
    if (!DATA || !DATA.mods) return;
    renderModTabs();
    var html = "";
    for (var i = 0; i < DATA.mods.length; i++) {
      var m = DATA.mods[i];
      if (modCat !== null && !m.cat.some(function (category) { return category.toLowerCase() === modCat.toLowerCase(); })) continue;
      var chips = "";
      for (var c = 0; c < m.cat.length; c++) {
        chips += '<span class="chip">' + esc(catLabel(m.cat[c])) + '</span>';
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

  /* ---------- галерея и лайтбокс ---------- */
  function shotSrc(s, thumb) {
    return "/img/shots/" + s.f + (thumb ? ".thumb.jpg" : ".jpg") + "?v=marao_retake_20261104T065313Z";
  }

  function renderGallery() {
    var tabs = document.getElementById("galTabs");
    var grid = document.getElementById("gallery-grid");
    if (!tabs || !grid) return;
    var html = "";
    for (var i = 0; i < GAL_CATS.length; i++) {
      var c = GAL_CATS[i][0];
      var n = 0;
      for (var j = 0; j < SHOTS.length; j++) if (c === "all" || SHOTS[j].c === c) n++;
      if (c === "all") n = Math.min(n, ALL_COUNT);
      html += '<button role="tab" data-c="' + c + '" class="' + (c === galCat ? "on" : "") + '">' +
              esc(t(GAL_CATS[i][1])) + '<span class="c">' + n + '</span></button>';
    }
    tabs.innerHTML = html;

    galVisible = [];
    for (var s = 0; s < SHOTS.length; s++) {
      if (galCat === "all" ? s < ALL_COUNT : SHOTS[s].c === galCat) galVisible.push(SHOTS[s]);
    }
    html = "";
    for (var v = 0; v < galVisible.length; v++) {
      var sh = galVisible[v];
      var big = sh.big && galCat === "all";
      html += '<figure class="shot' + (big ? " big" : "") + '" data-i="' + v + '">' +
              '<img src="' + shotSrc(sh, !big) + '" alt="' + esc(t(sh.k)) + '" loading="lazy">' +
              '<figcaption><span class="k">' + esc(t(catKey(sh.c))) + '</span>' + esc(t(sh.k)) + '</figcaption></figure>';
    }
    grid.innerHTML = html;
  }

  function catKey(c) {
    for (var i = 0; i < GAL_CATS.length; i++) if (GAL_CATS[i][0] === c) return GAL_CATS[i][1];
    return c;
  }

  function openLightbox(i) {
    var lb = document.getElementById("lightbox");
    if (!lb || !galVisible.length) return;
    lbIndex = (i + galVisible.length) % galVisible.length;
    var sh = galVisible[lbIndex];
    document.getElementById("lbImg").src = shotSrc(sh, false);
    document.getElementById("lbImg").alt = t(sh.k);
    document.getElementById("lbCap").textContent = t(sh.k);
    lb.hidden = false;
    document.body.style.overflow = "hidden";
  }

  function closeLightbox() {
    var lb = document.getElementById("lightbox");
    if (!lb) return;
    lb.hidden = true;
    document.body.style.overflow = "";
  }

  function initGallery() {
    var tabs = document.getElementById("galTabs");
    var grid = document.getElementById("gallery-grid");
    var mtabs = document.getElementById("modTabs");
    if (tabs) tabs.addEventListener("click", function (e) {
      var b = e.target.closest("button");
      if (!b) return;
      galCat = b.getAttribute("data-c");
      renderGallery();
    });
    if (mtabs) mtabs.addEventListener("click", function (e) {
      var b = e.target.closest("button");
      if (!b) return;
      modCat = b.hasAttribute("data-all") ? null : modTabNames[Number(b.getAttribute("data-category-index"))];
      renderMods();
    });
    if (grid) grid.addEventListener("click", function (e) {
      var f = e.target.closest(".shot");
      if (f) openLightbox(+f.getAttribute("data-i"));
    });
    var lb = document.getElementById("lightbox");
    if (!lb) return;
    document.getElementById("lbClose").addEventListener("click", closeLightbox);
    document.getElementById("lbPrev").addEventListener("click", function (e) { e.stopPropagation(); openLightbox(lbIndex - 1); });
    document.getElementById("lbNext").addEventListener("click", function (e) { e.stopPropagation(); openLightbox(lbIndex + 1); });
    lb.addEventListener("click", function (e) { if (e.target === lb) closeLightbox(); });
    document.addEventListener("keydown", function (e) {
      if (lb.hidden) return;
      if (e.key === "Escape") closeLightbox();
      else if (e.key === "ArrowLeft") openLightbox(lbIndex - 1);
      else if (e.key === "ArrowRight") openLightbox(lbIndex + 1);
    });
  }

  /* блоки с .reveal проявляются при прокрутке; без IntersectionObserver — сразу */
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
    if (DATA.battle) setText("statBattle", DATA.battle.length);
  }

  function renderDynamic() {
    renderGallery();
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
    initGallery();
    initReveal();
    document.addEventListener("langchange", renderDynamic);

    fetch("/data/mods.json", { cache: "no-cache" })
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
