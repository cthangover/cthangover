using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Mods.Skills
{
	/// <summary>
	/// Grid card of one collected skill. Pure presentation: the panel binds a
	/// <see cref="SkillInfo"/> through <see cref="Construct"/> and toggles
	/// selection through <see cref="SetSelected"/>, while every colour comes
	/// from <see cref="SkillPalette"/> so the same rarity reads identically on
	/// the card, the study plate and the rarity filter tabs.
	/// <para/>
	/// Clicking is intentionally not handled here: the panel subscribes to
	/// <c>GuiInput</c>, so the card keeps a single responsibility and can be
	/// hosted by any list without knowing its owner. Hover is handled inside
	/// because it only repaints the card itself.
	/// </summary>
	public partial class SkillCardBehaviour : ListItem<SkillInfo>
	{
		[Export] private Panel bg;
		[Export] private Panel accent;
		[Export] private PanelContainer art;
		[Export] private TextureRect imgCard;
		[Export] private Label txtName;
		[Export] private Label txtGems;
		[Export] private PanelContainer badgeBox;
		[Export] private Label badgeLabel;
		[Export] private HBoxContainer effectRow;
		[Export] private Label effectValue;
		[Export] private Label effectLabel;

		private bool _selected;
		private bool _hover;

		/// <summary>Configures the card to accept mouse input for hover/selection feedback.</summary>
		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Stop;
			MouseEntered += OnMouseEntered;
			MouseExited += OnMouseExited;
		}

		/// <summary>
		/// Binds the skill definition to the card: artwork, translated name,
		/// rarity gems (one diamond per tier), the type badge and the first
		/// numeric effect as the card's short line. Skills without effects hide
        /// the effect row instead of leaving an empty band; a skill without
        /// artwork keeps the empty well, which is a valid authored state.
        /// </summary>
        public override void Construct(SkillInfo skillInfo)
        {
            base.Construct(skillInfo);
            ResolveNodes();

            if (art != null)
                art.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well, UiTokens.Line));

            if (skillInfo == null)
                return;

            if (imgCard != null)
                imgCard.Texture = skillInfo.Sprite;

            if (txtName != null)
            {
                txtName.Text = TranslationServer.Translate(skillInfo.Name);
                txtName.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                txtName.AddThemeColorOverride("font_color", UiTokens.InkBright);
            }

            if (txtGems != null)
            {
				txtGems.Text = new string('◆', Mathf.Clamp((int)skillInfo.RareType + 1, 1, 5));
                txtGems.AddThemeFontSizeOverride("font_size", UiTokens.FontMicro);
                txtGems.AddThemeColorOverride("font_color", SkillPalette.RarityColor(skillInfo.RareType));
            }

            ApplyTypeBadge(skillInfo);
            ApplyEffect(skillInfo);
            RefreshStyle();
        }

        /// <summary>
        /// Applies the panel-owned selection state: the selected card gets the
        /// golden surface, rarity border and a soft rarity glow, while hover
        /// only lifts the border. Both are repainted from scratch so the card
        /// can never end up half-styled after a filter rebuild.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            RefreshStyle();
        }

		/// <summary>Frees the node; the panel owns the card's lifetime and rebuilds the grid.</summary>
		public override void Destruct()
		{
			QueueFree();
		}

		private void ResolveNodes()
		{
			bg ??= GetNodeOrNull<Panel>("Bg");
			accent ??= GetNodeOrNull<Panel>("Accent");
			art ??= GetNodeOrNull<PanelContainer>("Art");
			imgCard ??= GetNodeOrNull<TextureRect>("Art/ImgCard");
			txtName ??= GetNodeOrNull<Label>("TxtName");
			txtGems ??= GetNodeOrNull<Label>("Gems");
			badgeBox ??= GetNodeOrNull<PanelContainer>("BadgeBox");
			badgeLabel ??= GetNodeOrNull<Label>("BadgeBox/BadgeLabel");
			effectRow ??= GetNodeOrNull<HBoxContainer>("EffectRow");
			effectValue ??= GetNodeOrNull<Label>("EffectRow/EffectValue");
			effectLabel ??= GetNodeOrNull<Label>("EffectRow/EffectLabel");
		}

		private void ApplyTypeBadge(SkillInfo skillInfo)
		{
			var color = SkillPalette.TypeColor(skillInfo.SkillType);

			if (badgeBox != null)
			{
				var style = UiKit.Frame(UiTokens.RadiusXs, UiTokens.BadgeFill(color), UiTokens.BadgeLine(color));
				style.ContentMarginLeft = 6;
				style.ContentMarginRight = 6;
				style.ContentMarginTop = 1;
				style.ContentMarginBottom = 1;
				badgeBox.AddThemeStyleboxOverride("panel", style);
			}

			if (badgeLabel != null)
			{
				badgeLabel.Text = TranslationServer.Translate(SkillPalette.TypeLocaleKey(skillInfo.SkillType));
				badgeLabel.Uppercase = true;
				badgeLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontMicro);
				badgeLabel.AddThemeColorOverride("font_color", color);
			}
		}

		private void ApplyEffect(SkillInfo skillInfo)
		{
			var effect = skillInfo.Effects != null && skillInfo.Effects.Count > 0 ? skillInfo.Effects[0] : null;
			var hasEffect = effect != null && !string.IsNullOrEmpty(effect.Key);

			if (effectRow != null)
				effectRow.Visible = hasEffect;

			if (!hasEffect)
				return;

			if (effectValue != null)
			{
				effectValue.Text = effect.FormatValue();
				effectValue.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
				effectValue.AddThemeColorOverride("font_color", UiTokens.AccentBright);
			}

			if (effectLabel != null)
			{
				effectLabel.Text = TranslationServer.Translate(effect.Key);
				effectLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
				effectLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
			}
		}

		private void RefreshStyle()
		{
			var rarity = SkillPalette.RarityColor(Model?.RareType ?? RareType.Common);

			if (accent != null)
			{
				var accentStyle = UiKit.Frame(UiTokens.RadiusXs, rarity);
				accentStyle.CornerRadiusBottomLeft = 0;
				accentStyle.CornerRadiusBottomRight = 0;
				accent.AddThemeStyleboxOverride("panel", accentStyle);
			}

			if (bg == null)
				return;

			var background = _selected ? UiTokens.SurfaceSelected : _hover ? UiTokens.SurfaceHover : UiTokens.Surface1;
			var border = _selected || _hover ? rarity : UiTokens.LineCard;
			var style = UiKit.Frame(UiTokens.RadiusMd, background, border);
			if (_selected)
			{
				style.ShadowColor = UiTokens.Alpha(rarity, 0.35f);
				style.ShadowSize = 4;
			}
			bg.AddThemeStyleboxOverride("panel", style);
		}

		private void OnMouseEntered()
		{
			_hover = true;
			RefreshStyle();
		}

		private void OnMouseExited()
		{
			_hover = false;
			RefreshStyle();
		}
	}
}
