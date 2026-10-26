using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
namespace Mods.ModManager
{
    /// <summary>
    /// Free-form scheme canvas: the whole game on one "whatman", exactly like
    /// the approved mockup. Renders one <see cref="CollectionGroupCard"/> per
    /// resolved <see cref="ModCollectionGroup"/> with its draggable
    /// <see cref="CollectionModChip"/>s and paints dependency lines through
    /// <see cref="CollectionEdgeLayer"/>.
    /// <para/>
    /// The canvas owns only presentation and the layout dictionary: it computes
    /// a packed default arrangement for groups the player has not placed yet
    /// (new groups appear below the existing ones so nothing overlaps), clamps
    /// drags to the canvas, persists nothing itself and reports edits upward via
    /// <see cref="LayoutChanged"/>, <see cref="GroupSelected"/>,
    /// <see cref="ModSelected"/> and <see cref="GroupCommand"/>. The screen
    /// decides what a command means for the current draft and when to save.
    /// </summary>
    public partial class CollectionSchemeCanvas : ModWidget
    {
        private const float ColumnWidth = 460f;
        private const float GapX = 18f;
        private const float GapY = 16f;
        private const float Margin = 10f;
        private const float ModX0 = 14f;
        private const float ModY0 = 76f;
        private const float ModGapX = 8f;
        private const float ModGapY = 8f;
        private const int ModColumns = 2;

        /// <summary>
        /// Floor for the scrollable workspace. A draggable canvas is useless if
        /// the scrollbars disappear once the packed layout happens to fit the
        /// window, so the workspace is always at least this large; content that
        /// grows beyond it extends the area further.
        /// </summary>
        private const float MinCanvasWidth = 3200f;

        /// <summary>See <see cref="MinCanvasWidth"/>.</summary>
        private const float MinCanvasHeight = 2000f;

        /// <summary>Raised when a group card is clicked; payload is the group key.</summary>
        public event Action<string> GroupSelected;

        /// <summary>Raised when a mod chip is clicked; payload is the mod id.</summary>
        public event Action<string> ModSelected;

        /// <summary>Raised by group header buttons; payload is the group and the command id.</summary>
        public event Action<ModCollectionGroup, string> GroupCommand;

        /// <summary>Raised after any drag or resize ends, so the layout can be flushed to the store.</summary>
        public event Action LayoutChanged;

        /// <summary>Scale or scroll changed; the toolbar and overview may refresh their presentation.</summary>
        public event Action ViewChanged;

        /// <summary>Whether dependency lines are painted; toggled from the header.</summary>
        public bool ShowEdges { get; set; } = true;

        /// <summary>Currently highlighted group key, if any.</summary>
        public string SelectedGroupKey { get; private set; }

        /// <summary>Currently highlighted mod id, if any.</summary>
        public string SelectedModId { get; private set; }

        private CollectionEdgeLayer _edges;
        private ModCollectionGraph _graph;
        private ModCollectionLayout _layout = new();
        private readonly Dictionary<string, CollectionGroupCard> _cards = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CollectionModChip> _chips = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _chipGroups = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CollectionModChip> _instances = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModCollectionLayoutRect> _visibleFrames = new(StringComparer.OrdinalIgnoreCase);
        private Control _content;
        private CollectionMiniMap _miniMap;
        private long _edgeMode;
        private float _zoom = 1f;
        private string _search = string.Empty;

        /// <summary>Scale of the canvas contents, independent of persisted logical coordinates.</summary>
        public float Zoom => _zoom;

        /// <summary>Occupied logical bounds, excluding the empty scrollable workspace floor.</summary>
        public Vector2 ContentBounds { get; private set; } = new(460f, 240f);

        /// <summary>Current visible rectangle in logical coordinates, used by the overview map.</summary>
        public Rect2 VisibleBounds
        {
            get
            {
                var scroll = GetParent<ScrollContainer>();
                return scroll == null ? new Rect2() : new Rect2(
                    new Vector2(scroll.ScrollHorizontal, scroll.ScrollVertical) / _zoom,
                    scroll.Size / _zoom);
            }
        }

