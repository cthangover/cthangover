using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Godot;

namespace Cthangover.Core.UI.Menu
{
	/// <summary>
	/// Fullscreen mod configuration overlay. Reads all discovered mods
	/// from <see cref="ModRegistry.AllDiscoveredMods"/>, determines
	/// enable state from <see cref="DisabledModsStore"/>, and displays
	/// them in a scrollable list sorted by dependency order.
	///
	/// Toggling a mod writes to <c>user://disabled_mods.json</c>
	/// immediately and shows a non-blocking toast in the bottom-right
	/// corner because changes only take effect after a full game restart
	/// (mods are loaded once at startup by <c>ModRegistry</c>).
	///
	/// The right panel shows details for the selected mod: thumbnail
	/// (loaded from <c>mod.png</c> or <c>mod.jpg</c> in the mod root),
	/// name, version, author, description, dependencies, and dependents.
	/// </summary>
	public partial class ModsMenu : Control
	{
		private VBoxContainer _modListContainer;
		private Control _rightPanel;
		private TextureRect _detailIcon;
		private Label _detailName;
		private Label _detailVersion;
		private Label _detailAuthor;
		private Label _detailDescription;
		private Label _detailDepends;
		private Label _detailUsedBy;
		private Label _detailConflicts;
		private Button _toggleBtn;
		private Button _catalogExportBtn;
		private Button _installBtn;
		private Label _statusLabel;
		private Button _backBtn;
		private Button _resetOrderBtn;
		private Label _titleLabel;
		private CheckButton _filterDisabledCheck;

		private TabBar _tabBar;
		private HBoxContainer _tabHeaderBox;

		private HashSet<string> _disabledSet;
		private string _selectedModId;
		private ModEntry _selectedEntry;
		private bool _showDisabledMods = true;
		private readonly Dictionary<string, ModEntry> _entries = new();

		private enum ModTab { Device, Network }
		private ModTab _currentTab = ModTab.Device;

		private const int ThumbnailSize = 48;
		private const int DetailIconSize = 128;

		public override void _Ready()
		{
			_titleLabel = GetNode<Label>("Panel/Margin/VBox/Title");
			_modListContainer = GetNode<VBoxContainer>("Panel/Margin/VBox/HBox/LeftPanel/Scroll/ModList");
			_rightPanel = GetNode<Control>("Panel/Margin/VBox/HBox/RightPanel");
			_detailIcon = GetNode<TextureRect>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/Icon");
			_detailName = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/NameLabel");
			_detailVersion = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/VersionLabel");
			_detailAuthor = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/AuthorLabel");
			_detailDescription = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/DescLabel");
			_detailDepends = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/DependsLabel");
			_detailUsedBy = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/UsedByLabel");
			_toggleBtn = GetNode<Button>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/ToggleBtn");
			_toggleBtn.Visible = false;
			_statusLabel = GetNode<Label>("Panel/Margin/VBox/HBox/RightPanel/Margin/DetailVBox/StatusLabel");
			_backBtn = GetNode<Button>("Panel/Margin/VBox/BackBtn");
			_filterDisabledCheck = GetNode<CheckButton>("Panel/Margin/VBox/HBox/LeftPanel/FilterDisabledCheck");
			_resetOrderBtn = GetNode<Button>("Panel/Margin/VBox/HBox/LeftPanel/ResetOrderBtn");

			var rightPanelMargin = _rightPanel.GetChild(0);
			_rightPanel.RemoveChild(rightPanelMargin);
			((Control)rightPanelMargin).SizeFlagsHorizontal = SizeFlags.ExpandFill;
			var rightScroll = new ScrollContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			};
			rightScroll.SetAnchorsPreset(LayoutPreset.FullRect);
			rightScroll.AddChild(rightPanelMargin);
			_rightPanel.AddChild(rightScroll);

			var leftPanel = _filterDisabledCheck.GetParent() as VBoxContainer;

			_tabBar = new TabBar
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_tabBar.AddTab(TranslationServer.Translate("mods/tab_device"));
			_tabBar.AddTab(TranslationServer.Translate("mods/tab_network"));
			_tabBar.TabChanged += OnTabChanged;
			leftPanel.AddChild(_tabBar);
			leftPanel.MoveChild(_tabBar, 0);

