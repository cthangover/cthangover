using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Lights
{
    /// <summary>
    /// Singleton 2D lighting controller that drives a multi-light shader system.
    /// Maintains up to 11 lights: index 0 is the player's lamp, indices 1-10 are
    /// static scene lights. Pushes uniform arrays (positions, colors, radii,
    /// influence) to all registered ShaderMaterials — the ViewBox background/
    /// foreground plus any externally registered materials. The time-of-day
    /// system blends four phases (morning/day/evening/night) via a Vector4 weights
    /// uniform, with smooth transitions between phases. DarkMode overrides time
    /// with full-night weights. Materials are discovered lazily via SceneContextNode
    /// and cached, with a registration system for extra materials (e.g. mod-added
    /// sprites that need lighting). _EnterTree manages singleton enforcement
    /// with duplicate warnings, not assertions, to avoid blocking scene reloads.
    /// </summary>
    public partial class UiLightController : Control, IOnTimeEvent
    {
        public static UiLightController Instance { get; private set; }

        [Export] public Texture2D DefaultDepthMap { get; set; }
        [Export] public Texture2D DefaultAlbedoMap { get; set; }
        [Export] public bool DarkMode { get; set; }
        [Export] public bool UseLight { get; set; } = true;

        private int staticLightCount;

		public Texture2D CurrentDepthMap { get; private set; }
		public Texture2D CurrentAlbedoMap { get; private set; }

		private SceneEventController eventController;
		private ShaderMaterial viewBgMaterial;
		private ShaderMaterial viewFgMaterial;
		private readonly List<ShaderMaterial> extraMaterials = new();

		private readonly Vector2[] lightPositions = new Vector2[11];
		private readonly Color[] lightColors = new Color[11];
		private readonly float[] lightRadii = new float[11];
		private readonly float[] lightInfluence = new float[11];

        private LampBehaviour lampBehaviour;

        public int Priority => 0;

        public bool IsDarkMode
        {
            get => DarkMode;
            set
            {
                DarkMode = value;
                SetTime(GameData.Instance?.Runtime?.Time?.Normalized ?? 0f);
            }
        }

        public bool IsUseLight
        {
            get => UseLight;
            set
            {
                UseLight = value;
                ChangeUseTime(UseLight);
            }
        }

		public override void _EnterTree()
		{
			if (Instance != null && GodotObject.IsInstanceValid(Instance))
			{
				var scene = GetTree()?.CurrentScene?.Name ?? "?";
				var existingPath = Instance.GetPath().ToString();
				var myPath = GetPath().ToString();
				GameLogger.Log("DUPLICATE", $"UiLightController._EnterTree: Instance ALREADY SET by '{existingPath}', overwriting with duplicate at '{myPath}' on scene '{scene}'", LogLevel.Error);
			}
			Instance = this;
		}

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
            eventController?.RemoveTimerTickEventListener(this);
        }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            UpdateLampLightParams();
            lightPositions[0] = new Vector2(-1000f, -1000f);

            SetupDepthMap(DefaultDepthMap);
            SetupAlbedoMap(DefaultAlbedoMap);

            eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
            eventController?.AddTimerTickEventListener(this);

            GameLogger.Log("LIGHT", $"LightsCtrl._Ready: eventCtrl={(eventController != null ? "OK" : "NULL")}, DarkMode={DarkMode}, UseLight={UseLight}", LogLevel.Debug);

            CallDeferred(nameof(SetupTime));
        }

        public void UpdateLampLightParams()
        {
            var lightParams = LampBehaviour.GetLightParams();
            lightColors[0] = lightParams.Item1;
            lightRadii[0] = lightParams.Item2;
            lightInfluence[0] = lightParams.Item3;
        }

        public void ClearStaticLights()
        {
            for (int i = 1; i < 11; i++)
            {
                lightPositions[i] = new Vector2(-1000f, -1000f);
                lightRadii[i] = 0f;
                lightInfluence[i] = 0f;
                lightColors[i] = Colors.White;
            }

            staticLightCount = 0;
            UpdateShaders();

            GameLogger.Log("LIGHT", "LightsCtrl.ClearStaticLights: all static lights cleared");
        }

        public void SetStaticLights(string json)
        {
            ClearStaticLights();

            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var lights = JsonSerializer.Deserialize<List<LightDef>>(json, options);

                if (lights == null || lights.Count == 0)
                    return;

                int count = Mathf.Min(lights.Count, 10);
                var vpSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1024);
                for (int i = 0; i < count; i++)
                {
                    lightPositions[i + 1] = lights[i].ToPixelPos(vpSize);
                    lightRadii[i + 1] = lights[i].Radius;
                    lightInfluence[i + 1] = lights[i].Influence;
                    lightColors[i + 1] = lights[i].ToColor();
                }

                staticLightCount = count+1;
                UpdateShaders();

                GameLogger.Log("LIGHT", $"LightsCtrl.SetStaticLights: parsed {lights.Count} light(s), applied {count} static lights");
            }

            catch (System.Exception ex)
            {
                GameLogger.Log("LIGHT", $"LightsCtrl.SetStaticLights: JSON parse error: {ex.Message}", LogLevel.Error);
            }
        }

        private void SetupTime()
        {
	        SetTime(GameData.Instance?.Runtime?.Time?.Normalized ?? 0f);
        }

        public void OnTimerTick()
        {
            if (lampBehaviour == null)
                lampBehaviour = FindLampChild();

            if (lampBehaviour != null)
            {
                var time = GameData.Instance?.Runtime?.Time;
                int hours = time?.Hours ?? 0;
                bool newVis = DarkMode || !(hours >= 6 && hours < 22);
                lampBehaviour.Visible = newVis;
            }

            SetTime(GameData.Instance?.Runtime?.Time?.Normalized ?? 0f);
        }

		public void ChangeUseTime(bool value)
		{
			ForEachMaterial(m => m.SetShaderParameter("use_time", value));
		}

		public void RegisterMaterial(ShaderMaterial material)
		{
			if (material == null || extraMaterials.Contains(material))
				return;
			extraMaterials.Add(material);
			PushAllParams(material);
		}

		public void UnregisterMaterial(ShaderMaterial material)
		{
			extraMaterials.Remove(material);
		}

		private void PushAllParams(ShaderMaterial material)
		{
			var weights = DarkMode
				? new Vector4(0, 0, 0, 1)
				: CalculateTimeWeights(GameData.Instance?.Runtime?.Time?.Normalized ?? 0f);
			material.SetShaderParameter("time_weights", weights);
			material.SetShaderParameter("use_time", UseLight);

			var posArray = new Godot.Collections.Array<Vector2>();
			var colArray = new Godot.Collections.Array<Color>();
			var radArray = new Godot.Collections.Array<float>();
			var infArray = new Godot.Collections.Array<float>();
			for (int i = 0; i < 11; i++)
			{
				posArray.Add(lightPositions[i]);
				colArray.Add(lightColors[i]);
				radArray.Add(lightRadii[i]);
				infArray.Add(lightInfluence[i]);
			}

			int count = 0;
			if (lightPositions[0].X > -100)
				count++;
			count += staticLightCount;

			material.SetShaderParameter("light_count", count);
			material.SetShaderParameter("light_positions", posArray);
			material.SetShaderParameter("light_colors", colArray);
			material.SetShaderParameter("light_radii", radArray);
			material.SetShaderParameter("light_influence", infArray);
			material.SetShaderParameter("light_radius_scale", 1.0f);
		}

        public void SetupDepthMap(Texture2D depthMap)
        {
            CurrentDepthMap = depthMap;
            ForEachMaterial(m => m.SetShaderParameter("depth_mask", CurrentDepthMap));
        }

        public void SetupAlbedoMap(Texture2D albedoMap)
        {
            CurrentAlbedoMap = albedoMap;
            ForEachMaterial(m => m.SetShaderParameter("albedo_map", CurrentAlbedoMap));
        }

        public void UpdateShaders()
        {
            int count = 0;
            if (lightPositions[0].X > -100)
                count++;
            count += staticLightCount;

            SetShaderArrays(count);
            ChangeUseTime(UseLight);
        }

		public void UpdateLightPos(Vector2 position)
		{
			lightPositions[0] = position;
			int count = 0;
			if (lightPositions[0].X > -100)
				count++;
			count += staticLightCount;

			SetShaderArrays(count);
			ChangeUseTime(UseLight);
		}

		public void SetPlayerLight(Vector2 position, float radius, float influence)
		{
			lightPositions[0] = position;
			lightRadii[0] = radius;
			lightInfluence[0] = influence;
		}

        public void SetTime(float normalizedHour)
        {
            if (DarkMode)
            {
                var night = new Vector4(0, 0, 0, 1);
                ForEachMaterial(m => m.SetShaderParameter("time_weights", night));
            }
            else
            {
                var weights = CalculateTimeWeights(normalizedHour);
                ForEachMaterial(m => m.SetShaderParameter("time_weights", weights));
            }
            UpdateShaders();
        }

        private void SetShaderArrays(int count)
        {
            var posArray = new Godot.Collections.Array<Vector2>();
            var colArray = new Godot.Collections.Array<Color>();
            var radArray = new Godot.Collections.Array<float>();
            var infArray = new Godot.Collections.Array<float>();

            for (int i = 0; i < 11; i++)
            {
                posArray.Add(lightPositions[i]);
                colArray.Add(lightColors[i]);
                radArray.Add(lightRadii[i]);
                infArray.Add(lightInfluence[i]);
            }

            ForEachMaterial(m =>
            {
                m.SetShaderParameter("light_count", count);
                m.SetShaderParameter("light_positions", posArray);
                m.SetShaderParameter("light_colors", colArray);
                m.SetShaderParameter("light_radii", radArray);
                m.SetShaderParameter("light_influence", infArray);
                m.SetShaderParameter("light_radius_scale", 1.0f);
            });
        }

		private void ForEachMaterial(System.Action<ShaderMaterial> action)
		{
			if (viewBgMaterial == null || viewFgMaterial == null)
			{
				var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
				if (viewBox != null)
				{
					viewBgMaterial ??= viewBox.Background?.Material as ShaderMaterial;
					viewFgMaterial ??= viewBox.Foreground?.Material as ShaderMaterial;
				}
			}

			if (viewBgMaterial != null) action(viewBgMaterial);
			if (viewFgMaterial != null) action(viewFgMaterial);

			for (int i = extraMaterials.Count - 1; i >= 0; i--)
			{
				if (extraMaterials[i] == null)
					extraMaterials.RemoveAt(i);
				else
					action(extraMaterials[i]);
			}

			var total = (viewBgMaterial != null ? 1 : 0) + (viewFgMaterial != null ? 1 : 0) + extraMaterials.Count;
			if (total == 0)
				GameLogger.Log("LIGHT", "LightsCtrl.ForEachMaterial: NO materials found to push params to!", LogLevel.Error);
		}

        private LampBehaviour FindLampChild()
        {
            return SceneContextNode.FindNode<LampBehaviour>("Lamp");
        }

        //
        // 06:00-10:00 - Morning
        // 10:00-18:00 - Day
        // 18:00-22:00 - Evening
        // 22:00-06:00 - Night
        //
        private static Vector4 CalculateTimeWeights(float hour)
        {
            float night   = 0.0f;
            float morning = 0.0f;
            float day     = 0.0f;
            float evening = 0.0f;

            if (hour >= 22.0f || hour < 4.0f)
            {
                night = 1.0f;
            }
            else if (hour >= 4.0f && hour < 6.0f)
            {
                float t = (hour - 4.0f) / 2.0f;
                night = 1.0f - t;
                morning = t;
            }
            else if (hour >= 6.0f && hour < 9.0f)
            {
                morning = 1.0f;
            }
            else if (hour >= 9.0f && hour < 11.0f)
            {
                float t = (hour - 9.0f) / 2.0f;
                morning = 1.0f - t;
                day = t;
            }
            else if (hour >= 11.0f && hour < 17.0f)
            {
                day = 1.0f;
            }
            else if (hour >= 17.0f && hour < 19.0f)
            {
                float t = (hour - 17.0f) / 2.0f;
                day = 1.0f - t;
                evening = t;
            }
            else if (hour >= 19.0f && hour < 20.0f)
            {
                evening = 1.0f;
            }
            else if (hour >= 20.0f && hour < 22.0f)
            {
                float t = (hour - 20.0f) / 2.0f;
                evening = 1.0f - t;
                night = t;
            }

            return new Vector4(morning, day, evening, night);
        }
    }
}