        /// <summary>Overview rectangles use the same logical geometry as the group frames.</summary>
        public IReadOnlyDictionary<string, ModCollectionLayoutRect> GroupFrames => _visibleFrames;

        /// <summary>Connects the scene-defined overview control after both controls entered the tree.</summary>
        public void SetMiniMap(CollectionMiniMap miniMap)
        {
            _miniMap = miniMap;
            _miniMap.Bind(this);
        }

        /// <summary>0: selected mod only, 1: all dependencies, 2: hidden; group boundaries never change severity.</summary>
        public void SetEdgeMode(long mode)
        {
            _edgeMode = mode;
            SetEdgesVisible(mode != 2);
            RebuildEdges();
        }

        /// <summary>Filters presentation only; hidden search matches do not leave the game's composition.</summary>
        public void SetSearch(string query)
        {
            _search = query?.Trim() ?? string.Empty;
            foreach (var chip in _instances.Values)
            {
                var node = FindNode(chip.ModId);
                var match = _search.Length == 0 || chip.ModId.Contains(_search, StringComparison.OrdinalIgnoreCase)
                    || (node?.Title?.Contains(_search, StringComparison.OrdinalIgnoreCase) ?? false);
                chip.Modulate = new Color(1, 1, 1, match ? 1f : 0.2f);
            }
        }

        /// <summary>Increases scale without rewriting any stored group or chip positions.</summary>
        public void ZoomIn() => SetZoom(_zoom * 1.15f);

        /// <summary>Decreases scale without rewriting any stored group or chip positions.</summary>
        public void ZoomOut() => SetZoom(_zoom / 1.15f);

        /// <summary>Fits occupied group bounds horizontally; the empty scroll floor does not shrink the scheme.</summary>
        public void FitWidth()
        {
            var scroll = GetParent<ScrollContainer>();
            if (scroll == null || scroll.Size.X <= 0f)
                return;
            SetZoom(Mathf.Min(1f, (scroll.Size.X - 24f) / Mathf.Max(1f, ContentBounds.X)));
            scroll.ScrollHorizontal = 0;
            scroll.ScrollVertical = 0;
            RefreshViewport();
        }

        /// <summary>Re-packs the current groups; composition and individual chip locations stay unchanged.</summary>
        public void ArrangeGroups()
        {
            var defaults = ComputeDefaultLayout();
            foreach (var pair in defaults)
            {
                var rect = Rect(pair.Key);
                rect.X = pair.Value.X;
                rect.Y = pair.Value.Y;
                _cards[pair.Key].SetFrame(new Rect2(pair.Value, new Vector2(rect.W, rect.H)));
            }
            UpdateCanvasSize();
            RebuildEdges();
            LayoutChanged?.Invoke();
        }

        /// <summary>Centers the scroll view on a logical canvas position (selection or overview click).</summary>
        public void CenterOn(Vector2 position)
        {
            var scroll = GetParent<ScrollContainer>();
            if (scroll == null)
                return;
            scroll.ScrollHorizontal = Mathf.RoundToInt(position.X * _zoom - scroll.Size.X / 2f);
            scroll.ScrollVertical = Mathf.RoundToInt(position.Y * _zoom - scroll.Size.Y / 2f);
            RefreshViewport();
        }

        /// <summary>Reveals the selected occurrence, keeping another group's copy from stealing focus.</summary>
        public void ShowSelection()
        {
            if (SelectedModId != null && _instances.TryGetValue(SelectedGroupKey + "|" + SelectedModId, out var chip))
                CenterOn(chip.Position + _cards[chip.GroupKey].Position + chip.Size / 2f);
            else if (SelectedGroupKey != null && _cards.TryGetValue(SelectedGroupKey, out var card))
                CenterOn(card.Position + card.Size / 2f);
        }

        private bool _panning;
        private Vector2 _panStartMouse;
        private Vector2 _panStartScroll;

