using Godot;

namespace Cthangover.Core.UI.Theming
{
    /// <summary>
    /// Central design tokens of the Cthangover UI. Single source of truth for
    /// colours, radii, font sizes and spacing at runtime; mirrors
    /// <c>mockups/style_guide/tokens.css</c>. Panels must not hardcode colours —
    /// they reference these tokens (directly or through <see cref="CthangoverTheme"/>
    /// and <see cref="UiKit"/>). Two materials share one palette: "graphite"
    /// (panels, menus, inventory, actions) and "paper" (quest journal pages).
    /// </summary>
    public static class UiTokens
    {
        // ── Backdrop and surfaces: graphite ─────────────────────────────
        /// <summary>Page-level background behind everything, near-black graphite.</summary>
        public static readonly Color Page = Color.FromHtml("07080b");
        /// <summary>Full-screen backdrop behind the topmost overlay.</summary>
        public static readonly Color Backdrop = new(0f, 0f, 0f, 0.9f);
        /// <summary>Base surface — panels, buttons, cards.</summary>
        public static readonly Color Surface0 = Color.FromHtml("0e0f15");
        /// <summary>Secondary surface for rows and nested panels.</summary>
        public static readonly Color Surface1 = Color.FromHtml("171923");
        /// <summary>Primary surface for cards and buttons.</summary>
        public static readonly Color Surface2 = Color.FromHtml("101118");
        /// <summary>Tertiary surface for the most inset blocks.</summary>
        public static readonly Color Surface3 = Color.FromHtml("1b1d26");
        /// <summary>Raised panel surface for prominent cards.</summary>
        public static readonly Color SurfaceRaised = Color.FromHtml("1a1c26");
        /// <summary>Hover-state surface one step above <see cref="Surface0"/>.</summary>
        public static readonly Color SurfaceHover = Color.FromHtml("1b1e2a");
        /// <summary>Selected-state surface with a warm gold cast.</summary>
        public static readonly Color SurfaceSelected = Color.FromHtml("1e1a10");
        /// <summary>Deepest well — text inputs and inset wells.</summary>
        public static readonly Color Well = Color.FromHtml("0d0e14");
        /// <summary>Fill behind count badges.</summary>
        public static readonly Color CountBg = Color.FromHtml("242733");

        // ── Surfaces: paper journal ─────────────────────────────────────
        /// <summary>Top gradient stop of the paper journal surface.</summary>
        public static readonly Color PaperTop = Color.FromHtml("261f18");
        /// <summary>Paper journal surface (quest pages), warm dark brown.</summary>
        public static readonly Color Paper = Color.FromHtml("15110d");
        /// <summary>Border colour of the paper journal surface.</summary>
        public static readonly Color PaperLine = Color.FromHtml("3a3229");
        /// <summary>Inset border of paper wells (journal page frames).</summary>
        public static readonly Color PaperInset = new(200f / 255f, 170f / 255f, 120f / 255f, 0.16f);
        /// <summary>Warm ruled lines drawn across journal pages.</summary>
        public static readonly Color PaperRule = new(200f / 255f, 170f / 255f, 120f / 255f, 0.45f);
        /// <summary>Wax-seal brown for journal stamps and seals.</summary>
        public static readonly Color Seal = Color.FromHtml("a2603f");
        /// <summary>Brighter seal tone for readable stamp text.</summary>
        public static readonly Color SealText = Color.FromHtml("cd8f66");
        /// <summary>Very soft seal wash for stamped backgrounds.</summary>
        public static readonly Color SealSoft = new(162f / 255f, 96f / 255f, 63f / 255f, 0.07f);

        // ── Borders ─────────────────────────────────────────────────────
        /// <summary>Default border colour for cards, inputs, and separators.</summary>
        public static readonly Color Line = Color.FromHtml("2a2c38");
        /// <summary>Softest border for nested separators.</summary>
        public static readonly Color LineSoft = Color.FromHtml("262935");
        /// <summary>Border of cards and interactive cells.</summary>
        public static readonly Color LineCard = Color.FromHtml("2f3242");
        /// <summary>Border of raised cards and emphasized containers.</summary>
        public static readonly Color LineStrong = Color.FromHtml("4a4e61");
        /// <summary>Faintest border for subtle row separators.</summary>
        public static readonly Color LineFaint = Color.FromHtml("3a3d4d");
        /// <summary>Disabled-state border colour.</summary>
        public static readonly Color LineDisabled = Color.FromHtml("23252f");

