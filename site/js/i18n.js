const I18N = {
  ru: {
    lang_name: "Русский",
    nav_about: "О проекте",
    nav_features: "Возможности",
    nav_arch: "Архитектура",
    nav_screenshots: "Скриншоты",
    nav_videos: "Видео",
    nav_story: "Сюжет",
    nav_systems: "Системы",
    nav_quickstart: "Быстрый старт",
    nav_docs: "Документация",
    nav_docs_back: "← На сайт",
    nav_repo: "Репозиторий",

    hero_title: "cthangover",
    hero_tagline: "Мод-ориентированная платформа для визуальных новелл с элементами RPG",
    hero_btn_repo: "Исходный код в репозитории",

    about_title: "О проекте",
    about_p1: "cthangover — это визуальная новелла с элементами RPG, построенная на уникальной мод-ориентированной архитектуре. Проект написан на Godot 4.6 (C# / .NET 8) и распространяется под лицензией MIT.",
    about_p2: "В Godot-проекте практически нет ресурсов. Лёгкое C#-ядро собирает готовую игру в рантайме из модов, расположенных в папке mods/. Моды содержат сцены, сценарии, ресурсы, а также C#-код в исходном виде — он компилируется «на лету» через Roslyn. Убирая или дополняя моды, можно удалять или добавлять сцены, механики и целые игровые подсистемы.",
    about_p3: "Это открывает дорогу в бесконечный моддинг и разработку собственных проектов на базе ядра. Для доработки модов не требуется даже наличие Godot или SDK — достаточно текстового редактора и базовых знаний JSON.",

    features_title: "Ключевые возможности",
    features_subtitle: "Что уже работает в базовой поставке",

    feat_time_title: "Шейдерное время суток",
    feat_time_desc: "Один задник — вместо сотен вариаций. Автоматический расчёт фаз дня и ночи прямо на GPU, без лишних ресурсов.",

    feat_light_title: "2D-освещение",
    feat_light_desc: "Динамические источники света, карты глубины и перетаскиваемая лампа игрока оживляют каждую сцену.",

    feat_dsl_title: "DSL-сценарии",
    feat_dsl_desc: "Собственный предметно-ориентированный язык для написания сценариев. Простой синтаксис, 26 команд, расширяется модами.",

    feat_rpg_title: "RPG-системы",
    feat_rpg_desc: "Персонажи с уровнями, опытом, квестами, инвентарём, крафтом, скиллами и коллекционными картами редкости.",

    feat_battle_title: "Пошаговый бой",
    feat_battle_desc: "Два равноправных режима боя — пошаговый и в стиле Final Fantasy. Переключаются на лету через моды.",

    feat_mod_title: "Система модов",
    feat_mod_desc: "Игра собрана из прозрачных «кирпичиков». Каждый мод можно рассмотреть, вынуть и увидеть, как меняется игра.",

    arch_title: "Мод-ориентированная архитектура",
    arch_kernel: "Ядро (C#)",
    arch_kernel_desc: "Лёгкое ядро без ресурсов. Загружает и активирует моды, компилирует C#-код (Roslyn), разрешает конфликты.",
    arch_mods: "Моды (mods/)",
    arch_mods_desc: "Сцены, сценарии (.scenario), текстуры, звуки, шейдеры, JSON-данные, исходный C#-код. Всё, из чего состоит игра.",
    arch_game: "Игра",
    arch_game_desc: "Собирается в рантайме из активных модов. Убрал мод пошагового боя, добавил FF — теперь боёвка в стиле Final Fantasy. Поменял местами — снова пошаговая. Добавил мод на еду — нужно готовить, есть и кормить партнёров. Убрал — никакой возни с выживанием.",
    arch_footer: "Для доработки модов не нужен Godot или SDK. Текстовый редактор, JSON и немного C# (опционально) — этого достаточно, чтобы изменить или создать игровую механику.",

    screenshots_title: "Скриншоты",
    screenshot_n: "Скриншот",

    videos_title: "Видео",
    video_lighting_title: "Освещение и время суток",

    story_title: "Сюжет",
    story_p1: "Cthangover (Cthulhu + Hangover) — это история о том, как великий Аль-Азиф, известный как «Книга Мертвых», встретил своего единственного и непобедимого врага — хронического алкоголика.",
    story_p2: "Главный герой, пропащий субъект из параллельной реальности, случайно прочёл пару строк на незнакомом языке, после чего небосвод пошёл трещинами, а в прогалы хлынули щупальца, когти и неописуемые формы из миров за гранью сна. Человечество погрузилось в хаос, но виновник апокалипсиса безучастно ковыряет в носу, принимая рёв космических ужасов за галлюцинации.",
    story_p3: "Смертоносное проклятие Некрономикона, ломающее психику сильнейших магов, разбивается о «непробиваемую» тупость и химическую зависимость пьяницы. Сама Книга, запертая в этой абсурдной связке, впервые в своём существовании оказывается в тупике: она бессмертна, но обречена делить вечность с существом, которому на неё плевать.",
    story_p4: "Чёрный юмор, грязные словечки, абсурдные ситуации и необычный сюжет.",

    systems_title: "Ключевые подсистемы",
    systems_subtitle: "Каждая подсистема — это набор C#-классов, Godot-сцен и JSON-данных, задокументированных и расширяемых через моды.",

    sys_01_title: "Roslyn-компиляция модов",
    sys_01_desc: "C# код из mods/ компилируется на лету в рантайме через Microsoft.CodeAnalysis с SHA-256 кэшированием.",
    sys_02_title: "JSON-патчи",
    sys_02_desc: "Моды изменяют данные друг друга: wildcard, $add/$remove/$set, identity-key merging.",
    sys_03_title: "Шина событий",
    sys_03_desc: "Двухуровневый pub-sub: типизированный C# + строковый GDScript с авто-мостом через [EventKey].",
    sys_04_title: "DSL сценариев",
    sys_04_desc: "Собственный язык .scenario, расширяемый парсер, 35+ командных стратегий — можно легко добавлять свои команды.",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext открывает весь C# API для GDScript-модов: сцены, квесты, события.",
    sys_06_title: "JSON → C# фабрики",
    sys_06_desc: "24 типобезопасные фабрики загружают JSON-сущности из всех модов и кэшируют результат.",
    sys_07_title: "Сохранения модов",
    sys_07_desc: "Каждый мод сохраняет и загружает своё состояние через IModSaveable — без конфликтов.",
    sys_08_title: "Граф зависимостей",
    sys_08_desc: "Топологическая сортировка загрузки: depends + ручной порядок + обнаружение конфликтов.",

    cta_title: "Присоединяйся",
    cta_p1: "Проект с открытым исходным кодом под лицензией MIT.",
    cta_p2: "Нужна конструктивная критика, свежий взгляд и обратная связь. Если тебе интересны визуальные новеллы, моддинг, Godot или просто необычная архитектура — загляни в репозиторий.",
    cta_btn_repo: "Перейти в репозиторий",
    cta_btn_license: "Лицензия MIT",

    footer_text: "© 2026 Исаев Илья",
    footer_opensource: "Open Source / MIT License",

    screenshot_placeholder: "Изображение"
  },

  en: {
    lang_name: "English",
    nav_about: "About",
    nav_features: "Features",
    nav_arch: "Architecture",
    nav_screenshots: "Screenshots",
    nav_videos: "Videos",
    nav_story: "Story",
    nav_systems: "Systems",
    nav_quickstart: "Quick Start",
    nav_docs: "Documentation",
    nav_docs_back: "← Back to site",
    nav_repo: "Repository",

    hero_title: "cthangover",
    hero_tagline: "A mod-oriented platform for visual novels with RPG elements",
    hero_btn_repo: "Source in the repository",

    about_title: "About the Project",
    about_p1: "cthangover is a visual novel with RPG elements, built on a unique mod-oriented architecture. Written in Godot 4.6 (C# / .NET 8) and distributed under the MIT license.",
    about_p2: "The Godot project contains almost no resources. A lightweight C# kernel assembles a complete game at runtime from mods located in the mods/ folder. Mods contain scenes, scenarios, resources, and C# source code — compiled on-the-fly via Roslyn. By removing or adding mods, you can remove or add scenes, mechanics, and entire game subsystems.",
    about_p3: "This opens the door to limitless modding and development of your own projects on top of the kernel. To modify mods, you don't even need Godot or an SDK — a text editor and basic JSON knowledge are enough.",

    features_title: "Key Features",
    features_subtitle: "What's already working out of the box",

    feat_time_title: "Shader-based Time of Day",
    feat_time_desc: "One background instead of hundreds of variations. Automatic GPU-based calculation of day/night phases without extra assets.",

    feat_light_title: "2D Lighting",
    feat_light_desc: "Dynamic light sources, depth maps, and a draggable player lamp bring every scene to life.",

    feat_dsl_title: "Scenario DSL",
    feat_dsl_desc: "A custom domain-specific language for writing scenarios. Simple syntax, 26 commands, extensible via mods.",

    feat_rpg_title: "RPG Systems",
    feat_rpg_desc: "Characters with levels, XP, quests, inventory, crafting, skills, and collectible rarity cards.",

    feat_battle_title: "Turn-Based Battle",
    feat_battle_desc: "Two equal battle modes — turn-based and Final Fantasy style. Switch on the fly via mods.",

    feat_mod_title: "Mod System",
    feat_mod_desc: "The game is assembled from transparent bricks. Each mod can be inspected, removed, and you'll see how the game changes.",

    arch_title: "Mod-Oriented Architecture",
    arch_kernel: "Kernel (C#)",
    arch_kernel_desc: "A lightweight core with no resources. Loads and activates mods, compiles C# code (Roslyn), resolves conflicts.",
    arch_mods: "Mods (mods/)",
    arch_mods_desc: "Scenes, scenarios (.scenario), textures, sounds, shaders, JSON data, C# source code. Everything the game is made of.",
    arch_game: "Game",
    arch_game_desc: "Assembled at runtime from active mods. Removed the turn-based battle mod, added FF — now you have Final Fantasy style combat. Swapped them back — turn-based again. Added a food mod — now you cook, eat, and feed your companions. Removed it — no survival fuss.",
    arch_footer: "You don't need Godot or an SDK to modify mods. A text editor, JSON, and a bit of C# (optional) — that's enough to change or create a game mechanic.",

    screenshots_title: "Screenshots",
    screenshot_n: "Screenshot",

    videos_title: "Videos",
    video_lighting_title: "Lighting & Time of Day",

    story_title: "Story",
    story_p1: "Cthangover (Cthulhu + Hangover) is the story of how the great Al-Azif, also known as the Book of the Dead, met its one and only invincible enemy — a chronic alcoholic.",
    story_p2: "The protagonist, a lost soul from a parallel reality, accidentally read a couple of lines in an unknown language, after which the firmament cracked open and tentacles, claws, and indescribable forms from worlds beyond sleep poured through. Humanity plunged into chaos, but the culprit of the apocalypse is idly picking his nose, mistaking the roar of cosmic horrors for hallucinations.",
    story_p3: "The deadly curse of the Necronomicon, which breaks the minds of the mightiest mages, shatters against the impenetrable stupidity and chemical dependency of a drunkard. The Book itself, trapped in this absurd bond, finds itself at a dead end for the first time in its existence: it is immortal, but doomed to share eternity with a being who couldn't care less.",
    story_p4: "Black humor, foul language, absurd situations, and an unusual plot.",

    systems_title: "Core Subsystems",
    systems_subtitle: "Each subsystem is a set of C# classes, Godot scenes, and JSON data — documented and extensible via mods.",

    sys_01_title: "Roslyn Mod Compilation",
    sys_01_desc: "C# source from mods/ compiled at runtime via Microsoft.CodeAnalysis with SHA-256 caching.",
    sys_02_title: "JSON Patches",
    sys_02_desc: "Mods patch each other's data: wildcards, $add/$remove/$set, identity-key merging.",
    sys_03_title: "Event Bus",
    sys_03_desc: "Dual-layer pub-sub: typed C# + string-based GDScript with auto-bridging via [EventKey].",
    sys_04_title: "Scenario DSL",
    sys_04_desc: "Custom .scenario language, extensible parser, 35+ command strategies — add your own commands easily.",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext exposes the full C# API to GDScript mods: scenes, quests, events.",
    sys_06_title: "JSON → C# Factories",
    sys_06_desc: "24 type-safe factories load JSON entities from all mods and cache results.",
    sys_07_title: "Per-Mod Saves",
    sys_07_desc: "Each mod saves and loads its own state via IModSaveable — zero conflicts.",
    sys_08_title: "Dependency Graph",
    sys_08_desc: "Topological mod ordering: depends declarations + manual order + conflict detection.",

    cta_title: "Get Involved",
    cta_p1: "Open source project under the MIT license.",
    cta_p2: "Constructive criticism, fresh perspectives, and feedback are needed. If you're interested in visual novels, modding, Godot, or just unusual architecture — check out the repository.",
    cta_btn_repo: "Go to repository",
    cta_btn_license: "MIT License",

    footer_text: "© 2026 Isaev Ilya",
    footer_opensource: "Open Source / MIT License",

    screenshot_placeholder: "Image"
  },

  zh: {
    lang_name: "中文",
    nav_about: "关于项目",
    nav_features: "功能特点",
    nav_arch: "架构",
    nav_screenshots: "截图",
    nav_videos: "视频",
    nav_story: "剧情",
    nav_systems: "系统",
    nav_quickstart: "快速开始",
    nav_docs: "文档",
    nav_docs_back: "← 返回网站",
    nav_repo: "代码仓库",

    hero_title: "cthangover",
    hero_tagline: "面向模组的视觉小说与RPG平台",
    hero_btn_repo: "仓库中的源代码",

    about_title: "关于项目",
    about_p1: "cthangover 是一款带有RPG元素的视觉小说，基于独特的模组导向架构构建。项目使用 Godot 4.6（C# / .NET 8）开发，并以 MIT 许可证发布。",
    about_p2: "Godot 项目中几乎没有资源。轻量级的 C# 内核在运行时从 mods/ 文件夹中的模组组装完整游戏。模组包含场景、剧本、资源以及 C# 源代码——通过 Roslyn 即时编译。移除或添加模组，即可删除或添加场景、机制和整个游戏子系统。",
    about_p3: "这为无限模组开发和在内核之上构建自己的项目打开了大门。修改模组甚至不需要 Godot 或 SDK——文本编辑器和基础 JSON 知识就足够了。",

    features_title: "关键功能",
    features_subtitle: "开箱即用的功能",

    feat_time_title: "基于着色器的昼夜系统",
    feat_time_desc: "一个背景取代数百个变体。直接在 GPU 上自动计算昼夜阶段，无需额外资源。",

    feat_light_title: "2D照明",
    feat_light_desc: "动态光源、深度图和可拖动的玩家灯，让每个场景栩栩如生。",

    feat_dsl_title: "剧本DSL",
    feat_dsl_desc: "用于编写剧本的自定义领域特定语言。简洁的语法，26条命令，可通过模组扩展。",

    feat_rpg_title: "RPG系统",
    feat_rpg_desc: "具有等级、经验值、任务、背包、合成、技能和可收集稀有卡牌的角色。",

    feat_battle_title: "回合制战斗",
    feat_battle_desc: "两种平等战斗模式——回合制和最终幻想式。通过模组随时切换。",

    feat_mod_title: "模组系统",
    feat_mod_desc: "游戏由透明的「积木」组装而成。每个模组都可以检视、移除，观察游戏如何变化。",

    arch_title: "模组导向架构",
    arch_kernel: "内核 (C#)",
    arch_kernel_desc: "无资源的轻量级核心。加载并激活模组，编译 C# 代码（Roslyn），解决冲突。",
    arch_mods: "模组 (mods/)",
    arch_mods_desc: "场景、剧本 (.scenario)、纹理、音效、着色器、JSON 数据、C# 源代码。构成游戏的一切。",
    arch_game: "游戏",
    arch_game_desc: "在运行时由活动模组组装。移除回合制战斗模组，添加 FF——现在你有了最终幻想式战斗。换回来——又回到回合制。添加食物模组——现在你需要烹饪、进食并喂养伙伴。移除——无需为生存操心。",
    arch_footer: "修改模组不需要 Godot 或 SDK。文本编辑器、JSON 和一点 C#（可选）——足以改变或创建游戏机制。",

    screenshots_title: "截图",
    screenshot_n: "截图",

    videos_title: "视频",
    video_lighting_title: "光照与时间系统",

    story_title: "剧情",
    story_p1: "Cthangover（克苏鲁 + 宿醉）讲述了伟大的阿尔-阿吉夫（亦称「死者之书」）遇到它唯一不可战胜的敌人——一个慢性酒精中毒者的故事。",
    story_p2: "主角，一个来自平行现实的堕落灵魂，偶然读了几句陌生语言的文字，随后苍穹开裂，触手、利爪和来自梦境外世界的难以名状之物倾泻而入。人类陷入混乱，但末日的罪魁祸首却在漫不经心地挖鼻孔，将宇宙恐怖的咆哮当作幻觉。",
    story_p3: "死灵之书的致命诅咒能击溃最强法师的心智，却在一名醉鬼无坚不摧的愚蠢和化学依赖面前碎成粉末。这本书本身被困在这荒谬的联结中，在其存在中首次陷入僵局：它永生不死，却注定要与一个对它毫不在意的生物共享永恒。",
    story_p4: "黑色幽默，粗俗言辞，荒诞情境和非凡的剧情。",

    systems_title: "核心子系统",
    systems_subtitle: "每个子系统都是一组 C# 类、Godot 场景和 JSON 数据——已文档化并可通过模组扩展。",

    sys_01_title: "Roslyn 模组编译",
    sys_01_desc: "C# 源代码通过 Microsoft.CodeAnalysis 在运行时编译，带 SHA-256 缓存。",
    sys_02_title: "JSON 补丁",
    sys_02_desc: "模组相互修补数据：通配符、$add/$remove/$set、identity-key 合并。",
    sys_03_title: "事件总线",
    sys_03_desc: "双层发布-订阅：类型化 C# + 基于字符串的 GDScript，通过 [EventKey] 自动桥接。",
    sys_04_title: "剧本 DSL",
    sys_04_desc: "自定义 .scenario 语言、可扩展解析器、35+ 命令策略——轻松添加自己的命令。",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext 向 GDScript 模组暴露完整 C# API：场景、任务、事件。",
    sys_06_title: "JSON → C# 工厂",
    sys_06_desc: "24 个类型安全的工厂从所有模组加载 JSON 实体并缓存结果。",
    sys_07_title: "模组存档",
    sys_07_desc: "每个模组通过 IModSaveable 独立保存和加载状态——零冲突。",
    sys_08_title: "依赖图",
    sys_08_desc: "拓扑模组排序：depends 声明 + 手动顺序 + 冲突检测。",

    cta_title: "加入我们",
    cta_p1: "MIT 许可证下的开源项目。",
    cta_p2: "需要建设性的批评、新鲜视角和反馈。如果你对视觉小说、模组开发、Godot 或独特的架构感兴趣——来看看代码仓库。",
    cta_btn_repo: "前往仓库",
    cta_btn_license: "MIT 许可证",

    footer_text: "© 2026 Isaev Ilya",
    footer_opensource: "开源 / MIT 许可证",

    screenshot_placeholder: "图片"
  },

  ja: {
    lang_name: "日本語",
    nav_about: "概要",
    nav_features: "機能",
    nav_arch: "アーキテクチャ",
    nav_screenshots: "スクリーンショット",
    nav_videos: "動画",
    nav_story: "ストーリー",
    nav_systems: "システム",
    nav_quickstart: "クイックスタート",
    nav_docs: "ドキュメント",
    nav_docs_back: "← サイトに戻る",
    nav_repo: "リポジトリ",

    hero_title: "cthangover",
    hero_tagline: "RPG要素を持つモッド指向ビジュアルノベルプラットフォーム",
    hero_btn_repo: "リポジトリのソースコード",

    about_title: "プロジェクトについて",
    about_p1: "cthangover は、独自のモッド指向アーキテクチャ上に構築された RPG 要素を持つビジュアルノベルです。Godot 4.6（C# / .NET 8）で開発され、MIT ライセンスで公開されています。",
    about_p2: "Godot プロジェクトにはリソースがほとんどありません。軽量な C# カーネルが、mods/ フォルダ内のモッドから実行時に完全なゲームを組み立てます。モッドにはシーン、シナリオ、リソース、C# ソースコードが含まれ、Roslyn でオンザフライコンパイルされます。モッドを削除または追加することで、シーン、メカニクス、ゲームサブシステム全体を削除または追加できます。",
    about_p3: "これにより、無限のモッド開発とカーネル上での独自プロジェクト構築への道が開かれます。モッドの変更に Godot や SDK さえ必要ありません——テキストエディタと基本的な JSON 知識だけで十分です。",

    features_title: "主な機能",
    features_subtitle: "すぐに使える機能",

    feat_time_title: "シェーダーベースの時間帯",
    feat_time_desc: "数百のバリエーションの代わりに1つの背景。GPU上で昼夜のフェーズを自動計算し、追加リソース不要。",

    feat_light_title: "2D ライティング",
    feat_light_desc: "動的光源、深度マップ、ドラッグ可能なプレイヤーランプが各シーンに命を吹き込みます。",

    feat_dsl_title: "シナリオ DSL",
    feat_dsl_desc: "シナリオ作成のためのカスタムドメイン特化言語。シンプルな構文、26のコマンド、モッドで拡張可能。",

    feat_rpg_title: "RPG システム",
    feat_rpg_desc: "レベル、経験値、クエスト、インベントリ、クラフト、スキル、収集可能なレアリティカードを持つキャラクター。",

    feat_battle_title: "ターン制バトル",
    feat_battle_desc: "2つの同等のバトルモード——ターン制とファイナルファンタジー式。モッドでオンザフライ切り替え。",

    feat_mod_title: "モッドシステム",
    feat_mod_desc: "ゲームは透明な「レンガ」から組み立てられます。各モッドを検査、削除して、ゲームがどのように変化するかを確認できます。",

    arch_title: "モッド指向アーキテクチャ",
    arch_kernel: "カーネル (C#)",
    arch_kernel_desc: "リソースのない軽量コア。モッドを読み込み・有効化し、C# コードをコンパイル（Roslyn）、競合を解決。",
    arch_mods: "モッド (mods/)",
    arch_mods_desc: "シーン、シナリオ (.scenario)、テクスチャ、サウンド、シェーダー、JSON データ、C# ソースコード。ゲームを構成するすべて。",
    arch_game: "ゲーム",
    arch_game_desc: "アクティブなモッドから実行時に組み立てられます。ターン制バトルモッドを削除し、FF を追加——これでファイナルファンタジー式戦闘に。元に戻すと——再びターン制に。食べ物モッドを追加——料理し、食べ、仲間に食事を与える必要があります。削除——サバイバルの心配なし。",
    arch_footer: "モッドの変更に Godot や SDK は不要です。テキストエディタ、JSON、少しの C#（任意）——これだけでゲームメカニクスを変更または作成できます。",

    screenshots_title: "スクリーンショット",
    screenshot_n: "スクリーンショット",

    videos_title: "動画",
    video_lighting_title: "ライティングと時間帯",

    story_title: "ストーリー",
    story_p1: "Cthangover（クトゥルフ + 二日酔い）は、偉大なるアル・アジフ、別名「死者の書」が、唯一無敵の敵——慢性アルコール中毒者——に出会った物語です。",
    story_p2: "平行現実から来たダメ人間の主人公は、見知らぬ言語の数行を偶然読み、その後天空に亀裂が走り、触手、鉤爪、そして夢の彼方の世界からの名状しがたきものたちが流れ込みます。人類は混乱に陥りますが、黙示録の元凶は鼻をほじりながら、宇宙的恐怖の咆哮を幻覚だと思い込んでいます。",
    story_p3: "最強の魔道士の精神さえ砕くネクロノミコンの致命的な呪いは、酔っ払いの「貫通不能な」愚かさと化学的依存の前に粉々になります。この不条理な絆に閉じ込められた書物自体は、その存在の中で初めて行き詰まります：それは不死だが、自分に全く関心のない存在と永遠を共にする運命にあります。",
    story_p4: "ブラックユーモア、汚い言葉、不条理な状況、そして非凡なプロット。",

    systems_title: "主要サブシステム",
    systems_subtitle: "各サブシステムは C# クラス、Godot シーン、JSON データのセット——文書化され、モッドで拡張可能。",

    sys_01_title: "Roslyn モッドコンパイル",
    sys_01_desc: "C# ソースコードを Microsoft.CodeAnalysis でランタイムコンパイル、SHA-256 キャッシュ付き。",
    sys_02_title: "JSON パッチ",
    sys_02_desc: "モッド同士のデータ修正：ワイルドカード、$add/$remove/$set、identity-key マージ。",
    sys_03_title: "イベントバス",
    sys_03_desc: "二層 pub-sub：型付き C# + 文字列ベース GDScript、[EventKey] で自動ブリッジング。",
    sys_04_title: "シナリオ DSL",
    sys_04_desc: "独自の .scenario 言語、拡張可能なパーサー、35以上のコマンド戦略——独自コマンドを簡単に追加可能。",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext が全 C# API を GDScript モッドに公開：シーン、クエスト、イベント。",
    sys_06_title: "JSON → C# ファクトリー",
    sys_06_desc: "24の型安全ファクトリーが全モッドから JSON エンティティを読み込みキャッシュ。",
    sys_07_title: "モッドセーブ",
    sys_07_desc: "各モッドが IModSaveable で独自の状態を保存・読み込み——競合ゼロ。",
    sys_08_title: "依存グラフ",
    sys_08_desc: "トポロジカルモッド順序：depends 宣言 + 手動順序 + 競合検出。",

    cta_title: "参加しよう",
    cta_p1: "MIT ライセンスのオープンソースプロジェクト。",
    cta_p2: "建設的な批評、新鮮な視点、フィードバックが必要です。ビジュアルノベル、モッド開発、Godot、またはユニークなアーキテクチャに興味があれば——リポジトリをご覧ください。",
    cta_btn_repo: "リポジトリへ",
    cta_btn_license: "MIT ライセンス",

    footer_text: "© 2026 Isaev Ilya",
    footer_opensource: "オープンソース / MIT ライセンス",

    screenshot_placeholder: "画像"
  },

  ko: {
    lang_name: "한국어",
    nav_about: "소개",
    nav_features: "기능",
    nav_arch: "아키텍처",
    nav_screenshots: "스크린샷",
    nav_videos: "비디오",
    nav_story: "스토리",
    nav_systems: "시스템",
    nav_quickstart: "빠른 시작",
    nav_docs: "문서",
    nav_docs_back: "← 사이트로",
    nav_repo: "저장소",

    hero_title: "cthangover",
    hero_tagline: "RPG 요소가 있는 모드 지향 비주얼 노벨 플랫폼",
    hero_btn_repo: "저장소 소스 코드",

    about_title: "프로젝트 소개",
    about_p1: "cthangover는 독특한 모드 지향 아키텍처 위에 구축된 RPG 요소가 있는 비주얼 노벨입니다. Godot 4.6 (C# / .NET 8)으로 작성되었으며 MIT 라이선스로 배포됩니다.",
    about_p2: "Godot 프로젝트에는 리소스가 거의 없습니다. 경량 C# 커널이 mods/ 폴더의 모드에서 런타임에 완전한 게임을 조립합니다. 모드에는 장면, 시나리오, 리소스 및 C# 소스 코드가 포함되어 있으며 Roslyn을 통해 실시간 컴파일됩니다. 모드를 제거하거나 추가하면 장면, 메커니즘 및 전체 게임 하위 시스템을 제거하거나 추가할 수 있습니다.",
    about_p3: "이것은 무한한 모드 개발과 커널 위에 자신의 프로젝트를 구축할 수 있는 길을 열어줍니다. 모드를 수정하는 데 Godot나 SDK조차 필요하지 않습니다 — 텍스트 편집기와 기본 JSON 지식만으로 충분합니다.",

    features_title: "주요 기능",
    features_subtitle: "기본으로 작동하는 기능",

    feat_time_title: "셰이더 기반 시간대",
    feat_time_desc: "수백 가지 변형 대신 하나의 배경. GPU에서 주야간 단계를 자동 계산, 추가 리소스 불필요.",

    feat_light_title: "2D 조명",
    feat_light_desc: "동적 광원, 깊이 맵, 드래그 가능한 플레이어 램프가 모든 장면에 생명을 불어넣습니다.",

    feat_dsl_title: "시나리오 DSL",
    feat_dsl_desc: "시나리오 작성을 위한 사용자 정의 도메인 특화 언어. 간단한 구문, 26개 명령, 모드로 확장 가능.",

    feat_rpg_title: "RPG 시스템",
    feat_rpg_desc: "레벨, 경험치, 퀘스트, 인벤토리, 제작, 스킬, 수집 가능한 희귀도 카드를 가진 캐릭터.",

    feat_battle_title: "턴제 배틀",
    feat_battle_desc: "두 가지 동등한 전투 모드 — 턴제와 파이널 판타지식. 모드로 실시간 전환.",

    feat_mod_title: "모드 시스템",
    feat_mod_desc: "게임은 투명한 '벽돌'로 조립됩니다. 각 모드를 검사하고 제거하여 게임이 어떻게 변하는지 볼 수 있습니다.",

    arch_title: "모드 지향 아키텍처",
    arch_kernel: "커널 (C#)",
    arch_kernel_desc: "리소스 없는 경량 코어. 모드를 로드 및 활성화하고 C# 코드를 컴파일(Roslyn)하며 충돌을 해결합니다.",
    arch_mods: "모드 (mods/)",
    arch_mods_desc: "장면, 시나리오 (.scenario), 텍스처, 사운드, 셰이더, JSON 데이터, C# 소스 코드. 게임을 구성하는 모든 것.",
    arch_game: "게임",
    arch_game_desc: "활성 모드에서 런타임에 조립됩니다. 턴제 배틀 모드를 제거하고 FF를 추가 — 이제 파이널 판타지식 전투. 다시 바꾸면 — 턴제로. 음식 모드 추가 — 요리하고, 먹고, 동료에게 먹이를 줘야 합니다. 제거 — 생존 걱정 없음.",
    arch_footer: "모드 수정에 Godot나 SDK가 필요하지 않습니다. 텍스트 편집기, JSON, 약간의 C#(선택 사항) — 이것으로 게임 메커니즘을 변경하거나 만들 수 있습니다.",

    screenshots_title: "스크린샷",
    screenshot_n: "스크린샷",

    videos_title: "비디오",
    video_lighting_title: "조명과 시간",

    story_title: "스토리",
    story_p1: "Cthangover(크툴루 + 숙취)는 위대한 알-아지프, 일명 '죽은 자의 책'이 유일무이한 무적의 적 — 만성 알코올 중독자 — 를 만난 이야기입니다.",
    story_p2: "평행 현실에서 온 망가진 주인공은 낯선 언어로 된 몇 줄을 우연히 읽고, 그 후 하늘에 균열이 가고 촉수, 발톱, 꿈 너머 세계의 형언할 수 없는 것들이 쏟아져 들어옵니다. 인류는 혼돈에 빠지지만, 종말의 원흉은 코를 파며 우주적 공포의 포효를 환각으로 착각합니다.",
    story_p3: "최강 마법사의 정신조차 부수는 네크로노미콘의 치명적인 저주는 술주정뱅이의 '뚫을 수 없는' 어리석음과 화학적 의존 앞에 산산이 부서집니다. 이 부조리한 결속에 갇힌 책 자체는 그 존재에서 처음으로 막다른 골목에 다다릅니다: 불멸이지만, 자신에게 전혀 관심 없는 존재와 영원을 함께할 운명입니다.",
    story_p4: "블랙 유머, 거친 언어, 부조리한 상황, 그리고 독특한 플롯.",

    systems_title: "핵심 서브시스템",
    systems_subtitle: "각 서브시스템은 C# 클래스, Godot 장면, JSON 데이터의 집합 — 문서화되어 있고 모드로 확장 가능.",

    sys_01_title: "Roslyn 모드 컴파일",
    sys_01_desc: "C# 소스 코드를 Microsoft.CodeAnalysis로 런타임 컴파일, SHA-256 캐싱.",
    sys_02_title: "JSON 패치",
    sys_02_desc: "모드 간 데이터 패치: 와일드카드, $add/$remove/$set, identity-key 병합.",
    sys_03_title: "이벤트 버스",
    sys_03_desc: "이중 계층 pub-sub: 타입 C# + 문자열 기반 GDScript, [EventKey]로 자동 브리징.",
    sys_04_title: "시나리오 DSL",
    sys_04_desc: "커스텀 .scenario 언어, 확장 가능한 파서, 35개 이상의 명령 전략——쉽게 자신만의 명령 추가 가능.",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext가 전체 C# API를 GDScript 모드에 공개: 장면, 퀘스트, 이벤트.",
    sys_06_title: "JSON → C# 팩토리",
    sys_06_desc: "24개의 타입 안전 팩토리가 모든 모드에서 JSON 엔티티를 로드하고 캐시.",
    sys_07_title: "모드 저장",
    sys_07_desc: "각 모드가 IModSaveable을 통해 자체 상태를 저장/로드——충돌 없음.",
    sys_08_title: "의존성 그래프",
    sys_08_desc: "위상 정렬 모드 순서: depends 선언 + 수동 순서 + 충돌 감지.",

    cta_title: "참여하기",
    cta_p1: "MIT 라이선스의 오픈소스 프로젝트.",
    cta_p2: "건설적인 비판, 신선한 시각, 피드백이 필요합니다. 비주얼 노벨, 모드 개발, Godot 또는 독특한 아키텍처에 관심이 있다면 — 저장소를 확인하세요.",
    cta_btn_repo: "저장소로 이동",
    cta_btn_license: "MIT 라이선스",

    footer_text: "© 2026 Isaev Ilya",
    footer_opensource: "오픈소스 / MIT 라이선스",

    screenshot_placeholder: "이미지"
  },

  vi: {
    lang_name: "Tiếng Việt",
    nav_about: "Giới thiệu",
    nav_features: "Tính năng",
    nav_arch: "Kiến trúc",
    nav_screenshots: "Ảnh chụp",
    nav_videos: "Video",
    nav_story: "Cốt truyện",
    nav_systems: "Hệ thống",
    nav_quickstart: "Bắt đầu nhanh",
    nav_docs: "Tài liệu",
    nav_docs_back: "← Về trang",
    nav_repo: "Kho lưu trữ",

    hero_title: "cthangover",
    hero_tagline: "Nền tảng visual novel hướng mod với yếu tố RPG",
    hero_btn_repo: "Mã nguồn trong kho lưu trữ",

    about_title: "Về dự án",
    about_p1: "cthangover là một visual novel có yếu tố RPG, được xây dựng trên kiến trúc hướng mod độc đáo. Viết bằng Godot 4.6 (C# / .NET 8) và phân phối theo giấy phép MIT.",
    about_p2: "Dự án Godot hầu như không chứa tài nguyên. Một nhân C# nhẹ lắp ráp trò chơi hoàn chỉnh trong thời gian chạy từ các mod trong thư mục mods/. Mod chứa cảnh, kịch bản, tài nguyên và mã nguồn C# — được biên dịch ngay lập tức qua Roslyn. Bằng cách xóa hoặc thêm mod, bạn có thể xóa hoặc thêm cảnh, cơ chế và toàn bộ hệ thống con của trò chơi.",
    about_p3: "Điều này mở ra con đường phát triển mod vô hạn và xây dựng dự án riêng trên nền nhân. Để sửa đổi mod, bạn thậm chí không cần Godot hoặc SDK — chỉ cần trình soạn thảo văn bản và kiến thức JSON cơ bản.",

    features_title: "Tính năng chính",
    features_subtitle: "Những gì đã hoạt động ngay từ đầu",

    feat_time_title: "Thời gian trong ngày bằng Shader",
    feat_time_desc: "Một hình nền thay cho hàng trăm biến thể. Tự động tính toán pha ngày/đêm trên GPU, không cần thêm tài nguyên.",

    feat_light_title: "Ánh sáng 2D",
    feat_light_desc: "Nguồn sáng động, bản đồ độ sâu và đèn kéo thả của người chơi thổi hồn vào mỗi cảnh.",

    feat_dsl_title: "DSL Kịch bản",
    feat_dsl_desc: "Ngôn ngữ chuyên biệt tùy chỉnh để viết kịch bản. Cú pháp đơn giản, 26 lệnh, mở rộng qua mod.",

    feat_rpg_title: "Hệ thống RPG",
    feat_rpg_desc: "Nhân vật với cấp độ, kinh nghiệm, nhiệm vụ, túi đồ, chế tạo, kỹ năng và thẻ độ hiếm sưu tập.",

    feat_battle_title: "Chiến đấu theo lượt",
    feat_battle_desc: "Hai chế độ chiến đấu ngang hàng — theo lượt và phong cách Final Fantasy. Chuyển đổi linh hoạt qua mod.",

    feat_mod_title: "Hệ thống Mod",
    feat_mod_desc: "Trò chơi được lắp ráp từ những 'viên gạch' trong suốt. Mỗi mod có thể được kiểm tra, gỡ bỏ để thấy trò chơi thay đổi ra sao.",

    arch_title: "Kiến trúc hướng Mod",
    arch_kernel: "Nhân (C#)",
    arch_kernel_desc: "Lõi nhẹ không tài nguyên. Tải và kích hoạt mod, biên dịch mã C# (Roslyn), giải quyết xung đột.",
    arch_mods: "Mod (mods/)",
    arch_mods_desc: "Cảnh, kịch bản (.scenario), họa tiết, âm thanh, shader, dữ liệu JSON, mã nguồn C#. Mọi thứ tạo nên trò chơi.",
    arch_game: "Trò chơi",
    arch_game_desc: "Được lắp ráp trong thời gian chạy từ các mod đang hoạt động. Gỡ mod chiến đấu theo lượt, thêm FF — giờ bạn có chiến đấu kiểu Final Fantasy. Đổi lại — quay về theo lượt. Thêm mod thức ăn — giờ bạn nấu, ăn và cho đồng đội ăn. Gỡ đi — không lo sinh tồn.",
    arch_footer: "Bạn không cần Godot hay SDK để sửa mod. Trình soạn thảo văn bản, JSON và một chút C# (tùy chọn) — đủ để thay đổi hoặc tạo cơ chế trò chơi.",

    screenshots_title: "Ảnh chụp màn hình",
    screenshot_n: "Ảnh chụp",

    videos_title: "Video",
    video_lighting_title: "Ánh sáng & Thời gian",

    story_title: "Cốt truyện",
    story_p1: "Cthangover (Cthulhu + Nôn nao) là câu chuyện về cách Al-Azif vĩ đại, còn được gọi là 'Cuốn sách của Người chết', gặp kẻ thù bất khả chiến bại duy nhất của mình — một kẻ nghiện rượu mãn tính.",
    story_p2: "Nhân vật chính, một kẻ bất hảo từ thực tại song song, vô tình đọc vài dòng bằng ngôn ngữ lạ, sau đó bầu trời nứt toác và xúc tu, móng vuốt cùng những hình thù không thể miêu tả từ các thế giới bên kia giấc mơ tràn vào. Nhân loại chìm trong hỗn loạn, nhưng thủ phạm của ngày tận thế thì thản nhiên ngoáy mũi, nhầm tiếng gầm của kinh hoàng vũ trụ với ảo giác.",
    story_p3: "Lời nguyền chết chóc của Necronomicon, thứ bẻ gãy tâm trí của những pháp sư mạnh nhất, tan vỡ trước sự ngu ngốc 'không thể xuyên thủng' và sự lệ thuộc hóa chất của một gã say. Chính Cuốn sách, bị mắc kẹt trong mối ràng buộc phi lý này, lần đầu tiên trong sự tồn tại của mình rơi vào ngõ cụt: nó bất tử, nhưng phải chia sẻ vĩnh hằng với một sinh vật chẳng thèm quan tâm đến nó.",
    story_p4: "Hài hước đen, ngôn từ thô tục, tình huống phi lý và cốt truyện độc đáo.",

    systems_title: "Hệ thống con Cốt lõi",
    systems_subtitle: "Mỗi hệ thống con là một tập hợp các lớp C#, cảnh Godot và dữ liệu JSON — được ghi chép và mở rộng qua mod.",

    sys_01_title: "Biên dịch Mod Roslyn",
    sys_01_desc: "Mã nguồn C# được biên dịch thời gian chạy qua Microsoft.CodeAnalysis với bộ nhớ đệm SHA-256.",
    sys_02_title: "JSON Patch",
    sys_02_desc: "Mod vá dữ liệu của nhau: wildcard, $add/$remove/$set, identity-key merge.",
    sys_03_title: "Bus Sự kiện",
    sys_03_desc: "Pub-sub hai lớp: C# có kiểu + GDScript dựa trên chuỗi với cầu nối tự động qua [EventKey].",
    sys_04_title: "DSL Kịch bản",
    sys_04_desc: "Ngôn ngữ .scenario tùy chỉnh, trình phân tích cú pháp mở rộng, 35+ chiến lược lệnh — dễ dàng thêm lệnh của riêng bạn.",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext mở toàn bộ API C# cho mod GDScript: cảnh, nhiệm vụ, sự kiện.",
    sys_06_title: "Factory JSON → C#",
    sys_06_desc: "24 factory an toàn kiểu tải thực thể JSON từ tất cả mod và lưu vào bộ nhớ đệm.",
    sys_07_title: "Lưu Mod",
    sys_07_desc: "Mỗi mod lưu và tải trạng thái riêng qua IModSaveable — không xung đột.",
    sys_08_title: "Đồ thị Phụ thuộc",
    sys_08_desc: "Sắp xếp topo thứ tự mod: khai báo depends + thứ tự thủ công + phát hiện xung đột.",

    cta_title: "Tham gia",
    cta_p1: "Dự án mã nguồn mở theo giấy phép MIT.",
    cta_p2: "Cần phê bình mang tính xây dựng, góc nhìn mới và phản hồi. Nếu bạn quan tâm đến visual novel, modding, Godot hoặc kiến trúc độc đáo — hãy ghé kho lưu trữ.",
    cta_btn_repo: "Đến kho lưu trữ",
    cta_btn_license: "Giấy phép MIT",

    footer_text: "© 2026 Isaev Ilya",
    footer_opensource: "Mã nguồn mở / Giấy phép MIT",

    screenshot_placeholder: "Hình ảnh"
  },

  es: {
    lang_name: "Español",
    nav_about: "Acerca de",
    nav_features: "Características",
    nav_arch: "Arquitectura",
    nav_screenshots: "Capturas",
    nav_videos: "Videos",
    nav_story: "Historia",
    nav_systems: "Sistemas",
    nav_quickstart: "Inicio rápido",
    nav_docs: "Documentación",
    nav_docs_back: "← Volver al sitio",
    nav_repo: "Repositorio",

    hero_title: "cthangover",
    hero_tagline: "Plataforma de novelas visuales orientada a mods con elementos RPG",
    hero_btn_repo: "Código fuente en el repositorio",

    about_title: "Acerca del Proyecto",
    about_p1: "cthangover es una novela visual con elementos RPG, construida sobre una arquitectura única orientada a mods. Escrito en Godot 4.6 (C# / .NET 8) y distribuido bajo la licencia MIT.",
    about_p2: "El proyecto Godot casi no contiene recursos. Un núcleo C# ligero ensambla un juego completo en tiempo de ejecución desde los mods ubicados en la carpeta mods/. Los mods contienen escenas, guiones, recursos y código fuente C# — compilado al vuelo mediante Roslyn. Al eliminar o agregar mods, puedes eliminar o agregar escenas, mecánicas y subsistemas completos del juego.",
    about_p3: "Esto abre la puerta al modding ilimitado y al desarrollo de tus propios proyectos sobre el núcleo. Para modificar mods ni siquiera necesitas Godot o un SDK — un editor de texto y conocimientos básicos de JSON son suficientes.",

    features_title: "Características Clave",
    features_subtitle: "Lo que ya funciona desde el inicio",

    feat_time_title: "Hora del día por Shader",
    feat_time_desc: "Un solo fondo en lugar de cientos de variaciones. Cálculo automático de fases día/noche directamente en la GPU, sin recursos adicionales.",

    feat_light_title: "Iluminación 2D",
    feat_light_desc: "Fuentes de luz dinámicas, mapas de profundidad y una lámpara arrastrable del jugador dan vida a cada escena.",

    feat_dsl_title: "DSL de Guiones",
    feat_dsl_desc: "Un lenguaje de dominio específico personalizado para escribir guiones. Sintaxis simple, 26 comandos, extensible mediante mods.",

    feat_rpg_title: "Sistemas RPG",
    feat_rpg_desc: "Personajes con niveles, experiencia, misiones, inventario, fabricación, habilidades y cartas de rareza coleccionables.",

    feat_battle_title: "Batalla por Turnos",
    feat_battle_desc: "Dos modos de batalla equivalentes — por turnos y estilo Final Fantasy. Se cambian al vuelo mediante mods.",

    feat_mod_title: "Sistema de Mods",
    feat_mod_desc: "El juego se ensambla a partir de 'ladrillos' transparentes. Cada mod puede inspeccionarse, retirarse y observar cómo cambia el juego.",

    arch_title: "Arquitectura Orientada a Mods",
    arch_kernel: "Núcleo (C#)",
    arch_kernel_desc: "Un núcleo ligero sin recursos. Carga y activa mods, compila código C# (Roslyn), resuelve conflictos.",
    arch_mods: "Mods (mods/)",
    arch_mods_desc: "Escenas, guiones (.scenario), texturas, sonidos, shaders, datos JSON, código fuente C#. Todo lo que compone el juego.",
    arch_game: "Juego",
    arch_game_desc: "Ensamblado en tiempo de ejecución desde los mods activos. Quitas el mod de batalla por turnos, agregas FF — ahora tienes combate estilo Final Fantasy. Los cambias de nuevo — vuelta a los turnos. Agregas un mod de comida — ahora cocinas, comes y alimentas a tus compañeros. Lo quitas — sin preocupaciones de supervivencia.",
    arch_footer: "No necesitas Godot ni un SDK para modificar mods. Un editor de texto, JSON y un poco de C# (opcional) — es suficiente para cambiar o crear una mecánica de juego.",

    screenshots_title: "Capturas de Pantalla",
    screenshot_n: "Captura",

    videos_title: "Videos",
    video_lighting_title: "Iluminación y Hora del Día",

    story_title: "Historia",
    story_p1: "Cthangover (Cthulhu + Resaca) es la historia de cómo el gran Al-Azif, también conocido como el 'Libro de los Muertos', conoció a su único e invencible enemigo — un alcohólico crónico.",
    story_p2: "El protagonista, un sujeto perdido de una realidad paralela, leyó accidentalmente un par de líneas en un idioma desconocido, tras lo cual el firmamento se quebró y tentáculos, garras y formas indescriptibles de mundos más allá del sueño brotaron. La humanidad cayó en el caos, pero el culpable del apocalipsis sigue hurgándose la nariz, confundiendo el rugido de los horrores cósmicos con alucinaciones.",
    story_p3: "La maldición mortal del Necronomicón, que quiebra la mente de los magos más poderosos, se estrella contra la estupidez 'impenetrable' y la dependencia química de un borracho. El propio Libro, atrapado en este vínculo absurdo, se encuentra por primera vez en su existencia en un callejón sin salida: es inmortal, pero condenado a compartir la eternidad con un ser al que no le importa en absoluto.",
    story_p4: "Humor negro, lenguaje soez, situaciones absurdas y una trama inusual.",

    systems_title: "Subsistemas Clave",
    systems_subtitle: "Cada subsistema es un conjunto de clases C#, escenas Godot y datos JSON — documentados y extensibles mediante mods.",

    sys_01_title: "Compilación Roslyn de Mods",
    sys_01_desc: "Código C# compilado en tiempo de ejecución mediante Microsoft.CodeAnalysis con caché SHA-256.",
    sys_02_title: "Parches JSON",
    sys_02_desc: "Los mods modifican los datos de otros mods: wildcards, $add/$remove/$set, identity-key merging.",
    sys_03_title: "Bus de Eventos",
    sys_03_desc: "Pub-sub de doble capa: C# tipado + GDScript basado en cadenas con puenteo automático vía [EventKey].",
    sys_04_title: "DSL de Escenarios",
    sys_04_desc: "Lenguaje .scenario personalizado, parser extensible, 35+ estrategias de comando — añade tus propios comandos fácilmente.",
    sys_05_title: "C# ↔ GDScript",
    sys_05_desc: "GDActionContext expone la API completa de C# a mods GDScript: escenas, misiones, eventos.",
    sys_06_title: "Fábricas JSON → C#",
    sys_06_desc: "24 fábricas con seguridad de tipos cargan entidades JSON de todos los mods y las cachean.",
    sys_07_title: "Guardado por Mod",
    sys_07_desc: "Cada mod guarda y carga su propio estado mediante IModSaveable — sin conflictos.",
    sys_08_title: "Grafo de Dependencias",
    sys_08_desc: "Ordenamiento topológico de mods: declaraciones depends + orden manual + detección de conflictos.",

    cta_title: "Participa",
    cta_p1: "Proyecto de código abierto bajo la licencia MIT.",
    cta_p2: "Se necesita crítica constructiva, perspectivas frescas y retroalimentación. Si te interesan las novelas visuales, el modding, Godot o simplemente una arquitectura inusual — visita el repositorio.",
    cta_btn_repo: "Ir al repositorio",
    cta_btn_license: "Licencia MIT",

    footer_text: "© 2026 Isaev Ilya",
    footer_opensource: "Código Abierto / Licencia MIT",

    screenshot_placeholder: "Imagen"
  }
};