        /// <summary>
        /// Rebuilds the canvas for a resolved graph and its saved layout. The
        /// layout object is mutated in place (new groups/mods get default
        /// coordinates); pass the instance owned by the screen so the next
        /// <see cref="LayoutChanged"/> can persist it.
        /// </summary>
        public void Build(ModCollectionGraph graph, ModCollectionLayout layout)
        {
            EnsureConstructed();

            _graph = graph;
            _layout = layout ?? new ModCollectionLayout();
            _layout.Groups ??= new Dictionary<string, ModCollectionLayoutRect>();
            _layout.Mods ??= new Dictionary<string, ModCollectionLayoutPoint>();

            foreach (var child in _content.GetChildren())
            {
                if (child != _edges)
                    child.QueueFree();
            }

            _cards.Clear();
            _chips.Clear();
            _chipGroups.Clear();
            _instances.Clear();
            _visibleFrames.Clear();

            if (graph != null && graph.Groups.Count > 0)
            {
                EnsureLayout();
                BuildCards();
            }

            _edges.SetEdges(null, _chips, _content);
            _content.MoveChild(_edges, _content.GetChildCount() - 1);
            _edges.Visible = ShowEdges;
            UpdateCanvasSize();
            RebuildEdges();
            SetSearch(_search);
        }

        /// <summary>Highlights one group and/or one mod; pass nulls to clear.</summary>
        public void SetSelection(string groupKey, string modId)
        {
            EnsureConstructed();
            SelectedGroupKey = groupKey;
            SelectedModId = modId;

            foreach (var pair in _cards)
                pair.Value.SetSelected(string.Equals(pair.Key, groupKey, StringComparison.OrdinalIgnoreCase));

            foreach (var pair in _chipGroups)
            {
                if (_instances.TryGetValue(pair.Value + "|" + pair.Key, out var primary))
                    _chips[pair.Key] = primary;
            }

            foreach (var chip in _instances.Values)
            {
                var selected = string.Equals(chip.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(chip.ModId, modId, StringComparison.OrdinalIgnoreCase);
                chip.SetSelected(selected);
                if (selected)
                    _chips[chip.ModId] = chip;
            }

            RebuildEdges();
        }

        /// <summary>Shows or hides dependency lines.</summary>
        public void SetEdgesVisible(bool visible)
        {
            EnsureConstructed();
            ShowEdges = visible;
            _edges.Visible = visible;
            if (visible)
                _edges.QueueRedraw();
        }

        /// <summary>Finds the group key owning a rendered mod chip, for inspector navigation.</summary>
        public string GetGroupKeyOfMod(string modId)
        {
            if (modId != null && string.Equals(modId, SelectedModId, StringComparison.OrdinalIgnoreCase)
                && SelectedGroupKey != null)
                return SelectedGroupKey;
            return modId != null && _chipGroups.TryGetValue(modId, out var key) ? key : null;
        }

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            GuiInput += OnCanvasGuiInput;
            _content = new Control { MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_content);
            _content.Connect("draw", Callable.From((Action)DrawGrid));
            _edges = new CollectionEdgeLayer();
            _content.AddChild(_edges);
            _edges.EnsureConstructed();
            var scroll = GetParent<ScrollContainer>();
            if (scroll != null)
            {
                scroll.GetHScrollBar().ValueChanged += OnScrollChanged;
                scroll.GetVScrollBar().ValueChanged += OnScrollChanged;
                scroll.Resized += RefreshViewport;
            }
        }

        protected override void Destruct()
        {
            GuiInput -= OnCanvasGuiInput;
            var scroll = GetParent<ScrollContainer>();
            if (scroll != null && GodotObject.IsInstanceValid(scroll))
            {
                scroll.GetHScrollBar().ValueChanged -= OnScrollChanged;
                scroll.GetVScrollBar().ValueChanged -= OnScrollChanged;
                scroll.Resized -= RefreshViewport;
            }
        }

