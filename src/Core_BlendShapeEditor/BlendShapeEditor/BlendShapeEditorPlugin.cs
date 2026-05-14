using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Illusion.Component.UI;
using KK_Plugins.MaterialEditor;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;
using KKAPI.Studio.SaveLoad;
using UnityEngine;

namespace BlendShapeEditor
{
	[BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
	[BepInDependency(BlendshapeCreator.BlendshapeCreator.GUID, BlendshapeCreator.BlendshapeCreator.Version)]
	[BepInDependency(MaterialEditorPlugin.PluginGUID)]
	[BepInPlugin(GUID, PluginName, Version)]
	public class BlendShapeEditorPlugin : BaseUnityPlugin
	{
		public const string GUID = "org.njaecha.plugins.blendshapeeditor";
		public const string PluginName = "BlendShapeEditor";
		public const string Version = "0.0.3";

		internal new static ManualLogSource Logger;
		public static BlendShapeEditorPlugin Instance;

		public static ConfigEntry<float> DefaultBrushRadius { get; private set; }
		public static ConfigEntry<float> BrushRadiusScrollMod { get; private set; }
		public static ConfigEntry<float> DefaultBrushStrength { get; private set; }
		public static ConfigEntry<float> BrushStrengthScrollMod { get; private set; }
		public static ConfigEntry<float> GizmoSizeScrollMod { get; private set; }
		public static ConfigEntry<float> GizmoSoftSelectionScrollMod { get; private set; }
		public static ConfigEntry<L.Language> UILanguage { get; private set; }
		public static ConfigEntry<int> UndoMaxSteps { get; private set; }

		#region Colors
		
		public static ConfigEntry<Color> BrushColorMove	{get ; private set;}
		public static ConfigEntry<Color> BrushColorSmooth {get ; private set;}
		public static ConfigEntry<Color> BrushColorInflate {get ; private set;}
		
		public static ConfigEntry<Color> VertexColorDefault {get ; private set;}
		public static ConfigEntry<Color> VertexColorSelected {get ; private set;}
		public static ConfigEntry<Color> VertexColorHover {get ; private set;}
		
		public static ConfigEntry<Color> WireColorDefault {get ; private set;}
		public static ConfigEntry<Color> WireColorSelected {get ; private set;}
		public static ConfigEntry<Color> WireColorMirror {get ; private set;}

		public static ConfigEntry<Color> WeightGradientStop0 { get; private set; }
		public static ConfigEntry<Color> WeightGradientStop1 { get; private set; }
		public static ConfigEntry<Color> WeightGradientStop2 { get; private set; }
		public static ConfigEntry<Color> WeightGradientStop3 { get; private set; }
		public static ConfigEntry<Color> WeightMirrorGradientStop0 { get; private set; }
		public static ConfigEntry<Color> WeightMirrorGradientStop1 { get; private set; }
		public static ConfigEntry<Color> WeightMirrorGradientStop2 { get; private set; }
		public static ConfigEntry<Color> WeightMirrorGradientStop3 { get; private set; }

		#endregion
		
		#region Keyboard Shortcuts

		public static ConfigEntry<KeyboardShortcut> KeyStudioToggle { get; private set; }
		
		//global
		public static ConfigEntry<KeyboardShortcut> KeyUndo { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyRedo { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyMode { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyBake { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyCycleFalloff { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyMirror { get; private set; }
		
		public static ConfigEntry<KeyboardShortcut> KeyLayerDown { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerUp { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerNext { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerPrevious { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerRemove { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerNew { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerOpacityUp { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyLayerOpacityDown { get; private set; }
		
		public static ConfigEntry<KeyboardShortcut> KeyBrushMove { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyBrushSmooth { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyBrushInflate { get; private set; }
		
		public static ConfigEntry<KeyboardShortcut> KeyGizmoTranslate { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyGizmoRotate { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyGizmoScale { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyGizmoCycleGizmoSpace { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyGizmoSoftSelection { get; private set; }
		public static ConfigEntry<KeyboardShortcut> KeyGizmoCycleSoftMode { get; private set; }
		

		#endregion

		private void Awake()
		{
			Instance = this;
			Logger = base.Logger;

			#region Colors

			BrushColorMove = Config.Bind("Colors - Brush", "Move", Color.yellow, "Color for the brush in Move mode");
			BrushColorSmooth = Config.Bind("Colors - Brush", "Smooth", Color.green, "Color for the brush in Smooth mode");
			BrushColorInflate = Config.Bind("Colors - Brush", "Inflate", Color.magenta, "Color for the brush in Inflate mode");

			VertexColorDefault = Config.Bind("Colors - Mesh", "Vertex Default", Color.grey, "Default color for vertices");
			VertexColorHover = Config.Bind("Colors - Mesh", "Vertex Hovered", Color.yellow, "Hovered color for vertices");
			VertexColorSelected = Config.Bind("Colors - Mesh", "Vertex Selected", Color.green, "Selected color for vertices");
			
			WireColorDefault = Config.Bind("Colors - Mesh", "Wire Default", new Color(0,0,0,0.3f), "Default color for edges");
			WireColorSelected = Config.Bind("Colors - Mesh", "Wire Selected", new Color(0,200,80,200f), "Selected color for edges");
			WireColorMirror = Config.Bind("Colors - Mesh", "Wire Mirrored", new Color(0f, 0.6f, 0.8f, 0.78f), "Color for edges of mirrored vertices when symmetry is enabled");

			WeightGradientStop0 = Config.Bind("Colors - Weight Gradient", "Primary Stop 0 (weight 0.00)", new Color(0f, 0f, 1f), "Weight falloff gradient stop at weight 0.0 (low). Used for brush dots/edges and gizmo soft selection dots/edges.");
			WeightGradientStop1 = Config.Bind("Colors - Weight Gradient", "Primary Stop 1 (weight 0.33)", new Color(0f, 1f, 0f), "Weight falloff gradient stop at weight 0.33.");
			WeightGradientStop2 = Config.Bind("Colors - Weight Gradient", "Primary Stop 2 (weight 0.66)", new Color(1f, 1f, 0f), "Weight falloff gradient stop at weight 0.66.");
			WeightGradientStop3 = Config.Bind("Colors - Weight Gradient", "Primary Stop 3 (weight 1.00)", new Color(1f, 0f, 0f), "Weight falloff gradient stop at weight 1.0 (high).");
			WeightMirrorGradientStop0 = Config.Bind("Colors - Weight Gradient", "Mirror Stop 0 (weight 0.00)", new Color(0.1f, 0.1f, 0.5f), "Mirror-side weight falloff gradient stop at weight 0.0 (low).");
			WeightMirrorGradientStop1 = Config.Bind("Colors - Weight Gradient", "Mirror Stop 1 (weight 0.33)", new Color(0.1f, 0.6f, 0.7f), "Mirror-side weight falloff gradient stop at weight 0.33.");
			WeightMirrorGradientStop2 = Config.Bind("Colors - Weight Gradient", "Mirror Stop 2 (weight 0.66)", new Color(0.4f, 0.8f, 1f), "Mirror-side weight falloff gradient stop at weight 0.66.");
			WeightMirrorGradientStop3 = Config.Bind("Colors - Weight Gradient", "Mirror Stop 3 (weight 1.00)", new Color(1f, 1f, 1f), "Mirror-side weight falloff gradient stop at weight 1.0 (high).");
			EventHandler rebuildGradients = (s, e) => ShapePaintOverlay.RebuildGradients();
			WeightGradientStop0.SettingChanged += rebuildGradients;
			WeightGradientStop1.SettingChanged += rebuildGradients;
			WeightGradientStop2.SettingChanged += rebuildGradients;
			WeightGradientStop3.SettingChanged += rebuildGradients;
			WeightMirrorGradientStop0.SettingChanged += rebuildGradients;
			WeightMirrorGradientStop1.SettingChanged += rebuildGradients;
			WeightMirrorGradientStop2.SettingChanged += rebuildGradients;
			WeightMirrorGradientStop3.SettingChanged += rebuildGradients;
			ShapePaintOverlay.RebuildGradients();
			#endregion


			#region Keyboard Shortcuts Assignment

			KeyStudioToggle = Config.Bind("Shortcuts - General", "Plugin Toggle",
				new KeyboardShortcut(KeyCode.S, KeyCode.LeftShift),
				"Toggle panel with hotkey (Shift+S)");
			
			KeyUndo = Config.Bind("Shortcuts - General", "Undo",
				new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
				"Undo last sculpt action");

			KeyRedo = Config.Bind("Shortcuts - General", "Redo",
				new KeyboardShortcut(KeyCode.Y, KeyCode.LeftControl),
				"Redo last undone sculpt action");
			
			KeyMode = Config.Bind("Shortcuts - General", "Mode",
				new KeyboardShortcut(KeyCode.Tab),
				"Switch between brush and gizmo mode");
			
			KeyBake = Config.Bind("Shortcuts - General", "Bake",
				new KeyboardShortcut(KeyCode.B, KeyCode.LeftControl),
				"Bake shape edit to Blendshape");
			
			KeyMirror = Config.Bind("Shortcuts - General", "Mirror",
				new KeyboardShortcut(KeyCode.M, KeyCode.LeftControl),
				"Enable Mirroring");
			
			KeyCycleFalloff = Config.Bind("Shortcuts - General", "Cycle Falloff",
				new KeyboardShortcut(KeyCode.S, KeyCode.LeftShift),
				"Cycle Falloff Mode (Linear/Smooth/Sharp)");
			
			KeyLayerDown = Config.Bind("Shortcuts - Layer", "Move Down",
				new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl),
				"Move layer down");
			
			KeyLayerUp = Config.Bind("Shortcuts - Layer", "Move Up",
				new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl),
				"Move layer up");

			KeyLayerNext = Config.Bind("Shortcuts - Layer", "Next",
				new KeyboardShortcut(KeyCode.DownArrow),
				"Select next layer");
			
			KeyLayerPrevious = Config.Bind("Shortcuts - Layer", "Previous",
				new KeyboardShortcut(KeyCode.UpArrow),
				"Select previous layer");
			
			KeyLayerRemove = Config.Bind("Shortcuts - Layer", "Remove",
				new KeyboardShortcut(KeyCode.Backspace, KeyCode.LeftControl),
				"Remove layer");
			
			KeyLayerNew = Config.Bind("Shortcuts - Layer", "Add",
				new KeyboardShortcut(KeyCode.N, KeyCode.LeftControl),
				"Add new layer");
			
			KeyLayerOpacityUp = Config.Bind("Shortcuts - Layer", "Opacity Increase",
				new KeyboardShortcut(KeyCode.RightArrow),
				"Increase layer Opacity");
			
			KeyLayerOpacityDown = Config.Bind("Shortcuts - Layer", "Opacity Decrease",
				new KeyboardShortcut(KeyCode.LeftArrow),
				"Decrease layer Opacity");
			
			KeyBrushMove = Config.Bind("Shortcuts - Brush", "Move",
				new KeyboardShortcut(KeyCode.W),
				"Use Move brush");
			
			KeyBrushSmooth = Config.Bind("Shortcuts - Brush", "Smooth",
				new KeyboardShortcut(KeyCode.E),
				"Use Move brush");
			
			KeyBrushInflate = Config.Bind("Shortcuts - Brush", "Inflate",
				new KeyboardShortcut(KeyCode.R),
				"Use Inflate brush");
			
			KeyGizmoTranslate = Config.Bind("Shortcuts - Gizmo", "Translate",
				new KeyboardShortcut(KeyCode.W),
				"Use Translate gizmo");
			
			KeyGizmoRotate = Config.Bind("Shortcuts - Gizmo", "Rotate",
				new KeyboardShortcut(KeyCode.E),
				"Use Rotate gizmo");
			
			KeyGizmoScale = Config.Bind("Shortcuts - Gizmo", "Scale",
				new KeyboardShortcut(KeyCode.R),
				"Use Scale gizmo");
			
			KeyGizmoCycleGizmoSpace = Config.Bind("Shortcuts - Gizmo", "Cycle Gizmo Space",
				new KeyboardShortcut(KeyCode.None),
				"Cycle gizmo reference space (World/Object/Normal)");
			
			KeyGizmoSoftSelection = Config.Bind("Shortcuts - Gizmo", "Soft Selection",
				new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl),
				"Toggle Soft selection");
			
			KeyGizmoCycleSoftMode = Config.Bind("Shortcuts - Gizmo", "Cycle Soft Mode",
				new KeyboardShortcut(KeyCode.None),
				"Cycle soft selection mode (Volume/Surface)");
			
			#endregion
			

			BrushRadiusScrollMod = Config.Bind("Shortcuts", "Brush Radius Scroll Speed", 1.0f,
				new ConfigDescription(
					"Modifier that controls how fast the Brush Radius changes when scrolling the mouse wheel.",
					new AcceptableValueRange<float>(0.1f, 3f)));
			
			BrushStrengthScrollMod = Config.Bind("Shortcuts", "Brush Strength Scroll Speed", 2.0f,
				new ConfigDescription(
					"Modifier that controls how fast the Brush Strength changes when scrolling the mouse wheel.",
					new AcceptableValueRange<float>(0.1f, 3f)));
			
			GizmoSizeScrollMod = Config.Bind("Shortcuts", "Gizmo Size Scroll Speed", 1.0f,
				new ConfigDescription(
					"Modifier that controls how fast the Gizmo Size changes when scrolling the mouse wheel.",
					new AcceptableValueRange<float>(0.1f, 3f)));
			
			GizmoSoftSelectionScrollMod = Config.Bind("Shortcuts", "Soft Selection Scroll Speed", 1.0f,
				new ConfigDescription(
					"Modifier that controls how fast the Gizmo Soft Selection changes when scrolling the mouse wheel.",
					new AcceptableValueRange<float>(0.1f, 3f)));

			DefaultBrushRadius = Config.Bind("Blend Shape Editor", "Default Brush Radius", 0.05f,
				new ConfigDescription("Default brush radius", new AcceptableValueRange<float>(0.001f, 1f)));

			DefaultBrushStrength = Config.Bind("Blend Shape Editor", "Default Brush Strength", 0.5f,
				new ConfigDescription("Default brush strength", new AcceptableValueRange<float>(0.01f, 1f)));

			UILanguage = Config.Bind("General", "UI Language", L.Language.English,
				"UI display language");

			UndoMaxSteps = Config.Bind("Blend Shape Editor", "Undo Max Steps", 50,
				new ConfigDescription("Maximum number of undo steps", new AcceptableValueRange<int>(1, 200)));

			L.SetLanguage(UILanguage.Value);
			UILanguage.SettingChanged += (s, e) => L.SetLanguage(UILanguage.Value);

			Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
			
			CharacterApi.RegisterExtraBehaviour<BlendShapeEditorCharaController>(GUID);
			StudioSaveLoadApi.RegisterExtraBehaviour<BlendShapeEditorSceneController>(GUID);

			StudioAPI.StudioLoadedChanged += (s, e) => StudioUI.Init();
			MakerUI.Init();
		}
	}
}