        // ── Text ────────────────────────────────────────────────────────
        /// <summary>Brightest ink — headings and hero copy on dark surfaces.</summary>
        public static readonly Color InkBright = Color.FromHtml("f4f0e7");
        /// <summary>Primary ink for titles and emphasized copy.</summary>
        public static readonly Color Ink = Color.FromHtml("e8e5de");
        /// <summary>Standard body copy.</summary>
        public static readonly Color Body = Color.FromHtml("c9c5bc");
        /// <summary>Secondary text — captions, legends, metadata.</summary>
        public static readonly Color Muted = Color.FromHtml("8b8894");
        /// <summary>Least prominent text — disabled hints and footnotes.</summary>
        public static readonly Color Faint = Color.FromHtml("5c5966");

        // ── Accent: gold ────────────────────────────────────────────────
        /// <summary>Primary gold accent — selection, focus, and highlights.</summary>
        public static readonly Color Accent = Color.FromHtml("d9a441");
        /// <summary>Bright gold for emphasized text and icon glyphs.</summary>
        public static readonly Color AccentBright = Color.FromHtml("f0c169");
        /// <summary>Hover-brightened gold for interactive text and glyphs.</summary>
        public static readonly Color AccentHover = Color.FromHtml("ffe1a0");
        /// <summary>Dimmed gold for captions and secondary accents.</summary>
        public static readonly Color AccentDim = Color.FromHtml("a8946a");
        /// <summary>Soft accent fill for chips and selected rows.</summary>
        public static readonly Color AccentSoft = Alpha(Accent, 0.08f);
        /// <summary>Slight accent trace for subtle dividers.</summary>
        public static readonly Color AccentTrace = Alpha(Accent, 0.12f);
        /// <summary>Accent glow used behind emphasized cards.</summary>
        public static readonly Color AccentGlow = Alpha(Accent, 0.1f);
        /// <summary>Barely-visible accent wash for large surfaces.</summary>
        public static readonly Color AccentFaint = Alpha(Accent, 0.06f);
        /// <summary>Focused selection ring around controls.</summary>
        public static readonly Color AccentRing = Alpha(Accent, 0.5f);
        /// <summary>Stronger accent border for hover and selected states.</summary>
        public static readonly Color AccentLine = Alpha(Accent, 0.45f);
        /// <summary>Standard accent border for gold-outlined elements.</summary>
        public static readonly Color AccentLineStrong = Alpha(Accent, 0.75f);
        /// <summary>Weakest accent border, for hairline gold dividers.</summary>
        public static readonly Color AccentLineWeak = Alpha(Accent, 0.2f);

        // ── Status ──────────────────────────────────────────────────────
        /// <summary>Danger red for destructive buttons and error states.</summary>
        public static readonly Color Danger = Color.FromHtml("a63d40");
        /// <summary>Readable red for danger text on dark surfaces.</summary>
        public static readonly Color DangerText = Color.FromHtml("e8a9ab");
        /// <summary>Soft danger fill behind error banners and locked badges.</summary>
        public static readonly Color DangerSoft = Alpha(Danger, 0.15f);
        /// <summary>Saturated success green for fills.</summary>
        public static readonly Color Success = Color.FromHtml("8fae6b");
        /// <summary>Readable green for success text on dark surfaces.</summary>
        public static readonly Color SuccessText = Color.FromHtml("b9d3ae");
        /// <summary>Soft success fill behind badges and banners.</summary>
        public static readonly Color SuccessSoft = Alpha(Success, 0.1f);
        /// <summary>Caution amber for warnings.</summary>
        public static readonly Color Warning = Color.FromHtml("cfa96a");
        /// <summary>Informational blue, also the shield/defence colour.</summary>
        public static readonly Color Info = Color.FromHtml("7f95b5");
        /// <summary>Positive checkmark colour, alias of <see cref="SuccessText"/>.</summary>
        public static readonly Color Ok = Color.FromHtml("b9d3ae");