        private void BuildCards()
        {
            foreach (var group in _graph.Groups)
            {
                var rect = _layout.Groups[group.Key];
                var card = new CollectionGroupCard();
                _content.AddChild(card);

                var summary = Summarize(group);
                card.Init(group, summary.Label, summary.Color, string.Equals(group.Key, SelectedGroupKey, StringComparison.OrdinalIgnoreCase));
                card.SetFrame(new Rect2(rect.X ?? 0f, rect.Y ?? 0f, rect.W, rect.H));

                card.Selected += OnCardSelected;
                card.Command += OnCardCommand;
                card.Moved += OnCardMoved;
                card.Resized += OnCardResized;
                card.DragFinished += OnDragFinished;
                _cards[group.Key] = card;
                _visibleFrames[group.Key] = rect;

                foreach (var mod in group.Mods)
                {
                    var key = group.Key + "|" + mod.ModId;
                    var point = _layout.Mods.TryGetValue(key, out var stored) ? stored : null;
                    var chip = new CollectionModChip();
                    card.ModsLayer.AddChild(chip);

                    var node = FindNode(mod.ModId);
                    var requirement = ModDependency.Parse(mod.Requirement);
                    chip.Init(
                        group.Key,
                        mod.ModId,
                        mod.Role,
                        node?.Title ?? mod.ModId,
                        VersionOf(node),
                        requirement.IsValid && requirement.VersionRaw != null ? requirement.Operator + requirement.VersionRaw : null,
                        StateLabel(node),
                        StateColor(node),
                        BuildModTooltip(mod, node));
                    chip.Position = new Vector2(point?.X ?? ModX0, point?.Y ?? ModY0);
                    chip.Selected += OnChipSelected;
                    chip.Moved += OnChipMoved;
                    chip.DragFinished += OnDragFinished;
                    _instances[key] = chip;

                    if (!_chips.ContainsKey(mod.ModId))
                    {
                        _chips[mod.ModId] = chip;
                        _chipGroups[mod.ModId] = group.Key;
                    }
                }
            }
        }

        /// <summary>
        /// Packs groups the player has not placed yet: a fresh canvas uses a
        /// masonry layout, an already arranged canvas appends new groups below
        /// everything so an added mechanic never lands on top of the work.
        /// </summary>
        private void EnsureLayout()
        {
            var unplaced = _graph.Groups
                .Where(g => !_layout.Groups.TryGetValue(g.Key, out var rect) || rect == null || !rect.X.HasValue || !rect.Y.HasValue)
                .Select(g => g.Key)
                .ToList();
            var hasPlaced = unplaced.Count < _graph.Groups.Count;

            foreach (var group in _graph.Groups)
            {
                if (!_layout.Groups.TryGetValue(group.Key, out var rect) || rect == null)
                {
                    rect = new ModCollectionLayoutRect();
                    _layout.Groups[group.Key] = rect;
                }

                var used = new List<ModCollectionLayoutPoint>();
                foreach (var mod in group.Mods)
                {
                    var key = group.Key + "|" + mod.ModId;
                    if (!_layout.Mods.TryGetValue(key, out var point) || point == null)
                    {
                        point = NextFreeModSlot(used);
                        _layout.Mods[key] = point;
                    }
                    used.Add(point);
                }

                if (rect.W <= 0f || rect.H <= 0f)
                {
                    var maxX = used.Count > 0 ? used.Max(p => p.X + CollectionModChip.ChipWidth) : 0f;
                    var maxY = used.Count > 0 ? used.Max(p => p.Y + CollectionModChip.ChipHeight) : 0f;
                    rect.W = Mathf.Max(CollectionGroupCard.MinWidth, maxX + 16f);
                    rect.H = Mathf.Max(CollectionGroupCard.MinHeight, maxY + 16f);
                }
            }

            var defaults = ComputeDefaultLayout();
            var bottom = Margin;
            foreach (var group in _graph.Groups)
            {
                var rect = _layout.Groups[group.Key];
                if (rect.X.HasValue && rect.Y.HasValue && !unplaced.Contains(group.Key))
                    bottom = Mathf.Max(bottom, rect.Y.Value + rect.H + GapY);
            }

            foreach (var group in _graph.Groups)
            {
                var rect = _layout.Groups[group.Key];
                if (!rect.X.HasValue || !rect.Y.HasValue)
                {
                    if (!hasPlaced && defaults.TryGetValue(group.Key, out var packed))
                    {
                        rect.X = packed.X;
                        rect.Y = packed.Y;
                    }
                    else
                    {
                        rect.X = Margin;
                        rect.Y = bottom;
                        bottom += rect.H + GapY;
                    }
                }
            }
        }

