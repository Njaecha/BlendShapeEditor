using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI.Chara;
using KKAPI.Studio;
using KKAPI.Studio.SaveLoad;
using UnityEngine;

namespace KKShapeEditor
{
	[BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
	[BepInPlugin(GUID, PluginName, Version)]
	public class ShapeEditorPlugin : BaseUnityPlugin
	{
		public const string GUID = "com.ghostendsky.kkshapeeditor";
		public const string PluginName = "KKShapeEditor";
		public const string Version = "1.0.0";

		internal new static ManualLogSource Logger;
		internal static ShapeEditorPlugin Instance;

		internal static bool IsStudio { get; private set; }
		public static ConfigEntry<KeyboardShortcut> StudioToggleKey { get; private set; }
		public static ConfigEntry<float> DefaultBrushRadius { get; private set; }
		public static ConfigEntry<float> DefaultBrushStrength { get; private set; }
		public static ConfigEntry<L.Language> UILanguage { get; private set; }
		public static ConfigEntry<int> UndoMaxSteps { get; private set; }

		private void Awake()
		{
			Instance = this;
			Logger = base.Logger;
			IsStudio = StudioAPI.InsideStudio;

			StudioToggleKey = base.Config.Bind<KeyboardShortcut>("General", "Studio Panel Toggle",
				new KeyboardShortcut(KeyCode.S, new KeyCode[] { KeyCode.LeftShift }),
				"Toggle panel with hotkey (Shift+S)");

			DefaultBrushRadius = base.Config.Bind<float>("Shape Editor", "Default Brush Radius", 0.05f,
				new ConfigDescription("Default brush radius", new AcceptableValueRange<float>(0.001f, 1f)));

			DefaultBrushStrength = base.Config.Bind<float>("Shape Editor", "Default Brush Strength", 0.5f,
				new ConfigDescription("Default brush strength", new AcceptableValueRange<float>(0.01f, 1f)));

			UILanguage = base.Config.Bind<L.Language>("General", "UI Language", L.Language.English,
				"UI display language");

			UndoMaxSteps = base.Config.Bind<int>("Shape Editor", "Undo Max Steps", 50,
				new ConfigDescription("Maximum number of undo steps", new AcceptableValueRange<int>(1, 200)));

			L.SetLanguage(UILanguage.Value);
			UILanguage.SettingChanged += (s, e) => L.SetLanguage(UILanguage.Value);

			CharacterApi.RegisterExtraBehaviour<ShapeEditorController>("com.ghostendsky.kkshapeeditor");
			StudioSaveLoadApi.RegisterExtraBehaviour<ShapeEditorSceneController>("com.ghostendsky.kkshapeeditor");

			MakerUI.Init();
			StudioUI.Init();

			Logger.LogInfo("KKShapeEditor v1.0.0 loaded");
		}
	}
}
