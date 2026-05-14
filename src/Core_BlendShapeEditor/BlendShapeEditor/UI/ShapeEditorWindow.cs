using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using KKAPI.Utilities;
using UnityEngine;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;

namespace BlendShapeEditor
{
	public class ShapeEditorWindow
	{
		public static bool IsMouseOverUI { get; private set; }
		public bool ShowMeshHighlight { get; set; }
		public bool MatEditFilter { get; set; }
		public OpMode OperationMode { get; set; }
		public BrushToolType SelectedBrushTool { get; set; }
		public float BrushRadius { get; set; }
		public float BrushStrength { get; set; }
		public FalloffMode BrushFalloff { get; set; }
		public bool MirrorEnabled { get; set; }
		public int MirrorAxisIndex { get; set; }
		public float MirrorCenter { get; set; }
		public bool MirrorCenterSet { get; set; }
		public bool DeferSetMirrorCenter { get; set; }
		public bool DeferClearMirrorCenter { get; set; }
		public int GizmoModeIndex { get; set; }
		public int GizmoSpaceIndex { get; set; }
		public bool GizmoSoftSelection { get; set; }
		public int SoftSelectModeIndex { get; set; }
		public float GizmoSoftRadius { get; set; }
		public float GizmoSizeFactor { get; set; }
		public FalloffMode GizmoFalloff { get; set; }
		public bool CullBackVertices { get; set; }
		public bool CullBackWireframe { get; set; } = true;
		public bool IsEditMode { get; private set; }

		public ShapeEditorWindow(int windowId, Rect initialRect)
		{
			_windowId = windowId;
			_windowRect = initialRect;
			BrushRadius = BSE.DefaultBrushRadius.Value;
			BrushStrength = BSE.DefaultBrushStrength.Value;
			GizmoSoftRadius = 0.1f;
			GizmoSizeFactor = 0.10f;
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
			_windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, string.Format(L.WindowTitleFmt, BSE.Version), _windowStyle, GUILayout.MinWidth(400f));

			if (_showHelp)
			{
				_helpWindowRect = new Rect(_windowRect.xMax, _windowRect.y, 500f, _windowRect.height);
				_helpWindowRect = GUI.Window(_windowId + 1, _helpWindowRect, DrawHelpWindow, L.HelpWindowTitle, _windowStyle);
			}

