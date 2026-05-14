namespace BlendShapeEditor
{
	public static class L
	{
		private static Language _current;

		// Window chrome
		public static string WindowTitleFmt;
		public static string HelpWindowTitle;
		public static string MaterialEditorFilter;
		public static string MaterialEditorFilterTooltip;
		public static string SelectObject;
		public static string MaterialEditorNoRenderers;
		public static string ShowMeshHighlight;
		public static string TargetMesh;
		public static string NotSkinnedSuffix;
		public static string EnterEditMode;
		public static string ExitEditMode;
		public static string NoLayerWarning;

		// Mode switch
		public static string BrushMode;
		public static string GizmoMode;

		// Hint fragments
		public static string ScrollHint;
		public static string AltScrollHint;

		// Brush tools — plain names retained for HUD
		public static string MoveTool;
		public static string SmoothTool;
		public static string InflateTool;
		// Brush tools — format variants for the toolbar buttons
		public static string MoveToolLabelFmt;
		public static string MoveToolTooltipFmt;
		public static string SmoothToolLabelFmt;
		public static string SmoothToolTooltipFmt;
		public static string InflateToolLabelFmt;
		public static string InflateToolTooltipFmt;

		// Brush sliders
		public static string BrushRadiusFmt;
		public static string StrengthFmt;
		public static string FalloffLinear;
		public static string FalloffSmooth;
		public static string FalloffSharp;

		// Gizmo modes
		public static string TranslateLabelFmt;
		public static string RotateLabelFmt;
		public static string ScaleLabelFmt;

		// Gizmo space
		public static string WorldSpace;
		public static string ObjectSpace;
		public static string NormalSpace;
		public static string WorldSpaceTooltipFmt;
		public static string ObjectSpaceTooltipFmt;
		public static string NormalSpaceTooltipFmt;

		// Gizmo size
		public static string GizmoSizeFactorFmt;

		// Soft selection
		public static string SoftSelection;
		public static string SoftModeVolume;
		public static string SoftModeSurface;
		public static string SoftModeVolumeTooltipFmt;
		public static string SoftModeSurfaceTooltipFmt;
		public static string SoftSelectionRadiusFmt;
		public static string CullBackVertices;
		public static string CullBackWireframe;

		// Symmetry
		public static string Mirror;
		public static string SymmetryAxis;
		public static string SetCenter;
		public static string ClearCenter;
		public static string MirrorCenterFmt;

		// Layers
		public static string Layers;
		public static string AddLayerFmt;
		public static string LayerSelectTooltipFmt;
		public static string LayerMoveUpTooltipFmt;
		public static string LayerMoveDownTooltipFmt;
		public static string LayerRenameTooltip;
		public static string LayerRemoveTooltipFmt;
		public static string LayerDefaultNameFmt;

		// Bake
		public static string BakeHeader;
		public static string BakeNameLabel;
		public static string BakeButton;

		// Help
		public static string HelpRenderersHeader;
		public static string HelpRenderers;
		public static string HelpBrushToolsHeader;
		public static string HelpBrushTools;
		public static string HelpBrushParamsHeader;
		public static string HelpBrushParams;
		public static string HelpGizmoSelectionHeader;
		public static string HelpGizmoSelection;
		public static string HelpGizmoToolsHeader;
		public static string HelpGizmoTools;
		public static string HelpGizmoSpaceHeader;
		public static string HelpGizmoSpace;
		public static string HelpGizmoSoftSelectionHeader;
		public static string HelpGizmoSoftSelection;
		public static string HelpMirrorHeader;
		public static string HelpMirror;
		public static string HelpLayersHeader;
		public static string HelpLayers;
		public static string HelpBakeHeader;
		public static string HelpBake;
		public static string HelpWarning;
		public static string HelpAdditionalHeader;
		public static string HelpAdditional;

		static L()
		{
			Reload();
		}

		public static void SetLanguage(Language lang)
		{
			_current = lang;
			Reload();
		}

		private static void Reload()
		{
			switch (_current)
			{
				case Language.English:
				default: LoadEnglish(); return;
			}
		}

		private static void LoadEnglish()
		{
			// Window chrome
			WindowTitleFmt = "BlendShapeEditor v{0}";
			HelpWindowTitle = "Help";
			MaterialEditorFilter = "ME-Connect";
			MaterialEditorFilterTooltip = "Show the same renderer list as in Material Editor";
			SelectObject = "Please select an object";
			MaterialEditorNoRenderers = "Make sure there are visible Renderers in Material Editor!";
			ShowMeshHighlight = "Show Mesh Highlight";
			TargetMesh = "Target Mesh:";
			NotSkinnedSuffix = " (NOT SKINNED)";
			EnterEditMode = "Enter Edit Mode";
			ExitEditMode = "Exit Edit Mode";
			NoLayerWarning = "Create a layer to start sculpting";

			// Mode switch
			BrushMode = "Brush";
			GizmoMode = "Gizmo";

			// Hint fragments
			ScrollHint = "[Scroll]";
			AltScrollHint = "[Alt+Scroll]";

			// Brush tools — plain names retained for HUD
			MoveTool = "Move";
			SmoothTool = "Smooth";
			InflateTool = "Inflate";
			MoveToolLabelFmt = "Move [{0}]";
			MoveToolTooltipFmt = "Click to drag vertices in the direction of the mouse. [{0}]";
			SmoothToolLabelFmt = "Smooth [{0}]";
			SmoothToolTooltipFmt = "Click to level the surface under the brush. [{0}]";
			InflateToolLabelFmt = "Inflate [{0}]";
			InflateToolTooltipFmt = "Click to move vertices outwards. Hold [Alt] to deflate. [{0}]";

			// Brush sliders
			BrushRadiusFmt = "Brush Radius: {0}";
			StrengthFmt = "Strength: {0}";
			FalloffLinear = "Linear";
			FalloffSmooth = "Smooth";
			FalloffSharp = "Sharp";

			// Gizmo modes
			TranslateLabelFmt = "Translate [{0}]";
			RotateLabelFmt = "Rotate [{0}]";
			ScaleLabelFmt = "Scale [{0}]";

			// Gizmo space
			WorldSpace = "World";
			ObjectSpace = "Object";
			NormalSpace = "Normal";
			WorldSpaceTooltipFmt = "Gizmo in World-Space [{0}]";
			ObjectSpaceTooltipFmt = "Gizmo in Object-Space [{0}]";
			NormalSpaceTooltipFmt = "Gizmo in Normal-Space [{0}]";

			// Gizmo size
			GizmoSizeFactorFmt = "Gizmo Size: {0:F3}";

			// Soft selection
			SoftSelection = "Soft Selection";
			SoftModeVolume = "Volume";
			SoftModeSurface = "Surface";
			SoftModeVolumeTooltipFmt = "Volume based selection [{0}]";
			SoftModeSurfaceTooltipFmt = "Surface based selection [{0}]";
			SoftSelectionRadiusFmt = "Soft Selection Radius: {0:F3}";
			CullBackVertices = "Cull back-facing verts";
			CullBackWireframe = "Cull back-facing edges";

			// Symmetry
			Mirror = "Symmetry";
			SymmetryAxis = "Axis";
			SetCenter = "Set Center";
			ClearCenter = "Clear";
			MirrorCenterFmt = "Center: {0:F3}";

			// Layers
			Layers = "Layers";
			AddLayerFmt = "Add Layer [{0}]";
			LayerSelectTooltipFmt = "Select layer [{0}] / [{1}]";
			LayerMoveUpTooltipFmt = "Move layer up [{0}]";
			LayerMoveDownTooltipFmt = "Move layer down [{0}]";
			LayerRenameTooltip = "Rename layer";
			LayerRemoveTooltipFmt = "Remove layer [{0}]";
			LayerDefaultNameFmt = "Layer {0}";
			
			// Bake
			BakeHeader = "Bake to Blendshape";
			BakeNameLabel = "Name:";
			BakeButton = "Exit and Bake to BlendShape";

			// Help
			HelpRenderersHeader = "Renderers";
			HelpRenderers = "Pick a skinned mesh renderer to edit it's mesh. Since only skinned mesh renderers support Blendshapes, static meshes are not supported.";
			HelpBrushToolsHeader = "Brushes";
			HelpBrushTools =
				"Sculpt the mesh using [Left-Click] with the these tools:\n"+
				"- Move [{0}]: drags vertices along screen direction. Hold Shift to push/pull along the normal.\n" +
				"- Smooth [{1}]: averages vertex positions within the brush.\n" +
				"- Inflate [{2}]: pushes along the normal. Hold [Alt] to deflate.";
			HelpBrushParamsHeader = "Parameters";
			HelpBrushParams =
				"- Radius [Scroll]: brush size.\n" +
				"- Strength [Alt+Scroll]: per-frame influence.\n" +
				"- Falloff: Linear / Smooth / Sharp.";
			HelpGizmoSelectionHeader = "Selection";
			HelpGizmoSelection =
				"Select vertices with [Left-Click] or draw a selection box by holding [Alt].\n" +
				"Use back-face culling to only select vertices facing the camera.";
			HelpGizmoToolsHeader = "Gizmo";
			HelpGizmoTools = 
				"Move vertices by interacting with the Gizmo using [Left-Click]:\n" +
				"- Translate [{0}]: three axes, XY/XZ/YZ planes, center cube (Free).\n" +
			    "- Rotate [{1}]: three axis rings + outer white ring (ViewRotate, faces camera).\n" +
			    "- Scale [{2}]: three axes + center cube (uniform).\n"+
				"Change the size using [Alt+Scroll].";
			HelpGizmoSpaceHeader = "Gizmo-Space [{0}]";
			HelpGizmoSpace =
				"- World: fixed XYZ.\n" +
				"- Object: aligned to the character/item root rotation.\n" +
				"- Normal: aligned to the selected vertices' average normal.";
			HelpGizmoSoftSelectionHeader = "Soft-Selection [{0}]";
			HelpGizmoSoftSelection =
				"Extends influence beyond hard-selected vertices.\n" +
				"- Radius [Scroll]: influence distance.\n" +
				"- Mode [{0}]: Volume / Surface (helps with thin meshes).";
			HelpMirrorHeader = "Mirroring [{0}]";
			HelpMirror =
				"- Pick an axis (X/Y/Z).\n" +
				"- Set Center on a reference vertex. Default: 0,0,0.";
			HelpLayersHeader = "Layers";
			HelpLayers =
				"- Add new layer [{0}] | Remove layer [{1}]\n" +
				"- Switch layer selection with [{2}] / [{3}]\n" +
				"- Reorder layers with [{4}] / [{5}]\n" +
				"- Control layer weight with [{6}] / [{7}]\n" +
				"Layers can be renamed. Final position = sum(layer delta * weight).";
			HelpBakeHeader = "Bake to Blendshape";
			HelpBake =
				"When satisfied with edits, bake the new shape to a blendshape.\n" +
				"Layers are merged in to a final single blendshape.\n"+
				"Blendshapes are managed by BlendShapeCreator.";
			HelpWarning = "Any unbaked edits are lost when the character/scene is reloaded.";
			HelpAdditionalHeader = "Additional";
			HelpAdditional =
				"Shortcuts:\n" +
				"- Undo/Redo with [{0}] / [{1}]";
		}

		public enum Language
		{
			English,
			// more langs?
		}
	}
}
