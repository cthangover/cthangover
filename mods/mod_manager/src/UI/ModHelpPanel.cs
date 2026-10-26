namespace Mods.ModManager
{
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// Non-modal, contextual help for the mods hub. Controls carry a locale suffix in
    /// <see cref="HintKeyMetadata"/>; the layer finds the annotated ancestor under the
    /// pointer or keyboard focus, outlines it and places a compact card next to it.
    /// The root ignores mouse input, so the inspected control still performs its normal
    /// action. A click pins the current card without swallowing that click; Escape or
    /// the header button leaves help mode through <see cref="ModHubScreen"/>.
    /// </summary>
    public partial class ModHelpPanel : Cthangover.Mods.Interface.Base.Lists.ModWidget
    {
        public const string HintKeyMetadata = "mod_manager_hint";

        /// <summary>Optional locale-pair suffix for a contextual explanation without changing the target's stable help id.</summary>
        public const string HintTranslationMetadata = "mod_manager_hint_translation";

        private const float Gap = 10f;
        private const float ScreenMargin = 12f;
        private const float FooterClearance = 46f;

        private Panel _highlight;
        private PanelContainer _card;
        private Label _title;
        private RichTextLabel _body;
        private Label _footer;

        /// <summary>The annotated control currently explained by the card.</summary>
        public Control CurrentTarget { get; private set; }

        /// <summary>Whether the card stays on its target after the pointer moves away.</summary>
        public bool IsPinned { get; private set; }

        /// <summary>Associates a control with a mod_manager/hint_* locale pair.</summary>
        public static void Attach(Control control, string hintKey)
        {
            control?.SetMeta(HintKeyMetadata, hintKey);
        }

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            _highlight = GetNode<Panel>("Highlight");
            _card = GetNode<PanelContainer>("Card");
            _title = GetNode<Label>("Card/Margin/Column/Title");
            _body = GetNode<RichTextLabel>("Card/Margin/Column/Body");
            _footer = GetNode<Label>("Footer/Margin/FooterText");

            _highlight.AddThemeStyleboxOverride("panel", UiKit.Frame(
                UiTokens.RadiusSm, UiTokens.Alpha(UiTokens.Accent, 0.025f), UiTokens.AccentLineStrong));
            _card.AddThemeStyleboxOverride("panel", UiKit.Frame(
                UiTokens.RadiusSm, UiTokens.TooltipBg, UiTokens.AccentLine));
            GetNode<PanelContainer>("Footer").AddThemeStyleboxOverride("panel", UiKit.Frame(
                0, UiTokens.Surface0, UiTokens.Line));
            _title.AddThemeColorOverride("font_color", UiTokens.InkBright);
            _body.AddThemeColorOverride("default_color", UiTokens.Body);
            _footer.AddThemeColorOverride("font_color", UiTokens.Faint);

            _footer.Text = TranslationServer.Translate("mod_manager/help_active");
            _highlight.Visible = false;
            _card.Visible = false;
        }

        /// <summary>Enables inspection without choosing a page or blocking the hub.</summary>
        public void Open()
        {
            Visible = true;
            IsPinned = false;
            CurrentTarget = null;
            _highlight.Visible = false;
            _card.Visible = false;
            UpdateHoveredTarget();
        }

        /// <summary>Leaves help mode and removes every help-only visual.</summary>
        public void Close()
        {
            Visible = false;
            IsPinned = false;
            CurrentTarget = null;
            if (GodotObject.IsInstanceValid(_highlight))
                _highlight.Visible = false;
            if (GodotObject.IsInstanceValid(_card))
                _card.Visible = false;
        }

        /// <summary>
        /// Explains and pins one annotated control. Also used by F1 so keyboard users
        /// get the same stable card as pointer users get after a click.
        /// </summary>
        public void ShowFor(Control target)
        {
            Control annotated = FindTarget(target);
            if (annotated == null)
                return;

            Visible = true;
            IsPinned = true;
            SetTarget(annotated);
        }

        public override void _Process(double delta)
        {
            if (!Visible)
                return;

            if (IsPinned)
            {
                if (!GodotObject.IsInstanceValid(CurrentTarget) || !CurrentTarget.IsVisibleInTree())
                {
                    IsPinned = false;
                    CurrentTarget = null;
                    _highlight.Visible = false;
                    _card.Visible = false;
                    return;
                }

                LayoutAroundTarget();
                return;
            }

            UpdateHoveredTarget();
        }

        public override void _Input(InputEvent @event)
        {
            if (!Visible || @event is not InputEventMouseButton mouse
                || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
                return;

            Control target = FindTarget(GetViewport().GuiGetHoveredControl());
            if (target != null)
            {
                IsPinned = true;
                SetTarget(target);
            }
            else
            {
                IsPinned = false;
            }
        }

        private void UpdateHoveredTarget()
        {
            Control target = FindTarget(GetViewport().GuiGetHoveredControl());
            SetTarget(target);
        }

        private Control FindTarget(Control control)
        {
            Node node = control;
            while (node != null && node != this)
            {
                if (node is Control candidate
                    && candidate.IsVisibleInTree()
                    && candidate.HasMeta(HintKeyMetadata))
                {
                    return candidate;
                }

                node = node.GetParent();
            }

            return null;
        }

        private void SetTarget(Control target)
        {
            if (CurrentTarget == target)
            {
                if (target != null)
                    LayoutAroundTarget();
                return;
            }

            CurrentTarget = target;
            bool hasTarget = target != null;
            _highlight.Visible = hasTarget;
            _card.Visible = hasTarget;
            if (!hasTarget)
                return;

            string key = target.GetMeta(HintKeyMetadata).AsString();
            if (target.HasMeta(HintTranslationMetadata))
                key = target.GetMeta(HintTranslationMetadata).AsString();
            _title.Text = TranslationServer.Translate($"mod_manager/hint_{key}_title");
            _body.Text = TranslationServer.Translate($"mod_manager/hint_{key}_body");
            _card.ResetSize();
            LayoutAroundTarget();
        }

        private void LayoutAroundTarget()
        {
            if (CurrentTarget == null || !CurrentTarget.IsVisibleInTree())
                return;

            _highlight.GlobalPosition = CurrentTarget.GlobalPosition - new Vector2(3, 3);
            _highlight.Size = CurrentTarget.Size + new Vector2(6, 6);

            Vector2 viewport = GetViewportRect().Size;
            Vector2 cardSize = _card.GetCombinedMinimumSize();
            _card.Size = cardSize;

            Rect2 target = new(CurrentTarget.GlobalPosition, CurrentTarget.Size);
            float x = Mathf.Clamp(target.GetCenter().X - cardSize.X / 2f,
                ScreenMargin, Mathf.Max(ScreenMargin, viewport.X - cardSize.X - ScreenMargin));
            float y = target.End.Y + Gap;
            float bottomLimit = viewport.Y - FooterClearance;

            if (y + cardSize.Y > bottomLimit)
                y = target.Position.Y - cardSize.Y - Gap;
            if (y < ScreenMargin)
            {
                y = Mathf.Clamp(target.GetCenter().Y - cardSize.Y / 2f,
                    ScreenMargin, Mathf.Max(ScreenMargin, bottomLimit - cardSize.Y));
                x = target.End.X + Gap;
                if (x + cardSize.X > viewport.X - ScreenMargin)
                    x = target.Position.X - cardSize.X - Gap;
            }

            x = Mathf.Clamp(x, ScreenMargin, Mathf.Max(ScreenMargin, viewport.X - cardSize.X - ScreenMargin));
            _card.GlobalPosition = new Vector2(x, Mathf.Max(ScreenMargin, y));
        }
    }
}