			Vector2 screenMouse = new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y);
			IsMouseOverUI = _windowRect.Contains(screenMouse) || (_showHelp && _helpWindowRect.Contains(screenMouse)) || GUIUtility.hotControl != 0;
			if (_showHelp)
				IMGUIUtils.EatInputInRect(_helpWindowRect);
		}

		private void DrawWindow(int id)
		{
			try
			{
				GUILayout.BeginVertical();
				if (GUI.Button(new Rect(new Vector2(_windowRect.width-25, 5), new Vector2(20, 20)),"?"))
					_showHelp = !_showHelp;
				
				Color guic = GUI.color;
				if (MatEditFilter) GUI.color = Color.magenta;
				bool prevMef = MatEditFilter;
				MatEditFilter = GUI.Toggle(new Rect(5,2,110,20),MatEditFilter, new GUIContent(L.MaterialEditorFilter, L.MaterialEditorFilterTooltip));
				if (prevMef != MatEditFilter)
				{
					DeferRefreshRenderers = true;
				}
				GUI.color = guic;
				
				GUILayout.Space(5f);

				DrawShapeTab();

				GUILayout.EndVertical();
			}
			catch (Exception ex)
			{
				GUILayout.EndVertical();
				BSE.Logger.LogWarning("GUI draw error: " + ex.Message);
			}
			
			IMGUIUtils.DrawTooltip(_windowRect, 200);
			_windowRect = IMGUIUtils.DragResizeEatWindow(id,  _windowRect);
		}

		private void DrawHelpWindow(int id)
		{
			if (_helpLabelStyle == null)
				_helpLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

			_helpScroll = GUILayout.BeginScrollView(_helpScroll);
			Color guic = GUI.color;
			GUI.color = Color.red;
			GUILayout.Label(L.HelpWarning, "Box");
			GUI.color = guic;
			GUILayout.Label(L.HelpRenderersHeader, "Box");
			GUILayout.Label(L.HelpRenderers, _helpLabelStyle);
			if (IsEditMode)
			{
				if (OperationMode == OpMode.Brush)
				{
					GUILayout.Label(L.HelpBrushToolsHeader, "Box");
					GUILayout.Label(string.Format(L.HelpBrushTools, BSE.KeyBrushMove.S(), BSE.KeyBrushSmooth.S(), BSE.KeyBrushInflate.S()), _helpLabelStyle);
					GUILayout.Label(L.HelpBrushParamsHeader, "Box");
					GUILayout.Label(L.HelpBrushParams, _helpLabelStyle);
				}
				else
				{
					GUILayout.Label(L.HelpGizmoSelectionHeader, "Box");
					GUILayout.Label(L.HelpGizmoSelection, _helpLabelStyle);
					GUILayout.Label(L.HelpGizmoToolsHeader, "Box");
					GUILayout.Label(string.Format(L.HelpGizmoTools, BSE.KeyGizmoTranslate.S(), BSE.KeyGizmoRotate.S(), BSE.KeyGizmoScale.S()), _helpLabelStyle);
					GUILayout.Label(string.Format(L.HelpGizmoSpaceHeader, BSE.KeyGizmoCycleGizmoSpace.S()), "Box");
					GUILayout.Label(L.HelpGizmoSpace, _helpLabelStyle);
					GUILayout.Label(string.Format(L.HelpGizmoSoftSelectionHeader, BSE.KeyGizmoSoftSelection.S()), "Box");
					GUILayout.Label(string.Format(L.HelpGizmoSoftSelection, BSE.KeyGizmoCycleSoftMode.S()), _helpLabelStyle);
				}
			}
			GUILayout.Label(string.Format(L.HelpMirrorHeader, BSE.KeyMirror.S()), "Box");
			GUILayout.Label(L.HelpMirror, _helpLabelStyle);
			GUILayout.Label(L.HelpLayersHeader, "Box");
			GUILayout.Label(string.Format(L.HelpLayers, BSE.KeyLayerNew.S(), BSE.KeyLayerRemove.S(), BSE.KeyLayerNext.S(), BSE.KeyLayerPrevious.S(), BSE.KeyLayerDown.S(), BSE.KeyLayerUp.S(), BSE.KeyLayerOpacityDown.S(), BSE.KeyLayerOpacityUp.S()), _helpLabelStyle);
			GUILayout.Label(L.HelpBakeHeader, "Box");
			GUILayout.Label(L.HelpBake, _helpLabelStyle);
			GUILayout.Label(L.HelpAdditionalHeader, "Box");
			GUILayout.Label(string.Format(L.HelpAdditional, BSE.KeyUndo.S(), BSE.KeyRedo.S()), _helpLabelStyle);
			GUILayout.EndScrollView();
		}

		private void DrawShapeTab()
		{
			DeformData activeDeformData = ActiveDeformData;
			DrawRendererSelection();
			GUILayout.Space(5f);

			if (!IsEditMode)
			{
				if (SelectedRendererIndex == -1) GUI.enabled = false;
				if (GUILayout.Button(L.EnterEditMode))
					DeferEnterEditMode = true;
				GUI.enabled = true;
				return;
			}

			if (GUILayout.Button(L.ExitEditMode))
				DeferExitEditMode = true;
			DrawCullingToggles();
			GUILayout.Space(5f);

			if (activeDeformData == null || !activeDeformData.HasLayers)
			{
				Color guic = GUI.color;
				GUI.color = Color.yellow;
				GUILayout.Label(L.NoLayerWarning);
				GUI.color = guic;
				DrawLayerPanel(activeDeformData);
				return;
			}

			GUILayout.BeginHorizontal();
			if (Hotkey(BSE.KeyMode))
			{
				OperationMode = OperationMode == OpMode.Brush ? OpMode.Gizmo : OpMode.Brush;
				DeferUpdateWireColors = true;
			}
			if (GUILayout.Toggle(OperationMode == OpMode.Brush, L.BrushMode, "Button"))
			{
				OperationMode = OpMode.Brush;
				DeferUpdateWireColors = true;
			}
			GUILayout.Label($"[{BSE.KeyMode.S()}]", _labelTextCenterStyle, GUILayout.Width(30));
			if (GUILayout.Toggle(OperationMode == OpMode.Gizmo, L.GizmoMode, "Button"))
			{
				OperationMode = OpMode.Gizmo;
				DeferUpdateWireColors = true;
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(3f);

			if (OperationMode == OpMode.Brush)
				DrawBrushControls();
			else
				DrawGizmoControls();

			GUILayout.Space(8f);
			DrawLayerPanel(activeDeformData);
			GUILayout.Space(5f);
			GUILayout.FlexibleSpace();
			DrawBakeControls();
		}

		private void DrawBakeControls()
		{
			GUILayout.Label(L.BakeHeader, "Box");
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
			Color guic = GUI.color;
			GUI.color = BSE.BrushColorMove.Value;
			if (Hotkey(BSE.KeyBrushMove) || GUILayout.Toggle(SelectedBrushTool == BrushToolType.Move, new GUIContent(string.Format(L.MoveToolLabelFmt, BSE.KeyBrushMove.S()), string.Format(L.MoveToolTooltipFmt, BSE.KeyBrushMove.S())), "Button"))
				SelectedBrushTool = BrushToolType.Move;
			GUI.color = BSE.BrushColorSmooth.Value;
			if (Hotkey(BSE.KeyBrushSmooth) || GUILayout.Toggle(SelectedBrushTool == BrushToolType.Smooth, new GUIContent(string.Format(L.SmoothToolLabelFmt, BSE.KeyBrushSmooth.S()), string.Format(L.SmoothToolTooltipFmt, BSE.KeyBrushSmooth.S())), "Button"))
				SelectedBrushTool = BrushToolType.Smooth;
			GUI.color = BSE.BrushColorInflate.Value;
			if (Hotkey(BSE.KeyBrushInflate) || GUILayout.Toggle(SelectedBrushTool == BrushToolType.Inflate, new GUIContent(string.Format(L.InflateToolLabelFmt, BSE.KeyBrushInflate.S()), string.Format(L.InflateToolTooltipFmt, BSE.KeyBrushInflate.S())), "Button"))
				SelectedBrushTool = BrushToolType.Inflate;
			GUI.color = guic;
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label(string.Format(L.BrushRadiusFmt, BrushRadius.ToString("F3")));
			GUILayout.Label(L.ScrollHint, _labelTextRightStyle);
			GUILayout.EndHorizontal();
			BrushRadius = GUILayout.HorizontalSlider(BrushRadius, 0.001f, 0.5f);
			GUILayout.BeginHorizontal();
			GUILayout.Label(string.Format(L.StrengthFmt, BrushStrength.ToString("F2")));
			GUILayout.Label(L.AltScrollHint, _labelTextRightStyle);
			GUILayout.EndHorizontal();
			BrushStrength = GUILayout.HorizontalSlider(BrushStrength, 0.01f, 1f);

			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(BrushFalloff == FalloffMode.Linear, L.FalloffLinear, "Button"))
				BrushFalloff = FalloffMode.Linear;
			if (GUILayout.Toggle(BrushFalloff == FalloffMode.Smooth, L.FalloffSmooth, "Button"))
				BrushFalloff = FalloffMode.Smooth;
			if (GUILayout.Toggle(BrushFalloff == FalloffMode.Sharp, L.FalloffSharp, "Button"))
				BrushFalloff = FalloffMode.Sharp;
			GUILayout.EndHorizontal();

			DrawMirrorControls();
		}

		private void DrawGizmoControls()
		{
			GUILayout.BeginHorizontal();
			if (Hotkey(BSE.KeyGizmoTranslate) || GUILayout.Toggle(GizmoModeIndex == 0, string.Format(L.TranslateLabelFmt, BSE.KeyGizmoTranslate.S()), "Button"))
				GizmoModeIndex = 0;
			if (Hotkey(BSE.KeyGizmoRotate) || GUILayout.Toggle(GizmoModeIndex == 1, string.Format(L.RotateLabelFmt, BSE.KeyGizmoRotate.S()), "Button"))
				GizmoModeIndex = 1;
			if (Hotkey(BSE.KeyGizmoScale) || GUILayout.Toggle(GizmoModeIndex == 2, string.Format(L.ScaleLabelFmt, BSE.KeyGizmoScale.S()), "Button"))
				GizmoModeIndex = 2;
			GUILayout.EndHorizontal();

			if (Hotkey(BSE.KeyGizmoCycleGizmoSpace))
			{
				GizmoSpaceIndex++;
				if (GizmoSpaceIndex > 2) GizmoSpaceIndex = 0;
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(GizmoSpaceIndex == 0, new GUIContent(L.WorldSpace, string.Format(L.WorldSpaceTooltipFmt, BSE.KeyGizmoCycleGizmoSpace)), "Button"))
				GizmoSpaceIndex = 0;
			if (GUILayout.Toggle(GizmoSpaceIndex == 1, new GUIContent(L.ObjectSpace, string.Format(L.ObjectSpaceTooltipFmt, BSE.KeyGizmoCycleGizmoSpace)), "Button"))
				GizmoSpaceIndex = 1;
			if (GUILayout.Toggle(GizmoSpaceIndex == 2, new GUIContent(L.NormalSpace, string.Format(L.NormalSpaceTooltipFmt, BSE.KeyGizmoCycleGizmoSpace)), "Button"))
				GizmoSpaceIndex = 2;
			GUILayout.EndHorizontal();
			
			GUILayout.BeginHorizontal();
			GUILayout.Label(string.Format(L.GizmoSizeFactorFmt, GizmoSizeFactor));
			GUILayout.Label(L.AltScrollHint, _labelTextRightStyle);
			GUILayout.EndHorizontal();
			GizmoSizeFactor = GUILayout.HorizontalSlider(GizmoSizeFactor, 0.01f, 0.15f);

			if (Hotkey(BSE.KeyGizmoSoftSelection)) GizmoSoftSelection = !GizmoSoftSelection;
			GUILayout.BeginHorizontal();
			GizmoSoftSelection = GUILayout.Toggle(GizmoSoftSelection, L.SoftSelection);
			GUILayout.Label($"[{BSE.KeyGizmoSoftSelection.S()}]", _labelTextRightStyle);
			GUILayout.EndHorizontal();
			if (GizmoSoftSelection)
			{
				if (Hotkey(BSE.KeyGizmoSoftSelection))
				{
					SoftSelectModeIndex = SoftSelectModeIndex == 0 ? 1 : 0;
				}
				GUILayout.BeginHorizontal();
				if (GUILayout.Toggle(SoftSelectModeIndex == 0, new GUIContent(L.SoftModeVolume, string.Format(L.SoftModeVolumeTooltipFmt, BSE.KeyGizmoSoftSelection.S())), "Button"))
					SoftSelectModeIndex = 0;
				if (GUILayout.Toggle(SoftSelectModeIndex == 1, new GUIContent(L.SoftModeSurface, string.Format(L.SoftModeSurfaceTooltipFmt, BSE.KeyGizmoSoftSelection.S())), "Button"))
					SoftSelectModeIndex = 1;
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label(string.Format(L.SoftSelectionRadiusFmt, GizmoSoftRadius));
				GUILayout.Label(L.ScrollHint, _labelTextRightStyle);
				GUILayout.EndHorizontal();
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

			DrawMirrorControls();
		}

		private void DrawCullingToggles()
		{
			GUILayout.BeginHorizontal();
			CullBackVertices = GUILayout.Toggle(CullBackVertices, L.CullBackVertices);
			CullBackWireframe = GUILayout.Toggle(CullBackWireframe, L.CullBackWireframe);
			GUILayout.EndHorizontal();
		}

		private void DrawMirrorControls()
		{
			if (Hotkey(BSE.KeyMirror)) MirrorEnabled = !MirrorEnabled;
			GUILayout.BeginHorizontal();
			MirrorEnabled = GUILayout.Toggle(MirrorEnabled, L.Mirror);
			GUILayout.Label($"[{BSE.KeyMirror.S()}]", _labelTextRightStyle);
			GUILayout.EndHorizontal();
			if (!MirrorEnabled)
				return;

			GUILayout.BeginHorizontal();
			GUILayout.Label(L.SymmetryAxis, GUILayout.Width(30f));
			if (GUILayout.Toggle(MirrorAxisIndex == 0, "X", "Button"))
				MirrorAxisIndex = 0;
			if (GUILayout.Toggle(MirrorAxisIndex == 1, "Y", "Button"))
				MirrorAxisIndex = 1;
			if (GUILayout.Toggle(MirrorAxisIndex == 2, "Z", "Button"))
				MirrorAxisIndex = 2;
			GUILayout.EndHorizontal();

			if (MirrorCenterSet)
				GUILayout.Label(string.Format(L.MirrorCenterFmt, MirrorCenter));

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(L.SetCenter))
				DeferSetMirrorCenter = true;
			if (GUILayout.Button(L.ClearCenter))
				DeferClearMirrorCenter = true;
			GUILayout.EndHorizontal();
		}

		private void DrawLayerPanel(DeformData data)
		{
			GUILayout.Label(L.Layers, "Box");
			if (Hotkey(BSE.KeyLayerNew) || GUILayout.Button(string.Format(L.AddLayerFmt, BSE.KeyLayerNew.S())))
				DeferLayerAdd = true;

			if (data == null || data.Layers.Count == 0)
				return;

			_layerScroll = GUILayout.BeginScrollView(_layerScroll, GUILayout.Height(140f));
			for (var i = 0; i < data.Layers.Count; i++)
			{
				DeformLayer layer = data.Layers[i];
				bool isActive = data.ActiveLayerIndex == i;
				
				Color guic = GUI.color;
				if (isActive) GUI.color = new Color(0.4f, 0.8f, 1f);
				GUILayout.BeginHorizontal("Box");
				GUI.color = guic;
				if (_renamingLayerIndex == i)
				{
					_renamingText = GUILayout.TextField(_renamingText, GUILayout.MinWidth(100f));
					if (GUILayout.Button("OK", _smallButtonStyle, GUILayout.Width(30f)))
					{
						if (!string.IsNullOrEmpty(_renamingText))
							data.RenameLayer(i, _renamingText);
						_renamingLayerIndex = -1;
					}
				}
				else
				{ 
					guic = GUI.color;
					if (isActive) GUI.color = new Color(0.4f, 0.8f, 1f);
					if (GUILayout.Button(new GUIContent(layer.Name, string.Format(L.LayerSelectTooltipFmt, BSE.KeyLayerNext.S(), BSE.KeyLayerPrevious.S())), _layerButtonStyle, GUILayout.MinWidth(100f)))
					{
						data.SetActiveLayer(i);
					}
					GUI.color = guic;
				}

				float newWeight = GUILayout.HorizontalSlider(layer.Weight, 0f, 1f, slider: _layerSliderStyle, GUI.skin.horizontalSliderThumb, GUILayout.Width(80f));
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
				if (i == 0) GUI.enabled = false;
				if (GUILayout.Button(new GUIContent("˄", string.Format(L.LayerMoveUpTooltipFmt, BSE.KeyLayerUp.S())), _smallButtonStyle, GUILayout.Width(20f)))
				{
					DeferLayerMoveUp = i;
				}
				GUI.enabled = true;
				if (i == data.Layers.Count - 1) GUI.enabled = false;
				if (GUILayout.Button(new GUIContent("˅", string.Format(L.LayerMoveDownTooltipFmt, BSE.KeyLayerDown.S())), _smallButtonStyle, GUILayout.Width(20f)))
				{
					DeferLayerMoveDown = i;
				}
				GUI.enabled = true;
				if (GUILayout.Button(new GUIContent("R", L.LayerRenameTooltip), _smallButtonStyle, GUILayout.Width(20f)))
				{
					_renamingLayerIndex = i;
					_renamingText = data.Layers[i].Name;
				}
				Color guicColor = GUI.color;
				GUI.color = Color.red;
				if (GUILayout.Button(new GUIContent("╳", string.Format(L.LayerRemoveTooltipFmt, BSE.KeyLayerRemove.S())), _smallButtonStyle, GUILayout.Width(20f)))
				{
					DeferLayerRemove = i;
				}
				GUI.color = guicColor;
				
				
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			if (Hotkey(BSE.KeyLayerNext) && data.ActiveLayerIndex < data.Layers.Count - 1)
			{
				data.SetActiveLayer(data.ActiveLayerIndex + 1);
			}

			if (Hotkey(BSE.KeyLayerDown) && data.ActiveLayerIndex < data.Layers.Count - 1)
			{
				DeferLayerMoveDown = data.ActiveLayerIndex;
			}

			if (Hotkey(BSE.KeyLayerPrevious) && data.ActiveLayerIndex > 0)
			{
				data.SetActiveLayer(data.ActiveLayerIndex - 1);
			}
			
			if (Hotkey(BSE.KeyLayerUp) && data.ActiveLayerIndex > 0)
			{
				DeferLayerMoveUp = data.ActiveLayerIndex;
			}

			if (Hotkey(BSE.KeyLayerRemove))
			{
				DeferLayerRemove = data.ActiveLayerIndex;
			}

			if (Hotkey(BSE.KeyLayerOpacityUp))
			{
				float weight = data.ActiveLayer.Weight;
				if (weight + 0.1f > 1f) data.SetLayerWeight(data.ActiveLayerIndex, 1f);
				data.SetLayerWeight(data.ActiveLayerIndex, weight+0.1f);
			}

			if (Hotkey(BSE.KeyLayerOpacityDown))
			{
				float weight = data.ActiveLayer.Weight;
				if (weight - 0.1f < 0f) data.SetLayerWeight(data.ActiveLayerIndex, 0f);
				data.SetLayerWeight(data.ActiveLayerIndex, weight-0.1f);
			}
		}

		private void DrawRendererSelection()
		{
			if (Renderers.Count == 0)
			{
				Color guic = GUI.color;
				GUI.color = Color.red;
				GUILayout.Label(!MatEditFilter
					? L.SelectObject
					: L.MaterialEditorNoRenderers);
				GUI.color = guic;
				return;
			}

			if (IsEditMode && !_expandRendererPanel)
			{
				GUILayout.Space(5);
				if (GUILayout.Button("▶ Expand renderer selection"))
				{
					_expandRendererPanel = true;
				}
				return;
			}
			
			ShowMeshHighlight = GUILayout.Toggle(ShowMeshHighlight, L.ShowMeshHighlight);
			GUILayout.BeginHorizontal();
			GUILayout.Label(L.TargetMesh);
			if (IsEditMode)
			{
				if (GUILayout.Button("▼ Hide renderer selection"))
				{
					_expandRendererPanel = false;
				}
			}
			GUILayout.EndHorizontal();
			_rendererFilter = GUILayout.TextField(_rendererFilter);

			bool filtering = !string.IsNullOrEmpty(_rendererFilter);
			bool prev = GUI.enabled;
			if (IsEditMode)
				GUI.enabled = false;

			_rendererScroll = GUILayout.BeginScrollView(_rendererScroll, "Box", GUILayout.Height(80f));
			for (var i = 0; i < Renderers.Count; i++)
			{
				if (!Renderers[i])
					continue;
				if (filtering && Renderers[i].name.IndexOf(_rendererFilter, StringComparison.OrdinalIgnoreCase) < 0)
					continue;
				string name = Renderers[i].name;
				if (!(Renderers[i] is SkinnedMeshRenderer smr))
				{
					name += L.NotSkinnedSuffix;
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
			{
				Texture2D bg = EnsureBackgroundTex();
				_windowStyle = new GUIStyle(GUI.skin.window)
				{
					normal =
					{
						background = bg
					},
					onNormal =
					{
						background = bg
					}
				};
			}

			if (_labelTextRightStyle == null)
			{
				_labelTextRightStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleRight
				};
			}
			if (_labelTextCenterStyle == null)
			{
				RectOffset p = GUI.skin.label.padding;
				RectOffset m = GUI.skin.label.margin;
				_labelTextCenterStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					padding = new RectOffset(p.left,0,p.top,p.bottom),
					margin = new RectOffset(m.left+5,0,m.top,m.bottom),
				};
			}

			if (_smallButtonStyle == null)
			{
				RectOffset p = GUI.skin.button.padding;
				_smallButtonStyle = new GUIStyle(GUI.skin.button)
				{
					alignment = TextAnchor.MiddleCenter,
					padding = new RectOffset(left: 1, top: p.top, right: 1, bottom: p.bottom),
				};
			}
			
			if (_layerButtonStyle == null)
			{
				_layerButtonStyle = new GUIStyle(GUI.skin.button)
				{
					alignment = TextAnchor.MiddleLeft,
					active =
					{
						textColor = Color.cyan
					},
					hover = 
					{
						textColor = Color.cyan
					}
				};
			}
			
			if (_layerSliderStyle == null)
			{
				RectOffset p = GUI.skin.horizontalSlider.padding;
				RectOffset m = GUI.skin.horizontalSlider.margin;
				_layerSliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
				{
					padding = new RectOffset(p.left, p.right, p.top, p.bottom),
					margin = new RectOffset(m.left, m.right, m.top+5, m.bottom),
				};
			}
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

		private readonly List<KeyboardShortcut> _shortcutsUsed = new List<KeyboardShortcut>();
		private bool Hotkey(ConfigEntry<KeyboardShortcut> shortcut)
		{
			return Hotkey(shortcut.Value);
		}
		private bool Hotkey(KeyboardShortcut shortcut)
		{
			if (shortcut.MainKey == KeyCode.None) return false;
			bool down = shortcut.IsDown();
			if (down) HotkeyUsed = true;

			// remember if hotkey was already used this press (else holding the key will spam it, which is bad for the cycle hotkeys)
			var hotkeyUsage = false;
			if (InputHelper.LastReleasedKeyCode == shortcut.MainKey)
			{
				_shortcutsUsed.Remove(shortcut);
			}
			if (down && !_shortcutsUsed.Contains(shortcut))
			{
				hotkeyUsage = true;
				_shortcutsUsed.Add(shortcut);
				//BSE.Logger.LogDebug($"Hokey press registered: {shortcut.ToString()}");
			}
			
			return hotkeyUsage;
		}

		private readonly int _windowId;
		private Rect _windowRect;
		private GUIStyle _windowStyle;
		private Texture2D _bgTex;
		private bool _showHelp;
		private bool _expandRendererPanel;
		private Rect _helpWindowRect;
		private Vector2 _helpScroll;
		private GUIStyle _helpLabelStyle;
		private GUIStyle _labelTextCenterStyle;
		private GUIStyle _labelTextRightStyle;
		private GUIStyle _layerSliderStyle;
		private GUIStyle _layerButtonStyle;
		private GUIStyle _smallButtonStyle;
		private Vector2 _layerScroll;
		private Vector2 _rendererScroll;
		private string _rendererFilter = "";
		private int _renamingLayerIndex = -1;
		private string _renamingText = "";
		private string _bakeNameInput = "BSE_Shape";
		internal bool HotkeyUsed;

		public bool DeferEnterEditMode;
		public bool DeferExitEditMode;
		public bool DeferLayerAdd;
		public bool DeferRefreshRenderers;
		public bool DeferUpdateWireColors;
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