        private Dictionary<string, Vector2> ComputeDefaultLayout()
        {
            var result = new Dictionary<string, Vector2>();
            var groups = _graph?.Groups ?? new List<ModCollectionGroup>();
            if (groups.Count == 0)
                return result;

            var maxCols = Mathf.Min(3, Mathf.Max(1, Mathf.CeilToInt(groups.Count / 2f)));
            var heights = new float[maxCols];

            foreach (var group in groups)
            {
                var height = _layout.Groups[group.Key].H;
                var column = 0;
                for (var i = 1; i < maxCols; i++)
                {
                    if (heights[i] < heights[column])
                        column = i;
                }

                result[group.Key] = new Vector2(Margin + column * (ColumnWidth + GapX), Margin + heights[column]);
                heights[column] += height + GapY;
            }

            return result;
        }

        private static ModCollectionLayoutPoint NextFreeModSlot(List<ModCollectionLayoutPoint> used)
        {
            for (var i = 0; i < 400; i++)
            {
                var column = i % ModColumns;
                var row = i / ModColumns;
                var point = new ModCollectionLayoutPoint
                {
                    X = ModX0 + column * (CollectionModChip.ChipWidth + ModGapX),
                    Y = ModY0 + row * (CollectionModChip.ChipHeight + ModGapY),
                };

                if (!used.Any(u => Mathf.Abs(u.X - point.X) < CollectionModChip.ChipWidth * 0.9f
                                   && Mathf.Abs(u.Y - point.Y) < CollectionModChip.ChipHeight * 0.9f))
                {
                    return point;
                }
            }

            var bottom = used.Count > 0 ? used.Max(p => p.Y) : ModY0;
            return new ModCollectionLayoutPoint { X = ModX0, Y = bottom + CollectionModChip.ChipHeight + ModGapY };
        }

        private ModCollectionNode FindNode(string modId)
        {
            if (_graph?.Nodes == null)
                return null;

            foreach (var node in _graph.Nodes)
            {
                if (string.Equals(node.ModId, modId, StringComparison.OrdinalIgnoreCase))
                    return node;
            }

            return null;
        }

        private static string VersionOf(ModCollectionNode node)
        {
            var version = node?.InstalledVersion;
            return string.IsNullOrEmpty(version) ? null : "v" + version;
        }

        private static string StateLabel(ModCollectionNode node)
            => node == null ? CollectionStateNames.Translate(ModCollectionNodeState.Missing) : CollectionStateNames.Translate(node.State);

        private static Color StateColor(ModCollectionNode node)
            => node == null ? CollectionStateNames.Color(ModCollectionNodeState.Missing) : CollectionStateNames.Color(node.State);

        private static string BuildModTooltip(ModCollectionModRef mod, ModCollectionNode node)
        {
            var label = node?.Title ?? mod.ModId;
            return $"{label} · {mod.ModId} · {StateLabel(node)}";
        }

        /// <summary>Worst-state summary shown on the group card: missing beats mismatch beats downloads beats disable.</summary>
        private (string Label, Color Color) Summarize(ModCollectionGroup group)
        {
            if (!group.Selected)
                return (TranslationServer.Translate("mod_manager/collection_group_off"), UiTokens.Warning);

            var states = new List<ModCollectionNodeState>();
            foreach (var mod in group.Mods)
            {
                var node = FindNode(mod.ModId);
                states.Add(node?.State ?? ModCollectionNodeState.Missing);
            }

            if (states.Count == 0)
                return (TranslationServer.Translate("mod_manager/collection_group_empty"), UiTokens.Info);

            if (states.Any(s => s == ModCollectionNodeState.Missing))
                return (CountLabel(ModCollectionNodeState.Missing, states), UiTokens.Danger);
            if (states.Any(s => s == ModCollectionNodeState.VersionMismatch))
                return (CountLabel(ModCollectionNodeState.VersionMismatch, states), UiTokens.Warning);
            if (states.Any(s => s == ModCollectionNodeState.InCatalog))
                return (CountLabel(ModCollectionNodeState.InCatalog, states), UiTokens.Info);
            if (states.Any(s => s == ModCollectionNodeState.Disabled))
                return (CountLabel(ModCollectionNodeState.Disabled, states), UiTokens.Warning);

            return (TranslationServer.Translate("mod_manager/collection_group_ready"), UiTokens.Success);
        }

