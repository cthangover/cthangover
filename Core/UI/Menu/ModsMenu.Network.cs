using System;
using System.Collections.Generic;
using System.Threading;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Caches;
using Cthangover.Core.UI.Theming;
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
		private CancellationTokenSource _networkRefreshCts;

		/// <summary>
		/// Cancels an in-flight catalog fetch when the menu leaves the tree.
		/// Without this the awaited continuation in
		/// <see cref="RefreshNetworkMods"/> would rebuild lists on freed nodes
		/// after the player closed the menu mid-download. The source is
		/// disposed by <see cref="RefreshNetworkMods"/> when the fetch unwinds,
		/// not here, to avoid racing the pending HTTP request with a disposed
		/// token.
		/// </summary>
		public override void _ExitTree()
		{
			_networkRefreshCts?.Cancel();
		}

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

		/// <summary>
		/// Unique dictionary key for a remote entry: the same id may appear several
		/// times when catalogs publish conflicting variants (see
		/// <see cref="CatalogService"/>), so the first source URL disambiguates them.
		/// </summary>
		private static string RemoteKey(CatalogExportEntry entry)
			=> $"{entry.Id}|{FirstSource(entry)}";

		private static string FirstSource(CatalogExportEntry entry)
			=> entry?.SourceUrls != null && entry.SourceUrls.Count > 0 ? entry.SourceUrls[0] : string.Empty;

		private string SelectedSourceUrl()
			=> FirstSource(_selectedRemoteEntry);

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
			_networkStatusLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
			_tabHeaderBox.AddChild(_networkStatusLabel);
		}

		private void OnRefreshNetwork()
		{
			_networkStatusLabel.Text = TranslationServer.Translate("mods/network_loading");
			RefreshNetworkMods();
		}

		private async void RefreshNetworkMods()
		{
			_networkRefreshCts?.Cancel();
			var cts = new CancellationTokenSource();
			_networkRefreshCts = cts;

			try
			{
				await CatalogService.RefreshAsync(forceRefresh: true, cts.Token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch
			{
				if (GodotObject.IsInstanceValid(_networkStatusLabel))
					_networkStatusLabel.Text = TranslationServer.Translate("mods/network_error");
				return;
			}
			finally
			{
				if (_networkRefreshCts == cts)
					_networkRefreshCts = null;
				cts.Dispose();
			}

			if (!GodotObject.IsInstanceValid(this))
				return;

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
				_remoteEntries[RemoteKey(remoteEntry)] = entry;
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
				_installBtn.AddThemeColorOverride("font_color", UiTokens.Warning);
			}
			else if (cmp < 0)
			{
				_installBtn.Text = TranslationServer.Translate("mods/downgrade_button");
				_installBtn.Disabled = false;
				_installBtn.Visible = true;
				_installBtn.AddThemeColorOverride("font_color", UiTokens.Info);
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
					_installBtn.AddThemeColorOverride("font_color", UiTokens.Danger);
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

			await ModDownloader.InstallAsync(modId, version, SelectedSourceUrl());
		}

		private static Color GetStatusColor(RemoteModStatus status)
		{
			return status switch
			{
				RemoteModStatus.Installed => UiTokens.Success,
				RemoteModStatus.UpdateAvailable => UiTokens.Warning,
				RemoteModStatus.NotInstalled => UiTokens.Muted,
				RemoteModStatus.ModifiedLocally => UiTokens.Danger,
				_ => UiTokens.Muted,
			};
		}

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
					Color = UiTokens.Alpha(UiTokens.Accent, 0f),
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
				orderLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
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
				_versionLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
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
				_selectionBg.Color = selected ? UiTokens.AccentTrace : UiTokens.Alpha(UiTokens.Accent, 0f);
			}
		}
	}
}
