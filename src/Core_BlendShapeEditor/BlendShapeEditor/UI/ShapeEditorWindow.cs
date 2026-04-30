using System;
using System.Collections.Generic;
using UnityEngine;

namespace KKShapeEditor
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
		public int SubdivideLevel { get; set; } = 1;
		public bool IsEditMode { get; private set; }

		public ShapeEditorWindow(int windowId, Rect initialRect)
		{
			_windowId = windowId;
			_windowRect = initialRect;
			BrushRadius = ShapeEditorPlugin.DefaultBrushRadius.Value;
			BrushStrength = ShapeEditorPlugin.DefaultBrushStrength.Value;
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
			_windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "KKShapeEditor", _windowStyle, GUILayout.MinWidth(350f));

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
				_tabIndex = GUILayout.Toolbar(_tabIndex, L.TabNames);
				if (GUILayout.Button("?", GUILayout.Width(25f)))
					_showHelp = !_showHelp;
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);

				if (_tabIndex == 0)
					DrawShapeTab();
				else if (_tabIndex == 1)
					DrawSubdivideTab();

				GUILayout.EndVertical();
			}
			catch (Exception ex)
			{
				GUILayout.EndVertical();
				ShapeEditorPlugin.Logger.LogWarning("GUI draw error: " + ex.Message);
			}
			GUI.DragWindow();
		}

		private void DrawHelpWindow(int id)
		{
			if (_helpLabelStyle == null)
				_helpLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

			_helpScroll = GUILayout.BeginScrollView(_helpScroll);
			string text = _tabIndex == 0
				? (OperationMode == OpMode.Brush ? L.HelpBrush : L.HelpGizmo)
				: L.HelpSubdivide;
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
				bool prev = GUI.enabled;
				if (FaceSelect)
					GUI.enabled = false;
				if (GUILayout.Button(L.EnterEditMode))
					DeferEnterEditMode = true;
				GUI.enabled = prev;
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

			if (IsOnCharacter)
				DrawWeightRemapControls();
		}

		private void DrawWeightRemapControls()
		{
			GUILayout.Space(5f);
			GUILayout.BeginHorizontal();
			bool prev = GUI.enabled;
			if (!BodyMeshReadable)
			{
				GUI.enabled = false;
				GUILayout.Button(L.RemapWeights);
				GUI.enabled = prev;
			}
			else if (GUILayout.Button(L.RemapWeights))
			{
				DeferRemapWeights = true;
			}
			if (GUILayout.Button(L.RestoreWeights))
				DeferRestoreWeights = true;
			GUILayout.EndHorizontal();
			if (!BodyMeshReadable)
				GUILayout.Label(L.BodyMeshNotReadable);
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

		private void DrawSubdivideTab()
		{
			DrawRendererSelection();
			if (SelectedRendererIndex < 0 || SelectedRendererIndex >= Renderers.Count)
				return;

			Renderer renderer = Renderers[SelectedRendererIndex];
			if (!renderer)
				return;

			Mesh mesh = GetMeshFromRenderer(renderer);
			if (!mesh)
				return;

			int totalFaces = MeshHelper.GetTotalFaceCount(mesh);
			GUILayout.Label(string.Format(L.VerticesFacesFmt, mesh.vertexCount, totalFaces));
			GUILayout.Space(5f);

			bool faceSelectActive = FaceSelect;
			bool prev = GUI.enabled;
			if (IsEditMode)
				GUI.enabled = false;
			bool newFaceSelectActive = GUILayout.Toggle(faceSelectActive, L.SelectFaces);
			if (IsEditMode)
				GUI.enabled = prev;

			if (newFaceSelectActive != faceSelectActive)
			{
				if (newFaceSelectActive)
				{
					FaceSelect = FaceSelectOverlay.Create(renderer);
				}
				else if (FaceSelect)
				{
					UnityEngine.Object.Destroy(FaceSelect.gameObject);
					FaceSelect = null;
				}
			}

			if (FaceSelect)
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Toggle(!FaceSelect.BoxSelectMode, L.Brush, "Button"))
					FaceSelect.BoxSelectMode = false;
				if (GUILayout.Toggle(FaceSelect.BoxSelectMode, L.BoxSelect, "Button"))
					FaceSelect.BoxSelectMode = true;
				GUILayout.EndHorizontal();

				if (!FaceSelect.BoxSelectMode)
				{
					GUILayout.Label(string.Format(L.BrushRadiusFmt, FaceSelect.BrushRadius.ToString("F3")));
					FaceSelect.BrushRadius = GUILayout.HorizontalSlider(FaceSelect.BrushRadius, 0.001f, 0.5f);
				}

				GUILayout.Label(string.Format(L.SelectedFacesFmt, FaceSelect.SelectedFaces.Count, FaceSelect.TotalFaces));
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(L.AllButton, GUILayout.Width(40f)))
					DeferFaceSelectAll = true;
				if (GUILayout.Button(L.NoneButton, GUILayout.Width(50f)))
					DeferFaceSelectNone = true;
				if (GUILayout.Button(L.InvertButton, GUILayout.Width(55f)))
					DeferFaceSelectInvert = true;
				GUILayout.EndHorizontal();
			}

			GUILayout.Space(3f);
			if (IsEditMode)
				GUI.enabled = false;

			GUILayout.BeginHorizontal();
			GUILayout.Label(L.LevelLabel, GUILayout.Width(40f));
			if (GUILayout.Toggle(SubdivideLevel == 1, "1", "Button", GUILayout.Width(30f)))
				SubdivideLevel = 1;
			if (GUILayout.Toggle(SubdivideLevel == 2, "2", "Button", GUILayout.Width(30f)))
				SubdivideLevel = 2;
			if (GUILayout.Toggle(SubdivideLevel == 3, "3", "Button", GUILayout.Width(30f)))
				SubdivideLevel = 3;

			if (GUILayout.Button(L.Subdivide, GUILayout.Width(80f)))
			{
				if (ActiveDeformData != null && ActiveDeformData.HasLayers)
					ShowSubdivideLayerWarning = true;
				else
					DeferSubdivide = true;
			}
			if (MeshHelper.HasOriginal(renderer) && GUILayout.Button(L.Restore, GUILayout.Width(70f)))
				DeferRestore = true;
			GUILayout.EndHorizontal();

			if (IsEditMode)
				GUI.enabled = prev;

			if (ShowSubdivideLayerWarning)
			{
				GUILayout.Space(5f);
				GUILayout.Label(L.SubdivideLayerWarning);
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(L.ApplyButton, GUILayout.Width(80f)))
				{
					ShowSubdivideLayerWarning = false;
					DeferSubdivide = true;
				}
				if (GUILayout.Button(L.ClearButton, GUILayout.Width(80f)))
					ShowSubdivideLayerWarning = false;
				GUILayout.EndHorizontal();
			}
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
				if (GUILayout.Toggle(SelectedRendererIndex == i, Renderers[i].name, "Button"))
					SelectedRendererIndex = i;
			}
			GUILayout.EndScrollView();

			if (IsEditMode)
				GUI.enabled = prev;

			GUILayout.BeginHorizontal();
			bool hasLayers = ActiveDeformData != null && ActiveDeformData.Layers.Count > 0;
			bool prev2 = GUI.enabled;
			if (!hasLayers)
				GUI.enabled = false;
			if (GUILayout.Button(L.ExportDeform))
				DeferExport = true;
			GUI.enabled = prev2;

			bool rendererValid = SelectedRendererIndex >= 0 && SelectedRendererIndex < Renderers.Count && Renderers[SelectedRendererIndex] != null;
			bool prev3 = GUI.enabled;
			if (!rendererValid || !IsEditMode)
				GUI.enabled = false;
			if (GUILayout.Button(L.ImportDeform))
				DeferImport = true;
			GUI.enabled = prev3;
			GUILayout.EndHorizontal();
		}

		private static Mesh GetMeshFromRenderer(Renderer r)
		{
			SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
			if (smr)
				return smr.sharedMesh;
			MeshFilter mf = r.GetComponent<MeshFilter>();
			return mf ? mf.sharedMesh : null;
		}

		public void Cleanup()
		{
			if (FaceSelect)
			{
				UnityEngine.Object.Destroy(FaceSelect.gameObject);
				FaceSelect = null;
			}
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
		private int _tabIndex;
		private Vector2 _layerScroll;
		private Vector2 _rendererScroll;
		private string _rendererFilter = "";
		private int _renamingLayerIndex = -1;
		private string _renamingText = "";

		public bool DeferEnterEditMode;
		public bool DeferExitEditMode;
		public bool DeferSubdivide;
		public bool DeferRestore;
		public bool DeferFaceSelectAll;
		public bool DeferFaceSelectNone;
		public bool DeferFaceSelectInvert;
		public bool DeferLayerAdd;
		public int DeferLayerRemove = -1;
		public bool DeferSubdivideLayerWarningConfirm;
		public int DeferLayerMoveUp = -1;
		public int DeferLayerMoveDown = -1;
		public bool DeferRemapWeights;
		public bool DeferRestoreWeights;
		public bool DeferExport;
		public bool DeferImport;

		private int _weightSliderLayer = -1;
		private float _weightSliderBefore;
		public int WeightUndoLayer = -1;
		public float WeightUndoBefore;
		public float WeightUndoAfter;

		public List<Renderer> Renderers = new List<Renderer>();
		public int SelectedRendererIndex;
		public DeformData ActiveDeformData;
		public FaceSelectOverlay FaceSelect;
		public int VertexCount;
		public bool ShowSubdivideLayerWarning;
		public bool BodyMeshReadable;
		public bool IsOnCharacter;

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