        private static string CountLabel(ModCollectionNodeState state, List<ModCollectionNodeState> states)
            => $"{CollectionStateNames.Translate(state)}: {states.Count(s => s == state)}";

        private void OnCardSelected(string groupKey)
        {
            SelectedGroupKey = groupKey;
            SelectedModId = null;
            GroupSelected?.Invoke(groupKey);
        }

        private void OnChipSelected(CollectionModChip chip)
        {
            SelectedModId = chip.ModId;
            SelectedGroupKey = chip.GroupKey;
            ModSelected?.Invoke(chip.ModId);
        }

        private void OnCardCommand(string groupKey, string command)
        {
            if (_cards.TryGetValue(groupKey, out var card))
                GroupCommand?.Invoke(card.Group, command);
        }

        private void OnCardMoved(string groupKey, Vector2 position)
        {
            var rect = Rect(groupKey);
            rect.X = position.X;
            rect.Y = position.Y;
            UpdateCanvasSize();
            RebuildEdges();
        }

        private void OnCardResized(string groupKey, Vector2 size)
        {
            var rect = Rect(groupKey);
            rect.W = size.X;
            rect.H = size.Y;
            UpdateCanvasSize();
            RebuildEdges();
        }

        private void OnChipMoved(CollectionModChip chip, Vector2 position)
        {
            var key = chip.GroupKey + "|" + chip.ModId;
            if (!_layout.Mods.TryGetValue(key, out var point) || point == null)
            {
                point = new ModCollectionLayoutPoint();
                _layout.Mods[key] = point;
            }

            point.X = position.X;
            point.Y = position.Y;
            RebuildEdges();
        }

        private void OnDragFinished() => LayoutChanged?.Invoke();

        private ModCollectionLayoutRect Rect(string groupKey)
        {
            if (!_layout.Groups.TryGetValue(groupKey, out var rect) || rect == null)
            {
                rect = new ModCollectionLayoutRect();
                _layout.Groups[groupKey] = rect;
            }

            return rect;
        }

        private void UpdateCanvasSize()
        {
            var maxX = 0f;
            var maxY = 0f;
            foreach (var card in _cards.Values)
            {
                if (!GodotObject.IsInstanceValid(card))
                    continue;
                maxX = Mathf.Max(maxX, card.Position.X + card.Size.X);
                maxY = Mathf.Max(maxY, card.Position.Y + card.Size.Y);
            }

            var size = new Vector2(
                Mathf.Max(maxX + GapX + 160f, MinCanvasWidth),
                Mathf.Max(maxY + GapY + 120f, MinCanvasHeight));
            ContentBounds = new Vector2(Mathf.Max(460f, maxX + GapX), Mathf.Max(240f, maxY + GapY));
            // Drives the canvas rect through the surrounding ScrollContainer.
            // _edges carries FullRect anchors (CollectionEdgeLayer.Construct),
            // so it follows this resize automatically; assigning its Size
            // directly would be overridden and logs an anchors warning.
            _content.Size = size;
            _content.Scale = Vector2.One * _zoom;
            CustomMinimumSize = size * _zoom;
            RefreshViewport();
        }

