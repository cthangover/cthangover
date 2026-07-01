using System;
using System.Collections.Generic;
using Cthangover.Core.Interactive;
using Cthangover.Core.Mods;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.InteractiveEditor
{
	/// <summary>
	/// Editor window for creating and editing interactive object definitions.
	/// Shows a background preview with draggable collider handles and outputs
	/// the resulting JSON. Supports loading/saving to mod interactives directories.
	/// </summary>
	public partial class InteractiveEditorWindow : ToolWindow
	{
		private static readonly Color _rectColor = new(0f, 1f, 0f, 0.6f);
		private static readonly Color _circleColor = new(0f, 0.5f, 1f, 0.6f);
		private static readonly Color _vertexColor = new(1f, 0.8f, 0f, 0.8f);
		private static readonly Color _colliderOverlayColor = new(0f, 1f, 0f, 0.15f);

		private InteractiveEditorController _controller = new();

		private Control _previewContainer;
		private TextureRect _preview;
		private ColorRect _colliderOverlay;
		private string _currentBgId;

		private readonly List<InteractiveEditorHandle> _handles = new();

		private TextEdit _jsonEdit;

		private LineEdit _idEdit, _textureEdit;
		private OptionButton _layerSelect;
		private OptionButton _hitTypeSelect;
		private ItemList _bgList;

		private LineEdit _rectX, _rectY, _rectW, _rectH;
		private LineEdit _circleX, _circleY, _circleR;
		private VBoxContainer _rectParams, _circleParams, _polyParams;

		private ColorPickerButton _highlightColorBtn;
		private LineEdit _highlightScaleEdit;

		private LineEdit _clickScenario, _hoverEnter, _hoverLeave;
		private TextEdit _clickCommands;

		private VBoxContainer _vertexList;
		private Button _addVertexBtn;

		private bool _suppressingSidebarEvents;

		public InteractiveEditorWindow() : base("tools/interactive_editor/title")
		{
			Title = "Interactive Editor";
			Size = new Vector2I(1280, 820);

			var outer = CreateFillContainer();
			AddChild(outer);

			var toolbar = CreateToolbar();
			outer.AddChild(toolbar);

			var loadBtn = new Button { Text = "Load JSON" };
			loadBtn.Pressed += OnLoadJson;
			toolbar.AddChild(loadBtn);

			var saveBtn = new Button { Text = "Save" };
			saveBtn.Pressed += OnSave;
			toolbar.AddChild(saveBtn);

			var importDefBtn = new Button { Text = "Import from mod" };
			importDefBtn.Pressed += OnImportFromMod;
			toolbar.AddChild(importDefBtn);

			var mainHBox = new HBoxContainer();
			mainHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			mainHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			outer.AddChild(mainHBox);

			var sidebarVBox = new VBoxContainer();
			var sidebar = CreateSidebar(sidebarVBox, 290);
			mainHBox.AddChild(sidebar);

			BuildSidebar(sidebarVBox);

			_previewContainer = new Control
			{
				MouseFilter = Control.MouseFilterEnum.Stop,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			mainHBox.AddChild(_previewContainer);

			_preview = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f
			};
			_previewContainer.AddChild(_preview);

			_colliderOverlay = new ColorRect
			{
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false
			};
			_previewContainer.AddChild(_colliderOverlay);

			_previewContainer.Resized += () =>
			{
				RebuildHandles();
				UpdateColliderOverlay();
			};

			_jsonEdit = new TextEdit { CustomMinimumSize = new Vector2(0, 150) };
			_jsonEdit.AddThemeFontOverride("font", GetMonospaceFont());
			_jsonEdit.TextChanged += () => SetDirty();
			outer.AddChild(_jsonEdit);

			RefreshJson();
		}

		private void BuildSidebar(VBoxContainer sidebar)
		{
			AddLabel(sidebar, "ID:");
			_idEdit = AddLineEdit(sidebar, "my_interactive");
			_idEdit.TextChanged += OnParamChanged;

			AddLabel(sidebar, "Texture key:");
			_textureEdit = AddLineEdit(sidebar, "lamp");
			_textureEdit.TextChanged += OnParamChanged;

			AddLabel(sidebar, "Layer:");
			_layerSelect = new OptionButton();
			_layerSelect.AddItem("foreground");
			_layerSelect.AddItem("background");
			_layerSelect.AddItem("ui");
			_layerSelect.ItemSelected += _ => OnParamChanged();
			sidebar.AddChild(_layerSelect);

			AddLabel(sidebar, "Background preview:");
			_bgList = new ItemList();
			_bgList.CustomMinimumSize = new Vector2(0, 100);
			_bgList.ItemSelected += OnBgSelected;
			sidebar.AddChild(_bgList);
			LoadBackgroundList();

			AddLabel(sidebar, "Collider type:");
			_hitTypeSelect = new OptionButton();
			_hitTypeSelect.AddItem("rect");
			_hitTypeSelect.AddItem("circle");
			_hitTypeSelect.AddItem("polygon");
			_hitTypeSelect.ItemSelected += OnHitTypeChanged;
			sidebar.AddChild(_hitTypeSelect);

			_rectParams = new VBoxContainer();
			AddLabel(_rectParams, "Rect (norm. 0-1):");
			_rectX = AddLabeledLine(_rectParams, "x:"); _rectX.TextChanged += OnParamChanged;
			_rectY = AddLabeledLine(_rectParams, "y:"); _rectY.TextChanged += OnParamChanged;
			_rectW = AddLabeledLine(_rectParams, "w:"); _rectW.TextChanged += OnParamChanged;
			_rectH = AddLabeledLine(_rectParams, "h:"); _rectH.TextChanged += OnParamChanged;
			sidebar.AddChild(_rectParams);

			_circleParams = new VBoxContainer();
			AddLabel(_circleParams, "Circle (norm. 0-1):");
			_circleX = AddLabeledLine(_circleParams, "cx:"); _circleX.TextChanged += OnParamChanged;
			_circleY = AddLabeledLine(_circleParams, "cy:"); _circleY.TextChanged += OnParamChanged;
			_circleR = AddLabeledLine(_circleParams, "r:"); _circleR.TextChanged += OnParamChanged;
			_circleParams.Visible = false;
			sidebar.AddChild(_circleParams);

			_polyParams = new VBoxContainer();
			AddLabel(_polyParams, "Polygon vertices:");
			_addVertexBtn = new Button { Text = "+ Add vertex" };
			_addVertexBtn.Pressed += OnAddVertex;
			_polyParams.AddChild(_addVertexBtn);
			_vertexList = new VBoxContainer();
			_polyParams.AddChild(_vertexList);
			_polyParams.Visible = false;
			sidebar.AddChild(_polyParams);

			AddLabel(sidebar, "Highlight color:");
			_highlightColorBtn = new ColorPickerButton { Color = new Color(1f, 1f, 0f, 0.2f) };
			_highlightColorBtn.ColorChanged += _ => OnParamChanged();
			sidebar.AddChild(_highlightColorBtn);

			AddLabel(sidebar, "Highlight scale:");
			_highlightScaleEdit = AddLineEdit(sidebar, "1.02");
			_highlightScaleEdit.TextChanged += OnParamChanged;

			AddLabel(sidebar, "Cursor:");
			var cursorSelect = new OptionButton();
			cursorSelect.AddItem("(none)");
			cursorSelect.AddItem("PointingHand");
			cursorSelect.ItemSelected += idx =>
			{
				_controller.Cursor = idx == 0 ? "" : "PointingHand";
				OnParamChanged();
			};
			sidebar.AddChild(cursorSelect);

			AddLabel(sidebar, "OnClick scenario:");
			_clickScenario = AddLineEdit(sidebar, "scenarios/my_click.scenario");
			_clickScenario.TextChanged += OnParamChanged;

			AddLabel(sidebar, "OnClick commands:");
			_clickCommands = new TextEdit { CustomMinimumSize = new Vector2(0, 50) };
			_clickCommands.TextChanged += OnParamChanged;
			sidebar.AddChild(_clickCommands);

			AddLabel(sidebar, "OnHoverEnter DSL:");
			_hoverEnter = AddLineEdit(sidebar, "set cursor_hint=click_me");
			_hoverEnter.TextChanged += OnParamChanged;

			AddLabel(sidebar, "OnHoverLeave DSL:");
			_hoverLeave = AddLineEdit(sidebar, "set cursor_hint=");
			_hoverLeave.TextChanged += OnParamChanged;
		}

		private void AddLabel(Control parent, string text)
		{
			var label = new Label { Text = text };
			label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
			parent.AddChild(label);
		}

		private LineEdit AddLineEdit(Control parent, string placeholder)
		{
			var edit = new LineEdit { PlaceholderText = placeholder };
			parent.AddChild(edit);
			return edit;
		}

		private LineEdit AddLabeledLine(Control parent, string labelText)
		{
			var hbox = new HBoxContainer();
			var label = new Label { Text = labelText };
			label.CustomMinimumSize = new Vector2(25, 0);
			hbox.AddChild(label);
			var edit = new LineEdit();
			edit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			hbox.AddChild(edit);
			parent.AddChild(hbox);
			return edit;
		}

		private void LoadBackgroundList()
		{
			_bgList.Clear();
			var ids = ModResourceService.GetBackgroundIds();
			foreach (var id in ids)
				_bgList.AddItem(id);
		}

		private void OnBgSelected(long index)
		{
			var id = _bgList.GetItemText((int)index);
			_currentBgId = id;
			var tex = ModResourceService.LoadBackgroundTexture(id);
			if (tex != null)
			{
				_preview.Texture = tex;
			}
			OnParamChanged();
		}

		private void OnHitTypeChanged(long index)
		{
			_controller.HitType = _hitTypeSelect.GetItemText((int)index);
			_rectParams.Visible = _controller.HitType == "rect";
			_circleParams.Visible = _controller.HitType == "circle";
			_polyParams.Visible = _controller.HitType == "polygon";
			OnParamChanged();
		}

		private void OnAddVertex()
		{
			_controller.AddPolygonVertex();
			OnParamChanged();
		}

		private void OnParamChanged()
		{
			if (_suppressingSidebarEvents)
			{
				GameLogger.Log("INTERACTIVE_EDITOR", "OnParamChanged suppressed");
				return;
			}

			GameLogger.Log("INTERACTIVE_EDITOR", $"OnParamChanged hitType={_controller.HitType} bgSize=({_previewContainer.Size.X:F0}x{_previewContainer.Size.Y:F0})");
			ReadSidebarToController();
			RebuildHandles();
			UpdateColliderOverlay();
			RefreshJson();
			SetDirty();
			UpdateVertexList();
		}

		/// <summary>Lightweight update during handle drag. Does NOT rebuild handles.</summary>
		private void OnDragUpdate()
		{
			GameLogger.Log("INTERACTIVE_EDITOR", $"OnDragUpdate hitType={_controller.HitType}");
			UpdateColliderOverlay();
			RefreshJson();
			SetDirty();
		}

		private void ReadSidebarToController()
		{
			_controller.Id = string.IsNullOrWhiteSpace(_idEdit.Text) ? "new_interactive" : _idEdit.Text.Trim();
			_controller.Texture = _textureEdit.Text.Trim();
			_controller.Layer = _layerSelect.GetItemText(_layerSelect.Selected);

			if (_controller.HitType == "rect")
			{
				float.TryParse(_rectX.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v);
				_controller.RectX = v;
				float.TryParse(_rectY.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); _controller.RectY = v;
				float.TryParse(_rectW.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); _controller.RectW = v;
				float.TryParse(_rectH.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); _controller.RectH = v;
			}
			else if (_controller.HitType == "circle")
			{
				float.TryParse(_circleX.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v);
				_controller.CircleX = v;
				float.TryParse(_circleY.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); _controller.CircleY = v;
				float.TryParse(_circleR.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); _controller.CircleRadius = v;
			}

			_controller.HighlightColorHex = "#" + _highlightColorBtn.Color.ToHtml(false);
			float.TryParse(_highlightScaleEdit.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale);
			_controller.HighlightScale = scale;

			_controller.OnClickScenario = _clickScenario.Text.Trim();
			_controller.OnClickCommands = _clickCommands.Text.Trim();
			_controller.OnHoverEnter = _hoverEnter.Text.Trim();
			_controller.OnHoverLeave = _hoverLeave.Text.Trim();
		}

		/// <summary>
		/// Calculates where the background texture is rendered within _previewContainer,
		/// maintaining aspect ratio. Uses 1920x1024 as fallback when no texture is loaded.
		/// </summary>
		private Rect2 GetBackgroundRect()
		{
			var cw = _previewContainer.Size.X;
			var ch = _previewContainer.Size.Y;
			if (cw <= 0 || ch <= 0) return new Rect2();

			var texSize = new Vector2(1920, 1024);
			var tex = _preview.Texture;
			if (tex != null)
			{
				var ts = tex.GetSize();
				if (ts.X > 0 && ts.Y > 0) texSize = ts;
			}

			var scale = Mathf.Min(cw / texSize.X, ch / texSize.Y);
			var rw = texSize.X * scale;
			var rh = texSize.Y * scale;
			var ox = (cw - rw) / 2f;
			var oy = (ch - rh) / 2f;
			return new Rect2(ox, oy, rw, rh);
		}

		private Vector2 NormToPixel(Vector2 norm)
		{
			var bg = GetBackgroundRect();
			return bg.Position + norm * bg.Size;
		}

		private Vector2 PixelToNorm(Vector2 pixel)
		{
			var bg = GetBackgroundRect();
			if (bg.Size.X <= 0 || bg.Size.Y <= 0) return Vector2.Zero;
			return new Vector2(
				(pixel.X - bg.Position.X) / bg.Size.X,
				(pixel.Y - bg.Position.Y) / bg.Size.Y);
		}

		private void WriteControllerToSidebar()
		{
			_suppressingSidebarEvents = true;
			try
			{
				_idEdit.Text = _controller.Id;
				_textureEdit.Text = _controller.Texture;

				for (int i = 0; i < _layerSelect.ItemCount; i++)
					if (_layerSelect.GetItemText(i) == _controller.Layer)
						_layerSelect.Select(i);

				for (int i = 0; i < _hitTypeSelect.ItemCount; i++)
					if (_hitTypeSelect.GetItemText(i) == _controller.HitType)
						_hitTypeSelect.Select(i);

				_rectX.Text = _controller.RectX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				_rectY.Text = _controller.RectY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				_rectW.Text = _controller.RectW.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				_rectH.Text = _controller.RectH.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

				_circleX.Text = _controller.CircleX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				_circleY.Text = _controller.CircleY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				_circleR.Text = _controller.CircleRadius.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

				_rectParams.Visible = _controller.HitType == "rect";
				_circleParams.Visible = _controller.HitType == "circle";
				_polyParams.Visible = _controller.HitType == "polygon";

				_highlightColorBtn.Color = new Color(_controller.HighlightColorHex);
				_highlightScaleEdit.Text = _controller.HighlightScale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

				_clickScenario.Text = _controller.OnClickScenario;
				_clickCommands.Text = _controller.OnClickCommands;
				_hoverEnter.Text = _controller.OnHoverEnter;
				_hoverLeave.Text = _controller.OnHoverLeave;
			}
			finally
			{
				_suppressingSidebarEvents = false;
			}
		}

		private void RebuildHandles()
		{
			foreach (var h in _handles)
				h.QueueFree();
			_handles.Clear();

			var bg = GetBackgroundRect();
			if (bg.Size.X <= 0 || bg.Size.Y <= 0)
			{
				GameLogger.Log("INTERACTIVE_EDITOR", "RebuildHandles skipped: bg size <= 0");
				return;
			}

			GameLogger.Log("INTERACTIVE_EDITOR", $"RebuildHandles hitType={_controller.HitType} bg=({bg.Position.X:F0},{bg.Position.Y:F0} {bg.Size.X:F0}x{bg.Size.Y:F0})");

			var globalTopLeft = _previewContainer.GlobalPosition;

			switch (_controller.HitType)
			{
				case "rect":
				{
					var topLeft = NormToPixel(new Vector2(_controller.RectX, _controller.RectY));
					var bottomRight = NormToPixel(new Vector2(
						_controller.RectX + _controller.RectW,
						_controller.RectY + _controller.RectH));

					AddHandleAt(globalTopLeft + topLeft, _rectColor, 0, pos =>
					{
						var norm = PixelToNorm(pos - globalTopLeft);
						_controller.RectX = Mathf.Clamp(norm.X, 0f, 1f);
						_controller.RectY = Mathf.Clamp(norm.Y, 0f, 1f);
						OnDragUpdate();
					});
					AddHandleAt(globalTopLeft + bottomRight, _rectColor, 1, pos =>
					{
						var norm = PixelToNorm(pos - globalTopLeft);
						_controller.RectW = Mathf.Max(0.01f, norm.X - _controller.RectX);
						_controller.RectH = Mathf.Max(0.01f, norm.Y - _controller.RectY);
						OnDragUpdate();
					});
					break;
				}
				case "circle":
				{
					var refDim = Mathf.Min(bg.Size.X, bg.Size.Y);
					var center = NormToPixel(new Vector2(_controller.CircleX, _controller.CircleY));
					var radiusPx = _controller.CircleRadius * refDim;

					AddHandleAt(globalTopLeft + center, _circleColor, 0, pos =>
					{
						var norm = PixelToNorm(pos - globalTopLeft);
						_controller.CircleX = Mathf.Clamp(norm.X, 0f, 1f);
						_controller.CircleY = Mathf.Clamp(norm.Y, 0f, 1f);
						OnDragUpdate();
					});
					AddHandleAt(globalTopLeft + new Vector2(center.X + radiusPx, center.Y), _circleColor, 1, pos =>
					{
						var local = pos - globalTopLeft;
						var centerPx = NormToPixel(new Vector2(_controller.CircleX, _controller.CircleY));
						var dist = (local - centerPx).Length();
						_controller.CircleRadius = Mathf.Max(0.005f, dist / refDim);
						OnDragUpdate();
					});
					break;
				}
				case "polygon":
				{
					for (int i = 0; i < _controller.PolygonVertices.Count; i++)
					{
						var idx = i;
						var v = _controller.PolygonVertices[i];
						AddHandleAt(globalTopLeft + NormToPixel(v), _vertexColor, i, pos =>
						{
							var norm = PixelToNorm(pos - globalTopLeft);
							_controller.SetPolygonVertex(idx, new Vector2(
								Mathf.Clamp(norm.X, 0f, 1f),
								Mathf.Clamp(norm.Y, 0f, 1f)));
							OnDragUpdate();
						});
					}
					break;
				}
			}
		}

		private void UpdateColliderOverlay()
		{
			var bg = GetBackgroundRect();
			if (bg.Size.X <= 0 || bg.Size.Y <= 0)
			{
				_colliderOverlay.Visible = false;
				return;
			}

			_colliderOverlay.Visible = true;

			switch (_controller.HitType)
			{
				case "rect":
					_colliderOverlay.Position = NormToPixel(
						new Vector2(_controller.RectX, _controller.RectY));
					_colliderOverlay.Size = new Vector2(
						_controller.RectW * bg.Size.X, _controller.RectH * bg.Size.Y);
					_colliderOverlay.Color = _colliderOverlayColor;
					break;
				case "circle":
				{
					var r = _controller.CircleRadius * Mathf.Min(bg.Size.X, bg.Size.Y);
					var center = NormToPixel(
						new Vector2(_controller.CircleX, _controller.CircleY));
					_colliderOverlay.Position = center - new Vector2(r, r);
					_colliderOverlay.Size = new Vector2(r * 2f, r * 2f);
					_colliderOverlay.Color = _colliderOverlayColor;
					break;
				}
				case "polygon":
				{
					if (_controller.PolygonVertices.Count > 0)
					{
						float minX = float.MaxValue, minY = float.MaxValue,
							maxX = float.MinValue, maxY = float.MinValue;
						foreach (var v in _controller.PolygonVertices)
						{
							var p = NormToPixel(v);
							minX = Mathf.Min(minX, p.X);
							minY = Mathf.Min(minY, p.Y);
							maxX = Mathf.Max(maxX, p.X);
							maxY = Mathf.Max(maxY, p.Y);
						}
						_colliderOverlay.Position = new Vector2(minX, minY);
						_colliderOverlay.Size = new Vector2(maxX - minX, maxY - minY);
						_colliderOverlay.Color = _colliderOverlayColor;
					}
					break;
				}
			}
		}

		private void AddHandleAt(Vector2 globalPos, Color color, int index, Action<Vector2> onDrag)
		{
			var handle = new InteractiveEditorHandle(color, index);
			handle.DragUpdate = center =>
			{
				onDrag(center);
			};
			handle.Clicked = () =>
			{
				SelectHandle(index);
			};
			handle.DragEnd = () =>
			{
				GameLogger.Log("INTERACTIVE_EDITOR", $"DragEnd idx={index} syncing sidebar");
				_suppressingSidebarEvents = true;
				WriteControllerToSidebar();
				_suppressingSidebarEvents = false;
			};
			handle.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			_previewContainer.AddChild(handle);
			handle.GlobalPosition = globalPos - handle.Size / 2;
			_handles.Add(handle);
		}

		private void SelectHandle(int index)
		{
			if (_controller.HitType == "polygon" && index >= 0 && index < _controller.PolygonVertices.Count)
			{
				var v = _controller.PolygonVertices[index];
				GD.Print($"Selected vertex {index}: ({v.X:F3}, {v.Y:F3})");
			}
		}

		private void UpdateVertexList()
		{
			for (int i = _vertexList.GetChildCount() - 1; i >= 0; i--)
				_vertexList.GetChild(i).QueueFree();

			if (_controller.HitType != "polygon") return;

			for (int i = 0; i < _controller.PolygonVertices.Count; i++)
			{
				var idx = i;
				var v = _controller.PolygonVertices[i];
				var row = new HBoxContainer();

				var xEdit = new LineEdit();
				xEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				xEdit.Text = v.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				xEdit.TextChanged += txt =>
				{
					if (float.TryParse(xEdit.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
						_controller.SetPolygonVertex(idx, new Vector2(val, _controller.PolygonVertices[idx].Y));
					OnParamChanged();
				};
				row.AddChild(xEdit);

				var yEdit = new LineEdit();
				yEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				yEdit.Text = v.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
				yEdit.TextChanged += txt =>
				{
					if (float.TryParse(yEdit.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
						_controller.SetPolygonVertex(idx, new Vector2(_controller.PolygonVertices[idx].X, val));
					OnParamChanged();
				};
				row.AddChild(yEdit);

				var delBtn = new Button { Text = "X" };
				delBtn.CustomMinimumSize = new Vector2(30, 0);
				delBtn.Pressed += () =>
				{
					_controller.RemovePolygonVertex(idx);
					OnParamChanged();
				};
				row.AddChild(delBtn);

				_vertexList.AddChild(row);
			}
		}

		private void RefreshJson()
		{
			_jsonEdit.Text = _controller.ToJson();
		}

		private void OnLoadJson()
		{
			var dialog = new FileDialog();
			dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
			dialog.AddFilter("*.json", "JSON Files");
			dialog.AddFilter("*.interactive", "Interactive Files");
			dialog.FileSelected += path =>
			{
				var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
				if (file != null)
				{
					var text = file.GetAsText();
					file.Close();
					if (_controller.FromJson(text))
					{
						WriteControllerToSidebar();
						UpdateVertexList();
						RebuildHandles();
						UpdateColliderOverlay();
						RefreshJson();
						MarkClean();
					}
					else
					{
						GD.PrintErr("Failed to parse JSON");
					}
				}
				dialog.QueueFree();
			};
			dialog.Canceled += () => dialog.QueueFree();
			AddChild(dialog);
			dialog.PopupCentered(new Vector2I(800, 600));
		}

		private void OnImportFromMod()
		{
			var ids = ModManager.Instance.CollectInteractives();
			if (ids.Count == 0)
			{
				GD.Print("No interactive definitions found in mods");
				return;
			}

			var dialog = new AcceptDialog { Title = "Select Definition" };
			dialog.Size = new Vector2I(400, 300);

			var list = new ItemList();
			list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			foreach (var kv in ids)
				list.AddItem(kv.Key);
			list.ItemActivated += idx =>
			{
				var id = list.GetItemText((int)idx);
				var def = InteractiveFactory.Instance.Get(id);
				if (def != null)
				{
					_controller.FromDefinition(def);
					WriteControllerToSidebar();
					UpdateVertexList();
					RebuildHandles();
					UpdateColliderOverlay();
					RefreshJson();
					MarkClean();
				}
				dialog.QueueFree();
			};
			dialog.AddChild(list);
			AddChild(dialog);
			dialog.PopupCentered();
		}

		private void OnSave()
		{
			var dialog = new FileDialog();
			dialog.FileMode = FileDialog.FileModeEnum.SaveFile;
			dialog.AddFilter("*.json", "JSON Files");
			dialog.FileSelected += path =>
			{
				var json = _controller.ToJson();
				var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
				if (file != null)
				{
					file.StoreString(json);
					file.Close();
					MarkClean();
					GD.Print($"Saved to {path}");
				}
				dialog.QueueFree();
			};
			dialog.Canceled += () => dialog.QueueFree();
			AddChild(dialog);
			dialog.PopupCentered(new Vector2I(800, 600));
		}

		private void OnParamChanged(string _ = null) => OnParamChanged();

		protected override void Cleanup()
		{
			foreach (var h in _handles)
				h.QueueFree();
			_handles.Clear();
		}
	}
}