        // ── Type accents (items and actions) ────────────────────────────
        /// <summary>Quest-type accent (gold) for cards and list rows.</summary>
        public static readonly Color TypeQuest = Color.FromHtml("d9a441");
        /// <summary>Used/already-spent accent (orange); second half of the map zone hatch pair.</summary>
        public static readonly Color TypeUsed = Color.FromHtml("cfa96a");
        /// <summary>Target highlight accent (teal).</summary>
        public static readonly Color TypeTarget = Color.FromHtml("4fb3a5");
        /// <summary>Food/consumable accent (green).</summary>
        public static readonly Color TypeFood = Color.FromHtml("8fae6b");
        /// <summary>Resource item accent (blue).</summary>
        public static readonly Color TypeResource = Color.FromHtml("7f95b5");
        /// <summary>Recipe type accent (violet).</summary>
        public static readonly Color TypeRecipe = Color.FromHtml("c78fc9");
        /// <summary>Enemy accent (red) — also the locked-zone marker badge.</summary>
        public static readonly Color TypeEnemy = Color.FromHtml("c96b6e");
        /// <summary>Self-target accent (blue).</summary>
        public static readonly Color TypeSelf = Color.FromHtml("7f95b5");
        /// <summary>Passive ability accent (green).</summary>
        public static readonly Color TypePassive = Color.FromHtml("8fae6b");
        /// <summary>Active action-card accent (purple).</summary>
        public static readonly Color TypeActive = Color.FromHtml("c78fc9");

        // ── Health, AP, cooldown, stacks ────────────────────────────────
        /// <summary>Healthy portion of health bars and positive status numbers.</summary>
        public static readonly Color HpOk = Color.FromHtml("8fae6b");
        /// <summary>Low-health portion of health bars and damage numbers.</summary>
        public static readonly Color HpLow = Color.FromHtml("c96b6e");
        /// <summary>Health bar text tint over the fill.</summary>
        public static readonly Color HpText = Color.FromHtml("9dbb8f");
        /// <summary>
        /// Defence segment of the layered health bar: blue fill drawn over the
        /// health fill while a shield absorbs damage. Same blue as
        /// <see cref="Info"/> and the floating defence numbers of
        /// <c>ShowDamageBehaviour.SpawnDefence</c>, so one colour always means
        /// "protection". Mirrors <c>--ct-hp-shield</c> in tokens.css.
        /// </summary>
        public static readonly Color HpShield = Color.FromHtml("7f95b5");
        /// <summary>Cooldown sweep colour for ability slots.</summary>
        public static readonly Color Cooldown = Color.FromHtml("d9c79a");
        /// <summary>Stack count text over item cells.</summary>
        public static readonly Color Stack = Color.FromHtml("f0ece2");

        // ── Details ─────────────────────────────────────────────────────
        /// <summary>Base fill of primary buttons (dark warm brown).</summary>
        public static readonly Color BtnPrimary = Color.FromHtml("241f12");
        /// <summary>Hover fill of primary buttons.</summary>
        public static readonly Color BtnPrimaryHover = Color.FromHtml("2f2816");
        /// <summary>Hover fill of secondary buttons.</summary>
        public static readonly Color BtnHover = Color.FromHtml("232633");
        /// <summary>Disabled button fill.</summary>
        public static readonly Color BtnDisabled = Color.FromHtml("15161d");
        /// <summary>Idle fill of ribbon tabs (top navigation).</summary>
        public static readonly Color Ribbon = Color.FromHtml("2a241b");
        /// <summary>Hover fill of ribbon tabs.</summary>
        public static readonly Color RibbonHover = Color.FromHtml("38301f");
        /// <summary>Ribbon tab text on the idle ribbon fill.</summary>
        public static readonly Color RibbonText = Color.FromHtml("d9cdb2");
        /// <summary>Active ribbon tab text on dark fill.</summary>
        public static readonly Color RibbonTextActive = Color.FromHtml("1a1409");
        /// <summary>Background of character stat blocks.</summary>
        public static readonly Color StatBg = Color.FromHtml("161822");
        /// <summary>Separator lines inside stat blocks.</summary>
        public static readonly Color StatLine = Color.FromHtml("2c2f3c");
        /// <summary>Key text of key/value chips (e.g. quest reward labels).</summary>
        public static readonly Color ChipKey = Color.FromHtml("7d7461");
        /// <summary>Value text of key/value chips (e.g. quest reward counts).</summary>
        public static readonly Color ChipValue = Color.FromHtml("b8a06d");
        /// <summary>Item drop-target tint.</summary>
        public static readonly Color DropBg = Color.FromHtml("1a2019");
        /// <summary>Translucent black sidebar backdrop over scenes.</summary>
        public static readonly Color SideBg = new(0f, 0f, 0f, 0.2f);
        /// <summary>Pure white for icons and highlights that must not tint.</summary>
        public static readonly Color White = Colors.White;
        /// <summary>Near-invisible white wash for subtle surfaces.</summary>
        public static readonly Color WhiteTrace = new(1f, 1f, 1f, 0.04f);
        /// <summary>Soft accent fill behind large count badges.</summary>
        public static readonly Color AccentCount = Alpha(Accent, 0.22f);
        /// <summary>Fill of a hovered battle target highlight.</summary>
        public static readonly Color TargetHover = Color.FromHtml("13191b");
        /// <summary>Border of a hovered battle target highlight.</summary>
        public static readonly Color TargetLine = new(79f / 255f, 179f / 255f, 165f / 255f, 0.65f);
        /// <summary>Semi-opaque backdrop of slot tags and map labels.</summary>
        public static readonly Color SlotTagBg = new(13f / 255f, 14f / 255f, 20f / 255f, 0.85f);
        /// <summary>Tooltip backdrop.</summary>
        public static readonly Color TooltipBg = new(16f / 255f, 17f / 255f, 24f / 255f, 0.97f);
        /// <summary>Toast notification backdrop.</summary>
        public static readonly Color ToastBg = new(33f / 255f, 33f / 255f, 33f / 255f, 0.8f);
        /// <summary>Scrollbar thumb fill.</summary>
        public static readonly Color ScrollThumb = Color.FromHtml("252734");
        /// <summary>Fill of the cooldown sweep overlay.</summary>
        public static readonly Color CooldownSoft = Alpha(Cooldown, 0.08f);
        /// <summary>Border of the cooldown sweep arc.</summary>
        public static readonly Color CooldownLine = Alpha(Cooldown, 0.45f);

