/* ============================================================
   cthangover — i18n: UI-словарь (7 языков) + рантайм перевода.
   Строки ключей — данные для index/docs/quickstart страниц.
   Выбор языка: localStorage("cthangover_lang"), фолбэк en.
   ============================================================ */

var LANGS = [["ru","Русский"],["en","English"],["es","Español"],["zh","中文"],["ja","日本語"],["ko","한국어"],["vi","Tiếng Việt"]];
var I18N = {
ru:{qs_overview:"Обзор",nav_home:"Главная",nav_docs:"Документация",nav_quickstart:"Быстрый старт",nav_repo:"Репозиторий",
nav_gallery:"Галерея",cta_gallery:"Смотреть игру",gal_cap:"В игре",gal_title:"Как это выглядит",gal_sub:"Каждый кадр снят самой игрой: интеграционные тесты проходят её и сохраняют скриншоты, поэтому здесь нет ни одного макета.",gal_all:"Все",gal_story:"Новелла",gal_battle:"Бои",gal_rpg:"RPG-мир",gal_ui:"Интерфейс",gal_tools:"Редакторы",g_cth_d:"Визуальная новелла с пошаговыми карточными боями, квестами, готовкой, наймом и линией Мураками.",g_rpg_d:"Игра в духе RPG Maker: пиксельные деревня и лес, ходьба на WASD, сбор предметов, торговец и бои на FF-ядре.",g_nov_d:"Самая маленькая играбельная новелла: город, бар и дом из текста, фонов и переходов — отправная точка моддера.",dsl_p3:"Литералы сценариев пишутся по-русски; пример показан с английским переводом мода из scenarios_en.properties.",s_novel_wolf:"Сценарий задаёт фон и ведёт рассказ",s_card_battle:"Карточный бой: Марао и Мураками против двух волков",s_rpg_village:"RPG-демо: деревня на тайловой карте",s_novel_menu:"Выбор у въезда в город",s_home_outside:"Дом Марао",s_livingroom:"Гостиная",s_outskirts:"Окраина города",s_ff_battle:"Бой на ядре в стиле Final Fantasy",s_victory:"Экран победы с опытом и добычей",s_forest:"Лес: что можно подобрать",s_house:"Дом старосты",s_trade:"Окно торговли: два инвентаря",s_inventory:"Инвентарь",s_characters:"Партия и карты действий",s_map:"Карта мира",s_skills:"Коллекция навыков",s_picker:"Выбор игры: один движок, несколько игр",s_scenario_editor:"Редактор сценариев",s_scene_editor:"Редактор сцен",s_rpg_editor:"Редактор тайловых карт",s_map_editor:"Редактор карты мира",s_light_editor:"Редактор света",
kicker:"мод-ориентированный движок · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"Игровой движок, который",hero_h1b:"собирают моды",
hero_tag:"Ядро Core/ не содержит ни одной игры — и даже жанра. При запуске оно загружает моды из mods/, компилирует их C#-код через Roslyn и собирает из них игру: визуальную новеллу, RPG на тайловых картах, бои, диалоги. Каждая механика — обычная папка, которую можно открыть, изменить и убрать.",
cta_start:"Начать за 5 минут",cta_docs:"Документация",
st_dsl_l:"команд DSL",st_mods_l:"модов в репозитории",st_loc_l:"строк C# ядра и модов",st_battle_l:"боевых системы",
arch_cap:"Сборочная линия",arch_title:"Игра собирается слоями",
arch_l1t:"Ядро · Core/",arch_l1d:"Сценарии DSL, реестр модов, сохранения, события, ресурсы, UI, аудио. Контента нет — только сервисы.",
arch_l2t:"Движковые моды · *_base",arch_l2d:"2D-оболочка, диалоги и сценарный DSL, персонажи, предметы, квесты, пошаговые бои, тайловый мир — каждый такой мод игра берёт или не берёт.",
arch_l3t:"Игра · коллекция",arch_l3d:"Игра — это коллекция: собственный мод контента плюс выбранные механики — новелла с боями, прогулка в духе RPG Maker, шаблон.",
arch_l4t:"Ваш мод",arch_l4d:"Линия сюжета, механика или новые правила боя — обычная папка в mods/.",
feat_cap:"Возможности",feat_title:"Что уже в ядре",
f1t:"Roslyn в рантайме",f1d:"C#-исходники модов компилируются при загрузке; кэш по хэшу содержимого.",
f2t:"tscn-патчи сцен",f2d:"Мод достраивает чужую сцену: узлы, свойства, связи между нодами.",
f3t:"JSON-патчи данных",f3d:"$set · $add · $remove, wildcard-пути, слияние массивов по ключу.",
f4t:"DSL сценариев",f4d:"{n} команд, условия в шапке сцены, метки и выборы с ключами локализации.",
f5t:"Шина событий",f5d:"20 типизированных событий C#, мост в GDScript через [EventKey].",
f6t:"Сохранения по слотам",f6d:"У каждого мода свой слот IModSaveable: версии, миграции, скриншоты.",
f7t:"Карта и время суток",f7d:"Зоны карты и четыре фазы суток — управляются прямо из сценария.",
f8t:"Mod Manager в игре",f8d:"Каталог, фильтры, установка, обновление и удаление модов без выхода из игры.",
battle_cap:"Правила боя",battle_title:"Боёвка — тоже подключаемый модуль",
battle_desc:"Движок turn_battle_base задаёт контракты правил; card_battle и ff_battle реализуют их независимо. Нужен свой режим — ещё один мод поверх того же движка.",
turn_t:"turn_battle_base",turn_d:"Контракты ruleset'ов, реестр, исполнители действий, статус-эффекты, экраны победы/поражения, отчёты опыта и лута.",
card_t:"card_battle",card_d:"Бой картами поверх общего движка.",
ff_t:"ff_battle",ff_d:"Классический пошаговый бой в стиле Final Fantasy.",
dsl_cap:"DSL · .scenario",dsl_title:"Сценарий — текстовый файл",
dsl_p1:"Сцена описывается текстом: шапка с указанием сцены-владельца, приоритетом и условием запуска, дальше — команды с метками и переходами. Реплики несут ключи локализации, поэтому перевод подключается файлами .properties.",
dsl_p2:"Никакого редактора: scenario-файлы правятся в том же редакторе, что и код. Синтаксис расширяется — новые команды это новые стратегии в моде.",
mods_cap:"Каталог",mods_title:"{n} модов уже в репозитории",
mods_note:"Названия и описания — как в manifest.json (english). Тестовые моды помечены.",
cat_engine:"движок",cat_gameplay:"геймплей",cat_story:"сюжет",cat_library:"библиотека",cat_ui:"интерфейс",cat_test:"тест",cat_tools:"инструменты",
story_cap:"Игры",story_title:"Один движок — разные игры",
story_p1:"Cthangover — визуальная новелла с пошаговыми боями и линией Мураками. RPG-демо — пиксельные деревня и лес, по которым ходишь на WASD и дерёшься на тех же боевых ядрах. Шаблон новеллы — самая маленькая играбельная новелла. Все они переключаются внутри одного запущенного процесса.",
story_p2:"Новая игра — это новая коллекция: ваш мод контента со сценами, персонажами и переводами плюс нужные механики. Ядро не знает ни про одну из них — и в этом смысл.",
shots_cap:"Кадры",shots_title:"Скриншоты",
cta_title:"Открытый код — открытый конвейер",
cta_p:"Проект распространяется по MIT. Репозиторий на собственном Gitea: ядро, моды, документация и сайт собираются из одного дерева исходников.",
cta_repo:"Открыть репозиторий",cta_license:"Лицензия MIT",
qs_title:"Быстрые старты",qs_sub:"Три трека — каждый ведёт к работающему результату. Материалы треков — обычные Markdown-файлы; перевод подключается по мере готовности, иначе честно показывается английский.",
qs_l1k:"Трек 1",qs_l1t:"Игрок",qs_l1d:"Усильте персонажа Марao JSON-патчем — без единой строчки кода.",qs_m1:"JSON-патч · 5 минут",
qs_l2k:"Трек 2",qs_l2t:"Моддер",qs_l2d:"Своя сцена: диалоги, выборы и переходы на DSL.",qs_m2:"JSON + DSL · 15 минут",
qs_l3k:"Трек 3",qs_l3t:"Программист",qs_l3d:"C#-мод: кастомное действие, настройки в меню, подписки на события.",qs_m3:"C# + Godot API · 20 минут",
qs_lang:"Язык материала",fallback_note:"материал показан на английском",
search_ph:"Поиск по разделам…",toc:"На этой странице",prev:"Назад",next:"Далее",
built_from:"собрано из",not_translated:"раздел доступен на английском",
on_git:"править в Gitea",depends:"зависит от",docs_sections:"Разделы",license:"Лицензия MIT",
built_by:"собрано из",md_files:"Markdown-файлов",
not_found:"Раздел ещё не написан",docs_here:"Документация — на английском: дерево слева, перевод подключается по мере готовности."},

en:{qs_overview:"Overview",nav_home:"Home",nav_docs:"Documentation",nav_quickstart:"Quick Starts",nav_repo:"Repository",
nav_gallery:"Gallery",cta_gallery:"See the game",gal_cap:"In the game",gal_title:"See it running",gal_sub:"Every frame here is taken by the game itself: integration tests play it and save screenshots, so the page never shows a mock-up.",gal_all:"All",gal_story:"Novel",gal_battle:"Battles",gal_rpg:"RPG world",gal_ui:"Interface",gal_tools:"Editors",g_cth_d:"A visual novel with turn-based card battles, quests, cooking, recruitment and Murakami's story line.",g_rpg_d:"An RPG Maker style game: a pixel village and forest walked with WASD, gathering, a merchant and battles on the FF core.",g_nov_d:"The smallest playable novel: a town, a bar and a home made of text, backgrounds and scene switches — the modder's starting point.",dsl_p3:"Scenario literals are written in Russian; the sample shows them with the mod's English translation from scenarios_en.properties.",s_novel_wolf:"A scenario sets the background and narrates",s_card_battle:"Card battle: Marao and Murakami against two wolves",s_rpg_village:"RPG Demo: the village on a tile map",s_novel_menu:"A choice at the town entry",s_home_outside:"Marao's house",s_livingroom:"The living room",s_outskirts:"Town outskirts",s_ff_battle:"Battle on the Final Fantasy style core",s_victory:"Victory screen with EXP and loot",s_forest:"The forest: things to pick up",s_house:"The elder's house",s_trade:"Trade window: two inventories",s_inventory:"Inventory",s_characters:"Party and action cards",s_map:"World map",s_skills:"Skill collection",s_picker:"Game picker: one engine, several games",s_scenario_editor:"Scenario editor",s_scene_editor:"Scene editor",s_rpg_editor:"Tile map editor",s_map_editor:"World map editor",s_light_editor:"Light editor",
kicker:"mod-oriented engine · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"A game engine",hero_h1b:"assembled from mods",
hero_tag:"The Core/ kernel ships no game at all — not even a genre. At boot it loads mods from mods/, compiles their C# through Roslyn and assembles the game from them: a visual novel, an RPG on tile maps, battles, dialogues. Every mechanic is a plain folder you can open, edit and remove.",
cta_start:"Start in 5 minutes",cta_docs:"Documentation",
st_dsl_l:"DSL commands",st_mods_l:"mods in the repository",st_loc_l:"lines of C#, kernel and mods",st_battle_l:"battle rulesets",
arch_cap:"Assembly line",arch_title:"The game is built in layers",
arch_l1t:"Kernel · Core/",arch_l1d:"Scenario DSL, mod registry, saves, events, resources, UI hosting, audio. No content — services only.",
arch_l2t:"Engine mods · *_base",arch_l2d:"The 2D shell, dialogues and the scenario DSL, characters, items, quests, turn-based battles, the tile world — each is a mod a game may take or leave.",
arch_l3t:"Game · collection",arch_l3d:"A game is a collection: its own content mod plus the mechanics it picks — a novel with battles, an RPG Maker style walk, a template.",
arch_l4t:"Your mod",arch_l4d:"A story line, a mechanic or new battle rules — a plain folder in mods/.",
feat_cap:"Features",feat_title:"Already in the kernel",
f1t:"Roslyn at runtime",f1d:"Mod C# sources compile on load; the cache is keyed by content hash.",
f2t:"tscn scene patches",f2d:"A mod extends someone else's scene: nodes, properties, connections.",
f3t:"JSON data patches",f3d:"$set · $add · $remove, wildcard paths, key-based array merging.",
f4t:"Scenario DSL",f4d:"{n} commands, conditions in the scene header, labels and localized choices.",
f5t:"Event bus",f5d:"20 typed C# events, GDScript bridge via [EventKey].",
f6t:"Slot-based saves",f6d:"Every mod gets its own IModSaveable slot: versions, migrations, screenshots.",
f7t:"Map and time of day",f7d:"Map zones and four day phases — driven right from a scenario.",
f8t:"In-game Mod Manager",f8d:"Catalog, filters, install, update and remove mods without leaving the game.",
battle_cap:"Battle rules",battle_title:"Combat is a pluggable module too",
battle_desc:"turn_battle_base defines the ruleset contracts; card_battle and ff_battle implement them independently. Your own mode is one more mod on the same engine.",
turn_t:"turn_battle_base",turn_d:"Ruleset contracts, core registry, action executors, status effects, victory/defeat screens, EXP and loot reports.",
card_t:"card_battle",card_d:"Card battle on top of the shared engine.",
ff_t:"ff_battle",ff_d:"Classic turn-based combat in the Final Fantasy style.",
dsl_cap:"DSL · .scenario",dsl_title:"A scenario is a text file",
dsl_p1:"A scene is plain text: a header naming the owner scene, priority and launch condition, then commands with labels and jumps. Lines carry localization keys, so translations arrive as .properties files.",
dsl_p2:"No editor required: scenario files are edited in the same editor as the code. The syntax is extensible — a new command is a new strategy class in a mod.",
mods_cap:"Catalog",mods_title:"{n} mods already in the repository",
mods_note:"Names and descriptions exactly as in manifest.json (english). Test mods are tagged.",
cat_engine:"engine",cat_gameplay:"gameplay",cat_story:"story",cat_library:"library",cat_ui:"interface",cat_test:"test",cat_tools:"tools",
story_cap:"Games",story_title:"One engine, different games",
story_p1:"Cthangover is a visual novel with turn-based battles and Murakami's story line. RPG Demo walks a pixel village and forest with WASD and fights on the same battle cores. Novel Template is the smallest playable novel. All of them switch inside one running process.",
story_p2:"A new game is a new collection: your content mod with its scenes, characters and localizations, plus the mechanics it needs. The kernel knows nothing about any of them — that is the point.",
shots_cap:"Frames",shots_title:"Screenshots",
cta_title:"Open code, open pipeline",
cta_p:"The project is MIT-licensed. The repository lives on a self-hosted Gitea: kernel, mods, documentation and this site are built from one source tree.",
cta_repo:"Open repository",cta_license:"MIT License",
qs_title:"Quick starts",qs_sub:"Three tracks — each ends with a working result. Track materials are plain Markdown files; translations arrive when ready, otherwise English is shown honestly.",
qs_l1k:"Track 1",qs_l1t:"Player",qs_l1d:"Buff the character Marao with a JSON patch — zero code.",qs_m1:"JSON patch · 5 minutes",
qs_l2k:"Track 2",qs_l2t:"Modder",qs_l2d:"Your own scene: dialogue, choices and jumps in DSL.",qs_m2:"JSON + DSL · 15 minutes",
qs_l3k:"Track 3",qs_l3t:"Programmer",qs_l3d:"A C# mod: custom action, menu settings, event subscriptions.",qs_m3:"C# + Godot API · 20 minutes",
qs_lang:"Material language",fallback_note:"material shown in English",
search_ph:"Search sections…",toc:"On this page",prev:"Previous",next:"Next",
built_from:"built from",not_translated:"section available in English",
on_git:"edit on Gitea",depends:"depends on",docs_sections:"Sections",license:"MIT License",
built_by:"assembled from",md_files:"Markdown files",
not_found:"Section not written yet",docs_here:"Documentation is English-first: tree on the left, translations arrive as they land."},

es:{qs_overview:"Resumen",nav_home:"Inicio",nav_docs:"Documentación",nav_quickstart:"Inicios rápidos",nav_repo:"Repositorio",
nav_gallery:"Galería",cta_gallery:"Ver el juego",gal_cap:"En el juego",gal_title:"Así se ve en marcha",gal_sub:"Cada imagen la toma el propio juego: las pruebas de integración lo juegan y guardan capturas, así que aquí no hay maquetas.",gal_all:"Todo",gal_story:"Novela",gal_battle:"Combates",gal_rpg:"Mundo RPG",gal_ui:"Interfaz",gal_tools:"Editores",g_cth_d:"Una novela visual con combates de cartas por turnos, misiones, cocina, reclutamiento y la historia de Murakami.",g_rpg_d:"Un juego al estilo RPG Maker: una aldea y un bosque en pixel art recorridos con WASD, recolección, un mercader y combates con el núcleo FF.",g_nov_d:"La novela jugable más pequeña: una ciudad, un bar y una casa hechos de texto, fondos y cambios de escena; el punto de partida del modder.",dsl_p3:"Los literales de los escenarios están en ruso; el ejemplo los muestra con la traducción inglesa del mod (scenarios_en.properties).",s_novel_wolf:"El escenario fija el fondo y narra",s_card_battle:"Combate de cartas: Marao y Murakami contra dos lobos",s_rpg_village:"RPG Demo: la aldea en un mapa de tiles",s_novel_menu:"Una elección a la entrada de la ciudad",s_home_outside:"La casa de Marao",s_livingroom:"La sala de estar",s_outskirts:"Las afueras",s_ff_battle:"Combate con el núcleo estilo Final Fantasy",s_victory:"Pantalla de victoria con EXP y botín",s_forest:"El bosque: cosas para recoger",s_house:"La casa del anciano",s_trade:"Ventana de comercio: dos inventarios",s_inventory:"Inventario",s_characters:"Grupo y cartas de acción",s_map:"Mapa del mundo",s_skills:"Colección de habilidades",s_picker:"Selector de juego: un motor, varios juegos",s_scenario_editor:"Editor de escenarios",s_scene_editor:"Editor de escenas",s_rpg_editor:"Editor de mapas de tiles",s_map_editor:"Editor del mapa del mundo",s_light_editor:"Editor de luces",
kicker:"motor orientado a mods · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"Un motor de juego",hero_h1b:"ensamblado con mods",
hero_tag:"El núcleo Core/ no trae ningún juego, ni siquiera un género. Al arrancar carga los mods de mods/, compila su C# con Roslyn y monta con ellos el juego: una novela visual, un RPG sobre mapas de tiles, combates, diálogos. Cada mecánica es una carpeta normal que puedes abrir, editar y quitar.",
cta_start:"Empieza en 5 minutos",cta_docs:"Documentación",
st_dsl_l:"comandos DSL",st_mods_l:"mods en el repositorio",st_loc_l:"líneas de C# núcleo y mods",st_battle_l:"sistemas de combate",
arch_cap:"Línea de montaje",arch_title:"El juego se construye por capas",
arch_l1t:"Núcleo · Core/",arch_l1d:"DSL de escenas, registro de mods, guardados, eventos, recursos, UI y audio. Sin contenido: solo servicios.",
arch_l2t:"Mods de motor · *_base",arch_l2d:"El shell 2D, los diálogos y el DSL de escenarios, personajes, objetos, misiones, combates por turnos, el mundo de tiles: cada uno es un mod que un juego toma o deja.",
arch_l3t:"Juego · colección",arch_l3d:"Un juego es una colección: su propio mod de contenido más las mecánicas que elige — una novela con combates, un paseo al estilo RPG Maker, una plantilla.",
arch_l4t:"Tu mod",arch_l4d:"Una línea argumental, una mecánica o reglas de combate nuevas: una carpeta más en mods/.",
feat_cap:"Características",feat_title:"Ya en el núcleo",
f1t:"Roslyn en runtime",f1d:"El C# de los mods se compila al cargar; caché por hash del contenido.",
f2t:"Parches tscn",f2d:"Un mod amplía la escena de otro: nodos, propiedades, conexiones.",
f3t:"Parches JSON",f3d:"$set · $add · $remove, rutas wildcard, fusión de arrays por clave.",
f4t:"DSL de escenas",f4d:"{n} comandos, condiciones en la cabecera, etiquetas y opciones localizadas.",
f5t:"Bus de eventos",f5d:"20 eventos tipados de C#, puente a GDScript vía [EventKey].",
f6t:"Guardados por slots",f6d:"Cada mod tiene su slot IModSaveable: versiones, migraciones, capturas.",
f7t:"Mapa y hora del día",f7d:"Zonas de mapa y cuatro fases del día, controladas desde el guion.",
f8t:"Mod Manager en el juego",f8d:"Catálogo, filtros, instalar, actualizar y borrar mods sin salir del juego.",
battle_cap:"Reglas de combate",battle_title:"El combate también es un módulo enchufable",
battle_desc:"turn_battle_base define los contratos; card_battle y ff_battle los implementan por separado. Tu propio modo es un mod más sobre el mismo motor.",
turn_t:"turn_battle_base",turn_d:"Contratos de rulesets, registro, ejecutores de acciones, estados, pantallas de victoria/derrota, informes de EXP y botín.",
card_t:"card_battle",card_d:"Combate por cartas sobre el motor compartido.",
ff_t:"ff_battle",ff_d:"Combate por turnos clásico al estilo Final Fantasy.",
dsl_cap:"DSL · .scenario",dsl_title:"Un guion es un archivo de texto",
dsl_p1:"Una escena es texto plano: cabecera con escena propietaria, prioridad y condición de arranque; después, comandos con etiquetas y saltos. Las líneas llevan claves de localización: las traducciones llegan como .properties.",
dsl_p2:"Sin editor: los .scenario se editan con el mismo editor que el código. La sintaxis es extensible: un comando nuevo es una estrategia nueva en un mod.",
mods_cap:"Catálogo",mods_title:"{n} mods ya en el repositorio",
mods_note:"Nombres y descripciones tal cual en manifest.json (inglés). Los mods de prueba van etiquetados.",
cat_engine:"motor",cat_gameplay:"jugabilidad",cat_story:"historia",cat_library:"librería",cat_ui:"interfaz",cat_test:"prueba",cat_tools:"herramientas",
story_cap:"Juegos",story_title:"Un motor, juegos distintos",
story_p1:"Cthangover es una novela visual con combates por turnos y la línea de Murakami. RPG Demo recorre un pueblo y un bosque pixelados con WASD y combate con los mismos núcleos de batalla. Novel Template es la novela jugable más pequeña. Todos cambian dentro de un mismo proceso en ejecución.",
story_p2:"Un juego nuevo es una colección nueva: tu mod de contenido con sus escenas, personajes y traducciones, más las mecánicas que necesita. El núcleo no sabe nada de ninguno, y esa es la idea.",
shots_cap:"Fotogramas",shots_title:"Capturas",
cta_title:"Código abierto, pipeline abierto",
cta_p:"El proyecto se distribuye bajo MIT. El repositorio vive en un Gitea propio: núcleo, mods, documentación y este sitio se construyen de un mismo árbol de fuentes.",
cta_repo:"Abrir repositorio",cta_license:"Licencia MIT",
qs_title:"Inicios rápidos",qs_sub:"Tres caminos — cada uno termina con un resultado funcional. Los materiales son archivos Markdown; la traducción llega cuando esté lista, si no se muestra el inglés con honestidad.",
qs_l1k:"Camino 1",qs_l1t:"Jugador",qs_l1d:"Mejora al personaje Marao con un parche JSON — sin código.",qs_m1:"Parche JSON · 5 minutos",
qs_l2k:"Camino 2",qs_l2t:"Modder",qs_l2d:"Tu propia escena: diálogo, decisiones y saltos en DSL.",qs_m2:"JSON + DSL · 15 minutos",
qs_l3k:"Camino 3",qs_l3t:"Programador",qs_l3d:"Un mod C#: acción propia, ajustes en el menú, suscripciones a eventos.",qs_m3:"C# + Godot API · 20 minutos",
qs_lang:"Idioma del material",fallback_note:"material mostrado en inglés",
search_ph:"Buscar secciones…",toc:"En esta página",prev:"Anterior",next:"Siguiente",
built_from:"generado desde",not_translated:"sección disponible en inglés",
on_git:"editar en Gitea",depends:"depende de",docs_sections:"Secciones",license:"Licencia MIT",
built_by:"generado a partir de",md_files:"archivos Markdown",
not_found:"Sección aún sin escribir",docs_here:"La documentación está en inglés: árbol a la izquierda; las traducciones llegan según estén listas."},

zh:{qs_overview:"总览",nav_home:"首页",nav_docs:"文档",nav_quickstart:"快速上手",nav_repo:"代码仓库",
nav_gallery:"画廊",cta_gallery:"查看游戏",gal_cap:"游戏内",gal_title:"运行中的样子",gal_sub:"这里的每一帧都由游戏自己截取：集成测试会实际游玩并保存截图，所以页面上没有任何效果图。",gal_all:"全部",gal_story:"小说",gal_battle:"战斗",gal_rpg:"RPG 世界",gal_ui:"界面",gal_tools:"编辑器",g_cth_d:"一部带有回合制卡牌战斗、任务、烹饪、招募以及村上故事线的视觉小说。",g_rpg_d:"RPG Maker 风格的游戏：用 WASD 走遍像素村庄和森林，采集物品、与商人交易，并使用 FF 核心战斗。",g_nov_d:"最小的可玩小说：由文本、背景和场景切换组成的城镇、酒吧和住宅——模组作者的起点。",dsl_p3:"剧本中的字面文本是俄语；示例展示的是该模组 scenarios_en.properties 中的英文翻译。",s_novel_wolf:"剧本设置背景并叙述",s_card_battle:"卡牌战斗：马拉奥和村上对战两只狼",s_rpg_village:"RPG Demo：瓦片地图上的村庄",s_novel_menu:"城镇入口处的选择",s_home_outside:"马拉奥的家",s_livingroom:"客厅",s_outskirts:"城镇郊外",s_ff_battle:"Final Fantasy 风格核心的战斗",s_victory:"胜利画面：经验与战利品",s_forest:"森林：可拾取的物品",s_house:"村长的家",s_trade:"交易窗口：两个背包",s_inventory:"背包",s_characters:"队伍与行动卡",s_map:"世界地图",s_skills:"技能收藏",s_picker:"游戏选择：一个引擎，多款游戏",s_scenario_editor:"剧本编辑器",s_scene_editor:"场景编辑器",s_rpg_editor:"瓦片地图编辑器",s_map_editor:"世界地图编辑器",s_light_editor:"光照编辑器",
kicker:"面向 Mod 的引擎 · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"一款",hero_h1b:"由模组拼成的游戏引擎",
hero_tag:"Core/ 内核不包含任何游戏，甚至不限定类型。启动时它从 mods/ 加载模组，用 Roslyn 编译其 C# 代码，再用它们组装出游戏：视觉小说、瓦片地图上的 RPG、战斗、对话。每个机制都是一个普通文件夹，可以打开、修改和删除。",
cta_start:"5 分钟上手",cta_docs:"文档",
st_dsl_l:"条 DSL 指令",st_mods_l:"个仓库内模组",st_loc_l:"行 C#（内核+模组）",st_battle_l:"套战斗规则",
arch_cap:"装配线",arch_title:"游戏分层组装",
arch_l1t:"内核 · Core/",arch_l1d:"场景 DSL、模组注册表、存档、事件、资源、UI、音频。没有内容——只有服务。",
arch_l2t:"引擎模组 · *_base",arch_l2d:"2D 外壳、对话与剧本 DSL、角色、物品、任务、回合制战斗、瓦片世界——每一个都是游戏可选可弃的模组。",
arch_l3t:"游戏 · 合集",arch_l3d:"游戏就是一个合集：它自己的内容模组，加上所选的机制——带战斗的小说、RPG Maker 风格的漫游、模板。",
arch_l4t:"你的模组",arch_l4d:"一条剧情线、一个机制或新的战斗规则——mods/ 里多一个文件夹而已。",
feat_cap:"特性",feat_title:"内核已具备",
f1t:"运行时 Roslyn",f1d:"模组的 C# 源码在加载时编译，按内容哈希缓存。",
f2t:"tscn 场景补丁",f2d:"模组可以扩建别人的场景：节点、属性、连接。",
f3t:"JSON 数据补丁",f3d:"$set · $add · $remove、通配符路径、按键合并数组。",
f4t:"场景 DSL",f4d:"{n} 条指令、场景头条件、标签与带本地化键的选项。",
f5t:"事件总线",f5d:"20 个 C# 类型化事件，经 [EventKey] 桥接 GDScript。",
f6t:"分槽存档",f6d:"每个模组有自己的 IModSaveable 槽位：版本、迁移、截图。",
f7t:"地图与昼夜",f7d:"地图区域与四个时段——直接由剧本驱动。",
f8t:"游戏内 Mod Manager",f8d:"目录、筛选、安装、更新与删除模组，无需退出游戏。",
battle_cap:"战斗规则",battle_title:"战斗也是可插拔模块",
battle_desc:"turn_battle_base 定义规则契约；card_battle 与 ff_battle 各自独立实现。想要自己的模式？在同一引擎上再写一个模组。",
turn_t:"turn_battle_base",turn_d:"规则契约、核心注册表、行动执行器、状态效果、胜负画面、经验与掉落报告。",
card_t:"card_battle",card_d:"构建于共用引擎之上的卡牌战斗。",
ff_t:"ff_battle",ff_d:"经典最终幻想风回合制战斗。",
dsl_cap:"DSL · .scenario",dsl_title:"剧本就是文本文件",
dsl_p1:"一个场景就是纯文本：头部声明所属场景、优先级与触发条件，随后是指令、标签与跳转。台词带本地化键，翻译以 .properties 文件接入。",
dsl_p2:"不需要编辑器：.scenario 与代码用同一个编辑器修改。语法可扩展——新指令就是模组里一个新的策略类。",
mods_cap:"目录",mods_title:"仓库中已有 {n} 个模组",
mods_note:"名称与描述与 manifest.json 一致（英文）。测试模组已标注。",
cat_engine:"引擎",cat_gameplay:"玩法",cat_story:"剧情",cat_library:"库",cat_ui:"界面",cat_test:"测试",cat_tools:"工具",
story_cap:"游戏",story_title:"一个引擎，不同的游戏",
story_p1:"Cthangover 是带回合制战斗和村上线的视觉小说。RPG Demo 用 WASD 走遍像素村庄和森林，并使用同样的战斗核心。Novel Template 是最小的可玩小说。它们都在同一个运行中的进程内切换。",
story_p2:"新游戏就是新合集：你的内容模组（场景、角色、翻译）加上需要的机制。内核对它们一无所知——这正是关键。",
shots_cap:"画面",shots_title:"截图",
cta_title:"开放代码，开放流水线",
cta_p:"项目以 MIT 许可发布。仓库托管在自建 Gitea 上：内核、模组、文档与本网站出自同一棵源码树。",
cta_repo:"打开仓库",cta_license:"MIT 许可证",
qs_title:"快速上手",qs_sub:"三条路线——每条都以可运行的成果收尾。路线材料是普通 Markdown 文件；翻译就绪后接入，否则如实显示英文。",
qs_l1k:"路线 1",qs_l1t:"玩家",qs_l1d:"用 JSON 补丁强化角色 Marao——无需任何代码。",qs_m1:"JSON 补丁 · 5 分钟",
qs_l2k:"路线 2",qs_l2t:"模组制作者",qs_l2d:"自己的场景：用 DSL 写对话、选项与跳转。",qs_m2:"JSON + DSL · 15 分钟",
qs_l3k:"路线 3",qs_l3t:"程序员",qs_l3d:"C# 模组：自定义动作、菜单设置、事件订阅。",qs_m3:"C# + Godot API · 20 分钟",
qs_lang:"材料语言",fallback_note:"材料以英文显示",
search_ph:"搜索章节…",toc:"本页目录",prev:"上一页",next:"下一页",
built_from:"构建自",not_translated:"该章节仅提供英文",
on_git:"在 Gitea 中编辑",depends:"依赖",docs_sections:"章节",license:"MIT 许可证",
built_by:"由",md_files:"个 Markdown 文件构建",
not_found:"章节尚未编写",docs_here:"文档以英文为主：左侧为目录树；翻译就绪后接入。"},

ja:{qs_overview:"概要",nav_home:"ホーム",nav_docs:"ドキュメント",nav_quickstart:"クイックスタート",nav_repo:"リポジトリ",
nav_gallery:"ギャラリー",cta_gallery:"ゲームを見る",gal_cap:"ゲーム内",gal_title:"動いている様子",gal_sub:"ここにある画像はすべてゲーム自身が撮影したものです。統合テストが実際にプレイしてスクリーンショットを保存するので、モックアップは一枚もありません。",gal_all:"すべて",gal_story:"ノベル",gal_battle:"バトル",gal_rpg:"RPG ワールド",gal_ui:"インターフェース",gal_tools:"エディター",g_cth_d:"ターン制カードバトル、クエスト、料理、仲間集め、そして村上のストーリーラインを備えたビジュアルノベル。",g_rpg_d:"RPG Maker 風のゲーム：WASD で歩くピクセルの村と森、採集、商人、FF コアでのバトル。",g_nov_d:"最小のプレイ可能なノベル：テキスト・背景・シーン切り替えだけで作られた町、バー、家。モッダーの出発点です。",dsl_p3:"シナリオのリテラルはロシア語で書かれています。サンプルは MOD の scenarios_en.properties にある英訳で表示しています。",s_novel_wolf:"シナリオが背景を設定し語る",s_card_battle:"カードバトル：マラオと村上 vs. 二匹の狼",s_rpg_village:"RPG Demo：タイルマップ上の村",s_novel_menu:"町の入口での選択",s_home_outside:"マラオの家",s_livingroom:"リビング",s_outskirts:"町はずれ",s_ff_battle:"Final Fantasy 風コアのバトル",s_victory:"経験値と戦利品の勝利画面",s_forest:"森：拾えるもの",s_house:"村長の家",s_trade:"取引ウィンドウ：二つのインベントリ",s_inventory:"インベントリ",s_characters:"パーティとアクションカード",s_map:"ワールドマップ",s_skills:"スキルコレクション",s_picker:"ゲーム選択：一つのエンジン、複数のゲーム",s_scenario_editor:"シナリオエディター",s_scene_editor:"シーンエディター",s_rpg_editor:"タイルマップエディター",s_map_editor:"ワールドマップエディター",s_light_editor:"ライトエディター",
kicker:"Mod指向エンジン · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"Modが組み上げる",hero_h1b:"ゲームエンジン",
hero_tag:"Core/ カーネルにはゲームが一つも含まれず、ジャンルさえ決まっていません。起動時に mods/ からModを読み込み、C# を Roslyn でコンパイルし、それらからゲームを組み立てます：ビジュアルノベル、タイルマップのRPG、バトル、会話。どのメカニクスも、開いて編集し削除できる普通のフォルダです。",
cta_start:"5分で開始",cta_docs:"ドキュメント",
st_dsl_l:"個のDSLコマンド",st_mods_l:"個のModを同梱",st_loc_l:"行のC#（本体+Mod）",st_battle_l:"種類の戦闘ルール",
arch_cap:"組み立てライン",arch_title:"ゲームはレイヤーで組み上がる",
arch_l1t:"カーネル · Core/",arch_l1d:"シナリオDSL・Modレジストリ・セーブ・イベント・リソース・UI・オーディオ。コンテントなし、サービスだけ。",
arch_l2t:"エンジンMod · *_base",arch_l2d:"2Dシェル、会話とシナリオDSL、キャラクター、アイテム、クエスト、ターン制バトル、タイルワールド——どれもゲームが選んで使うModです。",
arch_l3t:"ゲーム · コレクション",arch_l3d:"ゲームはコレクションです：独自のコンテンツModと選んだメカニクス——バトル付きノベル、RPGツクール風の探索、テンプレート。",
arch_l4t:"あなたのMod",arch_l4d:"新しい物語線・メカニクス・戦闘ルールは、mods/ の普通のフォルダ。",
feat_cap:"機能",feat_title:"カーネルに既にあるもの",
f1t:"ランタイム Roslyn",f1d:"ModのC#ソースをロード時にコンパイル。内容ハッシュでキャッシュ。",
f2t:"tscnシーンパッチ",f2d:"他のModのシーンを拡張：ノード・プロパティ・接続。",
f3t:"JSONパッチ",f3d:"$set · $add · $remove、ワイルドカード、キーによる配列マージ。",
f4t:"シナリオDSL",f4d:"{n}コマンド、ヘッダ条件、ラベル、ローカライズキー付き選択肢。",
f5t:"イベントバス",f5d:"20の型付きC#イベント。[EventKey] でGDScriptへブリッジ。",
f6t:"スロット式セーブ",f6d:"Modごとに IModSaveable スロット：バージョン・マイグレーション・スクリーンショット。",
f7t:"マップと時刻",f7d:"マップゾーンと4つの時間帯をシナリオから直接制御。",
f8t:"ゲーム内 Mod Manager",f8d:"カタログ・フィルタ・インストール・更新・削除をゲームから出ずに。",
battle_cap:"戦闘ルール",battle_title:"戦闘も着脱式モジュール",
battle_desc:"turn_battle_base がルールセット契約を定め、card_battle と ff_battle が独立に実装します。独自モードは同じエンジン上のもう一つのMod。",
turn_t:"turn_battle_base",turn_d:"ルールセット契約、レジストリ、アクション実行器、ステータス効果、勝敗画面、EXPと戦利品レポート。",
card_t:"card_battle",card_d:"共通エンジン上のカードバトル。",
ff_t:"ff_battle",ff_d:"FINAL FANTASY風のクラシックなターン制戦闘。",
dsl_cap:"DSL · .scenario",dsl_title:"シナリオはテキストファイル",
dsl_p1:"シーンはプレーンテキスト。ヘッダに所有シーン・優先度・開始条件を書き、続いてコマンドとラベル・ジャンプ。セリフはローカライズキーを持ち、翻訳は .properties で届きます。",
dsl_p2:"エディタは不要：.scenario はコードと同じエディタで編集します。構文は拡張可能——新しいコマンドはMod内の新しいストラテジークラス。",
mods_cap:"カタログ",mods_title:"リポジトリには既に{n}のMod",
mods_note:"名称・説明は manifest.json のまま（英語）。テスト用Modにはタグ付き。",
cat_engine:"エンジン",cat_gameplay:"ゲームプレイ",cat_story:"ストーリー",cat_library:"ライブラリ",cat_ui:"インターフェース",cat_test:"テスト",cat_tools:"ツール",
story_cap:"ゲーム",story_title:"一つのエンジン、異なるゲーム",
story_p1:"Cthangover はターン制バトルと村上のストーリーを持つビジュアルノベル。RPG Demo はピクセルの村と森を WASD で歩き、同じバトルコアで戦います。Novel Template は最小のプレイ可能なノベル。どれも一つの実行中プロセスの中で切り替わります。",
story_p2:"新しいゲームは新しいコレクション：シーン、キャラクター、翻訳を持つコンテンツModと必要なメカニクス。カーネルはそのどれも知りません——それが狙いです。",
shots_cap:"スクリーンショット",shots_title:"画面",
cta_title:"オープンなコード、オープンなパイプライン",
cta_p:"プロジェクトはMITライセンス。リポジトリはセルフホストのGiteaにあり、カーネル・Mod・ドキュメント・このサイトも同じソースツリーから組み立てられます。",
cta_repo:"リポジトリを開く",cta_license:"MITライセンス",
qs_title:"クイックスタート",qs_sub:"3つのコース——どれも動く結果で終わります。教材は普通のMarkdownファイル。翻訳は準備でき次第追加され、未整備なら英語を正直に表示します。",
qs_l1k:"コース 1",qs_l1t:"プレイヤー",qs_l1d:"JSONパッチでマラオを強化——コードは一行も不要。",qs_m1:"JSONパッチ · 5分",
qs_l2k:"コース 2",qs_l2t:"Modder",qs_l2d:"自分のシーンをDSLで：会話、選択肢、ジャンプ。",qs_m2:"JSON + DSL · 15分",
qs_l3k:"コース 3",qs_l3t:"プログラマー",qs_l3d:"C# Mod：カスタムアクション、メニュー設定、イベント購読。",qs_m3:"C# + Godot API · 20分",
qs_lang:"教材の言語",fallback_note:"教材は英語で表示",
search_ph:"セクションを検索…",toc:"このページの目次",prev:"前へ",next:"次へ",
built_from:"生成元",not_translated:"このセクションは英語のみ",
on_git:"Giteaで編集",depends:"依存",docs_sections:"セクション",license:"MITライセンス",
built_by:"組み立て元",md_files:"個のMarkdownファイル",
not_found:"セクションは未作成",docs_here:"ドキュメントは英語主体：左がツリー。翻訳は準備でき次第追加。"},

ko:{qs_overview:"개요",nav_home:"홈",nav_docs:"문서",nav_quickstart:"빠른 시작",nav_repo:"저장소",
nav_gallery:"갤러리",cta_gallery:"게임 보기",gal_cap:"게임 속",gal_title:"실제로 돌아가는 모습",gal_sub:"여기 있는 모든 화면은 게임이 직접 찍은 것입니다. 통합 테스트가 게임을 플레이하며 스크린샷을 저장하므로 목업은 하나도 없습니다.",gal_all:"전체",gal_story:"노벨",gal_battle:"전투",gal_rpg:"RPG 월드",gal_ui:"인터페이스",gal_tools:"에디터",g_cth_d:"턴제 카드 전투, 퀘스트, 요리, 영입, 그리고 무라카미 스토리 라인이 있는 비주얼 노벨.",g_rpg_d:"RPG Maker 스타일 게임: WASD로 걷는 픽셀 마을과 숲, 채집, 상인, FF 코어 전투.",g_nov_d:"가장 작은 플레이 가능한 노벨: 텍스트, 배경, 장면 전환으로 만든 마을, 바, 집 — 모더의 출발점.",dsl_p3:"시나리오 리터럴은 러시아어로 작성됩니다. 예시는 모드의 scenarios_en.properties 영어 번역으로 보여 줍니다.",s_novel_wolf:"시나리오가 배경을 정하고 이야기를 들려준다",s_card_battle:"카드 전투: 마라오와 무라카미 대 늑대 두 마리",s_rpg_village:"RPG Demo: 타일 맵 위의 마을",s_novel_menu:"마을 입구에서의 선택",s_home_outside:"마라오의 집",s_livingroom:"거실",s_outskirts:"마을 외곽",s_ff_battle:"Final Fantasy 스타일 코어의 전투",s_victory:"경험치와 전리품이 있는 승리 화면",s_forest:"숲: 주울 수 있는 것들",s_house:"촌장의 집",s_trade:"거래 창: 두 개의 인벤토리",s_inventory:"인벤토리",s_characters:"파티와 행동 카드",s_map:"월드 맵",s_skills:"스킬 컬렉션",s_picker:"게임 선택: 하나의 엔진, 여러 게임",s_scenario_editor:"시나리오 에디터",s_scene_editor:"장면 에디터",s_rpg_editor:"타일 맵 에디터",s_map_editor:"월드 맵 에디터",s_light_editor:"조명 에디터",
kicker:"모드 지향 엔진 · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"모드가 조립하는",hero_h1b:"게임 엔진",
hero_tag:"Core/ 커널에는 게임이 하나도 없고 장르조차 정해져 있지 않습니다. 시작할 때 mods/에서 모드를 불러와 C#을 Roslyn으로 컴파일하고, 그것들로 게임을 조립합니다: 비주얼 노벨, 타일 맵 위의 RPG, 전투, 대화. 모든 메커니즘은 열고 고치고 지울 수 있는 평범한 폴더입니다.",
cta_start:"5분 만에 시작",cta_docs:"문서",
st_dsl_l:"개의 DSL 명령",st_mods_l:"개의 모드 동봉",st_loc_l:"줄의 C#(커널+모드)",st_battle_l:"가지 전투 규칙",
arch_cap:"조립 라인",arch_title:"게임은 층층이 조립됩니다",
arch_l1t:"커널 · Core/",arch_l1d:"시나리오 DSL, 모드 레지스트리, 저장, 이벤트, 리소스, UI, 오디오. 콘텐츠 없이 서비스만.",
arch_l2t:"엔진 모드 · *_base",arch_l2d:"2D 셸, 대화와 시나리오 DSL, 캐릭터, 아이템, 퀘스트, 턴제 전투, 타일 월드 — 모두 게임이 고르는 모드입니다.",
arch_l3t:"게임 · 컬렉션",arch_l3d:"게임은 컬렉션입니다: 자체 콘텐츠 모드와 고른 메커니즘 — 전투가 있는 노벨, RPG 메이커 풍의 탐험, 템플릿.",
arch_l4t:"당신의 모드",arch_l4d:"새 이야기 선, 메커니즘, 전투 규칙—mods/ 의 평범한 폴더 하나.",
feat_cap:"기능",feat_title:"커널에 이미 있는 것",
f1t:"런타임 Roslyn",f1d:"모드의 C# 소스를 적재 시 컴파일, 내용 해시로 캐시.",
f2t:"tscn 장면 패치",f2d:"모드가 남의 장면을 확장: 노드, 속성, 연결.",
f3t:"JSON 데이터 패치",f3d:"$set · $add · $remove, 와일드카드, 키 기준 배열 병합.",
f4t:"시나리오 DSL",f4d:"{n}개 명령, 헤더 조건, 라벨, 현지화 키가 붙은 선택지.",
f5t:"이벤트 버스",f5d:"20개의 형식화된 C# 이벤트, [EventKey]로 GDScript 브리지.",
f6t:"슬롯별 저장",f6d:"모드마다 IModSaveable 슬롯: 버전, 마이그레이션, 스크린샷.",
f7t:"맵과 시간대",f7d:"맵 구역과 네 시간대를 시나리오에서 곧장 제어.",
f8t:"게임 내 Mod Manager",f8d:"카탈로그, 필터, 설치·갱신·삭제를 게임을 나가지 않고.",
battle_cap:"전투 규칙",battle_title:"전투도 탈부착 모듈",
battle_desc:"turn_battle_base 가 규칙 계약을 정의하고, card_battle 와 ff_battle 이 각자 구현합니다. 자신만의 모드도 같은 엔진 위의 모드 하나.",
turn_t:"turn_battle_base",turn_d:"룰셋 계약, 코어 레지스트리, 행동 실행기, 상태 효과, 승패 화면, EXP·전리품 보고.",
card_t:"card_battle",card_d:"공용 엔진 위의 카드 배틀.",
ff_t:"ff_battle",ff_d:"파이널판타지풍 클래식 턴제 전투.",
dsl_cap:"DSL · .scenario",dsl_title:"시나리오는 텍스트 파일",
dsl_p1:"장면은 평문입니다. 헤더에 소유 장면·우선순위·발동 조건을 적고, 명령과 라벨·점프가 이어집니다. 대사는 현지화 키를 가지며 번역은 .properties 로 붙습니다.",
dsl_p2:"에디터 불필요: .scenario 는 코드와 같은 에디터에서 고칩니다. 문법은 확장 가능—새 명령은 모드 안의 새 전략 클래스.",
mods_cap:"카탈로그",mods_title:"저장소에 이미 {n}개의 모드",
mods_note:"이름과 설명은 manifest.json 그대로(영어). 테스트 모드는 태그 표시.",
cat_engine:"엔진",cat_gameplay:"게임플레이",cat_story:"스토리",cat_library:"라이브러리",cat_ui:"인터페이스",cat_test:"테스트",cat_tools:"도구",
story_cap:"게임",story_title:"하나의 엔진, 서로 다른 게임",
story_p1:"Cthangover는 턴제 전투와 무라카미 이야기를 가진 비주얼 노벨입니다. RPG Demo는 픽셀 마을과 숲을 WASD로 걸으며 같은 전투 코어로 싸웁니다. Novel Template은 가장 작은 플레이 가능한 노벨입니다. 모두 실행 중인 하나의 프로세스 안에서 전환됩니다.",
story_p2:"새 게임은 새 컬렉션입니다: 장면, 캐릭터, 번역을 담은 콘텐츠 모드와 필요한 메커니즘. 커널은 그 어느 것도 모릅니다 — 그것이 핵심입니다.",
shots_cap:"장면",shots_title:"스크린샷",
cta_title:"열린 코드, 열린 파이프라인",
cta_p:"프로젝트는 MIT 라이선스. 저장소는 자체 Gitea에서: 커널·모드·문서·이 사이트까지 하나의 소스 트리에서 빌드됩니다.",
cta_repo:"저장소 열기",cta_license:"MIT 라이선스",
qs_title:"빠른 시작",qs_sub:"세 갈래 트랙—각각 작동하는 결과로 끝납니다. 교재는 평범한 Markdown 파일. 번역은 준비되는 대로 붙고, 그 전엔 영어를 정직하게 보여줍니다.",
qs_l1k:"트랙 1",qs_l1t:"플레이어",qs_l1d:"JSON 패치로 Marao 강화—코드 한 줄 없이.",qs_m1:"JSON 패치 · 5분",
qs_l2k:"트랙 2",qs_l2t:"모더",qs_l2d:"DSL로 자기 장면: 대화, 선택지, 점프.",qs_m2:"JSON + DSL · 15분",
qs_l3k:"트랙 3",qs_l3t:"프로그래머",qs_l3d:"C# 모드: 커스텀 액션, 메뉴 설정, 이벤트 구독.",qs_m3:"C# + Godot API · 20분",
qs_lang:"교재 언어",fallback_note:"교재는 영어로 표시",
search_ph:"섹션 검색…",toc:"이 페이지 목차",prev:"이전",next:"다음",
built_from:"생성 원본",not_translated:"이 섹션은 영어만 제공",
on_git:"Gitea에서 편집",depends:"의존",docs_sections:"섹션",license:"MIT 라이선스",
built_by:"조립원",md_files:"개의 Markdown 파일",
not_found:"아직 작성되지 않은 섹션",docs_here:"문서는 영어 우선: 왼쪽이 트리. 번역은 준비되는 대로."},

vi:{qs_overview:"Tổng quan",nav_home:"Trang chính",nav_docs:"Tài liệu",nav_quickstart:"Bắt đầu nhanh",nav_repo:"Kho mã",
nav_gallery:"Thư viện ảnh",cta_gallery:"Xem trò chơi",gal_cap:"Trong game",gal_title:"Khi chạy thực tế",gal_sub:"Mọi khung hình ở đây đều do chính trò chơi chụp: các bài kiểm thử tích hợp chơi game và lưu ảnh chụp màn hình, nên trang không có bản mô phỏng nào.",gal_all:"Tất cả",gal_story:"Tiểu thuyết",gal_battle:"Chiến đấu",gal_rpg:"Thế giới RPG",gal_ui:"Giao diện",gal_tools:"Trình chỉnh sửa",g_cth_d:"Tiểu thuyết hình ảnh với chiến đấu thẻ bài theo lượt, nhiệm vụ, nấu ăn, chiêu mộ và tuyến truyện của Murakami.",g_rpg_d:"Trò chơi phong cách RPG Maker: ngôi làng và khu rừng pixel đi bằng WASD, thu thập, thương nhân và chiến đấu trên lõi FF.",g_nov_d:"Tiểu thuyết chơi được nhỏ nhất: thị trấn, quán bar và ngôi nhà làm từ văn bản, phông nền và chuyển cảnh — điểm khởi đầu của modder.",dsl_p3:"Chuỗi trong kịch bản được viết bằng tiếng Nga; ví dụ hiển thị bản dịch tiếng Anh của mod từ scenarios_en.properties.",s_novel_wolf:"Kịch bản đặt phông nền và kể chuyện",s_card_battle:"Trận đấu thẻ bài: Marao và Murakami đấu hai con sói",s_rpg_village:"RPG Demo: ngôi làng trên bản đồ ô",s_novel_menu:"Lựa chọn ở lối vào thị trấn",s_home_outside:"Nhà của Marao",s_livingroom:"Phòng khách",s_outskirts:"Ngoại ô thị trấn",s_ff_battle:"Chiến đấu trên lõi phong cách Final Fantasy",s_victory:"Màn hình chiến thắng với EXP và chiến lợi phẩm",s_forest:"Khu rừng: đồ có thể nhặt",s_house:"Nhà trưởng làng",s_trade:"Cửa sổ giao dịch: hai túi đồ",s_inventory:"Túi đồ",s_characters:"Tổ đội và thẻ hành động",s_map:"Bản đồ thế giới",s_skills:"Bộ sưu tập kỹ năng",s_picker:"Chọn game: một engine, nhiều trò chơi",s_scenario_editor:"Trình sửa kịch bản",s_scene_editor:"Trình sửa cảnh",s_rpg_editor:"Trình sửa bản đồ ô",s_map_editor:"Trình sửa bản đồ thế giới",s_light_editor:"Trình sửa ánh sáng",
kicker:"engine hướng mod · Godot 4.6 · C# / .NET 8 · MIT",
hero_h1a:"Một engine trò chơi",hero_h1b:"do mod lắp ráp",
hero_tag:"Nhân Core/ không chứa trò chơi nào, kể cả thể loại. Khi khởi động, nó nạp mod từ mods/, biên dịch C# của chúng bằng Roslyn và lắp ráp trò chơi từ chúng: visual novel, RPG trên bản đồ ô, chiến đấu, hội thoại. Mỗi cơ chế là một thư mục bình thường mà bạn có thể mở, sửa và xóa.",
cta_start:"Bắt đầu trong 5 phút",cta_docs:"Tài liệu",
st_dsl_l:"lệnh DSL",st_mods_l:"mod trong kho",st_loc_l:"dòng C# (nhân + mod)",st_battle_l:"bộ luật chiến đấu",
arch_cap:"Dây chuyền lắp ráp",arch_title:"Trò chơi được lắp theo tầng",
arch_l1t:"Nhân · Core/",arch_l1d:"DSL kịch bản, registry mod, lưu game, sự kiện, tài nguyên, UI, âm thanh. Không có nội dung—chỉ có dịch vụ.",
arch_l2t:"Mod engine · *_base",arch_l2d:"Vỏ 2D, hội thoại và DSL kịch bản, nhân vật, vật phẩm, nhiệm vụ, chiến đấu theo lượt, thế giới ô — mỗi thứ là một mod mà trò chơi chọn dùng hay không.",
arch_l3t:"Trò chơi · bộ sưu tập",arch_l3d:"Trò chơi là một bộ sưu tập: mod nội dung riêng cộng với các cơ chế được chọn — novel có chiến đấu, khám phá kiểu RPG Maker, một mẫu.",
arch_l4t:"Mod của bạn",arch_l4d:"Một tuyến truyện, một cơ chế hay luật chiến đấu mới—một thư mục trong mods/.",
feat_cap:"Tính năng",feat_title:"Đã có trong nhân",
f1t:"Roslyn lúc chạy",f1d:"Mã C# của mod được biên dịch khi tải; cache theo hash nội dung.",
f2t:"Vá cảnh tscn",f2d:"Mod mở rộng cảnh của mod khác: node, thuộc tính, kết nối.",
f3t:"Vá dữ liệu JSON",f3d:"$set · $add · $remove, đường dẫn wildcard, gộp mảng theo khóa.",
f4t:"DSL kịch bản",f4d:"{n} lệnh, điều kiện trong phần đầu, nhãn, lựa chọn có khóa bản địa hóa.",
f5t:"Bus sự kiện",f5d:"20 sự kiện C# có kiểu, cầu sang GDScript qua [EventKey].",
f6t:"Lưu game theo slot",f6d:"Mỗi mod một slot IModSaveable: phiên bản, di trú, ảnh chụp.",
f7t:"Bản đồ và thời gian",f7d:"Vùng bản đồ và bốn pha ngày—điều khiển thẳng từ kịch bản.",
f8t:"Mod Manager trong game",f8d:"Danh mục, bộ lọc, cài, cập nhật và gỡ mod mà không thoát game.",
battle_cap:"Luật chiến đấu",battle_title:"Chiến đấu cũng là mô-đun cắm rời",
battle_desc:"turn_battle_base định nghĩa hợp đồng ruleset; card_battle và ff_battle triển khai độc lập. Chế độ của bạn chỉ là một mod nữa trên cùng engine.",
turn_t:"turn_battle_base",turn_d:"Hợp đồng ruleset, registry, bộ thực thi hành động, hiệu ứng trạng thái, màn thắng/thua, báo EXP và chiến lợi phẩm.",
card_t:"card_battle",card_d:"Đấu bài trên nền engine chung.",
ff_t:"ff_battle",ff_d:"Chiến đấu theo lượt cổ điển phong cách Final Fantasy.",
dsl_cap:"DSL · .scenario",dsl_title:"Kịch bản là tệp văn bản",
dsl_p1:"Một cảnh là văn bản thuần: phần đầu ghi cảnh sở hữu, ưu tiên, điều kiện khởi phát; tiếp theo là lệnh với nhãn và bước nhảy. Lời thoại mang khóa bản địa hóa, bản dịch đến bằng .properties.",
dsl_p2:"Không cần trình soạn thảo: .scenario sửa bằng chính trình soạn thảo mã nguồn. Cú pháp mở rộng được—một lệnh mới là một lớp chiến lược trong mod.",
mods_cap:"Danh mục",mods_title:"Kho đã có {n} mod",
mods_note:"Tên và mô tả nguyên như manifest.json (tiếng Anh). Mod kiểm thử được gắn thẻ.",
cat_engine:"engine",cat_gameplay:"lối chơi",cat_story:"cốt truyện",cat_library:"thư viện",cat_ui:"giao diện",cat_test:"kiểm thử",cat_tools:"công cụ",
story_cap:"Trò chơi",story_title:"Một engine, nhiều trò chơi",
story_p1:"Cthangover là visual novel có chiến đấu theo lượt và tuyến truyện Murakami. RPG Demo đi bộ quanh ngôi làng và khu rừng pixel bằng WASD và chiến đấu trên cùng các lõi chiến đấu. Novel Template là novel nhỏ nhất có thể chơi. Tất cả chuyển đổi trong cùng một tiến trình đang chạy.",
story_p2:"Trò chơi mới là một bộ sưu tập mới: mod nội dung của bạn với cảnh, nhân vật, bản dịch, cộng các cơ chế cần thiết. Nhân không biết gì về chúng — đó chính là mục đích.",
shots_cap:"Khung hình",shots_title:"Ảnh chụp",
cta_title:"Mã mở, pipeline mở",
cta_p:"Dự án phát hành theo MIT. Kho mã nằm trên Gitea tự quản: nhân, mod, tài liệu và chính trang web được dựng từ một cây mã nguồn.",
cta_repo:"Mở kho mã",cta_license:"Giấy phép MIT",
qs_title:"Bắt đầu nhanh",qs_sub:"Ba lộ trình—mỗi lộ trình kết thúc bằng một kết quả chạy được. Tài liệu là tệp Markdown thường; bản dịch có lúc nào thêm lúc đó, chưa có thì hiện tiếng Anh một cách trung thực.",
qs_l1k:"Lộ trình 1",qs_l1t:"Người chơi",qs_l1d:"Cường hóa nhân vật Marao bằng vá JSON—không cần dòng mã nào.",qs_m1:"Vá JSON · 5 phút",
qs_l2k:"Lộ trình 2",qs_l2t:"Modder",qs_l2d:"Cảnh của riêng bạn: hội thoại, lựa chọn, bước nhảy bằng DSL.",qs_m2:"JSON + DSL · 15 phút",
qs_l3k:"Lộ trình 3",qs_l3t:"Lập trình viên",qs_l3d:"Mod C#: hành động tùy chỉnh, cài đặt menu, đăng ký sự kiện.",qs_m3:"C# + Godot API · 20 phút",
qs_lang:"Ngôn ngữ tài liệu",fallback_note:"tài liệu hiển thị tiếng Anh",
search_ph:"Tìm mục…",toc:"Trong trang này",prev:"Trước",next:"Tiếp",
built_from:"dựng từ",not_translated:"mục này chỉ có tiếng Anh",
on_git:"sửa trên Gitea",depends:"phụ thuộc",docs_sections:"Mục lục",license:"Giấy phép MIT",
built_by:"lắp ráp từ",md_files:"tệp Markdown",
not_found:"Mục chưa được viết",docs_here:"Tài liệu ưu tiên tiếng Anh: cây bên trái; bản dịch có lúc nào thêm lúc đó."}
};