			_tabHeaderBox = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_tabHeaderBox.AddThemeConstantOverride("separation", 8);
			leftPanel.AddChild(_tabHeaderBox);
			leftPanel.MoveChild(_tabHeaderBox, 2);

			BuildNetworkHeader();

			_backBtn.Pressed += Close;
			_toggleBtn.Pressed += OnToggleSelected;
			_filterDisabledCheck.Toggled += OnFilterToggled;
			_resetOrderBtn.Pressed += OnResetOrder;

			_catalogExportBtn = new Button
			{
				Text = TranslationServer.Translate("mods/catalog_export"),
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
			};
			_catalogExportBtn.Pressed += OnCatalogExport;
			var detailVBox = _toggleBtn.GetParent();
			detailVBox.AddChild(_catalogExportBtn);

			_installBtn = new Button
			{
				Text = TranslationServer.Translate("mods/install_button"),
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_installBtn.Pressed += OnInstallSelected;
			detailVBox.AddChild(_installBtn);

			_backBtn.Text = TranslationServer.Translate("mods/back");
			_titleLabel.Text = TranslationServer.Translate("mods/title");

			_disabledSet = DisabledModsStore.Load();
			BuildModList();
		}

		private void Close()
		{
			QueueFree();
		}

		private void OnTabChanged(long tab)
		{
			_currentTab = (ModTab)(int)tab;
			ClearDetailPanel();

			var isDevice = _currentTab == ModTab.Device;
			_filterDisabledCheck.Visible = isDevice;
			_resetOrderBtn.Visible = isDevice;
			_refreshNetworkBtn.Visible = !isDevice;
			_networkStatusLabel.Visible = !isDevice;
			if (isDevice)
				BuildDeviceList();
			else
				BuildNetworkList();
		}

		private void BuildModList()
		{
			BuildDeviceList();
		}

		private void BuildDeviceList()
		{
			_entries.Clear();

			foreach (var child in _modListContainer.GetChildren())
				child.QueueFree();

			var allMods = ModRegistry.Instance.AllDiscoveredMods;
			if (allMods == null || allMods.Count == 0)
				return;

			var sortedIds = ModRegistry.Instance.GetOrderedModIds(filterDisabled: false);

			for (int i = 0; i < sortedIds.Count; i++)
			{
				var id = sortedIds[i];
				if (!allMods.TryGetValue(id, out var modInfo))
					continue;

				var enabled = !_disabledSet.Contains(id);
				if (!_showDisabledMods && !enabled)
					continue;

				var canMoveUp = CanMoveUp(id, i, sortedIds);
				var canMoveDown = CanMoveDown(id, i, sortedIds);
				var entry = new ModEntry(modInfo, enabled, i, canMoveUp, canMoveDown);
				entry.SetCallbacks(() => SelectMod(id, entry), () => ToggleMod(id, entry),
					() => OnMoveUp(id), () => OnMoveDown(id));
				_modListContainer.AddChild(entry);
				_entries[id] = entry;
			}

			if (_entries.Count > 0)
			{
				var firstId = _entries.Keys.First();
				SelectMod(firstId, _entries[firstId]);
			}
			else
			{
				ClearDetailPanel();
			}
		}

		private void OnFilterToggled(bool showDisabled)
		{
			_showDisabledMods = showDisabled;
			BuildModList();
		}

		private void OnResetOrder()
		{
			var dialog = new ConfirmationDialog();
			dialog.Title = TranslationServer.Translate("mods/reset_order_title");
			dialog.DialogText = TranslationServer.Translate("mods/reset_order_text");
			dialog.OkButtonText = TranslationServer.Translate("mods/reset_order_confirm");
			dialog.CancelButtonText = TranslationServer.Translate("mods/reset_order_cancel");
			dialog.Exclusive = true;
			AddChild(dialog);

			dialog.Confirmed += () =>
			{
				ModLoadOrderStore.Save(new List<string>());
				ModRegistry.Instance.InvalidateOrder();
				BuildModList();
				ShowRestartToast();
				dialog.QueueFree();
			};
			dialog.Canceled += () => dialog.QueueFree();

			dialog.PopupCentered();
		}

