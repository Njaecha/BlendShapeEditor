using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
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
        
        #endregion

        static i18n()
        {
            Load(Language.English);
        }

        public static void SetLanguage(Language lang)
        {
            Load(lang);
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

            Newtonsoft.Json.Linq.JObject data = Newtonsoft.Json.Linq.JObject.Parse(json);

            var fields = typeof(i18n).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                string jsonKey = field.Name;
                var attr = field.GetCustomAttribute<i18nKeyAttribute>(false);
                if (attr != null)
                    jsonKey = attr.Key;

                Newtonsoft.Json.Linq.JToken token = data[jsonKey];
                if (token != null)
                {
                    string value = (string)token;
                    field.SetValue(null, value);
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