/* ---------- shared runtime ---------- */
var LS_KEY = "cthangover_lang";
var DEFAULT_LANG = "en";

function getInitialLang(){
  var urlLang = null;
  try { urlLang = (location.search.match(/[?&]lang=([a-z]{2})/i) || [])[1] || null; } catch(e) {}
  if (urlLang && I18N[urlLang]) {
    try { localStorage.setItem(LS_KEY, urlLang); } catch(e) {}
    return urlLang;
  }
  var stored = null;
  try { stored = localStorage.getItem(LS_KEY); } catch(e) {}
  if (stored && I18N[stored]) return stored;
  var nav = ((typeof navigator !== "undefined" && navigator.language) || "").toLowerCase();
  for (var i=0;i<LANGS.length;i++){
    if (nav.indexOf(LANGS[i][0]) === 0) return LANGS[i][0];
  }
  return DEFAULT_LANG;
}

var lang = getInitialLang();

/* t("key") — строка интерфейса на текущем языке (фолбэк en, затем сам ключ).
   t("key", {n: 31}) подставляет {n} — так числа из data/mods.json попадают в фразы. */
function t(k, vars){
  var d = I18N[lang] || I18N.en;
  var s = d[k] || I18N.en[k] || k;
  if (vars) for (var v in vars) s = s.split("{" + v + "}").join(vars[v]);
  return s;
}