        private void RebuildEdges()
        {
            var edges = new List<CollectionEdge>();
            if (_graph?.Edges != null)
            {
                foreach (var edge in _graph.Edges)
                {
                    if (!_chips.TryGetValue(edge.FromModId, out _) || !_chips.TryGetValue(edge.ToModId, out _))
                        continue;

                    var fromGroup = GetGroupKeyOfMod(edge.FromModId);
                    var toGroup = GetGroupKeyOfMod(edge.ToModId);
                    var active = string.Equals(edge.FromModId, SelectedModId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(edge.ToModId, SelectedModId, StringComparison.OrdinalIgnoreCase);
                    if (_edgeMode == 0 && !active)
                        continue;
                    edges.Add(new CollectionEdge
                    {
                        FromModId = edge.FromModId,
                        ToModId = edge.ToModId,
                        Cross = !string.Equals(fromGroup, toGroup, StringComparison.OrdinalIgnoreCase),
                        Active = active,
                        Problem = HasProblem(edge.FromModId) || HasProblem(edge.ToModId),
                    });
                }
            }

            _edges.SetEdges(edges, _chips, _content);
            RefreshViewport();
        }

        private bool HasProblem(string modId)
        {
            var node = FindNode(modId);
            return node == null || node.State == ModCollectionNodeState.Missing
                || node.State == ModCollectionNodeState.VersionMismatch;
        }

        private void SetZoom(float value)
        {
            var scroll = GetParent<ScrollContainer>();
            var center = scroll == null ? Vector2.Zero : VisibleBounds.GetCenter();
            _zoom = Mathf.Clamp(value, 0.3f, 1.5f);
            UpdateCanvasSize();
            CenterOn(center);
        }

        private void OnScrollChanged(double value) => RefreshViewport();

        private void RefreshViewport()
        {
            _content?.QueueRedraw();
            _miniMap?.QueueRedraw();
            ViewChanged?.Invoke();
        }

        private void DrawGrid()
        {
            var visible = VisibleBounds;
            var start = visible.Position.Snapped(new Vector2(20f, 20f));
            var color = new Color(UiTokens.LineCard, 0.55f);
            for (var x = start.X; x <= visible.End.X + 20f; x += 20f)
            {
                for (var y = start.Y; y <= visible.End.Y + 20f; y += 20f)
                    _content.DrawCircle(new Vector2(x, y), 0.8f, color);
            }
        }

        /// <summary>
        /// Pans the surrounding ScrollContainer by dragging the empty canvas
        /// with the left button held. The canvas floor is the only control that
        /// receives this event: group cards, chips, headers and buttons consume
        /// their presses first (Godot then keeps them as the mouse focus until
        /// release), so their own drags stay untouched. Positions are anchored
        /// to the press point instead of accumulated deltas, so a scroll edge
        /// does not build up drift once the pointer returns.
        /// </summary>
        private void OnCanvasGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton wheel && wheel.Pressed && wheel.CtrlPressed)
            {
                if (wheel.ButtonIndex == MouseButton.WheelUp)
                    ZoomIn();
                else if (wheel.ButtonIndex == MouseButton.WheelDown)
                    ZoomOut();
                else
                    return;
                AcceptEvent();
                return;
            }
            if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    var scroll = GetParent<ScrollContainer>();
                    if (scroll == null)
                        return;

                    _panning = true;
                    _panStartMouse = button.GlobalPosition;
                    _panStartScroll = new Vector2(scroll.ScrollHorizontal, scroll.ScrollVertical);
                    MouseDefaultCursorShape = CursorShape.Drag;
                }
                else if (_panning)
                {
                    EndPan();
                }

                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion motion && _panning)
            {
                if (!motion.ButtonMask.HasFlag(MouseButtonMask.Left))
                {
                    EndPan();
                    return;
                }

                var scroll = GetParent<ScrollContainer>();
                if (scroll != null)
                {
                    var delta = motion.GlobalPosition - _panStartMouse;
                    scroll.ScrollHorizontal = Mathf.RoundToInt(_panStartScroll.X - delta.X);
                    scroll.ScrollVertical = Mathf.RoundToInt(_panStartScroll.Y - delta.Y);
                }

                AcceptEvent();
            }
        }

        private void EndPan()
        {
            _panning = false;
            MouseDefaultCursorShape = CursorShape.Arrow;
        }
    }
}