		private void ClearDetailPanel()
		{
			_selectedModId = null;
			_selectedEntry = null;
			_detailName.Text = string.Empty;
			_detailVersion.Text = string.Empty;
			_detailAuthor.Text = string.Empty;
			_detailDescription.Text = string.Empty;
			_detailDepends.Text = string.Empty;
			_detailUsedBy.Text = string.Empty;
			if (_detailConflicts != null)
				_detailConflicts.Text = string.Empty;
			_detailIcon.Texture = null;
			_statusLabel.Text = string.Empty;
			_catalogExportBtn.Visible = false;
			if (_installBtn != null)
				_installBtn.Visible = false;
		}

		private bool CanMoveUp(string modId, int index, IReadOnlyList<string> sortedIds)
		{
			if (index <= 0)
				return false;

			var info = (ModInfo)ModRegistry.Instance.GetMod(modId);
			var depIds = info?.Manifest?.DependencyIds;
			if (depIds == null || depIds.Count == 0)
				return true;

			foreach (var dep in depIds)
			{
				if (sortedIds[index - 1] == dep)
					return false;
			}

			return true;
		}

		private bool CanMoveDown(string modId, int index, IReadOnlyList<string> sortedIds)
		{
			if (index >= sortedIds.Count - 1)
				return false;

			var belowId = sortedIds[index + 1];
			var belowInfo = (ModInfo)ModRegistry.Instance.GetMod(belowId);
			var belowDeps = belowInfo?.Manifest?.DependencyIds;
			if (belowDeps == null || belowDeps.Count == 0)
				return true;

			if (belowDeps.Contains(modId))
				return false;

			return true;
		}

		private void OnMoveUp(string modId)
		{
			ModLoadOrderStore.MoveUp(modId);
			OnLoadOrderChanged();
		}

		private void OnMoveDown(string modId)
		{
			ModLoadOrderStore.MoveDown(modId);
			OnLoadOrderChanged();
		}

		private void OnLoadOrderChanged()
		{
			ModRegistry.Instance.InvalidateOrder();
			ShowRestartToast();
			BuildModList();
		}

		private void SelectMod(string modId, ModEntry entry)
		{
			_selectedModId = modId;
			_selectedEntry = entry;

			foreach (var e in _entries.Values)
				e.SetSelected(false);
			entry.SetSelected(true);

			var allMods = ModRegistry.Instance.AllDiscoveredMods;
			if (!allMods.TryGetValue(modId, out var modInfo))
				return;

			var enabled = !_disabledSet.Contains(modId);

			_detailName.Text = modInfo.DisplayTitle;
			_detailVersion.Text = $"{TranslationServer.Translate("mods/version")}: {modInfo.Version ?? "—"}";
			_detailAuthor.Text = $"{TranslationServer.Translate("mods/author")}: {modInfo.Author ?? "—"}";
			_detailDescription.Text = modInfo.LocalizedDescription;

			UpdateStatusLabel(modId, enabled);

			var manifest = ((ModInfo)modInfo).Manifest;
			var depIds = manifest?.DependencyIds;
			if (depIds != null && depIds.Count > 0)
				_detailDepends.Text = $"{TranslationServer.Translate("mods/depends")}: {string.Join(", ", depIds)}";
			else
				_detailDepends.Text = $"{TranslationServer.Translate("mods/depends")}: —";

			var usedBy = new List<string>();
			foreach (var kvp in allMods)
			{
				if (kvp.Key == modId)
					continue;
				var otherManifest = ((ModInfo)kvp.Value).Manifest;
				var otherDeps = otherManifest?.DependencyIds;
				if (otherDeps != null && otherDeps.Contains(modId))
					usedBy.Add(kvp.Key);
			}
			_detailUsedBy.Text = usedBy.Count > 0
				? $"{TranslationServer.Translate("mods/used_by")}: {string.Join(", ", usedBy)}"
				: $"{TranslationServer.Translate("mods/used_by")}: —";

			ShowConflictInfo(modId);

			_catalogExportBtn.Visible = true;

			LoadModThumbnail(modInfo);
		}