function esc(s){
  var d = document.createElement("div");
  d.appendChild(document.createTextNode(String(s)));
  return d.innerHTML;
}

function applyI18n(){
  document.documentElement.lang = lang;
  var els = document.querySelectorAll("[data-i18n]");
  for (var i=0;i<els.length;i++){ els[i].textContent = t(els[i].getAttribute("data-i18n")); }
  var phs = document.querySelectorAll("[data-i18n-ph]");
  for (var p=0;p<phs.length;p++){ phs[p].placeholder = t(phs[p].getAttribute("data-i18n-ph")); }
  try { localStorage.setItem(LS_KEY, lang); } catch(e) {}
  var cur = null;
  for (var q=0;q<LANGS.length;q++) if (LANGS[q][0]===lang) cur = LANGS[q];
  var sl = document.getElementById("langCurName");
  if (sl) sl.textContent = cur ? cur[1] : lang;
  var mb = document.querySelectorAll("#langMenu button");
  for (var m=0;m<mb.length;m++){ mb[m].className = mb[m].getAttribute("data-l")===lang ? "sel" : ""; }
}

/* Смена языка: статичные data-i18n переводятся здесь, динамические блоки
   страница перерисовывает сама по событию "langchange". */
function setLang(l){
  if (!I18N[l]) l = DEFAULT_LANG;
  if (l === lang) return;
  lang = l;
  applyI18n();
  document.dispatchEvent(new CustomEvent("langchange", { detail: lang }));
}

