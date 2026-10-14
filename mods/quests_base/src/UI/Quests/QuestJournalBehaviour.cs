using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cthangover.Core.Quests;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.Quests
{
	/// <summary>
	/// Quest journal widget styled as an open folio: a two-page spread on a dark
	/// backdrop, with filter tabs tucked into the top of the left page. The left page is a
	/// table of contents listing quests filtered by active
	/// (<see cref="QuestStatus.Progress"/>) or completed (<see cref="QuestStatus.End"/>)
	/// state; <see cref="QuestStatus.NotStarted"/> quests are never shown. The right
	/// page renders the selected quest as a journal entry: entries with index less than
	/// the current <see cref="QuestData.Status"/> are struck through, the entry at the
	/// current status is highlighted, and entries beyond it are hidden. For completed
	/// quests every entry is struck through. Earned <see cref="QuestData.Tags"/> are
	/// displayed as gold tags under the entry. All decoration (surface gradients,
	/// inner borders, spine shading, tags) is generated in code because mod scenes
	/// ignore theme overrides and the project ships no font or texture assets.
	/// The palette is the shared graphite/gold one (<see cref="UiTokens"/>) so the
	/// journal does not stand out from the inventory and character panels.
	/// </summary>
	public partial class QuestJournalBehaviour : Widget
	{
		private enum FilterType
		{
			Active,
			Completed
		}

		private static readonly Color InkColor = UiTokens.Ink;
		private static readonly Color InkBrightColor = UiTokens.InkBright;
		private static readonly Color InkDimColor = UiTokens.Muted;
		private static readonly Color InkFaintColor = UiTokens.Faint;
		private static readonly Color PageTopColor = UiTokens.Surface1;
		private static readonly Color PageColor = UiTokens.Surface0;
		private static readonly Color PageLineColor = UiTokens.LineSoft;
		private static readonly Color RuleColor = UiTokens.LineFaint;
		private static readonly Color CoverLineColor = UiTokens.Line;
		private static readonly Color GoldColor = UiTokens.Accent;
		private static readonly Color GoldGlowColor = UiTokens.AccentGlow;
		private static readonly Color GoldTraceColor = UiTokens.AccentFaint;
		private static readonly Color SealColor = UiTokens.AccentLine;
		private static readonly Color SealTextColor = UiTokens.AccentBright;
		private static readonly Color SealFillColor = UiTokens.AccentSoft;

		private const int FilterTabsHeight = 34;
		private const int FilterTabsGap = 14;

		private FilterType _currentFilter = FilterType.Active;
		private string _selectedQuestId;

		private VBoxContainer _questListContainer;
		private RichTextLabel _entryText;
		private Label _questNameLabel;
		private Label _stageLabel;
		private Label _emptyLabel;
		private MarginContainer _sealsBlock;
		private HFlowContainer _sealsContainer;
		private Button _activeFilterBtn;
		private Button _completedFilterBtn;

		// Scene nodes (quest_journal.tscn): the folio layout lives in the scene,
		// the behaviour resolves it and applies gradient textures, styles and
		// the dynamic quest data.
		private Label _title;
		private Button _closeButton;
		private Label _tocHeader;
		private Label _entryHeader;
		private Label _sealsHeader;
		private TextureRect _leftPageGradient;
		private TextureRect _spineShade;
		private TextureRect _rightPageGradient;
		private Panel _diamond;
		private TextureRect _dividerLineL;
		private TextureRect _dividerLineR;

		protected override void OnceConstruct()
		{
			MouseFilter = MouseFilterEnum.Stop;

			_title = GetNode<Label>("Title");
			_closeButton = GetNode<Button>("CloseButton");
			_activeFilterBtn = GetNode<Button>("BookArea/Book/LeftPage/Content/Column/FilterTabs/ActiveFilterBtn");
			_completedFilterBtn = GetNode<Button>("BookArea/Book/LeftPage/Content/Column/FilterTabs/CompletedFilterBtn");
			_tocHeader = GetNode<Label>("BookArea/Book/LeftPage/Content/Column/TocHeader");
			_emptyLabel = GetNode<Label>("BookArea/Book/LeftPage/Content/Column/EmptyLabel");
			_questListContainer = GetNode<VBoxContainer>("BookArea/Book/LeftPage/Content/Column/Scroll/QuestListContainer");
			_entryHeader = GetNode<Label>("BookArea/Book/RightPage/Content/Scroll/Column/EntryHeader");
			_questNameLabel = GetNode<Label>("BookArea/Book/RightPage/Content/Scroll/Column/NameMargin/QuestNameLabel");
			_stageLabel = GetNode<Label>("BookArea/Book/RightPage/Content/Scroll/Column/StageLabel");
			_entryText = GetNode<RichTextLabel>("BookArea/Book/RightPage/Content/Scroll/Column/EntryText");
			_sealsBlock = GetNode<MarginContainer>("BookArea/Book/RightPage/Content/Scroll/Column/SealsBlock");
			_sealsContainer = GetNode<HFlowContainer>("BookArea/Book/RightPage/Content/Scroll/Column/SealsBlock/Column/SealsContainer");
			_sealsHeader = GetNode<Label>("BookArea/Book/RightPage/Content/Scroll/Column/SealsBlock/Column/SealsHeader");
			_leftPageGradient = GetNode<TextureRect>("BookArea/Book/LeftPage/PageGradient");
			_spineShade = GetNode<TextureRect>("BookArea/Book/Spine/SpineShade");
			_rightPageGradient = GetNode<TextureRect>("BookArea/Book/RightPage/PageGradient");
			_diamond = GetNode<Panel>("BookArea/Book/RightPage/Content/Scroll/Column/Divider/Row/Diamond");
			_dividerLineL = GetNode<TextureRect>("BookArea/Book/RightPage/Content/Scroll/Column/Divider/Row/LineL");
			_dividerLineR = GetNode<TextureRect>("BookArea/Book/RightPage/Content/Scroll/Column/Divider/Row/LineR");

			ApplyTheme();
			ApplyCaptions();

			_activeFilterBtn.Pressed += () => ApplyFilter(FilterType.Active);
			_completedFilterBtn.Pressed += () => ApplyFilter(FilterType.Completed);
			_closeButton.Pressed += () => Hide();
		}

		/// <summary>Gradient textures, page styles and decorations for scene nodes (texture assets ship no assets, so gradients are generated here).</summary>
		private void ApplyTheme()
		{
			UiKit.ApplyPanelBackdrop(GetNode<ColorRect>("Bg"));

			GetNode<Panel>("BookArea/Book/LeftPage").AddThemeStyleboxOverride("panel", MakePageStyle(true));
			GetNode<Panel>("BookArea/Book/RightPage").AddThemeStyleboxOverride("panel", MakePageStyle(false));
			GetNode<Panel>("BookArea/Book/Spine").AddThemeStyleboxOverride("panel", MakeSpineStyle());
			GetNode<Panel>("BookArea/Book/LeftPage/InnerBorder").AddThemeStyleboxOverride("panel", MakeInnerBorderStyle());
			GetNode<Panel>("BookArea/Book/RightPage/InnerBorder").AddThemeStyleboxOverride("panel", MakeInnerBorderStyle());

			_leftPageGradient.Texture = MakePageGradientTexture();
			_rightPageGradient.Texture = MakePageGradientTexture();
			_spineShade.Texture = MakeSpineShadeTexture();

			_title.AddThemeFontSizeOverride("font_size", UiTokens.FontTitle);
			_title.AddThemeColorOverride("font_color", UiTokens.InkBright);

			_tocHeader.Text = TranslateUpper("ui/journal/toc");
			_entryHeader.Text = TranslateUpper("ui/journal/entry_header");
			_sealsHeader.Text = TranslateUpper("ui/journal/seals");
			foreach (var header in new[] { _tocHeader, _entryHeader, _sealsHeader })
			{
				header.AddThemeFontSizeOverride("font_size", 14);
				header.AddThemeColorOverride("font_color", UiTokens.AccentDim);
			}

			_questNameLabel.AddThemeColorOverride("font_color", InkBrightColor);
			_entryText.AddThemeFontSizeOverride("normal_font_size", 16);
			_entryText.AddThemeConstantOverride("line_separation", 7);
			_entryText.AddThemeColorOverride("default_color", InkColor);

			_emptyLabel.AddThemeColorOverride("font_color", InkFaintColor);

			StyleFilterTab(_activeFilterBtn, _currentFilter == FilterType.Active);
			StyleFilterTab(_completedFilterBtn, _currentFilter == FilterType.Completed);

			_diamond.AddThemeStyleboxOverride("panel", MakeSolidStyle(GoldColor));
			_dividerLineL.Texture = MakeFadingLineTexture(false);
			_dividerLineR.Texture = MakeFadingLineTexture(true);

			CthangoverTheme.ApplyIcon(_closeButton);
		}

		/// <summary>Localized captions for all static labels of the scene.</summary>
		private void ApplyCaptions()
		{
			_title.Text = TranslateUpper("ui/journal/title");
			_closeButton.TooltipText = TranslationServer.Translate("ui/journal/close");
			_activeFilterBtn.Text = TranslationServer.Translate("ui/journal/active");
			_completedFilterBtn.Text = TranslationServer.Translate("ui/journal/completed");
			_emptyLabel.Text = TranslationServer.Translate("ui/journal/empty");
		}

		/// <summary>Translation-safe upper-casing: a missing key yields an empty string instead of dereferencing a null StringName.</summary>
		private static string TranslateUpper(string key)
		{
			var translated = TranslationServer.Translate(key)?.ToString();
			return string.IsNullOrEmpty(translated) ? string.Empty : translated.ToUpperInvariant();
		}

		private static StyleBoxFlat MakeSolidStyle(Color color)
		{
			var style = new StyleBoxFlat();
			style.BgColor = color;
			return style;
		}

		private static StyleBoxFlat MakeSpineStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = PageColor;
			style.BorderColor = CoverLineColor;
			style.BorderWidthTop = 1;
			style.BorderWidthBottom = 1;
			return style;
		}

		private static StyleBoxFlat MakeInnerBorderStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = UiTokens.Alpha(Colors.Black, 0f);
			style.BorderColor = PageLineColor;
			style.BorderWidthLeft = 1;
			style.BorderWidthRight = 1;
			style.BorderWidthTop = 1;
			style.BorderWidthBottom = 1;
			style.CornerRadiusTopLeft = 3;
			style.CornerRadiusTopRight = 3;
			style.CornerRadiusBottomLeft = 3;
			style.CornerRadiusBottomRight = 3;
			return style;
		}

		private static GradientTexture2D MakePageGradientTexture()
		{
			var gradient = new Gradient();
			gradient.SetColor(0, PageTopColor);
			gradient.SetColor(1, PageColor);

			var texture = new GradientTexture2D();
			texture.Gradient = gradient;
			texture.Width = 8;
			texture.Height = 256;
			texture.FillFrom = new Vector2(0.5f, 0f);
			texture.FillTo = new Vector2(0.5f, 1f);
			return texture;
		}

		private static GradientTexture2D MakeSpineShadeTexture()
		{
			var gradient = new Gradient();
			gradient.SetColor(0, UiTokens.Alpha(Colors.Black, 0f));
			gradient.SetOffset(0, 0f);
			gradient.SetColor(1, UiTokens.Alpha(Colors.Black, 0f));
			gradient.SetOffset(1, 1f);
			gradient.AddPoint(0.5f, UiTokens.Alpha(Colors.Black, 0.62f));

			var texture = new GradientTexture2D();
			texture.Gradient = gradient;
			texture.Width = 128;
			texture.Height = 4;
			texture.FillFrom = new Vector2(0f, 0.5f);
			texture.FillTo = new Vector2(1f, 0.5f);
			return texture;
		}

		private static GradientTexture2D MakeFadingLineTexture(bool fadeToRight)
		{
			var gradient = new Gradient();
			gradient.SetColor(0, fadeToRight ? RuleColor : UiTokens.Alpha(Colors.Black, 0f));
			gradient.SetColor(1, fadeToRight ? UiTokens.Alpha(Colors.Black, 0f) : RuleColor);

			var texture = new GradientTexture2D();
			texture.Gradient = gradient;
			texture.Width = 256;
			texture.Height = 1;
			texture.FillFrom = new Vector2(0f, 0.5f);
			texture.FillTo = new Vector2(1f, 0.5f);
			return texture;
		}

		protected override void ShowConstruct()
		{
			_selectedQuestId = null;
			ApplyFilter(_currentFilter);
		}

		protected override void HideDestruct()
		{
			ClearChildren(_questListContainer);
			ClearChildren(_sealsContainer);
			_selectedQuestId = null;
		}

		private static void StyleFilterTab(Button button, bool active)
		{
			button.AddThemeFontSizeOverride("font_size", UiTokens.FontBody);
			button.AddThemeStyleboxOverride("normal", MakeFilterTabStyle(active, false));
			button.AddThemeStyleboxOverride("hover", MakeFilterTabStyle(active, true));
			button.AddThemeStyleboxOverride("pressed", MakeFilterTabStyle(active, true));
			button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			button.AddThemeColorOverride("font_color", active ? UiTokens.AccentBright : UiTokens.Muted);
			button.AddThemeColorOverride("font_hover_color", active ? UiTokens.AccentBright : UiTokens.Ink);
			button.AddThemeColorOverride("font_pressed_color", active ? UiTokens.AccentBright : UiTokens.Ink);
		}

		private static StyleBoxFlat MakeFilterTabStyle(bool active, bool hover)
		{
			var style = new StyleBoxFlat();
			style.BgColor = active ? UiTokens.AccentSoft : (hover ? UiTokens.WhiteTrace : Colors.Transparent);
			style.BorderColor = active ? UiTokens.AccentLine : Colors.Transparent;
			style.BorderWidthLeft = 1;
			style.BorderWidthRight = 1;
			style.BorderWidthTop = 1;
			style.BorderWidthBottom = 1;
			style.CornerRadiusTopLeft = UiTokens.RadiusSm;
			style.CornerRadiusTopRight = UiTokens.RadiusSm;
			style.CornerRadiusBottomLeft = UiTokens.RadiusSm;
			style.CornerRadiusBottomRight = UiTokens.RadiusSm;
			style.ContentMarginLeft = 13;
			style.ContentMarginRight = 13;
			style.ContentMarginTop = 7;
			style.ContentMarginBottom = 7;
			return style;
		}

		private static StyleBoxFlat MakePageStyle(bool left)
		{
			var style = new StyleBoxFlat();
			style.BgColor = PageColor;
			style.BorderColor = CoverLineColor;
			style.BorderWidthLeft = 1;
			style.BorderWidthRight = 1;
			style.BorderWidthTop = 1;
			style.BorderWidthBottom = 1;
			style.ShadowColor = UiTokens.Alpha(Colors.Black, 0.85f);
			style.ShadowSize = 24;
			style.ShadowOffset = new Vector2(0f, 10f);

			if (left)
			{
				style.CornerRadiusTopLeft = 6;
				style.CornerRadiusBottomLeft = 6;
			}
			else
			{
				style.CornerRadiusTopRight = 6;
				style.CornerRadiusBottomRight = 6;
			}

			return style;
		}

		private void ApplyFilter(FilterType filter)
		{
			_currentFilter = filter;
			StyleFilterTab(_activeFilterBtn, filter == FilterType.Active);
			StyleFilterTab(_completedFilterBtn, filter == FilterType.Completed);

			ClearChildren(_questListContainer);

			var allQuests = QuestFactory.Instance.GetAll();
			List<QuestBase> filtered = null;
			if (allQuests != null && allQuests.Count > 0)
			{
				if (filter == FilterType.Active)
					filtered = allQuests.Where(q => q.Status == QuestStatus.Progress).ToList();
				else
					filtered = allQuests.Where(q => q.Status == QuestStatus.End).ToList();
			}

			if (filtered == null || filtered.Count == 0)
			{
				_emptyLabel.Visible = true;
				_selectedQuestId = null;
				ShowPlaceholder();
				return;
			}

			_emptyLabel.Visible = false;

			if (!filtered.Any(q => q.ID == _selectedQuestId))
				_selectedQuestId = filtered[0].ID;

			foreach (var quest in filtered)
				_questListContainer.AddChild(BuildQuestItem(quest));

			BuildDetailPanel(filtered.First(q => q.ID == _selectedQuestId));
		}

		private Button BuildQuestItem(QuestBase quest)
		{
			var selected = quest.ID == _selectedQuestId;

			var button = new Button();
			button.FocusMode = FocusModeEnum.None;
			button.CustomMinimumSize = new Vector2(0, 46);
			button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			button.AddThemeStyleboxOverride("normal", MakeItemStyle(selected, false));
			button.AddThemeStyleboxOverride("hover", MakeItemStyle(selected, true));
			button.AddThemeStyleboxOverride("pressed", MakeItemStyle(selected, true));
			button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			button.Pressed += () => OnQuestClicked(quest);

			var row = new HBoxContainer();
			row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			row.OffsetLeft = 14;
			row.OffsetRight = -12;
			row.AddThemeConstantOverride("separation", 10);
			row.MouseFilter = MouseFilterEnum.Ignore;
			button.AddChild(row);

			var marker = MakeDiamond(quest.Status == QuestStatus.End ? InkFaintColor : GoldColor, 9);
			row.AddChild(marker);

			var name = new Label();
			name.Text = TranslationServer.Translate(quest.Name);
			name.AddThemeFontSizeOverride("font_size", 17);
			name.AddThemeColorOverride("font_color", selected ? InkBrightColor : (quest.Status == QuestStatus.End ? InkDimColor : InkColor));
			name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			name.ClipText = true;
			name.VerticalAlignment = VerticalAlignment.Center;
			name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			name.SizeFlagsStretchRatio = 2f;
			name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			name.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(name);

			row.AddChild(MakeFadingLine(true));

			var stage = new Label();
			stage.Text = quest.Status == QuestStatus.End
				? TranslationServer.Translate("ui/journal/finished")
				: (quest.StatusToDescription != null && quest.StatusToDescription.Count > 0
					? ToRoman(Math.Min(quest.Data.Status + 1, quest.StatusToDescription.Count))
					: "—");
			stage.AddThemeFontSizeOverride("font_size", 14);
			stage.AddThemeColorOverride("font_color", quest.Status == QuestStatus.End ? InkFaintColor : UiTokens.AccentDim);
			stage.VerticalAlignment = VerticalAlignment.Center;
			stage.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			stage.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(stage);

			return button;
		}

		private static StyleBoxFlat MakeItemStyle(bool selected, bool hover)
		{
			var style = new StyleBoxFlat();
			style.BgColor = selected ? GoldGlowColor : (hover ? GoldTraceColor : UiTokens.Alpha(Colors.Black, 0f));
			style.ContentMarginLeft = 2;
			style.ContentMarginRight = 2;
			style.CornerRadiusTopRight = 4;
			style.CornerRadiusBottomRight = 4;

			if (selected)
			{
				style.BorderColor = GoldColor;
				style.BorderWidthLeft = 2;
			}

			return style;
		}

		private void OnQuestClicked(QuestBase quest)
		{
			_selectedQuestId = quest.ID;
			ApplyFilter(_currentFilter);
		}

		private void BuildDetailPanel(QuestBase quest)
		{
			_questNameLabel.Text = TranslationServer.Translate(quest.Name);

			var descriptions = quest.StatusToDescription;
			var hasEntries = descriptions != null && descriptions.Count > 0;
			var isClosed = quest.Status == QuestStatus.End;
			var currentStatus = quest.Data.Status;

			if (isClosed)
			{
				_stageLabel.Text = TranslationServer.Translate("ui/journal/finished");
				_stageLabel.AddThemeColorOverride("font_color", InkDimColor);
			}
			else if (hasEntries)
			{
				_stageLabel.Text = string.Format(
					TranslationServer.Translate("ui/journal/entry_of"),
					ToRoman(Math.Min(currentStatus + 1, descriptions.Count)),
					ToRoman(descriptions.Count));
				_stageLabel.AddThemeColorOverride("font_color", GoldColor);
			}
			else
			{
				_stageLabel.Text = "";
			}

			BuildEntryText(descriptions, hasEntries, isClosed, currentStatus);
			BuildSeals(quest);
		}

		private void BuildEntryText(IDictionary<int, string> descriptions, bool hasEntries, bool isClosed, int currentStatus)
		{
			if (!hasEntries)
			{
				_entryText.Text = "[i][color=#" + UiTokens.Faint.ToHtml(false) + "]" +
					TranslationServer.Translate("ui/journal/no_entries") + "[/color][/i]";
				return;
			}

			var builder = new StringBuilder();
			foreach (var key in descriptions.Keys.OrderBy(k => k))
			{
				if (key > currentStatus)
					continue;

				var text = TranslationServer.Translate(descriptions[key]);
				if (isClosed || key < currentStatus)
				{
					builder.Append("[color=#").Append(UiTokens.Faint.ToHtml(false)).Append("][s]")
						.Append(text).Append("[/s][/color]");
				}
				else
				{
					builder.Append("[color=#").Append(UiTokens.Accent.ToHtml(false)).Append("]• [/color]")
						.Append("[color=#").Append(UiTokens.InkBright.ToHtml(false)).Append("]")
						.Append(text).Append("[/color]");
				}

				builder.Append("\n\n");
			}

			_entryText.Text = builder.ToString();
		}

		private void BuildSeals(QuestBase quest)
		{
			ClearChildren(_sealsContainer);

			var tags = quest.Data?.Tags;
			_sealsBlock.Visible = tags != null && tags.Count > 0;
			if (tags == null || tags.Count == 0)
				return;

			foreach (var tag in tags.OrderBy(t => t))
				_sealsContainer.AddChild(MakeSeal(quest, tag));
		}

		private static Control MakeSeal(QuestBase quest, string tag)
		{
			var seal = new PanelContainer();

			var style = new StyleBoxFlat();
			style.BgColor = SealFillColor;
			style.BorderColor = SealColor;
			style.BorderWidthLeft = 1;
			style.BorderWidthRight = 1;
			style.BorderWidthTop = 1;
			style.BorderWidthBottom = 1;
			style.ContentMarginLeft = 12;
			style.ContentMarginRight = 12;
			style.ContentMarginTop = 5;
			style.ContentMarginBottom = 5;
			style.CornerRadiusTopLeft = 4;
			style.CornerRadiusTopRight = 4;
			style.CornerRadiusBottomLeft = 4;
			style.CornerRadiusBottomRight = 4;
			seal.AddThemeStyleboxOverride("panel", style);
			seal.PivotOffset = Vector2.Zero;
			seal.Rotation = -0.0436f;
			seal.Resized += () => seal.PivotOffset = seal.Size / 2f;

			var label = new Label();
			label.Text = QuestTagLocale.Translate(quest, tag);
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", SealTextColor);
			label.MouseFilter = MouseFilterEnum.Ignore;
			seal.AddChild(label);

			return seal;
		}

		private void ShowPlaceholder()
		{
			_questNameLabel.Text = "";
			_stageLabel.Text = "";
			_entryText.Text = "[i][color=#" + UiTokens.Faint.ToHtml(false) + "]" +
				TranslationServer.Translate("ui/journal/empty") + "[/color][/i]";
			_sealsBlock.Visible = false;
		}

		private static void ClearChildren(Node container)
		{
			foreach (var child in container.GetChildren())
			{
				container.RemoveChild(child);
				child.QueueFree();
			}
		}

		private static Panel MakeDiamond(Color color, int size)
		{
			var diamond = new Panel();
			diamond.CustomMinimumSize = new Vector2(size, size);
			diamond.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			diamond.PivotOffset = new Vector2(size / 2f, size / 2f);
			diamond.Rotation = 0.7854f;

			var style = new StyleBoxFlat();
			style.BgColor = color;
			diamond.AddThemeStyleboxOverride("panel", style);
			diamond.MouseFilter = MouseFilterEnum.Ignore;
			return diamond;
		}

		private static TextureRect MakeFadingLine(bool fadeToRight)
		{
			var rect = new TextureRect();
			rect.Texture = MakeFadingLineTexture(fadeToRight);
			rect.CustomMinimumSize = new Vector2(30, 1);
			rect.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rect.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			rect.StretchMode = TextureRect.StretchModeEnum.Scale;
			rect.MouseFilter = MouseFilterEnum.Ignore;
			return rect;
		}

		private static string ToRoman(int value)
		{
			if (value <= 0)
				return "—";

			int[] values = { 50, 40, 10, 9, 5, 4, 1 };
			string[] symbols = { "L", "XL", "X", "IX", "V", "IV", "I" };

			var builder = new StringBuilder();
			for (var i = 0; i < values.Length; i++)
			{
				while (value >= values[i])
				{
					builder.Append(symbols[i]);
					value -= values[i];
				}
			}

			return builder.ToString();
		}
	}
}