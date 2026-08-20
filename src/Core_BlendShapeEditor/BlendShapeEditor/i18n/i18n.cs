using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using LitJson;
using MessagePack;
using UnityEngine;

namespace BlendShapeEditor
{
    public static class i18n
    {
        #region Fields
        
        // Window chrome
        public static string WindowTitleFmt;
        public static string HelpWindowTitle;
        public static string MaterialEditorFilter;
        public static string MaterialEditorFilterTooltip;
        public static string SelectObject;
        public static string MaterialEditorNoRenderers;
        public static string ShowMeshHighlight;
        public static string TargetMesh;
        public static string ExpandRendererPanel;
        public static string CollapseRendererPanel;
        public static string RefreshRenderers;
        public static string RefreshRenderersTooltip;
        public static string NotSkinnedSuffix;
        public static string EnterEditMode;
        public static string ExitEditMode;
        public static string NoLayerWarning;
        public static string ExpandExistingShapeList;
        public static string CollapseExistingShapeList;
        public static string EditExistingShapeTooltip;
        public static string EditExistingShapeButton;
        public static string ShapeCountSingularFmt;
        public static string ShapeCountPluralFmt;

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
        public static string DrawTool;
        public static string DrawSharpTool;
        public static string BlobTool;
        public static string ClayTool;
        public static string ClayStripsTool;
        public static string ClayThumbTool;
        public static string CreaseTool;
        public static string LayerTool;
        public static string FillTool;
        public static string FlattenTool;

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
        public static string VertexDisplayAll;
        public static string VertexDisplayBackface;
        public static string VertexDisplayInteract;
        public static string VertexDisplayTooltip;
        public static string CullBackWireframe;
        public static string WireframeCullOn;
        public static string WireframeCullOff;
        public static string WireframeInteractOnly;
        public static string WireframeCullTooltip;

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
        public static string LayerHideTooltip;
        public static string LayerShowTooltip;
        public static string LayerDefaultNameFmt;

        // Preview
        public static string PreviewWeightFmt;
        public static string PreviewWeightTooltip;
        public static string SyncPreviewLabel;
        public static string SyncPreviewTooltip;

        // Bake
        public static string BakeHeader;
        public static string BakeNameLabel;
        public static string BakePrefixLabel;
        public static string BakeSeparateLabelOnFmt;
        public static string BakeSeparateLabelOffFmt;
        public static string BakeSeparateTooltip;
        public static string BakeButton;
        public static string UpdateButton;
        public static string BakeCalcNormalsOnFmt;
        public static string BakeCalcNormalsOffFmt;
        public static string BakeCalcNormalsTooltip;
        public static string BakeNameConflictWarning;

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

        // Key display names, indexed by KeyCode name via KeyName().
        public static string Key_LeftControl;
        public static string Key_RightControl;
        public static string Key_LeftShift;
        public static string Key_RightShift;
        public static string Key_LeftAlt;
        public static string Key_RightAlt;
        public static string Key_LeftWindows;
        public static string Key_RightWindows;
        public static string Key_LeftCommand;
        public static string Key_RightCommand;
        public static string Key_UpArrow;
        public static string Key_DownArrow;
        public static string Key_LeftArrow;
        public static string Key_RightArrow;
        public static string Key_Home;
        public static string Key_End;
        public static string Key_PageUp;
        public static string Key_PageDown;
        public static string Key_Insert;
        public static string Key_Delete;
        public static string Key_Tab;
        public static string Key_Backspace;
        public static string Key_Return;
        public static string Key_KeypadEnter;
        public static string Key_Escape;
        public static string Key_Space;
        public static string Key_CapsLock;
        public static string Key_Mouse0;
        public static string Key_Mouse1;
        public static string Key_Mouse2;
        public static string Key_None;
        
        #endregion

        static i18n()
        {
            Load(Language.English);
        }

        public static void SetLanguage(Language lang)
        {
            Load(lang);
        }

        /// Localized display name for a KeyCode. Falls back to KeyCode.ToString() if untranslated.
        public static string KeyName(KeyCode key)
        {
            var field = typeof(i18n).GetField("Key_" + key, BindingFlags.Public | BindingFlags.Static);
            var value = field?.GetValue(null) as string;
            return string.IsNullOrEmpty(value) ? key.ToString() : value;
        }

        private static void Load(Language lang)
        {
            var assembly = Assembly.GetCallingAssembly();
            var resourceName = "BlendShapeEditor." + lang + ".json";

            string json;
            foreach (string manifestResourceName in assembly.GetManifestResourceNames())
            {
                BepInEx.Logging.Logger.CreateLogSource("AFD").LogInfo($"Manifest resource: {manifestResourceName}");
            }
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Embedded resource '{resourceName}' not found");

                using (StreamReader reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }
            }

            JsonData data = JsonMapper.ToObject(json);

            var fields = typeof(i18n).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                string jsonKey = field.Name;
                var attr = field.GetCustomAttribute<i18nKeyAttribute>(false);
                if (attr != null)
                    jsonKey = attr.Key;

                if (data.Keys.Contains(jsonKey))
                {
                    JsonData token = data[jsonKey];
                    if (token != null && token.IsString)
                    {
                        field.SetValue(null, (string)token);
                    }
                }
            }
        }
        public enum Language
        {
            English,
            Spanish
            // more langs?
        }
    }
}