		private void UpdateStatusLabel(string modId, bool enabled)
		{
			if (enabled)
			{
				_statusLabel.Text = TranslationServer.Translate("mods/enabled");
				_statusLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
			}
			else
			{
				var allMods = ModRegistry.Instance.AllDiscoveredMods;
				if (allMods.TryGetValue(modId, out var modInfo))
				{
					var reasons = ((ModInfo)modInfo).LoadFailureReasons;
					if (reasons != null && reasons.Count > 0)
					{
						_statusLabel.Text = string.Join("\n", reasons);
					}
					else
					{
						var hasDisabledDep = HasDisabledDependency(modId);
						_statusLabel.Text = hasDisabledDep
							? TranslationServer.Translate("mods/dependency_disabled")
							: TranslationServer.Translate("mods/disabled");
					}
				}
				else
				{
					_statusLabel.Text = TranslationServer.Translate("mods/disabled");
				}
				_statusLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.4f, 0.4f));
			}
		}

		private void ShowConflictInfo(string modId)
		{
			var overrides = ModConflictRegistry.GetOverridesBy(modId);
			var overridden = ModConflictRegistry.GetOverridesOf(modId);

			if (overrides.Count == 0 && overridden.Count == 0)
				return;

			var lines = new List<string>();
			foreach (var c in overrides)
			{
				var marker = c.WasConcatenated ? "+" : "!";
				lines.Add($"{marker} overrides {c.OverriddenModId ?? "?"}: {c.ResourceType}/{c.ResourceId}");
			}
			foreach (var c in overridden)
			{
				var marker = c.WasConcatenated ? "+" : "!";
				lines.Add($"{marker} overridden by {c.WinningModId}: {c.ResourceType}/{c.ResourceId}");
			}

			if (_detailConflicts == null)
			{
				_detailConflicts = new Label();
				_detailUsedBy.GetParent().AddChild(_detailConflicts);
			}
			_detailConflicts.Text = TranslationServer.Translate("mods/overrides_title") + ":\n" + string.Join("\n", lines);
			_detailConflicts.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.2f));
		}

		private bool HasDisabledDependency(string modId)
		{
			var allMods = ModRegistry.Instance.AllDiscoveredMods;
			if (!allMods.TryGetValue(modId, out var modInfo))
				return false;

			var manifest = ((ModInfo)modInfo).Manifest;
			var depIds = manifest?.DependencyIds;
			if (depIds == null)
				return false;

			foreach (var dep in depIds)
			{
				if (_disabledSet.Contains(dep))
					return true;
			}
			return false;
		}

		private void LoadModThumbnail(IModInfo modInfo)
		{
			_detailIcon.Texture = null;

			var provider = ((ModInfo)modInfo).FileProvider;
			if (provider == null)
				return;

			byte[] imageData = null;
			if (provider.FileExists("mod.png"))
				imageData = provider.ReadFileBinary("mod.png");
			else if (provider.FileExists("mod.jpg"))
				imageData = provider.ReadFileBinary("mod.jpg");

			if (imageData == null)
				return;

			var image = new Image();
			Error error;

			var firstByte = imageData[0];
			if (firstByte == 0x89)
				error = image.LoadPngFromBuffer(imageData);
			else if (firstByte == 0xFF)
				error = image.LoadJpgFromBuffer(imageData);
			else
				return;

			if (error != Error.Ok)
			{
				image.Dispose();
				return;
			}

			var texture = ImageTexture.CreateFromImage(image);
			image.Dispose();

			if (texture != null)
			{
				texture.SetSizeOverride(new Vector2I(DetailIconSize, DetailIconSize));
				_detailIcon.Texture = texture;
			}
		}

		private void OnToggleSelected()
		{
			if (string.IsNullOrEmpty(_selectedModId))
				return;

			var currentlyEnabled = !_disabledSet.Contains(_selectedModId);

			if (currentlyEnabled)
			{
				_disabledSet.Add(_selectedModId);
				var cascaded = new HashSet<string>();
				CollectDependents(_selectedModId, cascaded);
				foreach (var dep in cascaded)
					_disabledSet.Add(dep);
			}
			else
			{
				_disabledSet.Remove(_selectedModId);

				var allMods = ModRegistry.Instance.AllDiscoveredMods;
				if (allMods.TryGetValue(_selectedModId, out var modInfo))
				{
					var manifest = ((ModInfo)modInfo).Manifest;
					var depIds = manifest?.DependencyIds;
					if (depIds != null)
					{
						foreach (var dep in depIds)
							_disabledSet.Remove(dep);
					}
				}
			}

			DisabledModsStore.Save(_disabledSet);

			if (!_showDisabledMods)
			{
				BuildModList();
			}
			else
			{
				foreach (var kvp in _entries)
				{
					if (kvp.Value is ModEntry me)
						me.SetEnabled(!_disabledSet.Contains(kvp.Key));
				}

				if (_selectedEntry != null)
				{
					var newEnabled = !_disabledSet.Contains(_selectedModId);
					UpdateStatusLabel(_selectedModId, newEnabled);
				}
			}

			ShowRestartToast();
		}

		private void CollectDependents(string modId, HashSet<string> result)
		{
			var allMods = ModRegistry.Instance.AllDiscoveredMods;
			foreach (var kvp in allMods)
			{
				if (result.Contains(kvp.Key))
					continue;

				var manifest = ((ModInfo)kvp.Value).Manifest;
				var depIds = manifest?.DependencyIds;
				if (depIds != null && depIds.Contains(modId))
				{
					result.Add(kvp.Key);
					CollectDependents(kvp.Key, result);
				}
			}
		}

		private void ToggleMod(string modId, ModEntry entry)
		{
			SelectMod(modId, entry);
			OnToggleSelected();
		}

		private void ShowRestartToast()
		{
			var panel = GetNode<Panel>("Panel");
			var existingToasts = panel.GetNodeOrNull<Control>("RestartToast");
			if (existingToasts != null)
				existingToasts.QueueFree();

			var container = new Panel
			{
				Name = "RestartToast",
				MouseFilter = MouseFilterEnum.Ignore,
			};

			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.13f, 0.13f, 0.13f, 0.8f),
				CornerRadiusTopLeft = 6,
				CornerRadiusTopRight = 6,
				CornerRadiusBottomLeft = 6,
				CornerRadiusBottomRight = 6,
				ContentMarginLeft = 12,
				ContentMarginRight = 12,
				ContentMarginTop = 6,
				ContentMarginBottom = 6,
			};
			container.AddThemeStyleboxOverride("panel", style);

			var label = new Label
			{
				Text = TranslationServer.Translate("mods/restart_required"),
				Modulate = new Color(1, 1, 1, 0),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			label.AddThemeFontSizeOverride("font_size", 14);
			container.AddChild(label);

			container.AnchorRight = 1f;
			container.OffsetLeft = -280;
			container.OffsetRight = -16;
			container.OffsetTop = 8;

			panel.AddChild(container);

			var tween = CreateTween();
			tween.TweenProperty(label, "modulate", new Color(1, 1, 1, 1), 0.3f);
			tween.TweenProperty(label, "modulate", new Color(1, 1, 1, 0), 0.5f).SetDelay(5f);
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(container))
					container.QueueFree();
			}));
		}

		private partial class ModEntry : HBoxContainer
		{
			private readonly TextureRect _thumbnail;
			private readonly Label _nameLabel;
			private readonly Label _versionLabel;
			private readonly Label _orderLabel;
			private readonly CheckButton _toggle;
			private readonly Button _upBtn;
			private readonly Button _downBtn;
			private readonly ColorRect _selectionBg;
			private readonly ColorRect _disabledOverlay;
			private bool _enabled;
			private bool _updatingState;
			private System.Action _onSelect;
			private System.Action _onToggle;
			private System.Action _onMoveUp;
			private System.Action _onMoveDown;

			public ModEntry(IModInfo modInfo, bool enabled, int orderIndex, bool canMoveUp, bool canMoveDown)
			{
				MouseFilter = MouseFilterEnum.Stop;
				SizeFlagsHorizontal = SizeFlags.ExpandFill;
				CustomMinimumSize = new Vector2(0, ThumbnailSize + 8);

				_selectionBg = new ColorRect
				{
					Color = new Color(0.3f, 0.5f, 0.7f, 0f),
					MouseFilter = MouseFilterEnum.Ignore
				};
				_selectionBg.SetAnchorsPreset(LayoutPreset.FullRect);
				AddChild(_selectionBg);

				_disabledOverlay = new ColorRect
				{
					Color = new Color(0f, 0f, 0f, enabled ? 0f : 0.3f),
					MouseFilter = MouseFilterEnum.Ignore
				};
				_disabledOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
				AddChild(_disabledOverlay);

				_orderLabel = new Label
				{
					Text = (orderIndex + 1).ToString(),
					CustomMinimumSize = new Vector2(28, 0),
					HorizontalAlignment = HorizontalAlignment.Right
				};
				_orderLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.55f));
				_orderLabel.AddThemeFontSizeOverride("font_size", 12);
				AddChild(_orderLabel);

				_thumbnail = new TextureRect
				{
					CustomMinimumSize = new Vector2(ThumbnailSize, ThumbnailSize),
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
				};
				AddChild(_thumbnail);

				var textBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				_nameLabel = new Label
				{
					Text = modInfo.DisplayTitle,
					MouseFilter = MouseFilterEnum.Pass
				};
				textBox.AddChild(_nameLabel);

				_versionLabel = new Label
				{
					Text = $"v{modInfo.Version ?? "0.0.0.0"}",
					MouseFilter = MouseFilterEnum.Pass
				};
				_versionLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
				_versionLabel.AddThemeFontSizeOverride("font_size", 12);
				textBox.AddChild(_versionLabel);

				AddChild(textBox);

				_toggle = new CheckButton
				{
					ButtonPressed = enabled,
					SizeFlagsHorizontal = SizeFlags.ShrinkEnd
				};
				_toggle.Toggled += toggledOn =>
				{
					if (_updatingState)
						return;
					_onToggle?.Invoke();
				};
				AddChild(_toggle);

				var arrowBox = new VBoxContainer();
				arrowBox.AddThemeConstantOverride("separation", 0);

				_upBtn = new Button
				{
					Text = "▲",
					CustomMinimumSize = new Vector2(32, 20),
					Disabled = !canMoveUp,
					Flat = true
				};
				_upBtn.AddThemeFontSizeOverride("font_size", 10);
				_upBtn.Pressed += () => _onMoveUp?.Invoke();
				arrowBox.AddChild(_upBtn);

				_downBtn = new Button
				{
					Text = "▼",
					CustomMinimumSize = new Vector2(32, 20),
					Disabled = !canMoveDown,
					Flat = true
				};
				_downBtn.AddThemeFontSizeOverride("font_size", 10);
				_downBtn.Pressed += () => _onMoveDown?.Invoke();
				arrowBox.AddChild(_downBtn);

				AddChild(arrowBox);

				_enabled = enabled;
				UpdateTextColor();

				GuiInput += e =>
				{
					if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					{
						_onSelect?.Invoke();
						AcceptEvent();
					}
				};

				LoadThumbnail(modInfo);
			}

			public void SetCallbacks(System.Action onSelect, System.Action onToggle,
				System.Action onMoveUp, System.Action onMoveDown)
			{
				_onSelect = onSelect;
				_onToggle = onToggle;
				_onMoveUp = onMoveUp;
				_onMoveDown = onMoveDown;
			}

			public void SetEnabled(bool enabled)
			{
				_updatingState = true;
				_enabled = enabled;
				_toggle.ButtonPressed = enabled;
				_disabledOverlay.Color = new Color(0f, 0f, 0f, enabled ? 0f : 0.3f);
				UpdateTextColor();
				_updatingState = false;
			}

			public void SetSelected(bool selected)
			{
				_selectionBg.Color = new Color(0.3f, 0.5f, 0.7f, selected ? 0.3f : 0f);
			}

			private void UpdateTextColor()
			{
				var color = _enabled
					? new Color(0.85f, 0.85f, 0.9f)
					: new Color(0.45f, 0.45f, 0.5f);
				_nameLabel.AddThemeColorOverride("font_color", color);
			}

			private void LoadThumbnail(IModInfo modInfo)
			{
				var provider = ((ModInfo)modInfo).FileProvider;
				if (provider == null)
					return;

				byte[] imageData = null;
				if (provider.FileExists("mod.png"))
					imageData = provider.ReadFileBinary("mod.png");
				else if (provider.FileExists("mod.jpg"))
					imageData = provider.ReadFileBinary("mod.jpg");

				if (imageData == null)
					return;

				var image = new Image();
				Error error;
				if (imageData[0] == 0x89)
					error = image.LoadPngFromBuffer(imageData);
				else if (imageData[0] == 0xFF)
					error = image.LoadJpgFromBuffer(imageData);
				else
					return;

				if (error != Error.Ok)
				{
					image.Dispose();
					return;
				}

				var texture = ImageTexture.CreateFromImage(image);
				image.Dispose();

				if (texture != null)
				{
					texture.SetSizeOverride(new Vector2I(ThumbnailSize, ThumbnailSize));
					_thumbnail.Texture = texture;
				}
			}

		}

	}
}
