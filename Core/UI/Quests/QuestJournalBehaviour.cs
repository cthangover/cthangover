using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cthangover.Core.Quests;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Quests
{
	/// <summary>
	/// Quest journal widget styled as an open folio: a two-page spread on a dark
	/// backdrop, with ribbon tabs overlapping the top edge. The left page is a
	/// table of contents listing quests filtered by active
	/// (<see cref="QuestStatus.Progress"/>) or completed (<see cref="QuestStatus.End"/>)
	/// state; <see cref="QuestStatus.NotStarted"/> quests are never shown. The right
	/// page renders the selected quest as a journal entry: entries with index less than
	/// the current <see cref="QuestData.Status"/> are struck through, the entry at the
	/// current status is highlighted, and entries beyond it are hidden. For completed
	/// quests every entry is struck through. Earned <see cref="QuestData.Tags"/> are
	/// displayed as wax seals under the entry. All decoration (parchment gradients,
	/// inner borders, spine shading, stamps) is generated in code because mod scenes
	/// ignore theme overrides and the project ships no font or paper assets.
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
		private static readonly Color PaperTopColor = UiTokens.PaperTop;
		private static readonly Color PaperBottomColor = UiTokens.Paper;
		private static readonly Color PageLineColor = UiTokens.PaperInset;
		private static readonly Color RuleColor = UiTokens.PaperRule;
		private static readonly Color CoverLineColor = UiTokens.PaperLine;
		private static readonly Color GoldColor = UiTokens.Accent;
		private static readonly Color GoldGlowColor = UiTokens.AccentGlow;
		private static readonly Color GoldTraceColor = UiTokens.AccentFaint;
		private static readonly Color SealColor = UiTokens.Seal;
		private static readonly Color SealTextColor = UiTokens.SealText;
		private static readonly Color SealFillColor = UiTokens.SealSoft;
		private static readonly Color RibbonColor = UiTokens.Ribbon;
		private static readonly Color RibbonHoverColor = UiTokens.RibbonHover;
		private static readonly Color RibbonTextColor = UiTokens.RibbonText;
		private static readonly Color RibbonActiveTextColor = UiTokens.RibbonTextActive;

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

		protected override void OnceConstruct()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Stop;

			var bg = new ColorRect();
			bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			bg.Color = UiTokens.Backdrop;
			bg.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(bg);

			var bookArea = new Control();
			bookArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			bookArea.OffsetLeft = 40;
			bookArea.OffsetTop = 84;
			bookArea.OffsetRight = -40;
			bookArea.OffsetBottom = -40;
			bookArea.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(bookArea);

			var book = new HBoxContainer();
			book.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			book.AddThemeConstantOverride("separation", 0);
			bookArea.AddChild(book);

			book.AddChild(BuildLeftPage());
			book.AddChild(BuildSpine());
			book.AddChild(BuildRightPage());

			BuildRibbons(bookArea);
			BuildHeader();
			BuildCloseButton();
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

		private Control BuildLeftPage()
		{
			var page = new Panel();
			page.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			page.SizeFlagsStretchRatio = 1f;
			page.AddThemeStyleboxOverride("panel", MakePageStyle(true));
			page.AddChild(MakePageGradient());
			page.AddChild(MakeInnerBorder());

			var content = new MarginContainer();
			content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			content.AddThemeConstantOverride("margin_left", 36);
			content.AddThemeConstantOverride("margin_right", 30);
			content.AddThemeConstantOverride("margin_top", 32);
			content.AddThemeConstantOverride("margin_bottom", 28);
			page.AddChild(content);

			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 14);
			content.AddChild(column);

			column.AddChild(MakeSectionHeader("ui/journal/toc"));

			_emptyLabel = new Label();
			_emptyLabel.Text = TranslationServer.Translate("ui/journal/empty");
			_emptyLabel.AddThemeFontSizeOverride("font_size", 14);
			_emptyLabel.AddThemeColorOverride("font_color", InkFaintColor);
			_emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_emptyLabel.Visible = false;
			column.AddChild(_emptyLabel);

			var scroll = new ScrollContainer();
			scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			column.AddChild(scroll);

			_questListContainer = new VBoxContainer();
			_questListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_questListContainer.AddThemeConstantOverride("separation", 2);
			scroll.AddChild(_questListContainer);

			return page;
		}

		private Control BuildRightPage()
		{
			var page = new Panel();
			page.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			page.SizeFlagsStretchRatio = 1f;
			page.AddThemeStyleboxOverride("panel", MakePageStyle(false));
			page.AddChild(MakePageGradient());
			page.AddChild(MakeInnerBorder());

			var content = new MarginContainer();
			content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			content.AddThemeConstantOverride("margin_left", 44);
			content.AddThemeConstantOverride("margin_right", 38);
			content.AddThemeConstantOverride("margin_top", 32);
			content.AddThemeConstantOverride("margin_bottom", 28);
			page.AddChild(content);

			var scroll = new ScrollContainer();
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			content.AddChild(scroll);

			var column = new VBoxContainer();
			column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			column.AddThemeConstantOverride("separation", 0);
			scroll.AddChild(column);

			column.AddChild(MakeSectionHeader("ui/journal/entry_header"));

			var nameMargin = new MarginContainer();
			nameMargin.AddThemeConstantOverride("margin_top", 14);
			nameMargin.AddThemeConstantOverride("margin_bottom", 6);
			column.AddChild(nameMargin);

			_questNameLabel = new Label();
			_questNameLabel.AddThemeFontSizeOverride("font_size", 30);
			_questNameLabel.AddThemeColorOverride("font_color", InkBrightColor);
			_questNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			nameMargin.AddChild(_questNameLabel);

			_stageLabel = new Label();
			_stageLabel.AddThemeFontSizeOverride("font_size", 13);
			column.AddChild(_stageLabel);

			column.AddChild(MakeDivider());

			_entryText = new RichTextLabel();
			_entryText.BbcodeEnabled = true;
			_entryText.FitContent = true;
			_entryText.ScrollActive = false;
			_entryText.SelectionEnabled = false;
			_entryText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_entryText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_entryText.AddThemeFontSizeOverride("normal_font_size", 16);
			_entryText.AddThemeConstantOverride("line_separation", 7);
			_entryText.AddThemeColorOverride("default_color", InkColor);
			column.AddChild(_entryText);

			column.AddChild(BuildSealsBlock());

			return page;
		}

		private VBoxContainer BuildSealsBlock()
		{
			_sealsBlock = new MarginContainer();
			_sealsBlock.AddThemeConstantOverride("margin_top", 28);

			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 10);
			_sealsBlock.AddChild(column);

			column.AddChild(MakeSectionHeader("ui/journal/seals"));

			_sealsContainer = new HFlowContainer();
			_sealsContainer.AddThemeConstantOverride("h_separation", 10);
			_sealsContainer.AddThemeConstantOverride("v_separation", 10);
			column.AddChild(_sealsContainer);

			var wrapper = new VBoxContainer();
			wrapper.AddChild(_sealsBlock);
			return wrapper;
		}

		private static Control BuildSpine()
		{
			var spine = new Panel();
			spine.SizeFlagsVertical = SizeFlags.ExpandFill;
			spine.CustomMinimumSize = new Vector2(56, 0);

			var style = new StyleBoxFlat();
			style.BgColor = PaperBottomColor;
			style.BorderColor = CoverLineColor;
			style.BorderWidthTop = 1;
			style.BorderWidthBottom = 1;
			spine.AddThemeStyleboxOverride("panel", style);

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

			var rect = new TextureRect();
			rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			rect.Texture = texture;
			rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			rect.StretchMode = TextureRect.StretchModeEnum.Scale;
			rect.MouseFilter = MouseFilterEnum.Ignore;
			spine.AddChild(rect);

			return spine;
		}

		private void BuildRibbons(Control bookArea)
		{
			var ribbons = new HBoxContainer();
			ribbons.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
			ribbons.OffsetLeft = 26;
			ribbons.OffsetTop = -30;
			ribbons.AddThemeConstantOverride("separation", 12);
			ribbons.MouseFilter = MouseFilterEnum.Ignore;

			_activeFilterBtn = MakeRibbonButton("ui/journal/active", FilterType.Active);
			_completedFilterBtn = MakeRibbonButton("ui/journal/completed", FilterType.Completed);
			ribbons.AddChild(_activeFilterBtn);
			ribbons.AddChild(_completedFilterBtn);
			bookArea.AddChild(ribbons);
			ribbons.Size = ribbons.GetCombinedMinimumSize();
		}

		private Button MakeRibbonButton(string localeKey, FilterType filter)
		{
			var button = new Button();
			button.Text = TranslationServer.Translate(localeKey);
			button.FocusMode = FocusModeEnum.None;
			button.AddThemeFontSizeOverride("font_size", 15);
			button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			button.Pressed += () => ApplyFilter(filter);
			StyleRibbon(button, filter == _currentFilter);
			return button;
		}

		private static void StyleRibbon(Button button, bool active)
		{
			button.AddThemeStyleboxOverride("normal", MakeRibbonStyle(active, false));
			button.AddThemeStyleboxOverride("hover", MakeRibbonStyle(active, true));
			button.AddThemeStyleboxOverride("pressed", MakeRibbonStyle(active, true));
			button.AddThemeColorOverride("font_color", active ? RibbonActiveTextColor : RibbonTextColor);
			button.AddThemeColorOverride("font_hover_color", active ? RibbonActiveTextColor : InkBrightColor);
			button.AddThemeColorOverride("font_pressed_color", active ? RibbonActiveTextColor : InkBrightColor);
		}

		private static StyleBoxFlat MakeRibbonStyle(bool active, bool hover)
		{
			var style = new StyleBoxFlat();
			style.BgColor = active ? GoldColor : (hover ? RibbonHoverColor : RibbonColor);
			style.BorderColor = active ? GoldColor : CoverLineColor;
			style.BorderWidthLeft = 1;
			style.BorderWidthRight = 1;
			style.BorderWidthTop = 1;
			style.ContentMarginLeft = 22;
			style.ContentMarginRight = 22;
			style.ContentMarginTop = 12;
			style.ContentMarginBottom = 20;
			style.CornerRadiusTopLeft = 4;
			style.CornerRadiusTopRight = 4;
			return style;
		}

		private static StyleBoxFlat MakePageStyle(bool left)
		{
			var style = new StyleBoxFlat();
			style.BgColor = PaperBottomColor;
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

		private static TextureRect MakePageGradient()
		{
			var gradient = new Gradient();
			gradient.SetColor(0, PaperTopColor);
			gradient.SetColor(1, PaperBottomColor);

			var texture = new GradientTexture2D();
			texture.Gradient = gradient;
			texture.Width = 8;
			texture.Height = 256;
			texture.FillFrom = new Vector2(0.5f, 0f);
			texture.FillTo = new Vector2(0.5f, 1f);

			var rect = new TextureRect();
			rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			rect.Texture = texture;
			rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			rect.StretchMode = TextureRect.StretchModeEnum.Scale;
			rect.MouseFilter = MouseFilterEnum.Ignore;
			return rect;
		}

		private static Panel MakeInnerBorder()
		{
			var border = new Panel();
			border.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			border.OffsetLeft = 14;
			border.OffsetTop = 14;
			border.OffsetRight = -14;
			border.OffsetBottom = -14;

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
			border.AddThemeStyleboxOverride("panel", style);
			border.MouseFilter = MouseFilterEnum.Ignore;
			return border;
		}

		private static HBoxContainer MakeSectionHeader(string localeKey)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			row.AddChild(MakeFadingLine(false));

			var label = new Label();
			label.Text = TranslationServer.Translate(localeKey).ToString().ToUpperInvariant();
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", UiTokens.AccentDim);
			row.AddChild(label);

			row.AddChild(MakeFadingLine(true));
			return row;
		}

		private static MarginContainer MakeDivider()
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			row.AddChild(MakeFadingLine(false));
			row.AddChild(MakeDiamond(GoldColor, 9));
			row.AddChild(MakeFadingLine(true));

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_top", 18);
			margin.AddThemeConstantOverride("margin_bottom", 20);
			margin.AddChild(row);
			return margin;
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
			var gradient = new Gradient();
			gradient.SetColor(0, fadeToRight ? RuleColor : UiTokens.Alpha(Colors.Black, 0f));
			gradient.SetColor(1, fadeToRight ? UiTokens.Alpha(Colors.Black, 0f) : RuleColor);

			var texture = new GradientTexture2D();
			texture.Gradient = gradient;
			texture.Width = 256;
			texture.Height = 1;
			texture.FillFrom = new Vector2(0f, 0.5f);
			texture.FillTo = new Vector2(1f, 0.5f);

			var rect = new TextureRect();
			rect.Texture = texture;
			rect.CustomMinimumSize = new Vector2(30, 1);
			rect.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rect.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			rect.StretchMode = TextureRect.StretchModeEnum.Scale;
			rect.MouseFilter = MouseFilterEnum.Ignore;
			return rect;
		}

		private void BuildHeader()
		{
			var title = new Label();
			title.Text = TranslationServer.Translate("ui/journal/title").ToString().ToUpperInvariant();
			title.AddThemeFontSizeOverride("font_size", UiTokens.FontTitle);
			title.AddThemeColorOverride("font_color", UiTokens.InkBright);
			title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
			title.OffsetLeft = 44;
			title.OffsetTop = 28;
			title.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(title);
		}

		private void BuildCloseButton()
		{
			var close = new Button();
			close.Text = "✕";
			close.TooltipText = TranslationServer.Translate("ui/journal/close");
			close.FocusMode = FocusModeEnum.None;
			close.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
			close.CustomMinimumSize = new Vector2(38, 38);
			close.OffsetLeft = -46;
			close.OffsetTop = 28;
			close.OffsetRight = -8;
			close.OffsetBottom = 66;
			close.AddThemeFontSizeOverride("font_size", 18);
			CthangoverTheme.ApplyIcon(close);

			var normal = new StyleBoxFlat();
			normal.BgColor = UiTokens.Alpha(Colors.Black, 0.35f);
			normal.BorderColor = UiTokens.PaperLine;
			normal.BorderWidthLeft = 1;
			normal.BorderWidthRight = 1;
			normal.BorderWidthTop = 1;
			normal.BorderWidthBottom = 1;
			normal.CornerRadiusTopLeft = UiTokens.RadiusSm;
			normal.CornerRadiusTopRight = UiTokens.RadiusSm;
			normal.CornerRadiusBottomLeft = UiTokens.RadiusSm;
			normal.CornerRadiusBottomRight = UiTokens.RadiusSm;

			var hover = normal.Duplicate() as StyleBoxFlat;
			hover.BgColor = UiTokens.DangerSoft;
			hover.BorderColor = UiTokens.AccentLine;

			close.AddThemeStyleboxOverride("normal", normal);
			close.AddThemeStyleboxOverride("hover", hover);
			close.AddThemeStyleboxOverride("pressed", hover);
			close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			close.AddThemeColorOverride("font_color", InkDimColor);
			close.AddThemeColorOverride("font_hover_color", InkBrightColor);
			close.AddThemeColorOverride("font_pressed_color", InkBrightColor);
			close.Pressed += () => Hide();
			AddChild(close);
		}

		private void ApplyFilter(FilterType filter)
		{
			_currentFilter = filter;
			StyleRibbon(_activeFilterBtn, filter == FilterType.Active);
			StyleRibbon(_completedFilterBtn, filter == FilterType.Completed);

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
				_sealsContainer.AddChild(MakeSeal(tag));
		}

		private static Control MakeSeal(string tag)
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
			label.Text = TranslationServer.Translate(tag);
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
