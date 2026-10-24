using System;
using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// One mounted map: the drawn layers, the actors and the player, plus the
    /// rules of walking around. Built by <see cref="RpgWorldHost"/> when a map
    /// scene is entered and freed when the scene is left.
    /// <para/>
    /// Draw order (children, bottom to top): back tiles, main tiles, the actor
    /// layer (Y-sorted: events and the player), front tiles; each tile layer draws its
    /// autotile cells over its plain tiles (<see cref="RpgAutotileLayer"/>). The whole node is
    /// scaled by the map's zoom with nearest filtering and follows the player
    /// like a camera, clamped to the map edges; a map smaller than the screen is
    /// centred.
    /// <para/>
    /// Each frame without a blocker (<see cref="RpgInput.IsWalkBlocked"/>) a held
    /// direction turns the player and, if the cell is free, starts a step. When a
    /// step ends the position is stored in <see cref="RpgState"/>, then a touch
    /// event on the cell fires, else the encounter table rolls. The action key
    /// fires the action event in front of the player (or under them); during a
    /// dialog it advances the dialog instead.
    /// <para/>
    /// Layers and actors share the material of <see cref="RpgLighting"/> (time of day, the
    /// player's glow, lit events), updated after the camera each frame.
    /// </summary>
    public partial class RpgWorldView : Node2D
    {
        private readonly Dictionary<string, RpgActor> eventActors = new();
        private readonly RpgTextures textures = new();
        private readonly Random random = new();
        private RpgMapGrid grid;
        private Node2D actorLayer;
        private Texture2D atlas;
        private RpgLighting lighting;

        /// <summary>The mounted map.</summary>
        public RpgMap Map { get; private set; }

        /// <summary>The map's tileset (null when it could not be resolved: the map is drawn empty).</summary>
        public RpgTileset Tileset { get; private set; }

        /// <summary>The player's actor.</summary>
        public RpgActor Player { get; private set; }

        /// <summary>Pixel edge of a tile before zoom.</summary>
        public int TileSize => Tileset?.TileSize ?? 32;

        /// <summary>Builds the layers and actors of <paramref name="map"/>; the player is placed by <see cref="PlacePlayer"/>.</summary>
        public void Build(RpgMap map)
        {
            Map = map;
            Name = "World";
            TextureFilter = TextureFilterEnum.Nearest;
            Scale = Vector2.One * Math.Max(1, map.Zoom);
            grid = RpgMapGrid.Parse(map);

            Tileset = RpgTilesetFactory.Instance.Get(map.Tileset);
            if (Tileset == null)
                GameLogger.Log("RPG", $"map '{map.ID}': tileset '{map.Tileset}' not found", LogLevel.Error);
            atlas = Tileset != null ? textures.Tileset(Tileset.Image) : null;
            var tileSet = Tileset != null && atlas != null ? RpgTileSetBuilder.Build(Tileset, atlas) : null;
            lighting = new RpgLighting(map, grid, Tileset);

            AddChild(BuildLayer(RpgLayer.Back, tileSet));
            AddChild(BuildLayer(RpgLayer.Main, tileSet));
            actorLayer = new Node2D { Name = "Actors", YSortEnabled = true };
            AddChild(actorLayer);
            AddChild(BuildLayer(RpgLayer.Front, tileSet));

            foreach (var mapEvent in RpgMapEvents.Of(map.ID))
                AddEventActor(mapEvent);
            RefreshEvents();

            Player = new RpgActor(TileSize) { Name = "Player", Material = lighting.Material };
            Player.UseCharset(textures.Charset(RpgPlayerLook.Charset()));
            Player.StepFinished += OnPlayerStepFinished;
            actorLayer.AddChild(Player);
        }

        /// <summary>Puts the player on a cell and stores the position.</summary>
        public void PlacePlayer(int x, int y, RpgDirection facing)
        {
            Player.Place(new Vector2I(x, y), facing);
            RpgState.Instance.Place(Map.ID, x, y, facing);
            UpdateCamera();
        }

        /// <summary>
        /// Re-evaluates every event's condition: hidden events stop drawing,
        /// blocking and reacting. Called on build and whenever game state that a
        /// condition may read changes.
        /// </summary>
        public void RefreshEvents()
        {
            foreach (var actor in eventActors.Values)
            {
                var index = RpgEventPages.ActiveIndex(actor.Event, ScenarioCondition.Evaluate);
                actor.Visible = index >= 0;
                if (index < 0 || index == actor.PageIndex)
                    continue;
                var first = actor.PageIndex == -2;
                actor.PageIndex = index;
                actor.Current = RpgEventPages.Effective(actor.Event, index);
                actor.RouteIndex = 0;
                actor.RouteTimer = actor.Current.Route?.Every ?? 0;
                ApplyLook(actor);
                if (first)
                    actor.Place(new Vector2I(actor.Event.X, actor.Event.Y), actor.Current.Dir);
                else if (index > 0 && actor.Event.Pages[index - 1].Dir.HasValue)
                    actor.Face(actor.Current.Dir);
            }
        }

        /// <summary>True when the cell lies on the map (a saved position may be stale after a map edit).</summary>
        public bool Contains(int x, int y) => grid.InBounds(x, y);

        /// <summary>True when the player could step into the cell now.</summary>
        public bool CanEnter(Vector2I cell)
        {
            if (!RpgPassability.IsWalkable(grid, Tileset, cell.X, cell.Y))
                return false;
            foreach (var actor in eventActors.Values)
            {
                if (actor.Visible && actor.Current?.IsSolid == true && actor.Cell == cell)
                    return false;
            }
            return true;
        }

        /// <summary>The actor of the event with this id (visible or not), or null.</summary>
        public RpgActor FindEvent(string id) => id != null && eventActors.TryGetValue(id, out var actor) ? actor : null;

        /// <summary>The visible event on a cell with the given trigger, or null.</summary>
        public RpgActor EventAt(Vector2I cell, RpgEventTrigger trigger)
        {
            foreach (var actor in eventActors.Values)
            {
                if (actor.Visible && actor.Cell == cell && actor.Current?.Trigger == trigger)
                    return actor;
            }
            return null;
        }

        /// <summary>
        /// Fires the action event the player faces, or the one under them
        /// (a sign on the floor). An NPC turns to the player first.
        /// </summary>
        public bool Interact()
        {
            if (Player == null || Player.IsMoving)
                return false;

            var target = EventAt(Player.Cell + RpgDirections.Delta(Player.Facing), RpgEventTrigger.Action)
                ?? EventAt(Player.Cell, RpgEventTrigger.Action);
            if (target == null)
                return false;

            if (target.Cell != Player.Cell)
                target.Face(RpgDirections.Opposite(Player.Facing));
            return RpgEventRunner.Run(target.Current, Map.ID);
        }

        /// <summary>Walking: one decision per frame while the player stands still.</summary>
        public override void _Process(double delta)
        {
            var blocked = RpgInput.IsWalkBlocked(this);
            if (!blocked)
                WalkRoutes(delta);
            if (Player != null && !Player.IsMoving && !blocked)
            {
                var held = RpgInput.HeldDirection();
                if (held.HasValue)
                {
                    var next = Player.Cell + RpgDirections.Delta(held.Value);
                    if (CanEnter(next))
                        Player.StepTo(held.Value);
                    else
                        Player.Face(held.Value);
                }
            }
            UpdateCamera();
            lighting.Update(this, eventActors.Values);
        }

        /// <summary>The action key: advances a running dialog, otherwise interacts.</summary>
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!RpgInput.IsActionPress(@event))
                return;

            var dialogBox = RpgInput.FindDialogBox();
            if (dialogBox?.IsRunning == true)
            {
                if (!dialogBox.IsWaitingAnswer)
                    dialogBox.NextAction();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!RpgInput.IsWalkBlocked(this) && Interact())
                GetViewport().SetInputAsHandled();
        }

        private void OnPlayerStepFinished(RpgActor player)
        {
            RpgState.Instance.Place(Map.ID, player.Cell.X, player.Cell.Y, player.Facing);

            var touch = EventAt(player.Cell, RpgEventTrigger.Touch);
            if (touch != null)
            {
                RpgEventRunner.Run(touch.Current, Map.ID);
                return;
            }

            var enemies = RpgEncounterRoller.Roll(Map.Encounters, random);
            if (enemies != null)
                RpgBattleService.Start(enemies, null);
        }

        private TileMapLayer BuildLayer(RpgLayer layer, TileSet tileSet)
        {
            var node = new TileMapLayer { Name = layer.ToString(), TileSet = tileSet, Material = lighting.Material };
            if (tileSet == null)
                return node;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetTile(layer, x, y);
                    if (index >= 0 && Tileset.AutotileOf(index) == null)
                        node.SetCell(new Vector2I(x, y), RpgTileSetBuilder.SourceId, RpgTileSetBuilder.AtlasCoords(Tileset, index));
                }
            }
            var autotiles = new RpgAutotileLayer(grid, layer, Tileset, textures);
            if (autotiles.Count > 0)
                node.AddChild(autotiles);
            else
                autotiles.Free();
            return node;
        }

        private void AddEventActor(RpgMapEvent mapEvent)
        {
            if (string.IsNullOrEmpty(mapEvent.Id) || eventActors.ContainsKey(mapEvent.Id))
            {
                GameLogger.Log("RPG", $"map '{Map.ID}': event without a unique id at {mapEvent.X},{mapEvent.Y} skipped", LogLevel.Error);
                return;
            }

            // The look and the cell come with the first page (RefreshEvents).
            var actor = new RpgActor(TileSize, mapEvent) { Name = "Event_" + mapEvent.Id, Material = lighting.Material };
            actorLayer.AddChild(actor);
            eventActors[mapEvent.Id] = actor;
        }

        /// <summary>Shows the active page's charset, tile or icon (nothing for an invisible trigger).</summary>
        private void ApplyLook(RpgActor actor)
        {
            var page = actor.Current;
            actor.ClearLook();
            if (!string.IsNullOrEmpty(page.Sprite))
                actor.UseCharset(textures.Charset(page.Sprite));
            else if (page.Tile >= 0 && Tileset != null)
                actor.UseTile(atlas, RpgTileSetBuilder.AtlasCoords(Tileset, page.Tile));
            else if (!string.IsNullOrEmpty(page.Icon))
                actor.UseIcon(textures.Icon(page.Icon));
        }

        /// <summary>
        /// Advances the routes of the shown events (<see cref="RpgMoveRoute"/>): one step per
        /// <c>Every</c> seconds; a walk into a blocked cell or onto the player only turns.
        /// </summary>
        private void WalkRoutes(double delta)
        {
            foreach (var actor in eventActors.Values)
            {
                var route = actor.Current?.Route;
                if (!actor.Visible || route?.Steps == null || route.Steps.Count == 0 || actor.IsMoving)
                    continue;
                actor.RouteTimer -= delta;
                if (actor.RouteTimer > 0)
                    continue;
                actor.RouteTimer = Math.Max(0.1, route.Every);
                if (actor.RouteIndex >= route.Steps.Count)
                {
                    if (!route.Repeat)
                        continue;
                    actor.RouteIndex = 0;
                }

                var step = RpgRouteStep.Resolve(route.Steps[actor.RouteIndex++], actor.Cell, Player?.Cell ?? actor.Cell, random);
                if (!step.Direction.HasValue)
                    continue;
                var next = actor.Cell + RpgDirections.Delta(step.Direction.Value);
                if (step.Moves && CanEnter(next) && next != Player?.Cell)
                    actor.StepTo(step.Direction.Value);
                else
                    actor.Face(step.Direction.Value);
            }
        }

        private void UpdateCamera()
        {
            if (Player == null || GetParent() is not Control host)
                return;

            var zoom = Scale.X;
            var mapSize = new Vector2(grid.Width, grid.Height) * TileSize * zoom;
            var screen = host.Size;
            var focus = (Player.Position + new Vector2(TileSize, TileSize) / 2f) * zoom;
            Position = new Vector2(Axis(screen.X, mapSize.X, focus.X), Axis(screen.Y, mapSize.Y, focus.Y));
        }

        private static float Axis(float screen, float map, float focus)
        {
            if (map <= screen)
                return Mathf.Round((screen - map) / 2f);
            return Mathf.Round(Mathf.Clamp(screen / 2f - focus, screen - map, 0f));
        }
    }
}
