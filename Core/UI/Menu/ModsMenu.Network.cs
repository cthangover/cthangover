using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Caches;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Menu
{
	/// <summary>
	/// Network/online tab of the mods menu. Fetches mod catalogs from
	/// URLs configured in <see cref="ModConfig.CatalogUrls"/>, merges
	/// and deduplicates entries, compares them against locally-installed
	/// mods via <see cref="ModInfo.InstallSha256"/> and version strings,
	/// and displays status badges (installed / update available / not
	/// installed / modified locally).
	///
	/// Lives in a separate file from <c>ModsMenu.cs</c> for readability
	/// — the device tab logic is in the main file, the network tab logic
	/// is here. Both share the same partial class.
	/// </summary>
	public partial class ModsMenu
	{
		private Button _refreshNetworkBtn;
		private Label _networkStatusLabel;
		private readonly Dictionary<string, RemoteModEntry> _remoteEntries = new();

		private CatalogExportEntry _selectedRemoteEntry;
		private CatalogEntryDownload _selectedDownload;
		private RemoteModStatus _selectedRemoteStatus;
		private static readonly HashSet<string> _pendingRestart = new();

		private static string GetCurrentLocale()
		{
			var locale = TranslationServer.GetLocale();
			if (string.IsNullOrEmpty(locale))
				locale = "en";
			return locale;
		}

		private static string GetStatusText(RemoteModStatus status)
		{
			return status switch
			{
				RemoteModStatus.Installed => TranslationServer.Translate("mods/status_installed"),
				RemoteModStatus.UpdateAvailable => TranslationServer.Translate("mods/status_update_available"),
				RemoteModStatus.NotInstalled => TranslationServer.Translate("mods/status_not_installed"),
				RemoteModStatus.ModifiedLocally => TranslationServer.Translate("mods/status_modified"),
				_ => "—",
			};
		}

		private void BuildNetworkHeader()
		{
			_refreshNetworkBtn = new Button
			{
				Text = TranslationServer.Translate("mods/network_refresh"),
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_refreshNetworkBtn.Pressed += OnRefreshNetwork;
			_tabHeaderBox.AddChild(_refreshNetworkBtn);

			_networkStatusLabel = new Label
			{
				Text = string.Empty,
				Visible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_networkStatusLabel.AddThemeFontSizeOverride("font_size", 12);
			_networkStatusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
			_tabHeaderBox.AddChild(_networkStatusLabel);
		}

		private void OnRefreshNetwork()
		{
			_networkStatusLabel.Text = TranslationServer.Translate("mods/network_loading");
			RefreshNetworkMods();
		}

		private async void RefreshNetworkMods()
		{
			try
			{
				await CatalogService.RefreshAsync(forceRefresh: true);
			}
			catch
			{
				_networkStatusLabel.Text = TranslationServer.Translate("mods/network_error");
				return;
			}

			BuildNetworkList();
		}

		private void BuildNetworkList()
		{
			_entries.Clear();
			_remoteEntries.Clear();

			foreach (var child in _modListContainer.GetChildren())
				child.QueueFree();

			CatalogService.LoadCache();
			var catalogEntries = CatalogService.CachedEntries;

			if (catalogEntries == null || catalogEntries.Count == 0)
			{
				if (CatalogService.IsFetching)
					_networkStatusLabel.Text = TranslationServer.Translate("mods/network_loading");
				else if (ModConfig.Instance.CatalogUrls.Count == 0)
					_networkStatusLabel.Text = TranslationServer.Translate("mods/network_no_urls");
				else
				{
					_networkStatusLabel.Text = TranslationServer.Translate("mods/network_empty");
					if (!CatalogService.IsFetching)
						RefreshNetworkMods();
				}
				return;
			}

			_networkStatusLabel.Text = TranslationServer.Translate("mods/network_ready");

			var allMods = ModRegistry.Instance.AllDiscoveredMods;

			for (int i = 0; i < catalogEntries.Count; i++)
			{
				var remoteEntry = catalogEntries[i];
				var localInfo = allMods != null && allMods.TryGetValue(remoteEntry.Id, out var mi)
					? (ModInfo)mi : null;

				var status = localInfo != null
					? CatalogService.GetRemoteModStatus(localInfo.Version, localInfo.InstallSha256, remoteEntry)
					: RemoteModStatus.NotInstalled;

				var entry = new RemoteModEntry(remoteEntry, localInfo, status, i);
				entry.SetCallback(() => SelectRemoteMod(remoteEntry, localInfo, entry));
				entry.SetVersionChangedCallback(() =>
				{
					if (_selectedRemoteEntry?.Id == remoteEntry.Id)
						SelectRemoteMod(remoteEntry, localInfo, entry);
				});
				_modListContainer.AddChild(entry);
				_remoteEntries[remoteEntry.Id] = entry;
			}
		}

		private void SelectRemoteMod(CatalogExportEntry remoteEntry, ModInfo localInfo,
			RemoteModEntry remoteModEntry)
		{
			foreach (var e in _entries.Values)
				e.SetSelected(false);
			foreach (var e in _remoteEntries.Values)
				e.SetSelected(false);
			remoteModEntry.SetSelected(true);

			_selectedModId = null;
			_selectedEntry = null;
			_selectedRemoteEntry = remoteEntry;
			_selectedDownload = remoteModEntry.SelectedDownload;

			_catalogExportBtn.Visible = false;
			_toggleBtn.Visible = false;

			UpdateInstallButton();

			var selectedVersion = _selectedDownload?.Version ?? remoteEntry.Version;
			var currentLang = GetCurrentLocale();
			_detailName.Text = remoteEntry.GetLocalizedName(currentLang);
			_detailVersion.Text = $"{TranslationServer.Translate("mods/version")}: {selectedVersion ?? "—"}";
			_detailAuthor.Text = $"{TranslationServer.Translate("mods/author")}: {remoteEntry.Author ?? "—"}";
			_detailDescription.Text = remoteEntry.GetLocalizedDescription(currentLang);

			_selectedRemoteStatus = ComputeSelectedStatus(localInfo, selectedVersion);
			_statusLabel.Text = GetStatusText(_selectedRemoteStatus);
			var statusColor = GetStatusColor(_selectedRemoteStatus);
			_statusLabel.AddThemeColorOverride("font_color", statusColor);

			_detailDepends.Text = remoteEntry.Depends != null && remoteEntry.Depends.Count > 0
				? $"{TranslationServer.Translate("mods/depends")}: {string.Join(", ", remoteEntry.Depends)}"
				: $"{TranslationServer.Translate("mods/depends")}: —";

			_detailUsedBy.Text = string.Empty;

			_detailIcon.Texture = null;

			ModIconCache.GetOrDownload(remoteEntry.Id, remoteEntry.IconUrl, texture =>
			{
				if (texture is ImageTexture imageTexture && GodotObject.IsInstanceValid(_detailIcon))
				{
					imageTexture.SetSizeOverride(new Vector2I(DetailIconSize, DetailIconSize));
					_detailIcon.Texture = imageTexture;
				}
			});
		}

		private RemoteModStatus ComputeSelectedStatus(ModInfo localInfo, string selectedVersion)
		{
			if (localInfo == null)
				return RemoteModStatus.NotInstalled;

			if (string.IsNullOrEmpty(selectedVersion))
				return RemoteModStatus.NotInstalled;

			var cmp = VersionHelper.Compare(selectedVersion, localInfo.Version ?? "0.0.0");
			if (cmp > 0)
				return RemoteModStatus.UpdateAvailable;
			if (cmp < 0)
				return RemoteModStatus.UpdateAvailable;

			var download = _selectedDownload;
			if (download != null
				&& !string.IsNullOrEmpty(localInfo.InstallSha256)
				&& !string.IsNullOrEmpty(download.InstallSha256)
				&& !string.Equals(localInfo.InstallSha256, download.InstallSha256,
					StringComparison.OrdinalIgnoreCase))
				return RemoteModStatus.ModifiedLocally;

			return RemoteModStatus.Installed;
		}

		private void UpdateInstallButton()
		{
			if (_installBtn == null || _selectedRemoteEntry == null)
				return;

			var modId = _selectedRemoteEntry.Id;
			if (_pendingRestart.Contains(modId))
			{
				_installBtn.Text = TranslationServer.Translate("mods/restart_button");
				_installBtn.Disabled = true;
				_installBtn.Visible = true;
				_installBtn.RemoveThemeColorOverride("font_color");
				return;
			}

			var allMods = ModRegistry.Instance.AllDiscoveredMods;
			allMods.TryGetValue(modId, out var mi);
			var localInfo = mi as ModInfo;

			var selectedVersion = _selectedDownload?.Version ?? _selectedRemoteEntry.Version;

			if (localInfo == null)
			{
				_installBtn.Text = TranslationServer.Translate("mods/install_button");
				_installBtn.Disabled = false;
				_installBtn.Visible = true;
				_installBtn.RemoveThemeColorOverride("font_color");
				return;
			}

			var cmp = VersionHelper.Compare(selectedVersion ?? "0", localInfo.Version ?? "0");
			if (cmp > 0)
			{
				_installBtn.Text = TranslationServer.Translate("mods/update_button");
				_installBtn.Disabled = false;
				_installBtn.Visible = true;
				_installBtn.AddThemeColorOverride("font_color", new Color(1f, 0.65f, 0.2f));
			}
			else if (cmp < 0)
			{
				_installBtn.Text = TranslationServer.Translate("mods/downgrade_button");
				_installBtn.Disabled = false;
				_installBtn.Visible = true;
				_installBtn.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
			}
			else
			{
				if (_selectedDownload != null
					&& !string.IsNullOrEmpty(localInfo.InstallSha256)
					&& !string.IsNullOrEmpty(_selectedDownload.InstallSha256)
					&& !string.Equals(localInfo.InstallSha256, _selectedDownload.InstallSha256,
						StringComparison.OrdinalIgnoreCase))
				{
					_installBtn.Text = TranslationServer.Translate("mods/overwrite_button");
					_installBtn.Disabled = false;
					_installBtn.Visible = true;
					_installBtn.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
				}
				else
				{
					_installBtn.Visible = false;
				}
			}
		}

		private async void OnInstallSelected()
		{
			if (_installBtn == null || _selectedRemoteEntry == null)
				return;

			var modId = _selectedRemoteEntry.Id;
			var version = _selectedDownload?.Version;
			_pendingRestart.Add(modId);
			_installBtn.Text = TranslationServer.Translate("mods/installing");
			_installBtn.Disabled = true;
			_installBtn.RemoveThemeColorOverride("font_color");

			ModDownloader.OnProgress += progress =>
			{
				if (!GodotObject.IsInstanceValid(_statusLabel))
					return;
				_statusLabel.Text = progress.Message;
			};

			ModDownloader.OnCompleted += (completedModId, success) =>
			{
				if (completedModId != modId)
					return;
				if (!GodotObject.IsInstanceValid(this))
					return;

				if (success)
				{
					_installBtn.Text = TranslationServer.Translate("mods/restart_button");
					_installBtn.Disabled = true;
					_statusLabel.Text = TranslationServer.Translate("mods/install_complete");
				}
				else
				{
					_pendingRestart.Remove(modId);
					UpdateInstallButton();
					_statusLabel.Text = TranslationServer.Translate("mods/install_failed");
				}
			};

			await ModDownloader.InstallAsync(modId, version);
		}

		private static Color GetStatusColor(RemoteModStatus status)
		{
			return status switch
			{
				RemoteModStatus.Installed => new Color(0.3f, 0.8f, 0.3f),
				RemoteModStatus.UpdateAvailable => new Color(1f, 0.65f, 0.2f),
				RemoteModStatus.NotInstalled => new Color(0.5f, 0.5f, 0.55f),
				RemoteModStatus.ModifiedLocally => new Color(1f, 0.4f, 0.4f),
				_ => new Color(0.6f, 0.6f, 0.6f),
			};
		}

		private void OnCatalogExport()
		{
			if (string.IsNullOrEmpty(_selectedModId))
				return;

			var allMods = ModRegistry.Instance.AllDiscoveredMods;
			if (!allMods.TryGetValue(_selectedModId, out var modInfo))
				return;

			ShowCatalogExportDialog((ModInfo)modInfo);
		}

		private void ShowCatalogExportDialog(ModInfo modInfo)
		{
			var window = new Window
			{
				Title = TranslationServer.Translate("mods/catalog_title"),
				Unresizable = false,
				Size = new Vector2I(560, 640),
				Exclusive = true,
			};

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 16);
			margin.AddThemeConstantOverride("margin_right", 16);
			margin.AddThemeConstantOverride("margin_top", 12);
			margin.AddThemeConstantOverride("margin_bottom", 12);
			margin.SetAnchorsPreset(LayoutPreset.FullRect);
			window.AddChild(margin);

			var scroll = new ScrollContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
			};
			margin.AddChild(scroll);

			var vbox = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			vbox.AddThemeConstantOverride("separation", 8);
			scroll.AddChild(vbox);

			var manifest = modInfo.Manifest;

			AddSectionLabel(vbox, "Mod Info");
			AddReadOnlyField(vbox, "mods/id", modInfo.Id);
			AddReadOnlyField(vbox, "mods/name", modInfo.DisplayTitle);
			AddReadOnlyField(vbox, "mods/version", modInfo.Version ?? "—");
			AddReadOnlyField(vbox, "mods/author", modInfo.Author ?? "—");

			AddSectionLabel(vbox, "Catalog Metadata");
			var urlEdit = AddLineEdit(vbox, "mods/catalog_download_url");

			var categories = Enum.GetValues<CategoryType>();
			var categoryNames = categories.Select(c => GetCategoryDisplayName(c)).ToArray();
			var categoryOpt = AddOptionButton(vbox, "mods/catalog_category", categoryNames);

			var tagsEdit = AddLineEdit(vbox, "mods/catalog_tags");

			var changelogEdit = new TextEdit
			{
				CustomMinimumSize = new Vector2(0, 60),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				WrapMode = TextEdit.LineWrappingMode.Boundary,
			};
			AddLabeledControl(vbox, "mods/catalog_changelog", changelogEdit);

			var statuses = new[] { "stable", "beta", "deprecated", "archived" };
			var statusOpt = AddOptionButton(vbox, "mods/catalog_status", statuses);

			var minGameVer = AddLineEdit(vbox, "mods/catalog_min_game_version");
			var iconUrlEdit = AddLineEdit(vbox, "mods/catalog_icon_url");

			AddSectionLabel(vbox, "JSON Preview");
			var previewEdit = new TextEdit
			{
				CustomMinimumSize = new Vector2(0, 160),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Editable = false,
				WrapMode = TextEdit.LineWrappingMode.None,
			};
			previewEdit.AddThemeFontSizeOverride("font_size", 11);
			vbox.AddChild(previewEdit);

			var btnRow = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			btnRow.AddThemeConstantOverride("separation", 8);

			var copyBtn = new Button
			{
				Text = TranslationServer.Translate("mods/catalog_copy"),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			btnRow.AddChild(copyBtn);

			var exportBtn = new Button
			{
				Text = TranslationServer.Translate("mods/catalog_export_file"),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			btnRow.AddChild(exportBtn);

			var closeBtn = new Button
			{
				Text = TranslationServer.Translate("mods/catalog_close"),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			btnRow.AddChild(closeBtn);

			vbox.AddChild(btnRow);

			var modsRoot = ModRegistry.ModsPath;

			AddSectionLabel(vbox, "Locales");
			var localeContainer = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			localeContainer.AddThemeConstantOverride("separation", 4);
			vbox.AddChild(localeContainer);

			var addLocaleBtn = new Button
			{
				Text = TranslationServer.Translate("mods/catalog_add_locale"),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			vbox.AddChild(addLocaleBtn);

			System.Func<List<CatalogLocaleEntry>> collectLocales = null;
			System.Action updatePreview = null;

			void AddLocaleRow(string lang = "", string name = "", string desc = "")
			{
				var row = new HBoxContainer
				{
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
				};
				row.AddThemeConstantOverride("separation", 4);

				var langEdit = new LineEdit
				{
					CustomMinimumSize = new Vector2(44, 0),
					PlaceholderText = "en",
					MaxLength = 5,
					Text = lang,
				};
				row.AddChild(langEdit);

				var nameEdit = new LineEdit
				{
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					PlaceholderText = TranslationServer.Translate("mods/catalog_locale_name"),
					Text = name,
				};
				row.AddChild(nameEdit);

				var descEdit = new LineEdit
				{
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					PlaceholderText = TranslationServer.Translate("mods/catalog_locale_desc"),
					Text = desc,
				};
				row.AddChild(descEdit);

				var removeBtn = new Button { Text = "×", CustomMinimumSize = new Vector2(28, 0) };
				removeBtn.Pressed += () =>
				{
					localeContainer.RemoveChild(row);
					row.QueueFree();
					updatePreview();
				};
				row.AddChild(removeBtn);

				langEdit.TextChanged += _ => updatePreview();
				nameEdit.TextChanged += _ => updatePreview();
				descEdit.TextChanged += _ => updatePreview();

				localeContainer.AddChild(row);
			}

			addLocaleBtn.Pressed += () => AddLocaleRow();

			collectLocales = () =>
			{
				var list = new List<CatalogLocaleEntry>();
				foreach (var child in localeContainer.GetChildren())
				{
					if (child is HBoxContainer row && row.GetChildCount() >= 3)
					{
						var lang = ((LineEdit)row.GetChild(0)).Text.Trim();
						var name = ((LineEdit)row.GetChild(1)).Text.Trim();
						var desc = ((LineEdit)row.GetChild(2)).Text.Trim();
						if (!string.IsNullOrEmpty(lang))
						{
							list.Add(new CatalogLocaleEntry
							{
								Lang = lang,
								Name = name.Length > 0 ? name : null,
								Description = desc.Length > 0 ? desc : null,
							});
						}
					}
				}
				if (list.Count == 0)
				{
					list.Add(new CatalogLocaleEntry
					{
						Lang = "en",
						Name = manifest.Name,
						Description = manifest.Description,
					});
				}
				return list;
			};

			updatePreview = () =>
			{
				var entry = BuildEntryFromManifest(manifest);
				entry.Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload
					{
						Version = entry.Version,
						Urls = new List<string> { urlEdit.Text.Trim() },
						SizeBytes = 0,
						ArchiveSha256 = "—",
						InstallSha256 = "—",
					},
				};
				entry.Category = categories[categoryOpt.Selected];
				var tagsText = tagsEdit.Text.Trim();
				entry.Tags = string.IsNullOrEmpty(tagsText)
					? null
					: tagsText.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
			entry.Downloads[0].Changelog = changelogEdit.Text.Trim().Length > 0 ? changelogEdit.Text.Trim() : null;
			entry.Status = statuses[statusOpt.Selected];
			var mgv = minGameVer.Text.Trim();
			entry.MinGameVersion = mgv.Length > 0 ? mgv : null;
			var iconUrl = iconUrlEdit.Text.Trim();
			entry.IconUrl = iconUrl.Length > 0 ? iconUrl : null;
			entry.Locale = collectLocales();
				previewEdit.Text = JsonSerializer.Serialize(entry, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
			};

			updatePreview();

			urlEdit.TextChanged += _ => updatePreview();
			categoryOpt.ItemSelected += _ => updatePreview();
			tagsEdit.TextChanged += _ => updatePreview();
			changelogEdit.TextChanged += () => updatePreview();
			statusOpt.ItemSelected += _ => updatePreview();
			minGameVer.TextChanged += _ => updatePreview();
			iconUrlEdit.TextChanged += _ => updatePreview();

			copyBtn.Pressed += () =>
			{
				DisplayServer.ClipboardSet(previewEdit.Text);
				GameLogger.Log("MODS_UI", $"Copied catalog JSON for '{modInfo.Id}' to clipboard");
			};

			exportBtn.Pressed += () =>
			{
				var entry = BuildEntryFromManifest(manifest);
				entry.Downloads = new List<CatalogEntryDownload>
				{
					new CatalogEntryDownload
					{
						Version = entry.Version,
						Urls = new List<string> { urlEdit.Text.Trim() },
						SizeBytes = 0,
					},
				};
				entry.Category = categories[categoryOpt.Selected];
				var tagsText = tagsEdit.Text.Trim();
				entry.Tags = string.IsNullOrEmpty(tagsText)
					? null
					: tagsText.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
			entry.Downloads[0].Changelog = changelogEdit.Text.Trim().Length > 0 ? changelogEdit.Text.Trim() : null;
			entry.Status = statuses[statusOpt.Selected];
			var mgv = minGameVer.Text.Trim();
			entry.MinGameVersion = mgv.Length > 0 ? mgv : null;
			var iconUrl = iconUrlEdit.Text.Trim();
			entry.IconUrl = iconUrl.Length > 0 ? iconUrl : null;
			entry.Locale = collectLocales();

				var isZip = modInfo.FileProvider.RootPath.EndsWith(".zip",
					System.StringComparison.OrdinalIgnoreCase);

				if (isZip)
					ModCatalogExporter.ExportFromZip(modInfo, entry, modsRoot);
				else
					ModCatalogExporter.ExportFromFolder(modInfo, entry, modsRoot);

				window.QueueFree();
			};

			closeBtn.Pressed += () => window.QueueFree();
			window.CloseRequested += () => window.QueueFree();

			var tree = Godot.Engine.GetMainLoop() as SceneTree;
			tree?.Root.AddChild(window);
			window.PopupCentered(window.Size);
			window.GrabFocus();
		}

		private static CatalogExportEntry BuildEntryFromManifest(ModManifest manifest)
		{
			var depends = manifest.Depends?.Select(d => d.ToString()).Where(s => s.Length > 0).ToList();

			return new CatalogExportEntry
			{
				Id = manifest.Id,
				Name = manifest.Name,
				Author = manifest.Author,
				Version = manifest.Version,
				Description = manifest.Description,
				Depends = depends,
				ConflictsWith = manifest.ConflictsWith,
			};
		}

		private static void AddLabeledControl(Container parent, string labelKey, Control control)
		{
			var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var label = new Label
			{
				Text = TranslationServer.Translate(labelKey),
			};
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
			box.AddChild(label);
			box.AddChild(control);
			parent.AddChild(box);
		}

		private static void AddReadOnlyField(Container parent, string labelKey, string value)
		{
			var label = new Label
			{
				Text = $"{TranslationServer.Translate(labelKey)}: {value}",
			};
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
			parent.AddChild(label);
		}

		private static LineEdit AddLineEdit(Container parent, string labelKey)
		{
			var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			AddLabeledControl(parent, labelKey, edit);
			return edit;
		}

		private static OptionButton AddOptionButton(Container parent, string labelKey, string[] items)
		{
			var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			foreach (var item in items)
				opt.AddItem(item);
			AddLabeledControl(parent, labelKey, opt);
			return opt;
		}

		private static void AddSectionLabel(Container parent, string text)
		{
			var sep = new HSeparator();
			parent.AddChild(sep);
			var label = new Label { Text = text };
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
			parent.AddChild(label);
		}

		private static string GetCategoryDisplayName(CategoryType cat) => cat switch
		{
			CategoryType.Story => TranslationServer.Translate("mods/category_story"),
			CategoryType.Characters => TranslationServer.Translate("mods/category_characters"),
			CategoryType.Gameplay => TranslationServer.Translate("mods/category_gameplay"),
			CategoryType.Visual => TranslationServer.Translate("mods/category_visual"),
			CategoryType.Audio => TranslationServer.Translate("mods/category_audio"),
			CategoryType.UI => TranslationServer.Translate("mods/category_ui"),
			CategoryType.Locale => TranslationServer.Translate("mods/category_locale"),
			CategoryType.Library => TranslationServer.Translate("mods/category_library"),
			CategoryType.Overhaul => TranslationServer.Translate("mods/category_overhaul"),
			CategoryType.Other => TranslationServer.Translate("mods/category_other"),
			_ => cat.ToString(),
		};

		private partial class RemoteModEntry : HBoxContainer
		{
			private readonly TextureRect _thumbnail;
			private readonly Label _nameLabel;
			private readonly Label _versionLabel;
			private readonly OptionButton _versionDropdown;
			private readonly Label _statusLabel;
			private readonly ColorRect _selectionBg;
			private readonly List<CatalogEntryDownload> _downloads;
			private System.Action _onSelect;
			private System.Action _onVersionChanged;

			public CatalogEntryDownload SelectedDownload =>
				_versionDropdown != null && _downloads != null && _versionDropdown.Selected >= 0
					? _downloads[_versionDropdown.Selected] : null;
			public string SelectedVersion => SelectedDownload?.Version;

			public RemoteModEntry(CatalogExportEntry remoteEntry, ModInfo localInfo,
				RemoteModStatus status, int orderIndex)
			{
				_downloads = remoteEntry.Downloads;

				MouseFilter = MouseFilterEnum.Stop;
				SizeFlagsHorizontal = SizeFlags.ExpandFill;
				CustomMinimumSize = new Vector2(0, ThumbnailSize + 8);

				_selectionBg = new ColorRect
				{
					Color = new Color(0.3f, 0.5f, 0.7f, 0f),
					MouseFilter = MouseFilterEnum.Ignore,
				};
				_selectionBg.SetAnchorsPreset(LayoutPreset.FullRect);
				AddChild(_selectionBg);

				var orderLabel = new Label
				{
					Text = (orderIndex + 1).ToString(),
					CustomMinimumSize = new Vector2(28, 0),
					HorizontalAlignment = HorizontalAlignment.Right,
				};
				orderLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.55f));
				orderLabel.AddThemeFontSizeOverride("font_size", 12);
				AddChild(orderLabel);

				_thumbnail = new TextureRect
				{
					CustomMinimumSize = new Vector2(ThumbnailSize, ThumbnailSize),
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				};
				AddChild(_thumbnail);

				ModIconCache.GetOrDownload(remoteEntry.Id, remoteEntry.IconUrl, texture =>
				{
					if (texture is ImageTexture imageTexture && GodotObject.IsInstanceValid(_thumbnail))
					{
						imageTexture.SetSizeOverride(new Vector2I(ThumbnailSize, ThumbnailSize));
						_thumbnail.Texture = imageTexture;
					}
				});

				var textBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				_nameLabel = new Label
				{
					Text = remoteEntry.GetLocalizedName(GetCurrentLocale()),
					MouseFilter = MouseFilterEnum.Pass,
				};
				textBox.AddChild(_nameLabel);

				var versionString = localInfo != null
					? $"v{localInfo.Version ?? "—"} → v{remoteEntry.Version ?? "—"}"
					: $"v{remoteEntry.Version ?? "—"}";
				_versionLabel = new Label
				{
					Text = versionString,
					MouseFilter = MouseFilterEnum.Pass,
				};
				_versionLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
				_versionLabel.AddThemeFontSizeOverride("font_size", 12);
				textBox.AddChild(_versionLabel);

				if (_downloads != null && _downloads.Count > 1)
				{
					_versionDropdown = new OptionButton
					{
						CustomMinimumSize = new Vector2(130, 0),
						SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
					};
					_versionDropdown.AddThemeFontSizeOverride("font_size", 11);

					foreach (var dl in _downloads)
					{
						var label = $"v{dl.Version}";
						if (dl == _downloads[0])
							label += " (latest)";
						if (localInfo != null && string.Equals(dl.Version, localInfo.Version,
							StringComparison.OrdinalIgnoreCase))
						{
							label += " (installed)";
						}
						_versionDropdown.AddItem(label);
					}

					_versionDropdown.ItemSelected += _ => _onVersionChanged?.Invoke();
					textBox.AddChild(_versionDropdown);
				}

				AddChild(textBox);

				_statusLabel = new Label
				{
					Text = GetStatusText(status),
					SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
				};
				_statusLabel.AddThemeColorOverride("font_color", GetStatusColor(status));
				_statusLabel.AddThemeFontSizeOverride("font_size", 11);
				AddChild(_statusLabel);

				GuiInput += e =>
				{
					if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					{
						_onSelect?.Invoke();
						AcceptEvent();
					}
				};
			}

			public void SetCallback(System.Action onSelect)
			{
				_onSelect = onSelect;
			}

			public void SetVersionChangedCallback(System.Action onVersionChanged)
			{
				_onVersionChanged = onVersionChanged;
			}

			public void SetSelected(bool selected)
			{
				_selectionBg.Color = new Color(0.3f, 0.5f, 0.7f, selected ? 0.3f : 0f);
			}
		}
	}
}