function buildLangMenu(menuEl){
  var mh = "";
  for (var i=0;i<LANGS.length;i++){
    mh += '<button data-l="'+LANGS[i][0]+'"><span>'+LANGS[i][1]+'</span><span class="code">'+LANGS[i][0].toUpperCase()+'</span></button>';
  }
  menuEl.innerHTML = mh;
  var mb = menuEl.querySelectorAll("button");
  for (var j=0;j<mb.length;j++){
    mb[j].addEventListener("click", function(e){
      e.stopPropagation();
      setLang(this.getAttribute("data-l"));
      menuEl.classList.remove("open");
    });
  }
}

/* ---------- инициализация шапки (общая для трёх страниц) ---------- */
function initHeaderChrome(activePage){
  var menu = document.getElementById("langMenu");
  var btn = document.getElementById("langBtn");
  if (menu && btn){
    buildLangMenu(menu);
    btn.addEventListener("click", function(e){ e.stopPropagation(); menu.classList.toggle("open"); });
    document.addEventListener("click", function(){ menu.classList.remove("open"); });
  }
  var toggle = document.getElementById("navToggle");
  var mnav = document.getElementById("mobileNav");
  if (toggle && mnav){
    toggle.addEventListener("click", function(){ mnav.classList.toggle("open"); });
  }
  var links = document.querySelectorAll("nav.main a, .mobile-nav a");
  for (var i=0;i<links.length;i++){
    links[i].classList.toggle("on", links[i].getAttribute("data-nav")===activePage);
  }
  applyI18n();
}
