using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Lights;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
	public partial class LightEditorWindow : Window
	{
		public static LightEditorWindow Open()
		{
			var window = new LightEditorWindow();
			var tree = Godot.Engine.GetMainLoop() as SceneTree;
			tree?.Root.AddChild(window);
			window.PopupCentered(new Vector2I(1200, 750));
			return window;
		}

		private LightEditorController controller;

		private ItemList backgroundList;
		private VBoxContainer lightListPanel;
		private Button addLightBtn;
		private TextureRect preview;
		private ShaderMaterial previewMaterial;

		private LineEdit radiusEdit;
		private LineEdit influenceEdit;
		private ColorPickerButton colorPicker;
		private TextEdit jsonEdit;

		private readonly List<LightEditorHandle> handles = new();
		private int selectedIndex = -1;

		private readonly List<string> bgIds = new();

        public LightEditorWindow()
        {
            Title = TranslationServer.Translate("tools/light_editor/title");
			Unresizable = false;
			Size = new Vector2I(1200, 750);
			CloseRequested += QueueFree;

			controller = new LightEditorController();
			BuildUI();
			LoadBackgroundList();
		}

		public override void _ExitTree()
		{
			foreach (var h in handles)
				h.QueueFree();
			handles.Clear();
		}

		private Vector2 PreviewSize()
		{
			var sz = preview.Size;
			if (sz.X <= 0 || sz.Y <= 0)
				sz = new Vector2(1920, 1080);
			return sz;
		}

		private void RefreshAll()
		{
			RepositionHandles();
			controller.MarkDirty();
			controller.UpdatePreview(previewMaterial, preview.GlobalPosition, PreviewSize());
			UpdateLightList();
			UpdateJsonEdit();
		}

		private void BuildUI()
		{
			var outerVBox = new VBoxContainer();
			outerVBox.AnchorRight = 1f;
			outerVBox.AnchorBottom = 1f;
			outerVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			outerVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			AddChild(outerVBox);

			var mainHBox = new HBoxContainer();
			mainHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			mainHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			outerVBox.AddChild(mainHBox);

			var sidebar = new PanelContainer();
			sidebar.CustomMinimumSize = new Vector2(280, 0);
			sidebar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			mainHBox.AddChild(sidebar);

			var sidebarScroll = new ScrollContainer();
			sidebarScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			sidebar.AddChild(sidebarScroll);

			var sidebarVBox = new VBoxContainer();
			sidebarScroll.AddChild(sidebarVBox);

            sidebarVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/background") });

            backgroundList = new ItemList();
            backgroundList.CustomMinimumSize = new Vector2(0, 140);
            backgroundList.ItemSelected += OnBackgroundSelected;
            sidebarVBox.AddChild(backgroundList);

            sidebarVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/static_lights") });

            lightListPanel = new VBoxContainer();
            sidebarVBox.AddChild(lightListPanel);

            addLightBtn = new Button { Text = TranslationServer.Translate("tools/light_editor/add_light") };
            addLightBtn.Pressed += OnAddLight;
            sidebarVBox.AddChild(addLightBtn);

            sidebarVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/selected_props") });

            var radiusHBox = new HBoxContainer();
            radiusHBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/radius") });
            radiusEdit = new LineEdit { Text = "300" };
            radiusEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            radiusEdit.TextChanged += OnRadiusChanged;
            radiusHBox.AddChild(radiusEdit);
            sidebarVBox.AddChild(radiusHBox);

            var infHBox = new HBoxContainer();
            infHBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/influence") });
            influenceEdit = new LineEdit { Text = "1" };
            influenceEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            influenceEdit.TextChanged += OnInfluenceChanged;
            infHBox.AddChild(influenceEdit);
            sidebarVBox.AddChild(infHBox);

            var colorHBox = new HBoxContainer();
            colorHBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/color") });
            colorPicker = new ColorPickerButton { Color = Colors.Yellow };
            colorPicker.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            colorPicker.ColorChanged += OnColorChanged;
            colorHBox.AddChild(colorPicker);
            sidebarVBox.AddChild(colorHBox);

            var importBtn = new Button { Text = TranslationServer.Translate("tools/light_editor/import_json") };
            importBtn.Pressed += OnImport;
            sidebarVBox.AddChild(importBtn);

            sidebarVBox.AddChild(new Label { Text = TranslationServer.Translate("tools/light_editor/norm_coords") });

			var previewContainer = new PanelContainer();
			previewContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			previewContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			mainHBox.AddChild(previewContainer);

			preview = new TextureRect();
			preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			preview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			preview.MouseFilter = Control.MouseFilterEnum.Stop;
			preview.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			preview.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			previewContainer.AddChild(preview);

			previewMaterial = controller.CreatePreviewMaterial();
			preview.Material = previewMaterial;

			jsonEdit = new TextEdit();
			jsonEdit.CustomMinimumSize = new Vector2(0, 70);
			jsonEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			jsonEdit.WrapMode = TextEdit.LineWrappingMode.Boundary;
			jsonEdit.TextChanged += OnJsonEditChanged;
			outerVBox.AddChild(jsonEdit);
		}

		private void OnJsonEditChanged()
		{
		}

		private void UpdateJsonEdit()
		{
			var json = controller.ExportLightsJson();
			if (jsonEdit.Text != json)
				jsonEdit.Text = json;
		}

		private void LoadBackgroundList()
		{
			bgIds.Clear();
			backgroundList.Clear();

			var files = ModManager.Instance.CollectFileList("backgrounds");
			foreach (var kvp in files)
			{
				var id = kvp.Key;
				if (id.EndsWith(".png") || id.EndsWith(".jpg") || id.EndsWith(".jpeg") || id.EndsWith(".webp"))
				{
					var name = id.Substring(0, id.LastIndexOf('.'));
					bgIds.Add(name);
					backgroundList.AddItem(name);
				}
			}
		}

		private void OnBackgroundSelected(long index)
		{
			var bgId = bgIds[(int)index];
			var tex = BackgroundFactory.Instance.Get(bgId);
			preview.Texture = tex;
			controller.SetBackgroundForPreview(tex, previewMaterial);

			var depthTex = BackgroundFactory.Instance.Get(bgId + "_depth");
			if (depthTex != null)
				controller.SetDepthForPreview(depthTex, previewMaterial);

			var albedoTex = BackgroundFactory.Instance.Get(bgId + "_albedo");
			if (albedoTex != null)
				controller.SetAlbedoForPreview(albedoTex, previewMaterial);
		}

		private void OnAddLight()
		{
			if (handles.Count >= 10)
				return;

			var light = new LightDef
			{
				X = 0.4f,
				Y = 0.3f,
				Radius = 300f,
				Influence = 1f,
				ColorHex = "#ffff00"
			};

			controller.AddLight(light);
			AddHandle(light, controller.Lights.Count - 1);
			RefreshAll();
		}

		private void AddHandle(LightDef light, int index)
		{
			var handle = new LightEditorHandle(light, index);
			handle.DragUpdate += globalCenter =>
			{
				var localCenter = globalCenter - preview.GlobalPosition;
				var currentSize = PreviewSize();
				light.FromPixelPos(localCenter, currentSize);
				RefreshAll();
			};
			handle.Clicked += () => SelectHandle(index);
			preview.AddChild(handle);
			handles.Add(handle);
			RepositionHandle(handle, light);
		}

		private void RepositionHandle(LightEditorHandle handle, LightDef light)
		{
			var size = PreviewSize();
			handle.GlobalPosition = preview.GlobalPosition
				+ new Vector2(light.X * size.X - 12, light.Y * size.Y - 12);
		}

		private void RepositionHandles()
		{
			for (int i = 0; i < handles.Count && i < controller.Lights.Count; i++)
				RepositionHandle(handles[i], controller.Lights[i]);
		}

		private void SelectHandle(int index)
		{
			selectedIndex = index;
			if (index < 0 || index >= controller.Lights.Count)
			{
				radiusEdit.Text = "300";
				influenceEdit.Text = "1";
				colorPicker.Color = Colors.Yellow;
				return;
			}

			var light = controller.Lights[index];
			radiusEdit.Text = light.Radius.ToString("F0");
			influenceEdit.Text = light.Influence.ToString("F2");
			colorPicker.Color = light.ToColor();
		}

		private void OnRadiusChanged(string text)
		{
			if (selectedIndex < 0 || selectedIndex >= controller.Lights.Count)
				return;
			if (float.TryParse(text, out var val))
			{
				controller.Lights[selectedIndex].Radius = val;
				RefreshAll();
			}
		}

		private void OnInfluenceChanged(string text)
		{
			if (selectedIndex < 0 || selectedIndex >= controller.Lights.Count)
				return;
			if (float.TryParse(text, out var val))
			{
				controller.Lights[selectedIndex].Influence = val;
				RefreshAll();
			}
		}

		private void OnColorChanged(Color color)
		{
			if (selectedIndex < 0 || selectedIndex >= controller.Lights.Count)
				return;
			controller.Lights[selectedIndex].ColorHex = color.ToHtml();
			RefreshAll();
		}

		private void UpdateLightList()
		{
			foreach (var child in lightListPanel.GetChildren())
				child.QueueFree();

			for (int i = 0; i < controller.Lights.Count; i++)
			{
				var light = controller.Lights[i];
				var hBox = new HBoxContainer();
				var label = new Label
				{
					Text = string.Format(TranslationServer.Translate("tools/light_editor/light_n"), i + 1, light.X, light.Y),
					SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
				};
				hBox.AddChild(label);

				var selBtn = new Button { Text = TranslationServer.Translate("tools/light_editor/btn_select") };
				int idx = i;
				selBtn.Pressed += () => SelectHandle(idx);
				hBox.AddChild(selBtn);

				var delBtn = new Button { Text = TranslationServer.Translate("tools/light_editor/btn_delete") };
				delBtn.Pressed += () => RemoveLight(idx);
				hBox.AddChild(delBtn);

				lightListPanel.AddChild(hBox);
			}

			addLightBtn.Disabled = controller.Lights.Count >= 10;
		}

		private void RemoveLight(int index)
		{
			if (index < 0 || index >= controller.Lights.Count)
				return;

			controller.RemoveLight(index);

			if (index < handles.Count)
			{
				handles[index].QueueFree();
				handles.RemoveAt(index);
			}

			for (int i = 0; i < handles.Count; i++)
				handles[i].LightIndex = i;

			if (selectedIndex == index)
				selectedIndex = -1;
			else if (selectedIndex > index)
				selectedIndex--;

			RefreshAll();
		}

		private void OnImport()
		{
			var json = jsonEdit.Text;
			if (string.IsNullOrWhiteSpace(json))
				return;

			try
			{
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var lights = JsonSerializer.Deserialize<List<LightDef>>(json, options);
				if (lights == null || lights.Count == 0)
					return;

				foreach (var h in handles)
					h.QueueFree();
				handles.Clear();
				controller.Clear();

				foreach (var light in lights)
				{
					controller.AddLight(light);
					AddHandle(light, controller.Lights.Count - 1);
				}

				selectedIndex = -1;
				RefreshAll();
			}
			catch
			{
			}
		}
	}
}
