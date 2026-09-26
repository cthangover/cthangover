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
        public static readonly Color Page = Color.FromHtml("07080b");
        public static readonly Color Backdrop = new(0f, 0f, 0f, 0.9f);
        public static readonly Color Surface0 = Color.FromHtml("0e0f15");
        public static readonly Color Surface1 = Color.FromHtml("171923");
        public static readonly Color Surface2 = Color.FromHtml("101118");
        public static readonly Color Surface3 = Color.FromHtml("1b1d26");
        public static readonly Color SurfaceRaised = Color.FromHtml("1a1c26");
        public static readonly Color SurfaceHover = Color.FromHtml("1b1e2a");
        public static readonly Color SurfaceSelected = Color.FromHtml("1e1a10");
        public static readonly Color Well = Color.FromHtml("0d0e14");
        public static readonly Color CountBg = Color.FromHtml("242733");

        // ── Surfaces: paper journal ─────────────────────────────────────
        public static readonly Color PaperTop = Color.FromHtml("261f18");
        public static readonly Color Paper = Color.FromHtml("15110d");
        public static readonly Color PaperLine = Color.FromHtml("3a3229");
        public static readonly Color PaperInset = new(200f / 255f, 170f / 255f, 120f / 255f, 0.16f);
        public static readonly Color PaperRule = new(200f / 255f, 170f / 255f, 120f / 255f, 0.45f);
        public static readonly Color Seal = Color.FromHtml("a2603f");
        public static readonly Color SealText = Color.FromHtml("cd8f66");
        public static readonly Color SealSoft = new(162f / 255f, 96f / 255f, 63f / 255f, 0.07f);

        // ── Borders ─────────────────────────────────────────────────────
        public static readonly Color Line = Color.FromHtml("2a2c38");
        public static readonly Color LineSoft = Color.FromHtml("262935");
        public static readonly Color LineCard = Color.FromHtml("2f3242");
        public static readonly Color LineStrong = Color.FromHtml("4a4e61");
        public static readonly Color LineFaint = Color.FromHtml("3a3d4d");
        public static readonly Color LineDisabled = Color.FromHtml("23252f");

        // ── Text ────────────────────────────────────────────────────────
        public static readonly Color InkBright = Color.FromHtml("f4f0e7");
        public static readonly Color Ink = Color.FromHtml("e8e5de");
        public static readonly Color Body = Color.FromHtml("c9c5bc");
        public static readonly Color Muted = Color.FromHtml("8b8894");
        public static readonly Color Faint = Color.FromHtml("5c5966");

        // ── Accent: gold ────────────────────────────────────────────────
        public static readonly Color Accent = Color.FromHtml("d9a441");
        public static readonly Color AccentBright = Color.FromHtml("f0c169");
        public static readonly Color AccentHover = Color.FromHtml("ffe1a0");
        public static readonly Color AccentDim = Color.FromHtml("a8946a");
        public static readonly Color AccentSoft = Alpha(Accent, 0.08f);
        public static readonly Color AccentTrace = Alpha(Accent, 0.12f);
        public static readonly Color AccentGlow = Alpha(Accent, 0.1f);
        public static readonly Color AccentFaint = Alpha(Accent, 0.06f);
        public static readonly Color AccentRing = Alpha(Accent, 0.5f);
        public static readonly Color AccentLine = Alpha(Accent, 0.45f);
        public static readonly Color AccentLineStrong = Alpha(Accent, 0.75f);
        public static readonly Color AccentLineWeak = Alpha(Accent, 0.2f);

        // ── Status ──────────────────────────────────────────────────────
        public static readonly Color Danger = Color.FromHtml("a63d40");
        public static readonly Color DangerText = Color.FromHtml("e8a9ab");
        public static readonly Color DangerSoft = Alpha(Danger, 0.15f);
        public static readonly Color Success = Color.FromHtml("8fae6b");
        public static readonly Color SuccessText = Color.FromHtml("b9d3ae");
        public static readonly Color SuccessSoft = Alpha(Success, 0.1f);
        public static readonly Color Warning = Color.FromHtml("cfa96a");
        public static readonly Color Info = Color.FromHtml("7f95b5");
        public static readonly Color Ok = Color.FromHtml("b9d3ae");

        // ── Type accents (items and actions) ────────────────────────────
        public static readonly Color TypeQuest = Color.FromHtml("d9a441");
        public static readonly Color TypeUsed = Color.FromHtml("cfa96a");
        public static readonly Color TypeTarget = Color.FromHtml("4fb3a5");
        public static readonly Color TypeFood = Color.FromHtml("8fae6b");
        public static readonly Color TypeResource = Color.FromHtml("7f95b5");
        public static readonly Color TypeRecipe = Color.FromHtml("c78fc9");
        public static readonly Color TypeEnemy = Color.FromHtml("c96b6e");
        public static readonly Color TypeSelf = Color.FromHtml("7f95b5");
        public static readonly Color TypePassive = Color.FromHtml("8fae6b");
        public static readonly Color TypeActive = Color.FromHtml("c78fc9");

        // ── Health, AP, cooldown, stacks ────────────────────────────────
        public static readonly Color HpOk = Color.FromHtml("8fae6b");
        public static readonly Color HpLow = Color.FromHtml("c96b6e");
        public static readonly Color HpText = Color.FromHtml("9dbb8f");
        public static readonly Color Cooldown = Color.FromHtml("d9c79a");
        public static readonly Color Stack = Color.FromHtml("f0ece2");

        // ── Details ─────────────────────────────────────────────────────
        public static readonly Color BtnPrimary = Color.FromHtml("241f12");
        public static readonly Color BtnPrimaryHover = Color.FromHtml("2f2816");
        public static readonly Color BtnHover = Color.FromHtml("232633");
        public static readonly Color BtnDisabled = Color.FromHtml("15161d");
        public static readonly Color Ribbon = Color.FromHtml("2a241b");
        public static readonly Color RibbonHover = Color.FromHtml("38301f");
        public static readonly Color RibbonText = Color.FromHtml("d9cdb2");
        public static readonly Color RibbonTextActive = Color.FromHtml("1a1409");
        public static readonly Color StatBg = Color.FromHtml("161822");
        public static readonly Color StatLine = Color.FromHtml("2c2f3c");
        public static readonly Color ChipKey = Color.FromHtml("7d7461");
        public static readonly Color ChipValue = Color.FromHtml("b8a06d");
        public static readonly Color DropBg = Color.FromHtml("1a2019");
        public static readonly Color SideBg = new(0f, 0f, 0f, 0.2f);
        public static readonly Color White = Colors.White;
        public static readonly Color WhiteTrace = new(1f, 1f, 1f, 0.04f);
        public static readonly Color AccentCount = Alpha(Accent, 0.22f);
        public static readonly Color TargetHover = Color.FromHtml("13191b");
        public static readonly Color TargetLine = new(79f / 255f, 179f / 255f, 165f / 255f, 0.65f);
        public static readonly Color SlotTagBg = new(13f / 255f, 14f / 255f, 20f / 255f, 0.85f);
        public static readonly Color TooltipBg = new(16f / 255f, 17f / 255f, 24f / 255f, 0.97f);
        public static readonly Color ToastBg = new(33f / 255f, 33f / 255f, 33f / 255f, 0.8f);
        public static readonly Color ScrollThumb = Color.FromHtml("252734");
        public static readonly Color CooldownSoft = Alpha(Cooldown, 0.08f);
        public static readonly Color CooldownLine = Alpha(Cooldown, 0.45f);

        // ── Radii ───────────────────────────────────────────────────────
        public const int RadiusXs = 3;
        public const int RadiusSm = 4;
        public const int RadiusMd = 6;
        public const int RadiusLg = 8;

        // ── Spacing ─────────────────────────────────────────────────────
        public const int Space1 = 4;
        public const int Space2 = 6;
        public const int Space3 = 8;
        public const int Space4 = 10;
        public const int Space5 = 12;
        public const int Space6 = 14;
        public const int Space7 = 16;
        public const int Space8 = 24;
        public const int Space9 = 32;

        // ── Font sizes ──────────────────────────────────────────────────
        public const int FontMicro = 9;
        public const int FontTiny = 10;
        public const int FontSmall = 11;
        public const int FontBodySmall = 12;
        public const int FontBody = 13;
        public const int FontBodyLarge = 14;
        public const int FontLead = 16;
        public const int FontH3 = 19;
        public const int FontTitle = 24;
        public const int FontH2 = 25;
        public const int FontH1 = 30;

        /// <summary>Returns the colour with a replaced alpha channel.</summary>
        public static Color Alpha(Color color, float alpha)
            => new(color.R, color.G, color.B, alpha);

        /// <summary>Badge fill (9%) and border (50%) derived from a type accent.</summary>
        public static Color BadgeFill(Color accent) => Alpha(accent, 0.09f);
        public static Color BadgeLine(Color accent) => Alpha(accent, 0.5f);
    }
}
