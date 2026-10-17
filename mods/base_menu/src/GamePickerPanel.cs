using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Interface.Menu;
using Cthangover.Mods.Interface.Theming;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// "Play": every game collection as a card — cover, name, description,
    /// readiness (what is missing and what can be installed), saves and the
    /// last play date — with New game / Continue / Saves / Install / Configure.
    /// Data comes from <see cref="GameCatalog"/> against all installed mods, so
    /// it works from the front-end where only the menu is loaded.
    /// </summary>
    public partial class GamePickerPanel : Control
    {
        private VBoxContainer _list;
        private readonly HashSet<string> _sessionInstalled = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised when the player opens a game's saves; the menu shows its save browser.</summary>
        public event Action<string> SavesRequested;

        public event Action Closed;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;

            var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.65f) };
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(backdrop);

            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusLg, UiTokens.Surface1, UiTokens.Line));
            panel.AnchorLeft = panel.AnchorRight = panel.AnchorTop = panel.AnchorBottom = 0.5f;
            panel.OffsetLeft = -460;
            panel.OffsetRight = 460;
            panel.OffsetTop = -310;
            panel.OffsetBottom = 310;
            panel.GrowHorizontal = GrowDirection.Both;
            panel.GrowVertical = GrowDirection.Both;
            AddChild(panel);

            var margin = new MarginContainer();
            foreach (var side in new[] { "left", "right", "top", "bottom" })
                margin.AddThemeConstantOverride($"margin_{side}", UiTokens.Space6);
            panel.AddChild(margin);

            var column = UiKit.Column(UiTokens.Space4);
            column.MouseFilter = MouseFilterEnum.Pass;
            margin.AddChild(column);

            column.AddChild(UiKit.Label(TranslationServer.Translate("menu/play/title"), UiTokens.FontH2, UiTokens.InkBright));

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            column.AddChild(scroll);

            _list = UiKit.Column(UiTokens.Space3);
            _list.MouseFilter = MouseFilterEnum.Pass;
            _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_list);

            var back = UiKit.Button(TranslationServer.Translate("settings/back"));
            back.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            back.Pressed += Close;
            column.AddChild(back);

            Visible = false;
        }

        public void Open()
        {
            Visible = true;
            Rebuild();
        }

        private void Close()
        {
            Visible = false;
            Closed?.Invoke();
        }

        private void Rebuild()
        {
            foreach (var child in _list.GetChildren())
                child.QueueFree();

            var games = GameCatalog.Games(_sessionInstalled);
            if (games.Count == 0)
            {
                _list.AddChild(UiKit.Label(TranslationServer.Translate("menu/play/no_games"), UiTokens.FontBody, UiTokens.Muted));
                return;
            }

            foreach (var game in games)
                _list.AddChild(BuildCard(game));
        }

        private Control BuildCard(ModCollectionGameSummary game)
        {
            var collection = game.Collection;
            var saves = SaveService.ListSaves(collection.Id);
            var newest = saves.OrderByDescending(s => s.SaveTime).FirstOrDefault();

            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2,
                ActiveGame.Id == collection.Id ? UiTokens.Accent : UiTokens.LineCard));

            var row = UiKit.Row(UiTokens.Space4);
            row.MouseFilter = MouseFilterEnum.Pass;
            card.AddChild(row);

            var cover = GameCatalog.LoadCover(collection);
            row.AddChild(new TextureRect
            {
                Texture = cover,
                CustomMinimumSize = new Vector2(176, 99),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            });

            var info = UiKit.Column(UiTokens.Space1);
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(info);
            info.AddChild(UiKit.Label(game.Name ?? collection.Id, UiTokens.FontH3, UiTokens.InkBright));
            var description = UiKit.Label(game.Description ?? string.Empty, UiTokens.FontBodySmall, UiTokens.Body);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            info.AddChild(description);

            var (statusText, statusColor) = Status(game);
            info.AddChild(UiKit.Label(statusText, UiTokens.FontSmall, statusColor));
            info.AddChild(UiKit.Label(saves.Count == 0
                    ? TranslationServer.Translate("menu/play/no_saves")
                    : string.Format(TranslationServer.Translate("menu/play/saves"), saves.Count, newest.SaveTime.ToLocalTime().ToString("dd.MM.yy HH:mm")),
                UiTokens.FontTiny, UiTokens.Muted));

            var buttons = UiKit.Column(UiTokens.Space2);
            buttons.MouseFilter = MouseFilterEnum.Pass;
            buttons.CustomMinimumSize = new Vector2(190, 0);
            row.AddChild(buttons);

            var start = UiKit.Button(TranslationServer.Translate("menu/play/new_game"), ButtonKind.Primary);
            start.Disabled = !game.IsReady;
            start.Pressed += () => GameFlow.StartGame(this, collection.Id);
            buttons.AddChild(start);

            if (newest != null)
            {
                var resume = UiKit.Button(TranslationServer.Translate("menu/play/continue"));
                resume.Disabled = !game.IsReady;
                resume.Pressed += () => GameFlow.RequestLoad(this, collection.Id, newest.FileName);
                buttons.AddChild(resume);

                var browse = UiKit.Button(TranslationServer.Translate("menu/play/browse_saves"));
                browse.Pressed += () => SavesRequested?.Invoke(collection.Id);
                buttons.AddChild(browse);
            }

            if (!game.IsReady && game.MissingIds.Count == 0 && game.MismatchIds.Count == 0
                && game.ProblemsCount == 0 && game.PlanInstallCount + game.PlanEnableCount > 0)
            {
                var install = UiKit.Button(TranslationServer.Translate("menu/play/install"));
                install.Pressed += () => Install(collection, install);
                buttons.AddChild(install);
            }

            if (MenuRegistry.GetButtonById(ModsHubId) != null)
            {
                var configure = UiKit.Button(TranslationServer.Translate("menu/play/configure"));
                configure.Pressed += () => OpenEditor(collection.Id);
                buttons.AddChild(configure);
            }

            return card;
        }

        private static (string Text, Color Color) Status(ModCollectionGameSummary game)
        {
            if (game.IsReady)
                return (TranslationServer.Translate("menu/play/status_ready"), UiTokens.SuccessText);
            if (game.MissingIds.Count > 0)
                return (string.Format(TranslationServer.Translate("menu/play/status_missing"), string.Join(", ", game.MissingIds)), UiTokens.DangerText);
            if (game.MismatchIds.Count > 0)
                return (string.Format(TranslationServer.Translate("menu/play/status_mismatch"), string.Join(", ", game.MismatchIds)), UiTokens.DangerText);
            if (game.ProblemsCount > 0)
                return (string.Format(TranslationServer.Translate("menu/play/status_problems"), game.ProblemsCount), UiTokens.Warning);
            return (string.Format(TranslationServer.Translate("menu/play/status_install"), game.PlanInstallCount, game.PlanEnableCount), UiTokens.Warning);
        }

        private const string ModsHubId = "mods_hub";

        /// <summary>Installs/enables the missing mods; entering the game afterwards reloads the mod set to pick them up.</summary>
        private async void Install(ModCollection collection, Button button)
        {
            button.Disabled = true;
            button.Text = TranslationServer.Translate("menu/play/installing");

            var collections = GameCatalog.LoadCollections();
            var catalog = CatalogService.CachedEntries ?? new List<CatalogExportEntry>();
            var graph = ModCollectionResolver.Resolve(collection, collections, ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(), catalog, _sessionInstalled);
            var ok = await ModCollectionInstaller.ApplyAsync(ModCollectionPlan.Build(graph, catalog), _sessionInstalled);
            if (!IsInstanceValid(this))
                return;

            if (ok)
                GameFlow.ReloadRequired = true;
            GameFlow.Confirm(this,
                TranslationServer.Translate("menu/play/install"),
                TranslationServer.Translate(ok ? "menu/play/install_done" : "menu/play/install_failed"),
                null, null);
            Rebuild();
        }

        /// <summary>Opens the mod manager on the game's scheme (loose coupling through the menu registry).</summary>
        private void OpenEditor(string collectionId)
        {
            var entry = MenuRegistry.GetButtonById(ModsHubId);
            var host = GetParent() as Control ?? this;
            var hub = MenuBuilder.OpenScene(host, entry?.ScenePath, ModsHubId);
            if (hub != null && hub.HasMethod("OpenCollectionEditor"))
                hub.CallDeferred("OpenCollectionEditor", collectionId);
            Close();
        }
    }
}
