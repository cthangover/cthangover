using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Mods.ModManager
{
    /// <summary>
    /// Level 1 of the collections tab: the gallery of ready-to-play games from
    /// the hub mockup. One card per <see cref="ModCollectionKind.Game"/>
    /// collection, built exclusively from
    /// <see cref="ModCollectionGallery.Build"/> over the live
    /// <see cref="ModCollectionStore.LoadAll"/> merge — there are no hardcoded
    /// collection ids, so any mod, catalog or player document appears here
    /// automatically. Every card shows the source badge from
    /// <see cref="ModCollection.Origin"/>, status badges derived from the
    /// summary (draft, missing, problems, enable, ready), the Installed/Total
    /// progress bar and its action buttons; the plan is applied through
    /// <see cref="ModCollectionInstaller"/> exactly like the scheme editor does.
    /// <para/>
    /// The whole layout is code-built (no .tscn) following the UiKit helper
    /// style; a dashed "build your own game" card and the empty state with a
    /// hint finish the screen.
    /// </summary>
    public partial class CollectionsGallery : ScrollContainer
    {
        /// <summary>Requested to open the scheme editor for the collection id.</summary>
        public event Action<string> EditSchemeRequested;

        /// <summary>Requested to create a fresh player game in the editor.</summary>
        public event Action NewGameRequested;

        /// <summary>Progress/status line for the hosting hub banner; null hides it.</summary>
        public event Action<string> StatusMessage;

        private VBoxContainer _host;
        private HFlowContainer _grid;
        private VBoxContainer _emptyBox;
        private Label _emptyHint;

        private readonly Dictionary<string, ModCollection> _index = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModCollectionPlan> _plans = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ModCollectionGameSummary> _summaries = new();
        private HashSet<string> _sessionInstalled = new(StringComparer.OrdinalIgnoreCase);
        private string _query = string.Empty;
        private string _installingId;

        public CollectionsGallery()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            HorizontalScrollMode = ScrollMode.Disabled;
            VerticalScrollMode = ScrollMode.Auto;

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", UiTokens.Space8);
            margin.AddThemeConstantOverride("margin_top", UiTokens.Space6);
            margin.AddThemeConstantOverride("margin_right", UiTokens.Space8);
            margin.AddThemeConstantOverride("margin_bottom", UiTokens.Space7);
            margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(margin);

            _host = UiKit.Column(UiTokens.Space5);
            _host.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            margin.AddChild(_host);

            BuildCrumbs();
            BuildHead();
            BuildGrid();
            BuildEmptyState();

            ModDownloader.OnProgress += OnInstallProgress;
            CatalogService.OnRefreshed += OnCatalogRefreshed;
        }

        /// <summary>Unsubscribes the static events; the gallery is freed together with the hub.</summary>
        public override void _ExitTree()
        {
            ModDownloader.OnProgress -= OnInstallProgress;
            CatalogService.OnRefreshed -= OnCatalogRefreshed;
        }

        /// <summary>Shares the hub-wide set of mods installed during this session.</summary>
        public void SetSessionInstalled(HashSet<string> sessionInstalled)
            => _sessionInstalled = sessionInstalled ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Applies the hub header search query; empty text shows everything.</summary>
        public void ApplyFilter(string query)
        {
            _query = query ?? string.Empty;
            RenderGrid();
        }

        /// <summary>
        /// Reloads the merged collection store, rebuilds the summaries through
        /// the core gallery and the per-game apply plans, then re-renders.
        /// Called on open, after a catalog refresh and after applying a plan.
        /// </summary>
        public void Refresh()
        {
            _index.Clear();
            foreach (var collection in ModCollectionStore.LoadAll())
            {
                if (!string.IsNullOrEmpty(collection?.Id))
                    _index[collection.Id] = collection;
            }

            var catalog = CatalogService.CachedEntries ?? new List<CatalogExportEntry>();
            var summaries = ModCollectionGallery.Build(
                _index,
                ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(),
                catalog,
                _sessionInstalled,
                CollectionText.Resolve,
                TranslationServer.GetLocale());

            _summaries.Clear();
            _plans.Clear();
            foreach (var summary in summaries)
            {
                _summaries.Add(summary);
                _plans[summary.Collection.Id] = BuildPlan(summary, catalog);
            }

            RenderGrid();
        }

        /* ═══════════════ Static skeleton ═══════════════ */

        private void BuildCrumbs()
        {
            var row = UiKit.Row(UiTokens.Space2);
            row.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/hub_tab_collections"), UiTokens.FontBodySmall, UiTokens.Faint));
            row.AddChild(UiKit.Label("›", UiTokens.FontBodySmall, UiTokens.Faint));
            row.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/gallery_crumb"), UiTokens.FontBodySmall, UiTokens.Body));
            row.AddChild(UiKit.Label("·", UiTokens.FontBodySmall, UiTokens.Faint));
            row.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/gallery_hint"), UiTokens.FontBodySmall, UiTokens.Faint));
            _host.AddChild(row);
        }

        private void BuildHead()
        {
            var row = UiKit.Row(UiTokens.Space3);
            row.AddChild(UiKit.SectionTitle(TranslationServer.Translate("mod_manager/gallery_title"), UiTokens.FontLead));
            row.AddChild(UiKit.Spacer());

            var newGame = UiKit.Button(TranslationServer.Translate("mod_manager/gallery_new_game"), ButtonKind.Primary);
            newGame.Pressed += () => NewGameRequested?.Invoke();
            row.AddChild(newGame);
            _host.AddChild(row);
        }

        private void BuildGrid()
        {
            _grid = new HFlowContainer();
            _grid.AddThemeConstantOverride("h_separation", UiTokens.Space5);
            _grid.AddThemeConstantOverride("v_separation", UiTokens.Space5);
            _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _grid.SizeFlagsVertical = SizeFlags.ExpandFill;
            _host.AddChild(_grid);
        }

        private void BuildEmptyState()
        {
            _emptyBox = UiKit.Column(UiTokens.Space2);
            _emptyBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _emptyBox.SizeFlagsVertical = SizeFlags.ExpandFill;
            _emptyBox.Alignment = BoxContainer.AlignmentMode.Center;
            _emptyBox.Visible = false;

            var title = UiKit.EmptyState(TranslationServer.Translate("mod_manager/collections_empty"), UiTokens.FontLead);
            _emptyHint = UiKit.Label(TranslationServer.Translate("mod_manager/gallery_empty_hint"), UiTokens.FontBodySmall, UiTokens.Faint);
            _emptyHint.HorizontalAlignment = HorizontalAlignment.Center;
            _emptyHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;

            _emptyBox.AddChild(title);
            _emptyBox.AddChild(_emptyHint);
            _host.AddChild(_emptyBox);
        }

        /* ═══════════════ Cards ═══════════════ */

        private void RenderGrid()
        {
            ClearChildren(_grid);

            var items = Filtered();
            _emptyBox.Visible = items.Count == 0;
            _grid.Visible = items.Count > 0;

            foreach (var summary in items)
                _grid.AddChild(BuildCard(summary));
        }

        private List<ModCollectionGameSummary> Filtered()
        {
            var query = _query.Trim();
            if (query.Length == 0)
                return _summaries;

            return _summaries
                .Where(s => (s.Name ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                            || (s.Description ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                            || (s.Collection?.Id ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                            || MatchesTags(s.Collection?.Tags, query))
                .ToList();
        }

        private static bool MatchesTags(List<string> tags, string query)
        {
            if (tags == null)
                return false;

            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(tag) && tag.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private Control BuildCard(ModCollectionGameSummary summary)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(360, 0) };
            card.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusLg, UiTokens.Surface2, UiTokens.Line));

            var column = UiKit.Column(UiTokens.Space3);
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.AddChild(column);

            var top = UiKit.Row(UiTokens.Space3);
            top.AddChild(BuildCover(summary.Name ?? summary.Collection.Id));
            var headColumn = UiKit.Column(2);
            headColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            headColumn.AddChild(UiKit.Label(summary.Name ?? summary.Collection.Id, UiTokens.FontBodyLarge, UiTokens.InkBright));
            headColumn.AddChild(UiKit.Label(BuildSubtitle(summary), UiTokens.FontTiny, UiTokens.Faint));
            top.AddChild(headColumn);
            column.AddChild(top);

            var description = UiKit.Label(
                string.IsNullOrEmpty(summary.Description) ? "—" : summary.Description,
                UiTokens.FontBodySmall, UiTokens.Muted);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(description);

            var badges = UiKit.Row(UiTokens.Space1);
            foreach (var (text, color) in BuildStatusBadges(summary))
                badges.AddChild(UiKit.Badge(text, color));
            column.AddChild(badges);

            if (summary.TotalMods > 0)
            {
                var progress = new ProgressBar
                {
                    MaxValue = 100,
                    Value = 100f * summary.InstalledCount / summary.TotalMods,
                    ShowPercentage = false,
                    CustomMinimumSize = new Vector2(0, 6),
                };
                progress.AddThemeStyleboxOverride("background", UiKit.Frame(UiTokens.RadiusXs, UiTokens.Surface1, UiTokens.LineSoft));
                progress.AddThemeStyleboxOverride("fill", UiKit.Frame(UiTokens.RadiusXs, UiTokens.Accent, null));
                column.AddChild(progress);
            }

            var foot = UiKit.Row(UiTokens.Space2);
            foot.AddChild(BuildPrimaryButton(summary));
            foot.AddChild(BuildEditButton(summary));
            if (CollectionDeletion.CanDelete(summary.Collection))
                foot.AddChild(BuildDeleteButton(summary));
            foot.AddChild(UiKit.Spacer());
            foot.AddChild(UiKit.Label(
                string.Format(TranslationServer.Translate("mod_manager/collection_result"), summary.InstalledCount, summary.TotalMods),
                UiTokens.FontSmall, UiTokens.Faint));
            column.AddChild(foot);

            return card;
        }

        /// <summary>Cover box with the first grapheme of the game name (mockup's emoji slot).</summary>
        private static Control BuildCover(string title)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(64, 64) };
            panel.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface1, UiTokens.Line));

            var letter = "·";
            var text = title?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                // First grapheme cluster so surrogate-pair emoji covers survive.
                var info = new StringInfo(text);
                if (info.LengthInTextElements > 0)
                    letter = info.SubstringByTextElements(0, 1).ToUpperInvariant();
            }

            var label = UiKit.Label(letter, UiTokens.FontH3, UiTokens.AccentBright);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            panel.AddChild(label);
            return panel;
        }

        private static string BuildSubtitle(ModCollectionGameSummary summary)
        {
            var collection = summary.Collection;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(collection.Author))
                parts.Add(collection.Author);
            if (!string.IsNullOrEmpty(collection.Version))
                parts.Add("v" + collection.Version);
            parts.Add(TranslationServer.Translate(OriginKey(collection.Origin)));
            parts.Add($"{summary.TotalMods} {TranslationServer.Translate("mod_manager/collection_mods")}");
            return string.Join(" · ", parts);
        }

        /// <summary>Source badge colours per <see cref="ModCollection.Origin"/> (blue built-in/catalog, violet player).</summary>
        private static string OriginKey(ModCollectionOrigin origin) => origin switch
        {
            ModCollectionOrigin.User => "mod_manager/collection_origin_user",
            ModCollectionOrigin.Catalog => "mod_manager/collection_origin_catalog",
            _ => "mod_manager/collection_origin_builtin",
        };

        /// <summary>
        /// Status badges exactly per the hub rules: draft for empty games;
        /// red/yellow "missing N" plus "problems: K" for unplayable ones;
        /// amber "enable N" when only disabled mods hold the game back; green
        /// "ready to play" otherwise. A disabled mod alone never breaks
        /// <see cref="ModCollectionGameSummary.IsReady"/>, so the badges follow
        /// the summary, not raw node states.
        /// </summary>
        private static List<(string Text, Color Color)> BuildStatusBadges(ModCollectionGameSummary summary)
        {
            var badges = new List<(string, Color)>();
            Func<string, string> translate = key => TranslationServer.Translate(key);

            if (summary.TotalMods == 0)
            {
                badges.Add((translate("mod_manager/gallery_draft"), UiTokens.Warning));
                return badges;
            }

            if (!summary.IsReady)
            {
                var gaps = summary.MissingIds.Count + summary.MismatchIds.Count;
                if (gaps > 0)
                {
                    var color = summary.MissingIds.Count > 0 ? UiTokens.DangerText : UiTokens.Warning;
                    badges.Add((string.Format(translate("mod_manager/gallery_missing_n"), gaps), color));
                }

                if (summary.ProblemsCount > 0)
                    badges.Add((string.Format(translate("mod_manager/gallery_problems_n"), summary.ProblemsCount), UiTokens.DangerText));

                return badges;
            }

            if (summary.PlanInstallCount == 0 && summary.PlanEnableCount > 0)
                badges.Add((string.Format(translate("mod_manager/gallery_enable_badge_n"), summary.PlanEnableCount), UiTokens.Warning));
            else
                badges.Add((translate("mod_manager/gallery_ready"), UiTokens.SuccessText));

            return badges;
        }

        /// <summary>The card's primary action: install the plan, enable the disabled mods, continue a draft or rest on a ready game.</summary>
        private Button BuildPrimaryButton(ModCollectionGameSummary summary)
        {
            Func<string, string> translate = key => TranslationServer.Translate(key);
            Button button;
            Action action;

            if (summary.PlanInstallCount > 0)
            {
                button = UiKit.Button(string.Format(translate("mod_manager/gallery_install_btn_n"), summary.PlanInstallCount), ButtonKind.Primary);
                action = () => ApplyPlan(summary.Collection.Id);
                button.Disabled = summary.MissingIds.Count > 0 || _installingId != null;
            }
            else if (summary.PlanEnableCount > 0)
            {
                button = UiKit.Button(string.Format(translate("mod_manager/gallery_enable_btn_n"), summary.PlanEnableCount), ButtonKind.Primary);
                action = () => ApplyPlan(summary.Collection.Id);
                button.Disabled = _installingId != null;
            }
            else if (summary.TotalMods == 0)
            {
                button = UiKit.Button(translate("mod_manager/gallery_continue"));
                action = () => EditSchemeRequested?.Invoke(summary.Collection.Id);
                button.Disabled = _installingId != null;
            }
            else
            {
                button = UiKit.Button(translate("mod_manager/gallery_ready_done"));
                action = null;
                button.Disabled = true;
            }

            if (action != null)
                button.Pressed += action;
            return button;
        }

        private Button BuildEditButton(ModCollectionGameSummary summary)
        {
            var button = UiKit.Button(TranslationServer.Translate("mod_manager/gallery_edit_scheme"));
            button.Disabled = _installingId != null;
            button.Pressed += () => EditSchemeRequested?.Invoke(summary.Collection.Id);
            return button;
        }

        private Button BuildDeleteButton(ModCollectionGameSummary summary)
        {
            var button = UiKit.Button(TranslationServer.Translate("mod_manager/collection_delete"), ButtonKind.Danger);
            button.Disabled = _installingId != null;
            button.Pressed += () => CollectionDeletion.Confirm(this, summary.Collection, () =>
            {
                StatusMessage?.Invoke(TranslationServer.Translate("mod_manager/collection_delete_done"));
                Refresh();
            });
            return button;
        }

        /* ═══════════════ Apply ═══════════════ */

        private ModCollectionPlan BuildPlan(ModCollectionGameSummary summary, List<CatalogExportEntry> catalog)
        {
            var graph = ModCollectionResolver.Resolve(
                summary.Collection,
                _index,
                ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(),
                catalog,
                _sessionInstalled,
                TranslationServer.GetLocale(),
                CollectionText.Resolve);
            return ModCollectionPlan.Build(graph, catalog);
        }

        /// <summary>
        /// Applies the plan of one card through the shared runner — the same
        /// install-then-enable routine the scheme editor performs — and
        /// refreshes the gallery so badges and buttons reflect the new state.
        /// </summary>
        private async void ApplyPlan(string collectionId)
        {
            if (_installingId != null
                || !_plans.TryGetValue(collectionId, out var plan)
                || plan.IsEmpty
                || plan.Missing.Count > 0)
            {
                return;
            }

            var summary = _summaries.FirstOrDefault(s => string.Equals(s.Collection?.Id, collectionId, StringComparison.OrdinalIgnoreCase));
            var title = summary?.Name ?? collectionId;

            _installingId = collectionId;
            StatusMessage?.Invoke(string.Format(TranslationServer.Translate("mod_manager/collection_progress"), title));
            RenderGrid();

            bool success;
            try
            {
                success = await ModCollectionInstaller.ApplyAsync(plan, _sessionInstalled);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_HUB", $"Gallery apply '{collectionId}' failed: {ex.Message}", LogLevel.Error);
                success = false;
            }

            _installingId = null;

            if (!GodotObject.IsInstanceValid(this))
                return;

            StatusMessage?.Invoke(TranslationServer.Translate(success
                ? "mod_manager/restart_required"
                : "mod_manager/collection_apply_failed"));
            Refresh();
        }

        private void OnInstallProgress(InstallProgress progress)
        {
            if (_installingId == null || progress == null)
                return;

            StatusMessage?.Invoke(progress.Message);
        }

        private void OnCatalogRefreshed()
        {
            if (GodotObject.IsInstanceValid(this))
                Refresh();
        }

        private static void ClearChildren(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                node.RemoveChild(child);
                child.QueueFree();
            }
        }
    }
}
