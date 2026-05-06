using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;
using KKAPI.Studio.SaveLoad;
using UnityEngine;

namespace BlendShapeEditor
{
	[BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
	[BepInDependency(BlendshapeCreator.BlendshapeCreator.GUID, BlendshapeCreator.BlendshapeCreator.Version)]
	[BepInPlugin(GUID, PluginName, Version)]
	public class BlendShapeEditorPlugin : BaseUnityPlugin
	{
		public const string GUID = "org.njaecha.plugins.blendshapeeditor";
		public const string PluginName = "BlendShapeEditor";
		public const string Version = "0.0.2";

		internal new static ManualLogSource Logger;
		public static BlendShapeEditorPlugin Instance;

		public static ConfigEntry<KeyboardShortcut> StudioToggleKey { get; private set; }
		public static ConfigEntry<KeyboardShortcut> UndoKey { get; private set; }
		public static ConfigEntry<KeyboardShortcut> RedoKey { get; private set; }
		public static ConfigEntry<float> DefaultBrushRadius { get; private set; }
		public static ConfigEntry<float> DefaultBrushStrength { get; private set; }
		public static ConfigEntry<L.Language> UILanguage { get; private set; }
		public static ConfigEntry<int> UndoMaxSteps { get; private set; }
		public static ConfigEntry<float> VertexSnapRadius { get; private set; }

		private void Awake()
		{
			Instance = this;
			Logger = base.Logger;

			StudioToggleKey = Config.Bind("General", "Studio Panel Toggle",
				new KeyboardShortcut(KeyCode.S, KeyCode.LeftShift),
				"Toggle panel with hotkey (Shift+S)");

			UndoKey = Config.Bind("Shortcuts", "Undo",
				new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
				"Undo last sculpt action");

			RedoKey = Config.Bind("Shortcuts", "Redo",
				new KeyboardShortcut(KeyCode.Y, KeyCode.LeftControl),
				"Redo last undone sculpt action");

			DefaultBrushRadius = Config.Bind("Blend Shape Editor", "Default Brush Radius", 0.05f,
				new ConfigDescription("Default brush radius", new AcceptableValueRange<float>(0.001f, 1f)));

			DefaultBrushStrength = Config.Bind("Blend Shape Editor", "Default Brush Strength", 0.5f,
				new ConfigDescription("Default brush strength", new AcceptableValueRange<float>(0.01f, 1f)));

			UILanguage = Config.Bind("General", "UI Language", L.Language.English,
				"UI display language");

			UndoMaxSteps = Config.Bind("Blend Shape Editor", "Undo Max Steps", 50,
				new ConfigDescription("Maximum number of undo steps", new AcceptableValueRange<int>(1, 200)));

			VertexSnapRadius = Config.Bind("Blend Shape Editor", "Vertex Snap Radius", 0.02f,
				new ConfigDescription("World-space radius for single-vertex click selection in Gizmo mode", new AcceptableValueRange<float>(0.001f, 0.5f)));

			L.SetLanguage(UILanguage.Value);
			UILanguage.SettingChanged += (s, e) => L.SetLanguage(UILanguage.Value);

			CharacterApi.RegisterExtraBehaviour<BlendShapeEditorCharaController>(GUID);
			StudioSaveLoadApi.RegisterExtraBehaviour<BlendShapeEditorSceneController>(GUID);

			StudioAPI.StudioLoadedChanged += (s, e) => StudioUI.Init();
			MakerAPI.MakerBaseLoaded += (s, e) => MakerUI.Init();
		}
	}
}