        // ── Radii ───────────────────────────────────────────────────────
        public const int RadiusXs = 3;
        /// <summary>Chip, pill and small tag radius.</summary>
        public const int RadiusSm = 4;
        /// <summary>Default button and input radius.</summary>
        public const int RadiusMd = 6;
        /// <summary>Card and panel corner radius.</summary>
        public const int RadiusLg = 8;

        // ── Spacing ─────────────────────────────────────────────────────
        public const int Space1 = 4;
        /// <summary>Hairline gap for paired elements.</summary>
        public const int Space2 = 6;
        /// <summary>Minimal gap for icon stacks.</summary>
        public const int Space3 = 8;
        /// <summary>Tight gap inside chips and badges.</summary>
        public const int Space4 = 10;
        /// <summary>Compact gap between label and value.</summary>
        public const int Space5 = 12;
        /// <summary>Gap inside cards and list rows.</summary>
        public const int Space6 = 14;
        /// <summary>Standard gap between related controls.</summary>
        public const int Space7 = 16;
        /// <summary>Large gap around page-level blocks.</summary>
        public const int Space8 = 24;
        /// <summary>Extra-large gap between major panel sections.</summary>
        public const int Space9 = 32;

        // ── Font sizes ──────────────────────────────────────────────────
        /// <summary>Micro text — marker state notes and fine print.</summary>
        public const int FontMicro = 9;
        /// <summary>Badges, notes and tiny captions.</summary>
        public const int FontTiny = 10;
        /// <summary>Small labels, legend text and hints.</summary>
        public const int FontSmall = 11;
        /// <summary>Compact body size for dense panels.</summary>
        public const int FontBodySmall = 12;
        /// <summary>Default body text size.</summary>
        public const int FontBody = 13;
        /// <summary>Large body text for readable primary copy.</summary>
        public const int FontBodyLarge = 14;
        /// <summary>Lead paragraph size for emphasized intros.</summary>
        public const int FontLead = 16;
        /// <summary>Minor heading size for in-panel section names.</summary>
        public const int FontH3 = 19;
        /// <summary>Card heading size (location card name, panel titles).</summary>
        public const int FontTitle = 24;
        /// <summary>Secondary heading size (e.g. map HUD title).</summary>
        public const int FontH2 = 25;
        /// <summary>Largest display size — hero titles of full-screen menus.</summary>
        public const int FontH1 = 30;

        /// <summary>Returns the colour with a replaced alpha channel.</summary>
        public static Color Alpha(Color color, float alpha)
            => new(color.R, color.G, color.B, alpha);

        /// <summary>Badge fill (9%) and border (50%) derived from a type accent.</summary>
        public static Color BadgeFill(Color accent) => Alpha(accent, 0.09f);
        /// <summary>Border half of a type badge: 50% accent alpha.</summary>
        public static Color BadgeLine(Color accent) => Alpha(accent, 0.5f);
    }
}
