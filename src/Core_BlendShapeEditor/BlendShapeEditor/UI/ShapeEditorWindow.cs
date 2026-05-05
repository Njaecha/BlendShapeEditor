using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class ShapeEditorWindow
	{
		public static bool IsMouseOverUI { get; private set; }
		public bool ShowMeshHighlight { get; set; }
		public OpMode OperationMode { get; set; }
		public BrushToolType SelectedBrushTool { get; set; }
		public float BrushRadius { get; set; }
		public float BrushStrength { get; set; }
		public FalloffMode BrushFalloff { get; set; }
		public bool SymmetryEnabled { get; set; }
		public int SymmetryAxisIndex { get; set; }
		public float SymmetryCenter { get; set; }
		public bool SymmetryCenterSet { get; set; }
		public bool DeferSetSymmetryCenter { get; set; }
		public bool DeferClearSymmetryCenter { get; set; }
		public int GizmoModeIndex { get; set; }
		public int GizmoSpaceIndex { get; set; }
		public bool GizmoSoftSelection { get; set; }
		public int SoftSelectModeIndex { get; set; }
		public float GizmoSoftRadius { get; set; }
		public FalloffMode GizmoFalloff { get; set; }
		public bool IsEditMode { get; private set; }

		public ShapeEditorWindow(int windowId, Rect initialRect)
		{
			_windowId = windowId;
			_windowRect = initialRect;
			BrushRadius = BlendShapeEditorPlugin.DefaultBrushRadius.Value;
			BrushStrength = BlendShapeEditorPlugin.DefaultBrushStrength.Value;
			GizmoSoftRadius = 0.1f;
		}

		public bool Visible { get; set; }

		public void Toggle()
		{
			Visible = !Visible;
		}

		public void SetEditMode(bool active)
		{
			IsEditMode = active;
		}

		public void DrawGUI()
		{
			if (!Visible)
				return;

			EnsureWindowStyle();
			_windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "BlendShapeEditor", _windowStyle, GUILayout.MinWidth(350f));

			if (_showHelp)
			{
				_helpWindowRect = new Rect(_windowRect.xMax, _windowRect.y, 300f, _windowRect.height);
				_helpWindowRect = GUI.Window(_windowId + 1, _helpWindowRect, DrawHelpWindow, "Help", _windowStyle);
			}

			Vector2 screenMouse = new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y);
			IsMouseOverUI = _windowRect.Contains(screenMouse) || (_showHelp && _helpWindowRect.Contains(screenMouse)) || GUIUtility.hotControl != 0;
			if (IsMouseOverUI)
				Input.ResetInputAxes();
		}

		private void DrawWindow(int id)
		{
			try
			{
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("?", GUILayout.Width(25f)))
					_showHelp = !_showHelp;
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);

				DrawShapeTab();

				GUILayout.EndVertical();
			}
			catch (Exception ex)
			{
				GUILayout.EndVertical();
				BlendShapeEditorPlugin.Logger.LogWarning("GUI draw error: " + ex.Message);
			}
			GUI.DragWindow();
		}

		private void DrawHelpWindow(int id)
		{
			if (_helpLabelStyle == null)
				_helpLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

			_helpScroll = GUILayout.BeginScrollView(_helpScroll);
			string text = OperationMode == OpMode.Brush ? L.HelpBrush : L.HelpGizmo;
			GUILayout.Label(text, _helpLabelStyle);
			GUILayout.EndScrollView();
		}

		private void DrawShapeTab()
		{
			DeformData activeDeformData = ActiveDeformData;
			DrawRendererSelection();
			GUILayout.Space(5f);

			if (!IsEditMode)
			{
				if (GUILayout.Button(L.EnterEditMode))
					DeferEnterEditMode = true;
				return;
			}

			if (GUILayout.Button(L.ExitEditMode))
				DeferExitEditMode = true;
			GUILayout.Space(5f);

			if (activeDeformData == null || !activeDeformData.HasLayers)
			{
				GUILayout.Label(L.NoLayerWarning);
				DrawLayerPanel(activeDeformData);
				return;
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(OperationMode == OpMode.Brush, L.BrushMode, "Button"))
				OperationMode = OpMode.Brush;
			if (GUILayout.Toggle(OperationMode == OpMode.Gizmo, L.GizmoMode, "Button"))
				OperationMode = OpMode.Gizmo;
			GUILayout.EndHorizontal();
			GUILayout.Space(3f);

			if (OperationMode == OpMode.Brush)
				DrawBrushControls();
			else
				DrawGizmoControls();

			GUILayout.Space(8f);
			DrawLayerPanel(activeDeformData);
			GUILayout.Space(5f);
			DrawBakeControls();
		}

		private void DrawBakeControls()
		{
			GUILayout.Label(L.BakeHeader);
			GUILayout.BeginHorizontal();
			GUILayout.Label(L.BakeNameLabel, GUILayout.Width(45f));
			_bakeNameInput = GUILayout.TextField(_bakeNameInput);
			GUILayout.EndHorizontal();

			bool hasLayers = ActiveDeformData != null && ActiveDeformData.HasLayers;
			bool prev = GUI.enabled;
			if (!hasLayers || !IsEditMode)
				GUI.enabled = false;
			if (GUILayout.Button(L.BakeButton))
			{
				BakeShapeName = _bakeNameInput;
				DeferBake = true;
			}
			GUI.enabled = prev;
		}

		private void DrawBrushControls()
		{
			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(SelectedBrushTool == BrushToolType.Move, L.MoveTool, "Button"))
				SelectedBrushTool = BrushToolType.Move;
			if (GUILayout.Toggle(SelectedBrushTool == BrushToolType.Smooth, L.SmoothTool, "Button"))
				SelectedBrushTool = BrushToolType.Smooth;
			if (GUILayout.Toggle(SelectedBrushTool == BrushToolType.Inflate, L.InflateTool, "Button"))
				SelectedBrushTool = BrushToolType.Inflate;
			GUILayout.EndHorizontal();

			GUILayout.Label(string.Format(L.BrushRadiusFmt, BrushRadius.ToString("F3")));
			BrushRadius = GUILayout.HorizontalSlider(BrushRadius, 0.001f, 0.5f);
			GUILayout.Label(string.Format(L.StrengthFmt, BrushStrength.ToString("F2")));
			BrushStrength = GUILayout.HorizontalSlider(BrushStrength, 0.01f, 1f);

			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(BrushFalloff == FalloffMode.Linear, L.FalloffLinear, "Button"))
				BrushFalloff = FalloffMode.Linear;
			if (GUILayout.Toggle(BrushFalloff == FalloffMode.Smooth, L.FalloffSmooth, "Button"))
				BrushFalloff = FalloffMode.Smooth;
			if (GUILayout.Toggle(BrushFalloff == FalloffMode.Sharp, L.FalloffSharp, "Button"))
				BrushFalloff = FalloffMode.Sharp;
			GUILayout.EndHorizontal();

			DrawSymmetryControls();
		}

		private void DrawGizmoControls()
		{
			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(GizmoModeIndex == 0, L.Translate, "Button"))
				GizmoModeIndex = 0;
			if (GUILayout.Toggle(GizmoModeIndex == 1, L.Rotate, "Button"))
				GizmoModeIndex = 1;
			if (GUILayout.Toggle(GizmoModeIndex == 2, L.Scale, "Button"))
				GizmoModeIndex = 2;
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(GizmoSpaceIndex == 0, L.WorldSpace, "Button"))
				GizmoSpaceIndex = 0;
			if (GUILayout.Toggle(GizmoSpaceIndex == 1, L.ObjectSpace, "Button"))
				GizmoSpaceIndex = 1;
			if (GUILayout.Toggle(GizmoSpaceIndex == 2, L.NormalSpace, "Button"))
				GizmoSpaceIndex = 2;
			GUILayout.EndHorizontal();

			GizmoSoftSelection = GUILayout.Toggle(GizmoSoftSelection, L.SoftSelection);
			if (GizmoSoftSelection)
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Toggle(SoftSelectModeIndex == 0, L.SoftModeVolume, "Button"))
					SoftSelectModeIndex = 0;
				if (GUILayout.Toggle(SoftSelectModeIndex == 1, L.SoftModeSurface, "Button"))
					SoftSelectModeIndex = 1;
				GUILayout.EndHorizontal();

				GUILayout.Label($"{L.SoftSelectionRadius}: {GizmoSoftRadius:F3}");
				GizmoSoftRadius = GUILayout.HorizontalSlider(GizmoSoftRadius, 0.001f, 0.5f);

				GUILayout.BeginHorizontal();
				if (GUILayout.Toggle(GizmoFalloff == FalloffMode.Linear, L.FalloffLinear, "Button"))
					GizmoFalloff = FalloffMode.Linear;
				if (GUILayout.Toggle(GizmoFalloff == FalloffMode.Smooth, L.FalloffSmooth, "Button"))
					GizmoFalloff = FalloffMode.Smooth;
				if (GUILayout.Toggle(GizmoFalloff == FalloffMode.Sharp, L.FalloffSharp, "Button"))
					GizmoFalloff = FalloffMode.Sharp;
				GUILayout.EndHorizontal();
			}

			DrawSymmetryControls();
		}

		private void DrawSymmetryControls()
		{
			SymmetryEnabled = GUILayout.Toggle(SymmetryEnabled, L.Symmetry);
			if (!SymmetryEnabled)
				return;

			GUILayout.BeginHorizontal();
			GUILayout.Label(L.SymmetryAxis, GUILayout.Width(30f));
			if (GUILayout.Toggle(SymmetryAxisIndex == 0, "X", "Button"))
				SymmetryAxisIndex = 0;
			if (GUILayout.Toggle(SymmetryAxisIndex == 1, "Y", "Button"))
				SymmetryAxisIndex = 1;
			if (GUILayout.Toggle(SymmetryAxisIndex == 2, "Z", "Button"))
				SymmetryAxisIndex = 2;
			GUILayout.EndHorizontal();

			if (SymmetryCenterSet)
				GUILayout.Label(string.Format(L.SymmetryCenterFmt, SymmetryCenter));

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(L.SetCenter))
				DeferSetSymmetryCenter = true;
			if (GUILayout.Button(L.ClearCenter))
				DeferClearSymmetryCenter = true;
			GUILayout.EndHorizontal();
		}

		private void DrawLayerPanel(DeformData data)
		{
			GUILayout.Label(L.Layers);
			if (GUILayout.Button(L.AddLayer))
				DeferLayerAdd = true;

			if (data == null || data.Layers.Count == 0)
				return;

			_layerScroll = GUILayout.BeginScrollView(_layerScroll, GUILayout.Height(140f));
			for (var i = 0; i < data.Layers.Count; i++)
			{
				DeformLayer layer = data.Layers[i];
				bool isActive = data.ActiveLayerIndex == i;
				GUILayout.BeginHorizontal();

				Color prevBg = GUI.backgroundColor;
				if (isActive)
					GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
				if (GUILayout.Toggle(isActive, "", GUILayout.Width(20f)))
					data.SetActiveLayer(i);
				GUI.backgroundColor = prevBg;

				if (_renamingLayerIndex == i)
				{
					_renamingText = GUILayout.TextField(_renamingText, GUILayout.Width(100f));
					if (GUILayout.Button("OK", GUILayout.Width(30f)))
					{
						if (!string.IsNullOrEmpty(_renamingText))
							data.RenameLayer(i, _renamingText);
						_renamingLayerIndex = -1;
					}
				}
				else
				{
					GUILayout.Label(layer.Name, GUILayout.Width(100f));
				}

				float newWeight = GUILayout.HorizontalSlider(layer.Weight, 0f, 1f, GUILayout.Width(60f));
				if (!Mathf.Approximately(newWeight, layer.Weight))
				{
					if (_weightSliderLayer != i)
					{
						_weightSliderLayer = i;
						_weightSliderBefore = layer.Weight;
					}
					data.SetLayerWeight(i, newWeight);
				}
				else if (_weightSliderLayer == i)
				{
					WeightUndoLayer = i;
					WeightUndoBefore = _weightSliderBefore;
					WeightUndoAfter = layer.Weight;
					_weightSliderLayer = -1;
				}

				GUILayout.Label(layer.Weight.ToString("F2"), GUILayout.Width(30f));
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			int activeIdx = data.ActiveLayerIndex;
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(L.RemoveLayer) && activeIdx >= 0)
				DeferLayerRemove = activeIdx;
			if (GUILayout.Button(L.RenameLayer) && activeIdx >= 0)
			{
				_renamingLayerIndex = activeIdx;
				_renamingText = data.Layers[activeIdx].Name;
			}
			if (GUILayout.Button(L.MoveUp) && activeIdx > 0)
				DeferLayerMoveUp = activeIdx;
			if (GUILayout.Button(L.MoveDown) && activeIdx < data.Layers.Count - 1)
				DeferLayerMoveDown = activeIdx;
			GUILayout.EndHorizontal();
		}

		private void DrawRendererSelection()
		{
			if (Renderers.Count == 0)
			{
				GUILayout.Label(L.SelectObject);
				return;
			}

			ShowMeshHighlight = GUILayout.Toggle(ShowMeshHighlight, L.ShowMeshHighlight);
			GUILayout.Label(L.TargetMesh);
			_rendererFilter = GUILayout.TextField(_rendererFilter);

			bool filtering = !string.IsNullOrEmpty(_rendererFilter);
			bool prev = GUI.enabled;
			if (IsEditMode)
				GUI.enabled = false;

			_rendererScroll = GUILayout.BeginScrollView(_rendererScroll, GUILayout.Height(80f));
			for (var i = 0; i < Renderers.Count; i++)
			{
				if (!Renderers[i])
					continue;
				if (filtering && Renderers[i].name.IndexOf(_rendererFilter, StringComparison.OrdinalIgnoreCase) < 0)
					continue;
				string name = Renderers[i].name;
				if (!(Renderers[i] is SkinnedMeshRenderer smr))
				{
					name += " (NOT SKINNED)";
					GUI.enabled = false;
				}
				Color guic = GUI.color;
				if (SelectedRendererIndex == i) GUI.color = Color.cyan;
				if (GUILayout.Toggle(SelectedRendererIndex == i, name, "Button"))
					SelectedRendererIndex = i;
				GUI.color = guic;
				GUI.enabled = true;
			}
			GUILayout.EndScrollView();

			if (IsEditMode)
				GUI.enabled = prev;
		}

		public void IncrementBakeShapeName()
		{
			string name = BakeShapeName;
			int underscoreIdx = name.LastIndexOf('_');
			if (underscoreIdx >= 0 && int.TryParse(name.Substring(underscoreIdx + 1), out int n))
			{
				name = name.Substring(0, underscoreIdx + 1) + (n + 1).ToString();
			}
			else
			{
				name = name + "_2";
			}
			BakeShapeName = name;
			_bakeNameInput = name;
		}

		public void Cleanup()
		{
			if (_bgTex)
			{
				UnityEngine.Object.Destroy(_bgTex);
				_bgTex = null;
			}
		}

		private void EnsureWindowStyle()
		{
			if (_windowStyle == null)
				_windowStyle = new GUIStyle(GUI.skin.window);

			Texture2D bg = EnsureBackgroundTex();
			_windowStyle.normal.background = bg;
			_windowStyle.onNormal.background = bg;
		}

		private Texture2D EnsureBackgroundTex()
		{
			if (!_bgTex)
			{
				_bgTex = new Texture2D(1, 1);
				_bgTex.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f, 0.92f));
				_bgTex.Apply();
			}
			return _bgTex;
		}

		private readonly int _windowId;
		private Rect _windowRect;
		private GUIStyle _windowStyle;
		private Texture2D _bgTex;
		private bool _showHelp;
		private Rect _helpWindowRect;
		private Vector2 _helpScroll;
		private GUIStyle _helpLabelStyle;
		private Vector2 _layerScroll;
		private Vector2 _rendererScroll;
		private string _rendererFilter = "";
		private int _renamingLayerIndex = -1;
		private string _renamingText = "";
		private string _bakeNameInput = "BSE_Shape";

		public bool DeferEnterEditMode;
		public bool DeferExitEditMode;
		public bool DeferLayerAdd;
		public int DeferLayerRemove = -1;
		public int DeferLayerMoveUp = -1;
		public int DeferLayerMoveDown = -1;
		public bool DeferBake;
		public string BakeShapeName = "BSE_Shape";

		private int _weightSliderLayer = -1;
		private float _weightSliderBefore;
		public int WeightUndoLayer = -1;
		public float WeightUndoBefore;
		public float WeightUndoAfter;

		public List<Renderer> Renderers = new List<Renderer>();
		public int SelectedRendererIndex = -1;
		public DeformData ActiveDeformData;
		public int VertexCount;

		public enum OpMode
		{
			Brush,
			Gizmo
		}

		public enum BrushToolType
		{
			Move,
			Smooth,
			Inflate
		}
	}
}
