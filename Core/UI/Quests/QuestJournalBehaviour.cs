using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cthangover.Core.Quests;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Quests
{
	/// <summary>
	/// Quest journal widget with a left-right split layout. The left panel lists
	/// quest names filtered by active (<see cref="QuestStatus.Progress"/>) or
	/// completed (<see cref="QuestStatus.End"/>) state; <see cref="QuestStatus.NotStarted"/>
	/// quests are never shown. Clicking a quest populates the right panel with its
	/// journal entries from <see cref="QuestBase.StatusToDescription"/>:
	/// entries with index less than the current <see cref="QuestData.Status"/> are
	/// rendered with strikethrough, the entry at the current status is shown normally,
	/// and entries beyond the current status are hidden. For completed quests all
	/// entries are shown strikethrough. Tags are not displayed.
	/// </summary>
	public partial class QuestJournalBehaviour : Widget
	{
		private enum FilterType
		{
			Active,
			Completed
		}

		private FilterType _currentFilter = FilterType.Active;

		private ScrollContainer _questScroll;
		private VBoxContainer _questListContainer;
		private RichTextLabel _entryText;
		private Label _questNameLabel;
		private Label _emptyLabel;
		private Button _activeFilterBtn;
		private Button _completedFilterBtn;

		private Button _selectedQuestButton;
		private QuestBase _selectedQuest;

		private static readonly Color SelectedColor = new Color(1f, 1f, 0.6f, 1f);
		private static readonly Color NormalColor = new Color(0.7f, 0.7f, 0.7f, 1f);

		protected override void OnceConstruct()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Stop;

			var bg = new ColorRect();
			bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			bg.Color = new Color(0, 0, 0, 0.85f);
			bg.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(bg);

			var title = new Label();
			title.Text = TranslationServer.Translate("ui/journal/title");
			title.AddThemeFontSizeOverride("font_size", 22);
			title.HorizontalAlignment = HorizontalAlignment.Center;
			title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
			title.OffsetTop = 10;
			title.OffsetBottom = 40;
			title.OffsetLeft = 20;
			title.OffsetRight = -20;
			title.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(title);

			var hbox = new HBoxContainer();
			hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			hbox.OffsetTop = 50;
			hbox.OffsetBottom = -60;
			hbox.OffsetLeft = 20;
			hbox.OffsetRight = -20;
			hbox.AddThemeConstantOverride("separation", 10);
			AddChild(hbox);

			var leftPanel = new VBoxContainer();
			leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			leftPanel.SizeFlagsStretchRatio = 2;
			leftPanel.AddThemeConstantOverride("separation", 6);
			leftPanel.CustomMinimumSize = new Vector2(220, 0);
			hbox.AddChild(leftPanel);

			var filterRow = new HBoxContainer();
			filterRow.AddThemeConstantOverride("separation", 4);
			leftPanel.AddChild(filterRow);

			_activeFilterBtn = new Button();
			_activeFilterBtn.Text = TranslationServer.Translate("ui/journal/active");
			_activeFilterBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_activeFilterBtn.Pressed += () => ApplyFilter(FilterType.Active);
			filterRow.AddChild(_activeFilterBtn);

			_completedFilterBtn = new Button();
			_completedFilterBtn.Text = TranslationServer.Translate("ui/journal/completed");
			_completedFilterBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_completedFilterBtn.Pressed += () => ApplyFilter(FilterType.Completed);
			filterRow.AddChild(_completedFilterBtn);

			_emptyLabel = new Label();
			_emptyLabel.Text = TranslationServer.Translate("ui/journal/empty");
			_emptyLabel.AddThemeFontSizeOverride("font_size", 14);
			_emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_emptyLabel.Modulate = new Color(0.5f, 0.5f, 0.5f, 1f);
			_emptyLabel.Visible = false;
			leftPanel.AddChild(_emptyLabel);

			_questScroll = new ScrollContainer();
			_questScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			_questScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			leftPanel.AddChild(_questScroll);

			_questListContainer = new VBoxContainer();
			_questListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_questListContainer.AnchorRight = 1.0f;
			_questListContainer.AddThemeConstantOverride("separation", 2);
			_questScroll.AddChild(_questListContainer);

			var rightPanel = new VBoxContainer();
			rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rightPanel.SizeFlagsStretchRatio = 3;
			rightPanel.AddThemeConstantOverride("separation", 8);
			hbox.AddChild(rightPanel);

			_questNameLabel = new Label();
			_questNameLabel.AddThemeFontSizeOverride("font_size", 18);
			_questNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_questNameLabel.Visible = false;
			rightPanel.AddChild(_questNameLabel);

			_entryText = new RichTextLabel();
			_entryText.BbcodeEnabled = true;
			_entryText.ScrollActive = true;
			_entryText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_entryText.SizeFlagsVertical = SizeFlags.ExpandFill;
			_entryText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_entryText.CustomMinimumSize = new Vector2(0, 80);
			_entryText.Visible = false;
			rightPanel.AddChild(_entryText);

			var closePanel = new Panel();
			closePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
			closePanel.AnchorLeft = 0.85f;
			closePanel.AnchorTop = 0.94f;
			closePanel.OffsetLeft = 0;
			closePanel.OffsetTop = 0;
			closePanel.OffsetRight = 0;
			closePanel.OffsetBottom = 0;
			AddChild(closePanel);

			var closeButton = new Button();
			closeButton.Text = TranslationServer.Translate("ui/journal/close");
			closeButton.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			closeButton.Pressed += () => Hide();
			closePanel.AddChild(closeButton);
		}

		protected override void ShowConstruct()
		{
			ClearSelection();
			ApplyFilter(_currentFilter);
		}

		protected override void HideDestruct()
		{
			ClearSelection();
			ClearQuestList();
		}

		private void ApplyFilter(FilterType filter)
		{
			_currentFilter = filter;
			ClearSelection();
			ClearQuestList();

			var allQuests = QuestFactory.Instance.GetAll();
			if (allQuests == null || allQuests.Count == 0)
			{
				_emptyLabel.Visible = true;
				return;
			}

			List<QuestBase> filtered;
			if (filter == FilterType.Active)
				filtered = allQuests.Where(q => q.Status == QuestStatus.Progress).ToList();
			else
				filtered = allQuests.Where(q => q.Status == QuestStatus.End).ToList();

			if (filtered.Count == 0)
			{
				_emptyLabel.Visible = true;
				return;
			}

			_emptyLabel.Visible = false;

			foreach (var quest in filtered)
			{
				var btn = new Button();
				btn.Text = TranslationServer.Translate(quest.Name);
				btn.Modulate = NormalColor;
				btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				btn.Alignment = HorizontalAlignment.Left;

				var capturedQuest = quest;
				var capturedBtn = btn;
				btn.Pressed += () => OnQuestClicked(capturedQuest, capturedBtn);

				_questListContainer.AddChild(btn);
			}
		}

		private void OnQuestClicked(QuestBase quest, Button button)
		{
			if (_selectedQuestButton != null && _selectedQuestButton != button)
				_selectedQuestButton.Modulate = NormalColor;

			_selectedQuestButton = button;
			_selectedQuest = quest;
			button.Modulate = SelectedColor;

			BuildDetailPanel(quest);
		}

		private void BuildDetailPanel(QuestBase quest)
		{
			_questNameLabel.Text = TranslationServer.Translate(quest.Name);
			_questNameLabel.Visible = true;
			_entryText.Visible = true;

			var descriptions = quest.StatusToDescription;
			if (descriptions == null || descriptions.Count == 0)
			{
				_entryText.Text = "";
				return;
			}

			var currentStatus = quest.Data.Status;
			var isClosed = quest.Status == QuestStatus.End;
			var sortedKeys = descriptions.Keys.OrderBy(k => k).ToList();

			var sb = new StringBuilder();
			foreach (var key in sortedKeys)
			{
				if (key > currentStatus)
					continue;

				var descText = TranslationServer.Translate(descriptions[key]);

				if (isClosed || key < currentStatus)
				{
					sb.Append("[s][color=gray]");
					sb.Append(descText);
					sb.Append("[/color][/s]");
				}
				else
				{
					sb.Append("[color=white]");
					sb.Append(descText);
					sb.Append("[/color]");
				}

				sb.Append("\n\n");
			}

			_entryText.Text = sb.ToString();
		}

		private void ClearSelection()
		{
			if (_selectedQuestButton != null)
			{
				_selectedQuestButton.Modulate = NormalColor;
				_selectedQuestButton = null;
			}

			_selectedQuest = null;
			_questNameLabel.Visible = false;
			_entryText.Visible = false;
			_entryText.Text = "";
		}

		private void ClearQuestList()
		{
			foreach (var child in _questListContainer.GetChildren())
				child.QueueFree();
		}
	}
}
