using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using HSPE;
using KKAPI.Studio;
using Studio;
using UnityEngine;
using Object = UnityEngine.Object;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;

namespace BlendShapeEditor
{
	[DefaultExecutionOrder(32001)]
	public class ShapePaintOverlay : MonoBehaviour
	{
		public static void RebuildGradients()
		{
			GradientAlphaKey[] alphaKeys = new[]
			{
				new GradientAlphaKey(1f, 0f),
				new GradientAlphaKey(1f, 1f)
			};
			WeightGradient.SetKeys(new[]
			{
				new GradientColorKey(BSE.WeightGradientStop0.Value, 0f),
				new GradientColorKey(BSE.WeightGradientStop1.Value, 0.33f),
				new GradientColorKey(BSE.WeightGradientStop2.Value, 0.66f),
				new GradientColorKey(BSE.WeightGradientStop3.Value, 1f)
			}, alphaKeys);
			MirrorWeightGradient.SetKeys(new[]
			{
				new GradientColorKey(BSE.WeightMirrorGradientStop0.Value, 0f),
				new GradientColorKey(BSE.WeightMirrorGradientStop1.Value, 0.33f),
				new GradientColorKey(BSE.WeightMirrorGradientStop2.Value, 0.66f),
				new GradientColorKey(BSE.WeightMirrorGradientStop3.Value, 1f)
			}, alphaKeys);
		}

		public void SetTarget(Renderer renderer)
		{
			_targetRenderer = renderer;
			_deformer = renderer ? renderer.GetComponent<ShapeDeformer>() : null;
			if (_gizmo != null)
				_gizmo.Deformer = _deformer;
			if (_moveTool != null)
				_moveTool.Deformer = _deformer;
		}

		private void Awake()
		{
			_camera = Camera.main;
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (!shader) return;
			_cursorMaterial = new Material(shader);
			_cursorMaterial.SetInt(SrcBlend, 5);
			_cursorMaterial.SetInt(DstBlend, 10);
			_cursorMaterial.SetInt(Cull, 0);
			_cursorMaterial.SetInt(ZWrite, 0);
			_cursorMaterial.SetInt(ZTest, 0);
			_highlightMaterial = new Material(shader);
			_highlightMaterial.SetInt(SrcBlend, 5);
			_highlightMaterial.SetInt(DstBlend, 1);
			_highlightMaterial.SetInt(Cull, 0);
			_highlightMaterial.SetInt(ZWrite, 0);
			_highlightMaterial.SetInt(ZTest, 8);
		}

		private void Update()
		{
			if (BSE.KeyStudioToggle.Value.IsDown())
			{
				Window?.Toggle();
				if (Window != null && Window.Visible)
					OnRefreshRenderers?.Invoke();
			}
			if (Window != null && Window.Visible && !Window.IsEditMode && GetCurrentSelection != null)
			{
				object selection = GetCurrentSelection();
				if (selection != _lastSelection)
				{
					_lastSelection = selection;
					OnRefreshRenderers?.Invoke();
				}
			}
			ProcessDeferredActions();
			
		}

		private void ProcessDeferredActions()
		{
			if (Window == null)
				return;
			if (Window.DeferRefreshRenderers)
			{
				Window.DeferRefreshRenderers = false;
				OnRefreshRenderers?.Invoke();
			}

			if (Window.DeferUpdateWireColors)
			{
				Window.DeferUpdateWireColors = false;
				_wireColorsDirty = true;
			}
			if (Window.DeferEnterEditMode)
			{
				Window.DeferEnterEditMode = false;
				OnRefreshRenderers?.Invoke();
				DoEnterEditMode();
			}
			if (Window.DeferExitEditMode)
			{
				Window.DeferExitEditMode = false;
				DoExitEditMode();
			}
			if (Window.DeferLayerAdd)
			{
				Window.DeferLayerAdd = false;
				DoLayerAdd();
			}
			if (Window.DeferLayerRemove >= 0)
			{
				int layerRemoveIdx = Window.DeferLayerRemove;
				Window.DeferLayerRemove = -1;
				DoLayerRemove(layerRemoveIdx);
			}
			if (Window.DeferLayerMoveUp >= 0)
			{
				int layerMoveUpIdx = Window.DeferLayerMoveUp;
				Window.DeferLayerMoveUp = -1;
				if (Window.ActiveDeformData != null)
				{
					Window.ActiveDeformData.MoveLayerUp(layerMoveUpIdx);
					_undoStack?.Push(new LayerReorderUndoEntry(Window.ActiveDeformData, true));
					StudioUndoBridge.PushDummy(this);
					_deformer?.InvalidateDeltaCache();
				}
			}
			if (Window.DeferLayerMoveDown >= 0)
			{
				int layerMoveDownIdx = Window.DeferLayerMoveDown;
				Window.DeferLayerMoveDown = -1;
				if (Window.ActiveDeformData != null)
				{
					Window.ActiveDeformData.MoveLayerDown(layerMoveDownIdx);
					_undoStack?.Push(new LayerReorderUndoEntry(Window.ActiveDeformData, false));
					StudioUndoBridge.PushDummy(this);
					_deformer?.InvalidateDeltaCache();
				}
			}
			if (Window.WeightUndoLayer >= 0)
			{
				int weightUndoLayer = Window.WeightUndoLayer;
				Window.WeightUndoLayer = -1;
				if (BlendShapeEditorPlugin.UndoLayerWeights.Value
				    && _undoStack != null && Window.ActiveDeformData != null && weightUndoLayer < Window.ActiveDeformData.Layers.Count)
				{
					_undoStack.Push(new LayerWeightUndoEntry(Window.ActiveDeformData.Layers[weightUndoLayer], Window.WeightUndoBefore, Window.WeightUndoAfter));
					StudioUndoBridge.PushDummy(this);
				}
			}
			if (Window.DeferSetMirrorCenter)
			{
				Window.DeferSetMirrorCenter = false;
				if (_gizmo != null && _gizmo.HasTarget)
				{
					Vector3 centroidLocal = _gizmo.CentroidLocal;
					int axisIdx = Window.MirrorAxisIndex;
					float center = axisIdx == 0 ? centroidLocal.x : (axisIdx == 1 ? centroidLocal.y : centroidLocal.z);
					Window.MirrorCenter = center;
					Window.MirrorCenterSet = true;
					_symmetryCenter = center;
					_symmetryCenterSet = true;
				}
			}
			if (Window.DeferClearMirrorCenter)
			{
				Window.DeferClearMirrorCenter = false;
				Window.MirrorCenter = 0f;
				Window.MirrorCenterSet = false;
				_symmetryCenter = 0f;
				_symmetryCenterSet = false;
			}

			if (Window.DeferCheckNameAvailability && _deformer)
			{
				BSE.Logger.LogDebug("Checking Name Availability");
				if (!Window.BakeSeparate)
				{
					Window.BakeNameingIssues = _deformer.ExistingBlendShapeNames.Contains(Window.BakeShapeName) ? Window.BakeShapeName : null;
				}
				else
				{
					var badNames = new List<string>();
					foreach (DeformLayer deformLayer in Window.ActiveDeformData.Layers)
					{
						if (_deformer.ExistingBlendShapeNames.Contains(Window.BakeShapeName + "_" + deformLayer.Name))
						{
							badNames.Add(Window.BakeShapeName + "_" + deformLayer.Name);
						}
					}

					Window.BakeNameingIssues = badNames.Count > 0 ? string.Join(", ", badNames.ToArray()) : null;
				}
				Window.DeferCheckNameAvailability = false;
			}
			if (Window.DeferBake)
			{
				Window.DeferBake = false;
				DoBake();
			}
		}
		
		// private Animator _animatorTurnedOff = null;
		private void DoEnterEditMode()
		{
			if (Window.Renderers.Count == 0)
				return;
			Renderer renderer = Window.SelectedRenderer;
			if (!renderer)
				return;
			DeformData deformData = GetDeformDataForRenderer(renderer, out bool studioMode);
			ShapeDeformer deformer = renderer.GetComponent<ShapeDeformer>();
			if (!deformer)
				deformer = renderer.gameObject.AddComponent<ShapeDeformer>();
			if (deformData != null)
			{
				deformer.StudioMode = studioMode;
				deformer.DeformData = deformData;
			}

			renderer.TryGetOwningController(out Object controller);
			// seems we actually don't need to disable the animator after all
			/*
			if (controller is BlendShapeEditorCharaController charaController)
			{
				Animator animator = charaController.ChaControl.animBody;
				if (animator.enabled)
				{
					_animatorTurnedOff = animator;
					animator.enabled = false;
				}

				if (Chainloader.PluginInfos.ContainsKey(Stiletto.Stiletto.GUID))
				{
					StilettoBridge.StilettoHalt(charaController.ChaControl);
				}
			}
			*/
			
			switch (renderer)
			{
				case SkinnedMeshRenderer smr:
					deformer.Init(smr);
					break;
				case MeshRenderer mr:
				{
					MeshFilter mf = renderer.GetComponent<MeshFilter>();
					if (mf && mr) deformer.Init(mf, mr);
					break;
				}
			}
			SetTarget(renderer);
			Window.ActiveDeformData = deformData;
			Window.SetEditMode(true);
			if (SelectionTool != null)
			{
				switch (renderer)
				{
					case SkinnedMeshRenderer smr:
						SelectionTool.SetTarget(smr);
						break;
					case MeshRenderer mr:
					{
						MeshFilter mf = mr.GetComponent<MeshFilter>();
						if (mf) SelectionTool.SetTarget(mf);
						break;
					}
				}
			}
			_undoStack = new UndoStack(BlendShapeEditorPlugin.UndoMaxSteps.Value);
			if (_moveTool == null)
				_moveTool = new MoveTool();
			if (_smoothTool == null)
				_smoothTool = new SmoothTool();
			if (_inflateTool == null)
				_inflateTool = new InflateTool();
			// TODO Add ifs, also fuck this shit, use fucking Activator.CreateInstance next time on a single field
			_drawTool = new DrawTool();
			_drawSharpTool = new DrawSharpTool();
			_blobTool = new BlobTool();
			_clayTool = new ClayTool();
			_clayStripsTool = new ClayStripsTool();
			_clayThumbTool = new ClayThumbTool();
			_creaseTool = new CreaseTool();
			_layerTool = new LayerTool();
			_fillTool = new FillTool();
			_flattenTool = new FlattenTool();
			if (_gizmo == null)
				_gizmo = new TransformGizmo();
			_moveTool.Deformer = _deformer;
			_gizmo.Deformer = _deformer;
			Transform objectRoot = null;
			
			switch (controller)
			{
				case BlendShapeEditorCharaController charCtrl:
					objectRoot = charCtrl.RootTransform;
					break;
				case BlendShapeEditorItemController itemCtrl:
					objectRoot = itemCtrl.RootTransform;
					break;
			}

			if (!objectRoot)
			{
				// should not happen
				BSE.Logger.LogError("ShapePaintOverlay.EnterEditMode() -> ObjectRoot is null");
				return;
			}
			_gizmo.SetObjectRoot(objectRoot);
			Mesh mesh = MeshHelper.GetMesh(renderer);
			if (!mesh) return;
			_smoothTool.BuildAdjacency(mesh.triangles, mesh.vertexCount, mesh.vertices);
			_fillTool.Adjacency = _smoothTool.Adjacency;
			Window.VertexCount = mesh.vertexCount;
			ActivateHighlight(mesh.vertexCount);
		}

		internal void DoExitEditMode()
		{
			Renderer renderer = Window.SelectedRenderer;
			if (_gizmo != null && _gizmo.IsDragging)
				_gizmo.EndDrag();
			_gizmo?.SetObjectRoot(null);
			DeactivateHighlight();
			SelectionTool?.CleanupCollider();
			if (_deformer)
				DestroyImmediate(_deformer);
			SetTarget(null);
			Window.ActiveDeformData = null;
			Window.SetEditMode(false);
			_wireVerts = null;
			_wireTris = null;
			_edges = null;
			_wireColors = null;
			_lineIndexBuffer = null;
			_visibleLineIndices = null;
			_prevVisibleCount = -1;
			_culledVisibleCount = 0;
			_hoverVertexIndex = -1;
			_vertsUploadDirty = true;
			_prevL2W = Matrix4x4.zero;
			if (_wireLineMesh)
			{
				Destroy(_wireLineMesh);
				_wireLineMesh = null;
			}
			StudioUndoBridge.ClearBSECommands();
			_undoStack = null;
			_isBrushing = false;
			/*
			if (_animatorTurnedOff)
			{
				_animatorTurnedOff.enabled = true;
			}
			renderer.TryGetOwningController(out Object controller);
			if (controller is BlendShapeEditorCharaController charaController &&
			    Chainloader.PluginInfos.ContainsKey(Stiletto.Stiletto.GUID) &&
			    StilettoBridge.StilettoIsHalt(charaController.ChaControl))
			{
				StilettoBridge.StilettoResume(charaController.ChaControl);
			}
			*/
		}

		private void DoBake()
		{
			if (!_deformer || !(_targetRenderer is SkinnedMeshRenderer smr))
				return;
			if (Window.ActiveDeformData == null || !Window.ActiveDeformData.HasLayers)
				return;
			string prefix = Window.BakeShapeName;
			if (string.IsNullOrEmpty(prefix))
				prefix = "BSE_Shape";

			if (Window.BakeSeparate)
			{
				var layers = new List<DeformLayer>(Window.ActiveDeformData.Layers);
				foreach (DeformLayer layer in layers)
				{
					if (layer?.Deltas == null)
						continue;
					Vector3[] scaled;
					if (Mathf.Approximately(layer.Weight, 1f))
					{
						scaled = layer.Deltas;
					}
					else
					{
						scaled = new Vector3[layer.Deltas.Length];
						for (var i = 0; i < scaled.Length; i++)
							scaled[i] = layer.Deltas[i] * layer.Weight;
					}
					var shapeName = $"{prefix}_{layer.Name}";
					int bsIndex = _deformer.BakeToBlendShape(shapeName, scaled, out Vector3[] deltaVerts, out Vector3[] deltaNormals);
					if (bsIndex < 0)
					{
						BSE.Logger.LogWarning($"DoBake: skipping layer '{layer.Name}' — bake failed");
						continue;
					}
					smr.SetBlendShapeWeight(bsIndex, 100f);
					if (!RegisterBakedShape(smr, shapeName, deltaVerts, deltaNormals))
						return;
				}
			}
			else
			{
				int bsIndex = _deformer.BakeToBlendShape(prefix, out Vector3[] deltaVerts, out Vector3[] deltaNormals);
				if (bsIndex < 0)
					return;
				smr.SetBlendShapeWeight(bsIndex, 100f);
				if (!RegisterBakedShape(smr, prefix, deltaVerts, deltaNormals))
					return;
				Window.IncrementBakeShapeName();
			}

			Window.ActiveDeformData.ClearLayers();
			DoExitEditMode();
		}

		private bool RegisterBakedShape(SkinnedMeshRenderer smr, string shapeName, Vector3[] deltaVerts, Vector3[] deltaNormals)
		{
			if (!_deformer.TryGetOwningController(out Object controller))
			{
				BSE.Logger.LogError("Could not bake and register Blendshape");
				BSE.Logger.LogError("ShapePaintOverlay.RegisterBakedShape() -> Controller is null");
				return false;
			}
			switch (controller)
			{
				case BlendShapeEditorItemController itemCtrl:
				{
					string path = itemCtrl.RootTransform.GetPathToChild(smr.transform);
					BlendShapeCreatorBridge.RegisterBlendShapeStudio(itemCtrl.ItemCtrlInfo, path, shapeName, deltaVerts, deltaNormals);
					RefreshPoseController(itemCtrl.gameObject);
					return true;
				}
				case BlendShapeEditorCharaController charCtrl:
				{
					string path = charCtrl.RootTransform.GetPathToChild(smr.transform);
					BlendShapeCreatorBridge.RegisterBlendShapeMaker(charCtrl.ChaControl, path, shapeName, deltaVerts, deltaNormals);
					RefreshPoseController(charCtrl.gameObject);
					return true;
				}
				default:
					BSE.Logger.LogError("Could not bake and register Blendshape");
					BSE.Logger.LogError("ShapePaintOverlay.RegisterBakedShape() -> Controller is null");
					return false;
			}
		}

		private static void RefreshPoseController(GameObject poseControllerOwner)
		{
			try
			{
				poseControllerOwner.GetComponent<PoseController>()?._blendShapesEditor?.RefreshSkinnedMeshRendererList();
			}
			catch (Exception)
			{
				// ignored
			}
		}

		private void DoLayerAdd()
		{
			if (Window.ActiveDeformData == null)
			{
				if (Window.Renderers.Count == 0)
					return;
				Renderer renderer = Window.SelectedRenderer;
				if (!renderer)
					return;
				DeformData deformData = GetDeformDataForRenderer(renderer, out bool studioMode);
				if (deformData == null)
					return;
				Window.ActiveDeformData = deformData;
			}
			Mesh mesh = MeshHelper.GetMesh(GetCurrentRenderer());
			if (!mesh)
				return;
			DeformLayer layer = Window.ActiveDeformData.AddLayer(mesh.vertexCount);
			_undoStack?.Push(new LayerAddUndoEntry(Window.ActiveDeformData, layer, Window.ActiveDeformData.Layers.Count - 1));
			StudioUndoBridge.PushDummy(this);
		}

		private void DoLayerRemove(int layerIndex)
		{
			if (Window.ActiveDeformData == null)
				return;
			DeformData data = Window.ActiveDeformData;
			DeformLayer removedLayer = null;
			int prevActiveIdx = data.ActiveLayerIndex;
			if (layerIndex >= 0 && layerIndex < data.Layers.Count)
				removedLayer = data.Layers[layerIndex];
			data.RemoveLayer(layerIndex);
			if (_undoStack != null && removedLayer != null)
			{
				_undoStack.Push(new LayerRemoveUndoEntry(data, removedLayer, layerIndex, prevActiveIdx));
				StudioUndoBridge.PushDummy(this);
			}
			_deformer?.InvalidateDeltaCache();
			if (Window.ActiveDeformData.ActiveLayer == null)
			{
				_gizmo?.SetTarget(null, null, null);
				SelectionTool?.ClearSelection();
			}
			else if (_gizmo != null && _gizmo.HasTarget)
			{
				_deferGizmoCentroidRefresh = true;
			}
		}

		internal Renderer GetCurrentRenderer()
		{
			if (Window == null || Window.Renderers.Count == 0)
				return null;
			return Window.SelectedRenderer;
		}

		private static DeformData GetDeformDataForRenderer(Renderer renderer, out bool studioMode)
		{
			switch (renderer.TryGetOwningController(out Object controller))
			{
				case true when controller is BlendShapeEditorCharaController charaController:
					studioMode = false;
					return charaController.GetOrCreateDeformData(renderer);
				case true when controller is BlendShapeEditorItemController itemController:
					studioMode = true;
					return itemController.GetOrCreateDeformData(renderer);
				default:
					studioMode = false;
					BlendShapeEditorPlugin.Logger.LogError("Could not get deform data for renderer");
					return null;
			}
		}

		private void LateUpdate()
		{
			if (!_targetRenderer || SelectionTool == null)
				return;
			if (Window == null || !Window.IsEditMode)
				return;
			if (_deformer && _deformer.DisplayMesh)
			{
				SelectionTool.RefreshCollider(_deformer.DisplayMesh);
				RefreshWireframe();
			}
			else
			{
				_refreshTimer += Time.deltaTime;
				if (_refreshTimer >= 0.5f)
				{
					_refreshTimer = 0f;
					SelectionTool.RefreshCollider();
					RefreshWireframe();
				}
			}
			if (_deferGizmoCentroidRefresh)
			{
				_deferGizmoCentroidRefresh = false;
				Vector3[] cachedVertices = SelectionTool.CachedVertices;
				if (_gizmo != null && cachedVertices != null)
					_gizmo.UpdateCentroid(cachedVertices);
			}
			Camera main = _camera;
			if (!main || Input == null)
				return;
			SelectionTool.Radius = Window.BrushRadius;
			SelectionTool.Strength = Window.BrushStrength;
			SelectionTool.Falloff = Window.BrushFalloff;
			if (_gizmo != null)
			{
				bool prevSoftEnabled = _gizmo.SoftSelectionEnabled;
				float prevSoftRadius = _gizmo.SoftSelectionRadius;
				SoftSelectMode prevSoftMode = _gizmo.SoftMode;
				_gizmo.Mode = (GizmoMode)Window.GizmoModeIndex;
				_gizmo.Space = (GizmoSpace)Window.GizmoSpaceIndex;
				_gizmo.SoftSelectionEnabled = Window.GizmoSoftSelection;
				_gizmo.SoftSelectionRadius = Window.GizmoSoftRadius;
				_gizmo.SoftFalloff = Window.GizmoFalloff;
				_gizmo.SoftMode = (SoftSelectMode)Window.SoftSelectModeIndex;
				_gizmo.SizeFactor = Window.GizmoSizeFactor;
				if (_gizmo.HasTarget && (prevSoftEnabled != _gizmo.SoftSelectionEnabled || !Mathf.Approximately(prevSoftRadius, _gizmo.SoftSelectionRadius) || prevSoftMode != _gizmo.SoftMode))
				{
					_softWeightsDirtyTime = Time.unscaledTime;
					_softWeightsDirty = true;
				}
				if (_softWeightsDirty && Time.unscaledTime - _softWeightsDirtyTime >= 0.15f)
				{
					_softWeightsDirty = false;
					Vector3[] cachedVertices2 = SelectionTool.CachedVertices;
					if (cachedVertices2 != null)
					{
						List<int>[] adjacency = _smoothTool?.Adjacency;
						_gizmo.ComputeSoftWeights(cachedVertices2, SelectionTool.Grid, adjacency);
						if (_symmetryEnabled && _gizmo.HasMirrorTarget)
							_gizmo.ComputeMirrorSoftWeights(cachedVertices2, SelectionTool.Grid, adjacency);
						_wireColorsDirty = true;
					}
				}
				// UpdateHover runs every frame for handle highlight rendering
				Transform targetTransform = SelectionTool.TargetTransform;
				_gizmo.UpdateHover(InputHelper.MousePosition, main, targetTransform);
			}
			bool prevSymEnabled = _symmetryEnabled;
			int prevSymAxis = _symmetryAxis;
			float prevSymCenter = _symmetryCenter;
			bool prevSymCenterSet = _symmetryCenterSet;
			_symmetryEnabled = Window.MirrorEnabled;
			_symmetryAxis = Window.MirrorAxisIndex;
			_symmetryCenter = Window.MirrorCenter;
			_symmetryCenterSet = Window.MirrorCenterSet;
			if (_gizmo != null)
			{
				_gizmo.SymmetryEnabled = _symmetryEnabled;
				_gizmo.SymmetryAxis = _symmetryAxis;
				_gizmo.SymmetryCenter = _symmetryCenterSet ? _symmetryCenter : 0f;
				if (!_symmetryEnabled && prevSymEnabled && _gizmo.HasMirrorTarget)
					_gizmo.ClearMirrorTarget();
				if ((_symmetryEnabled != prevSymEnabled || _symmetryAxis != prevSymAxis || _symmetryCenterSet != prevSymCenterSet || !Mathf.Approximately(_symmetryCenter, prevSymCenter)) && _symmetryEnabled && _gizmo.HasTarget)
				{
					Vector3[] cachedVertices3 = SelectionTool.CachedVertices;
					if (cachedVertices3 != null)
						UpdateGizmoTarget(cachedVertices3);
				}
			}
			
			// Backface cull edges + build per-vertex visibility mask (moved from OnRenderObject to overlap with other CPU work)
			if (_edges != null && _wireVerts != null && _lineIndexBuffer != null
				&& _wireTris != null && _triFrontFacing != null && _vertexVisible != null
				&& _triFrontFacing.Length == _wireTris.Length / 3
				&& _vertexVisible.Length == _wireVerts.Length)
			{
				Vector3 camPos = main.transform.position;

				// Pass 1: per-triangle facing + vertex visibility mask
				int triCount = _wireTris.Length / 3;
				Array.Clear(_vertexVisible, 0, _vertexVisible.Length);
				for (int t = 0; t < triCount; t++)
				{
					int b = t * 3;
					int i0 = _wireTris[b];
					int i1 = _wireTris[b + 1];
					int i2 = _wireTris[b + 2];
					Vector3 v0 = _wireVerts[i0];
					Vector3 v1 = _wireVerts[i1];
					Vector3 v2 = _wireVerts[i2];
					bool front = Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), v0 - camPos) <= 0f;
					_triFrontFacing[t] = front;
					if (front)
					{
						_vertexVisible[i0] = true;
						_vertexVisible[i1] = true;
						_vertexVisible[i2] = true;
					}
				}

				// Pass 2: edge cull using the cached per-triangle facing.
				// Skip culling entirely when the user has turned wireframe culling off.
				int edgeCount = _edges.Length;
				bool cullWire = Window == null || Window.CullBackWireframe;
				var writeIdx = 0;
				if (cullWire)
				{
					for (var i = 0; i < edgeCount; i++)
					{
						int tri0 = _edges[i].tri0;
						int tri1 = _edges[i].tri1;
						bool front0 = _triFrontFacing[tri0];
						bool front1 = tri1 >= 0 && _triFrontFacing[tri1];
						if (!front0 && !front1) continue;
						_lineIndexBuffer[writeIdx++] = _edges[i].v0;
						_lineIndexBuffer[writeIdx++] = _edges[i].v1;
					}
				}
				else
				{
					for (var i = 0; i < edgeCount; i++)
					{
						_lineIndexBuffer[writeIdx++] = _edges[i].v0;
						_lineIndexBuffer[writeIdx++] = _edges[i].v1;
					}
				}
				_culledVisibleCount = writeIdx;
			}
			
			// Update raycast hit for cursor rendering and brush entry condition
			if (ShapeEditorWindow.IsMouseOverUI)
			{
				_hasHit = false;
				_hoverVertexIndex = -1;
				return;
			}
			Ray ray = main.ScreenPointToRay(InputHelper.MousePosition);
			_hasHit = SelectionTool.Raycast(ray, out _lastHitPoint, out _lastHitNormal);

			

			// Find closest visible vertex to mouse cursor for hover highlight
			if (!Input.IsCamControlNow) UpdateHoverVertex(main, InputHelper.MousePosition);
		}

		private void UpdateHoverVertex(Camera cam, Vector2 screenMousePos)
		{
			if (_wireVerts == null || _wireVerts.Length == 0)
			{
				_hoverVertexIndex = -1;
				return;
			}
			// Both Input.MousePosition and WorldToScreenPoint use screen space (Y=0 at bottom) — compare directly
			float bestDistSq = DotPixelSize * DotPixelSize * 4f; // search radius = 2× dot size in pixels
			int bestIdx = -1;
			bool restrictToVisible = Window != null && Window.CullBackWireframe
				&& _vertexVisible != null && _vertexVisible.Length == _wireVerts.Length;
			for (var i = 0; i < _wireVerts.Length; i++)
			{
				if (restrictToVisible && !_vertexVisible[i]) continue;
				Vector3 screen = cam.WorldToScreenPoint(_wireVerts[i]);
				if (screen.z <= 0f) continue;
				float dx = screen.x - screenMousePos.x;
				float dy = screen.y - screenMousePos.y;
				float distSq = dx * dx + dy * dy;
				if (distSq < bestDistSq)
				{
					bestDistSq = distSq;
					bestIdx = i;
				}
			}
			_hoverVertexIndex = bestIdx;
		}

		private void ProcessBrushEvent(Event e, Camera cam, DeformLayer activeLayer)
		{
			if (activeLayer == null || Input.IsCamControlNow)
				return;

			// change brush with scroll wheel
			if (_hasHit || _isBrushing)
			{
				float delta = InputHelper.MouseScrollDelta.y;
				if (e.alt)
				{
					float v = delta * 0.001f * BSE.BrushStrengthScrollMod.Value;
					if (Window.BrushStrength + v < 0.01f) Window.BrushStrength = 0.01f;
					else if (Window.BrushStrength + v > 1f) Window.BrushStrength = 1f;
					else Window.BrushStrength += v;
				}
				else
				{
					float v = delta * 0.001f * BSE.BrushRadiusScrollMod.Value;
					if (Window.BrushRadius + v < 0.001f) Window.BrushRadius = 0.01f;
					else if (Window.BrushRadius + v > 0.5f) Window.BrushRadius = 1f;
					else Window.BrushRadius += v;
				}
			}
			
			// user left-clicked on model
			if (e.type == EventType.MouseDown && e.button == 0 && _hasHit)
			{
				_isBrushing = true;
				_brushBeforeSnapshot = new Dictionary<int, Vector3>();
				Input?.SetCameraEnabled(false);
				ApplyBrush(e, cam, activeLayer);
				e.Use();
				return;
			}
			// user is brushing
			if (_isBrushing)
			{
				// move brush while dragging brush
				if (e.type == EventType.MouseDrag && e.button == 0)
				{
					ApplyBrush(e, cam, activeLayer);
					e.Use();
					return;
				}

				// end brush when left-click-up
				if (e.type == EventType.MouseUp && e.button == 0)
				{
					_clayTool.StrokeStarted = false;
					_clayStripsTool.StrokeStarted = false;
					_clayThumbTool.StrokeStarted = false;
					_lastBrushWorldPosition = null;
					_isBrushing = false;
					_moveGrabVertices = null;
					_moveGrabResult = null;
					_moveGrabMirrorResult = null;
					_brushAffectedVertices = null;
					_brushMirrorAffectedVertices = null;
					_wireColorsDirty = true;
					CommitBrushUndoEntry(activeLayer);
					Input?.SetCameraEnabled(true);
					e.Use();
				}
			}
		}

		private void ApplyBrush(Event e, Camera cam, DeformLayer activeLayer)
		{
			Vector3[] deltas = activeLayer.Deltas;
			Vector3[] cachedVertices = SelectionTool.CachedVertices;
			Vector3[] cachedNormals = SelectionTool.CachedNormals;
			bool shouldSmooth = e.shift && _isBrushing;
			float direction = e.control ? -1f : 1f;
			IDeformTool smoothTool = _smoothTool;
			IDeformTool activeTool = shouldSmooth ? smoothTool : GetActiveBrushTool();
			if (activeTool == null) return;

			if (!(activeTool is MoveTool))
				activeTool.Direction = direction;

			BrushResult brushResult;

			if (activeTool is MoveTool moveTool)
			{
				// Move tool grabs vertices once on the first frame, then keeps moving them even when off-mesh
				if (_moveGrabVertices == null)
				{
					Ray ray = cam.ScreenPointToRay(InputHelper.MousePosition);
					BrushResult initialResult = SelectionTool.BrushSelect(ray);
					if (initialResult == null || initialResult.AffectedVertices.Count == 0)
						return;
					_moveGrabVertices = new Dictionary<int, float>(initialResult.AffectedVertices);
					_moveGrabResult = new BrushResult
					{
						HitPoint = initialResult.HitPoint,
						HitNormal = initialResult.HitNormal,
						AffectedVertices = _moveGrabVertices
					};
				}
				brushResult = _moveGrabResult;
				moveTool.RendererTransform = _targetRenderer ? _targetRenderer.transform : null;
				moveTool.UseViewPlane = !e.alt;
				Vector3 hitScreen = cam.WorldToScreenPoint(brushResult.HitPoint);
				Vector3 hitWorld = cam.ScreenToWorldPoint(hitScreen);
				Vector3 hitWorldOffset = cam.ScreenToWorldPoint(new Vector3(hitScreen.x + 1f, hitScreen.y, hitScreen.z));
				float pixelSize = Vector3.Distance(hitWorld, hitWorldOffset);
				float dx = e.delta.x;
				float dy = -e.delta.y; // e.delta is GUI space (Y+ = down); negate to get screen space (Y+ = up)
				if (moveTool.UseViewPlane)
					moveTool.MouseDelta = new Vector2(dx * pixelSize, dy * pixelSize);
				else
					moveTool.DragDelta = dy * pixelSize;
			}
			else
			{
				// Inflate/Smooth: need a live raycast hit each frame
				Ray ray = cam.ScreenPointToRay(InputHelper.MousePosition);
				brushResult = SelectionTool.BrushSelect(ray);
				if (brushResult == null || brushResult.AffectedVertices.Count == 0)
					return;
			}

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices.Where(pair => !_brushBeforeSnapshot.ContainsKey(pair.Key)))
			{
				_brushBeforeSnapshot[pair.Key] = deltas[pair.Key];
			}
			
			if (_lastBrushWorldPosition.HasValue && brushResult != null)
			{
				Vector3 strokeDir = (brushResult.HitPoint - _lastBrushWorldPosition.Value).normalized;
				if (activeTool is ClayStripsTool strips)
					strips.StrokeDirection = strokeDir;
				if (activeTool is ClayThumbTool thumb)
					thumb.StrokeDirection = strokeDir;
			}
			_lastBrushWorldPosition = brushResult?.HitPoint;
			
			if (!shouldSmooth && _isBrushing && brushResult != null)
			{
				if (activeTool is ClayTool clay && !clay.StrokeStarted)
				{
					clay.StrokePlaneOrigin = brushResult.HitPoint;
					clay.StrokePlaneNormal = brushResult.HitNormal;
					clay.StrokeStarted = true;
				}
				if (activeTool is ClayStripsTool strips && !strips.StrokeStarted)
				{
					strips.StrokePlaneOrigin = brushResult.HitPoint;
					strips.StrokePlaneNormal = brushResult.HitNormal;
					strips.StrokeStarted = true;
				}
				if (activeTool is ClayThumbTool thumb && !thumb.StrokeStarted)
				{
					thumb.StrokePlaneOrigin = brushResult.HitPoint;
					thumb.StrokePlaneNormal = brushResult.HitNormal;
					thumb.StrokeStarted = true;
				}
				if (activeTool is FlattenTool flatten)
					flatten.PlaneComputed = false;
			}
			
			activeTool.Apply(activeLayer, brushResult, cachedVertices, cachedNormals, cam);
			_brushAffectedVertices = brushResult.AffectedVertices;
			if (!_symmetryEnabled || !_targetRenderer)
			{
				_brushMirrorAffectedVertices = null;
				return;
			}
			{
				BrushResult mirrorResult;
				if (activeTool is MoveTool && _moveGrabMirrorResult != null)
				{
					mirrorResult = _moveGrabMirrorResult;
				}
				else
				{
					Transform xform = _targetRenderer.transform;
					Vector3 localHit = xform.InverseTransformPoint(brushResult.HitPoint);
					float symCenter = _symmetryCenterSet ? _symmetryCenter : 0f;
					int axis = _symmetryAxis;
					if (axis == 0)
						localHit.x = symCenter * 2f - localHit.x;
					else if (axis == 1)
						localHit.y = symCenter * 2f - localHit.y;
					else
						localHit.z = symCenter * 2f - localHit.z;
					Vector3 mirrorWorld = xform.TransformPoint(localHit);
					Vector3 localNormal = xform.InverseTransformDirection(brushResult.HitNormal);
					if (axis == 0)
						localNormal.x = -localNormal.x;
					else if (axis == 1)
						localNormal.y = -localNormal.y;
					else
						localNormal.z = -localNormal.z;
					Vector3 mirrorWorldNormal = xform.TransformDirection(localNormal);
					Vector3 colliderLocalPoint = SelectionTool.ColliderTransform ? SelectionTool.ColliderTransform.InverseTransformPoint(mirrorWorld) : localHit;
					mirrorResult = SelectionTool.BrushSelectAtPoint(colliderLocalPoint, mirrorWorld, mirrorWorldNormal);
					if (activeTool is MoveTool && mirrorResult != null && _moveGrabMirrorResult == null)
						_moveGrabMirrorResult = mirrorResult;
				}
				if (mirrorResult != null && mirrorResult.AffectedVertices.Count > 0)
				{
					foreach (KeyValuePair<int, float> pair in mirrorResult.AffectedVertices)
					{
						if (_brushBeforeSnapshot != null && !_brushBeforeSnapshot.ContainsKey(pair.Key))
							_brushBeforeSnapshot[pair.Key] = deltas[pair.Key];
					}
					MoveTool mirrorMoveTool = activeTool as MoveTool;
					if (mirrorMoveTool != null)
						mirrorMoveTool.MirrorAxis = _symmetryAxis;
					activeTool.Apply(activeLayer, mirrorResult, cachedVertices, cachedNormals, cam);
					if (mirrorMoveTool != null)
						mirrorMoveTool.MirrorAxis = -1;
					_brushMirrorAffectedVertices = mirrorResult.AffectedVertices;
				}
				else
				{
					_brushMirrorAffectedVertices = null;
				}
			}
			_wireColorsDirty = true;
		}

		private void CommitBrushUndoEntry(DeformLayer layer)
		{
			if (_undoStack == null || _brushBeforeSnapshot == null || _brushBeforeSnapshot.Count == 0 || layer == null)
			{
				_brushBeforeSnapshot = null;
				return;
			}
			var indices = new int[_brushBeforeSnapshot.Count];
			var before = new Vector3[_brushBeforeSnapshot.Count];
			var after = new Vector3[_brushBeforeSnapshot.Count];
			var i = 0;
			Vector3[] deltas = layer.Deltas;
			foreach (KeyValuePair<int, Vector3> pair in _brushBeforeSnapshot)
			{
				indices[i] = pair.Key;
				before[i] = pair.Value;
				after[i] = deltas[pair.Key];
				i++;
			}
			_undoStack.Push(new DeltaUndoEntry(layer, indices, before, after));
			StudioUndoBridge.PushDummy(this);
			_brushBeforeSnapshot = null;
		}

		private void PostUndoRedoCleanup()
		{
			DeformData data = Window.ActiveDeformData;
			if (data != null && data.ActiveLayer == null)
			{
				_gizmo?.SetTarget(null, null, null);
				SelectionTool?.ClearSelection();
			}
			else if (_gizmo != null && _gizmo.HasTarget)
			{
				_deferGizmoCentroidRefresh = true;
			}
		}

		public void UndoOneStep()
		{
			if (_undoStack == null || !_undoStack.CanUndo) return;
			UndoContext ctx = new UndoContext { Deformer = _deformer, Data = Window?.ActiveDeformData, Window = Window };
			_undoStack.Undo(ctx);
			PostUndoRedoCleanup();
		}

		public void RedoOneStep()
		{
			if (_undoStack == null || !_undoStack.CanRedo) return;
			UndoContext ctx = new UndoContext { Deformer = _deformer, Data = Window?.ActiveDeformData, Window = Window };
			_undoStack.Redo(ctx);
			PostUndoRedoCleanup();
		}

		private void CommitGizmoUndoEntry(DeformLayer layer)
		{
			if (_undoStack == null || _gizmoBeforeSnapshot == null || _gizmoBeforeSnapshot.Count == 0 || layer == null)
			{
				_gizmoBeforeSnapshot = null;
				return;
			}
			Vector3[] deltas = layer.Deltas;
			var indices = new int[_gizmoBeforeSnapshot.Count];
			var before = new Vector3[_gizmoBeforeSnapshot.Count];
			var after = new Vector3[_gizmoBeforeSnapshot.Count];
			var i = 0;
			var hasChanges = false;
			foreach (KeyValuePair<int, Vector3> pair in _gizmoBeforeSnapshot)
			{
				indices[i] = pair.Key;
				before[i] = pair.Value;
				after[i] = deltas[pair.Key];
				if ((after[i] - before[i]).sqrMagnitude > 0f)
					hasChanges = true;
				i++;
			}
			if (hasChanges)
			{
				_undoStack.Push(new DeltaUndoEntry(layer, indices, before, after));
				StudioUndoBridge.PushDummy(this);
			}
			_gizmoBeforeSnapshot = null;
		}

		private void ProcessGizmoEvent(Event e, Camera cam, DeformLayer activeLayer)
		{
			if (_gizmo == null || activeLayer == null || Input.IsCamControlNow)
				return;
			Transform targetTransform = SelectionTool.TargetTransform;
			Vector3[] cachedVertices = SelectionTool.CachedVertices;

			// scroll events
			if (_gizmo.HasTarget)
			{
				float delta = InputHelper.MouseScrollDelta.y;
				if (e.alt)
				{
					float v = delta * 0.001f * BSE.GizmoSizeScrollMod.Value;
					if (Window.GizmoSizeFactor + v < 0.01f) Window.GizmoSizeFactor = 0.01f;
					else if (Window.GizmoSizeFactor + v > 0.15f) Window.GizmoSizeFactor = 0.15f;
					else Window.GizmoSizeFactor += v;
				}
				else if (_gizmo.SoftSelectionEnabled)
				{
					float v = delta * 0.001f * BSE.GizmoSoftSelectionScrollMod.Value;
					if (Window.GizmoSoftRadius + v < 0.001f)  Window.GizmoSoftRadius = 0.001f;
					else if (Window.GizmoSoftRadius + v > 0.5f) Window.GizmoSoftRadius = 0.5f;
					Window.GizmoSoftRadius += v;
				}
			}
			
			// Gizmo drag (LMB on handle) — skipped when Alt is held (Alt+LMB = box selection)
			if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && _gizmo.HoveredAxis != GizmoEnums.None)
			{
				Vector2 screenPos = GUIToScreenPos(e.mousePosition);
				if (!_gizmo.BeginDrag(screenPos, cam, targetTransform, activeLayer, cachedVertices)) return;
				_gizmoBeforeSnapshot = new Dictionary<int, Vector3>(_gizmo.DragStartDeltas);
				Input?.SetCameraEnabled(false);
				e.Use();
				return;
			}
			if (e.type == EventType.MouseDrag && e.button == 0 && _gizmo.IsDragging)
			{
				_gizmo.UpdateDrag(GUIToScreenPos(e.mousePosition), cam, targetTransform, activeLayer, cachedVertices);
				e.Use();
				return;
			}
			if (e.type == EventType.MouseUp && e.button == 0 && _gizmo.IsDragging)
			{
				_gizmo.EndDrag();
				CommitGizmoUndoEntry(activeLayer);
				Input?.SetCameraEnabled(true);
				e.Use();
				return;
			}

			// Alt+LMB: box selection
			if (e.type == EventType.MouseDown && e.button == 0 && e.alt && !_gizmo.IsDragging)
			{
				_isBoxSelecting = true;
				_boxStart = e.mousePosition;
				_boxEnd = e.mousePosition;
				Input?.SetCameraEnabled(false);
				e.Use();
				return;
			}
			if (e.type == EventType.MouseDrag && e.button == 0 && _isBoxSelecting)
			{
				_boxEnd = e.mousePosition;
				e.Use();
				return;
			}
			if (e.type == EventType.MouseUp && e.button == 0 && _isBoxSelecting)
			{
				_isBoxSelecting = false;
				CommitBoxSelection(e.modifiers, cam, cachedVertices);
				Input?.SetCameraEnabled(true);
				e.Use();
				return;
			}

			// LMB click on model (no Alt, no gizmo handle): single-vertex snap
			if (_hoverVertexIndex >= 0)
			{
				if (e.type == EventType.MouseDown && e.button == 0)
				{
					Input?.SetCameraEnabled(false);
				}
				
				if (e.type == EventType.MouseUp && e.button == 0 && !_gizmo.IsDragging && !e.alt)
				{
					TrySelectVertexAtHit(e.modifiers, cachedVertices);
					UpdateGizmoTarget(cachedVertices);
					e.Use();
					Input?.SetCameraEnabled(true);
				}
			}
			
		}

		private void CommitBoxSelection(EventModifiers mods, Camera cam, Vector3[] cachedVertices)
		{
			float boxW = Mathf.Abs(_boxEnd.x - _boxStart.x);
			float boxH = Mathf.Abs(_boxEnd.y - _boxStart.y);
			bool isClick = boxW < 5f && boxH < 5f;

			if (isClick)
			{
				TrySelectVertexAtHit(mods, cachedVertices);
			}
			else
			{
				// Box selection operates in GUI space; SelectBox/DeselectBox expect screen space (Y-flipped)
				float xMin = Mathf.Min(_boxStart.x, _boxEnd.x);
				float xMax = Mathf.Max(_boxStart.x, _boxEnd.x);
				float yMin = Mathf.Min(_boxStart.y, _boxEnd.y);
				float yMax = Mathf.Max(_boxStart.y, _boxEnd.y);
				// Convert GUI rect to screen rect (flip Y)
				float screenYMin = Screen.height - yMax;
				float screenYMax = Screen.height - yMin;
				Rect screenRect = new Rect(xMin, screenYMin, xMax - xMin, screenYMax - screenYMin);
				bool[] visMask = (Window != null && Window.CullBackWireframe) ? _vertexVisible : null;
				if ((mods & EventModifiers.Control) != 0)
					SelectionTool.DeselectBox(cam, screenRect, visMask);
				else
					SelectionTool.SelectBox(cam, screenRect, (mods & EventModifiers.Shift) != 0, visMask);
			}

			UpdateGizmoTarget(cachedVertices);
		}

		private void TrySelectVertexAtHit(EventModifiers mods, Vector3[] cachedVertices)
		{
			// _lastHitPoint is already set by LateUpdate raycast; use it for vertex snap
			if (!_hasHit)
			{
				if ((mods & (EventModifiers.Shift | EventModifiers.Control)) == 0)
					SelectionTool.ClearSelection();
				return;
			}
			int bestIdx = _hoverVertexIndex;
			if (bestIdx >= 0)
			{
				if ((mods & EventModifiers.Shift) != 0)
					SelectionTool.SelectedVertices.Add(bestIdx);
				else if ((mods & EventModifiers.Control) != 0)
					SelectionTool.SelectedVertices.Remove(bestIdx);
				else
				{
					SelectionTool.ClearSelection();
					SelectionTool.SelectedVertices.Add(bestIdx);
				}
			}
			else if ((mods & (EventModifiers.Shift | EventModifiers.Control)) == 0)
			{
				SelectionTool.ClearSelection();
			}
		}

		private void UpdateGizmoTarget(Vector3[] vertices)
		{
			if (_gizmo == null || SelectionTool == null)
				return;
			Vector3[] cachedNormals = SelectionTool.CachedNormals;
			_gizmo.SetTarget(SelectionTool.SelectedVertices, vertices, cachedNormals);
			if (_symmetryEnabled && _gizmo.HasTarget && SelectionTool.Grid != null)
			{
				var mirrorIndices = new HashSet<int>();
				SpatialHashGrid grid = SelectionTool.Grid;
				float symmetryOffset = _symmetryCenterSet ? _symmetryCenter : 0f;
				int axis = _symmetryAxis;
				HashSet<int> selected = SelectionTool.SelectedVertices;
				foreach (int idx in selected)
				{
					if (idx < 0 || idx >= vertices.Length) continue;
					Vector3 mirrorPos = vertices[idx];
					switch (axis)
					{
						case 0:
							mirrorPos.x = symmetryOffset * 2f - mirrorPos.x;
							break;
						case 1:
							mirrorPos.y = symmetryOffset * 2f - mirrorPos.y;
							break;
						default:
							mirrorPos.z = symmetryOffset * 2f - mirrorPos.z;
							break;
					}
					grid.FindVerticesInRadius(mirrorPos, 0.0002f, (found, distSq) =>
					{
						if (!selected.Contains(found))
							mirrorIndices.Add(found);
					});
				}
				_gizmo.SetMirrorTarget(mirrorIndices);
			}
			List<int>[] adjacency = _smoothTool?.Adjacency;
			if (_gizmo.SoftSelectionEnabled && _gizmo.HasTarget)
				_gizmo.ComputeSoftWeights(vertices, SelectionTool.Grid, adjacency);
			if (_symmetryEnabled && _gizmo.HasMirrorTarget && _gizmo.SoftSelectionEnabled)
				_gizmo.ComputeMirrorSoftWeights(vertices, SelectionTool.Grid, adjacency);
			_wireColorsDirty = true;
		}

		// Event.mousePosition uses GUI space (Y=0 at top); camera/gizmo methods expect screen space (Y=0 at bottom)
		private static Vector2 GUIToScreenPos(Vector2 guiPos) => new Vector2(guiPos.x, Screen.height - guiPos.y);

		private IDeformTool GetActiveBrushTool()
		{
			switch (Window.SelectedBrushTool)
			{
				case ShapeEditorWindow.BrushToolType.Move:       return _moveTool;
				case ShapeEditorWindow.BrushToolType.Smooth:     return _smoothTool;
				case ShapeEditorWindow.BrushToolType.Inflate:    return _inflateTool;
				case ShapeEditorWindow.BrushToolType.Draw:       return _drawTool;
				case ShapeEditorWindow.BrushToolType.DrawSharp:  return _drawSharpTool;
				case ShapeEditorWindow.BrushToolType.Blob:       return _blobTool;
				case ShapeEditorWindow.BrushToolType.Clay:       return _clayTool;
				case ShapeEditorWindow.BrushToolType.ClayStrips: return _clayStripsTool;
				case ShapeEditorWindow.BrushToolType.ClayThumb:  return _clayThumbTool;
				case ShapeEditorWindow.BrushToolType.Crease:     return _creaseTool;
				case ShapeEditorWindow.BrushToolType.Layer:      return _layerTool;
				case ShapeEditorWindow.BrushToolType.Fill:       return _fillTool;
				case ShapeEditorWindow.BrushToolType.Flatten:    return _flattenTool;
				default: return _moveTool;
			}
		}

		private void OnRenderObject()
		{
			if (Camera.current != _camera)
				return;
			if (Window == null)
				return;
			if (Window.Visible && !Window.IsEditMode && Window.ShowMeshHighlight && _highlightMaterial)
				DrawMeshHighlight();
			if (!_cursorMaterial)
				return;
			if (!Window.IsEditMode)
				return;
			bool hasWire = _wireVerts != null && _wireTris != null;
			if (hasWire) DrawWireframe();
			if (!hasWire && !_hasHit)
				return;
			_cursorMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(Matrix4x4.identity);
			if (hasWire)
				DrawVertexDots(_camera);
			if (Window.OperationMode == ShapeEditorWindow.OpMode.Brush)
			{
				if (_hasHit)
					DrawBrushCircle(_lastHitPoint, _lastHitNormal, Window.BrushRadius);
			}
			if (_isBoxSelecting)
				DrawBoxSelectRect();
			GL.PopMatrix();
			if (Window.OperationMode != ShapeEditorWindow.OpMode.Gizmo || _gizmo == null || !_gizmo.HasTarget) return;
			Transform xform = SelectionTool?.TargetTransform;
			_gizmo.Render(Camera.main, xform);
			_gizmo.RenderSoftRadius(Camera.main, xform);
		}

		private void DrawBrushCircle(Vector3 center, Vector3 normal, float radius)
		{
			if (normal.sqrMagnitude < 0.001f)
				normal = Vector3.up;
			normal.Normalize();
			Vector3 tangent = Vector3.Cross(normal, Vector3.up);
			if (tangent.sqrMagnitude < 0.001f)
				tangent = Vector3.Cross(normal, Vector3.right);
			tangent.Normalize();
			Vector3 bitangent = Vector3.Cross(normal, tangent);
			GL.Begin(1);
			Color col;
			switch (Window.SelectedBrushTool)
			{
				case ShapeEditorWindow.BrushToolType.Move:       col = BSE.BrushColorMove.Value; break;
				case ShapeEditorWindow.BrushToolType.Smooth:     col = BSE.BrushColorSmooth.Value; break;
				case ShapeEditorWindow.BrushToolType.Inflate:    col = BSE.BrushColorInflate.Value; break;
				// Can't be fucked to add colors for these
				/*case ShapeEditorWindow.BrushToolType.Draw:       col = BSE.BrushColorDraw.Value; break;
				case ShapeEditorWindow.BrushToolType.DrawSharp:  col = BSE.BrushColorDrawSharp.Value; break;
				case ShapeEditorWindow.BrushToolType.Blob:       col = BSE.BrushColorBlob.Value; break;
				case ShapeEditorWindow.BrushToolType.Clay:       col = BSE.BrushColorClay.Value; break;
				case ShapeEditorWindow.BrushToolType.ClayStrips: col = BSE.BrushColorClayStrips.Value; break;
				case ShapeEditorWindow.BrushToolType.ClayThumb:  col = BSE.BrushColorClayThumb.Value; break;
				case ShapeEditorWindow.BrushToolType.Crease:     col = BSE.BrushColorCrease.Value; break;
				case ShapeEditorWindow.BrushToolType.Layer:      col = BSE.BrushColorLayer.Value; break;
				case ShapeEditorWindow.BrushToolType.Fill:       col = BSE.BrushColorFill.Value; break;
				case ShapeEditorWindow.BrushToolType.Flatten:    col = BSE.BrushColorFlatten.Value; break;*/
				default: col = new Color(1, 1, 0, 0.9f); break;
			}
			GL.Color(col);
			for (var i = 0; i < 32; i++)
			{
				float a0 = (float)i / 32f * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / 32f * Mathf.PI * 2f;
				Vector3 p0 = center + (tangent * Mathf.Cos(a0) + bitangent * Mathf.Sin(a0)) * radius;
				Vector3 p1 = center + (tangent * Mathf.Cos(a1) + bitangent * Mathf.Sin(a1)) * radius;
				GL.Vertex(p0);
				GL.Vertex(p1);
			}
			GL.End();
			float crossSize = radius * 0.05f;
			if (crossSize < 0.001f)
				crossSize = 0.001f;
			GL.Begin(1);
			GL.Color(col);
			GL.Vertex(center - tangent * crossSize);
			GL.Vertex(center + tangent * crossSize);
			GL.Vertex(center - bitangent * crossSize);
			GL.Vertex(center + bitangent * crossSize);
			GL.End();
		}

		private void DrawVertexDots(Camera cam)
		{
			if (_wireVerts == null || cam == null)
				return;
			// Compute billboard offset vectors in world space (camera-right / camera-up)
			// Convert dot pixel size to a world-space half-extent
			Vector3 camPos = cam.transform.position;
			Transform ct = cam.transform;
			// Pick a reference depth to convert pixels → world units. Use distance to mesh centroid estimate.
			float refDist = _wireVerts.Length > 0 ? Vector3.Distance(camPos, _wireVerts[0]) : 5f;
			// One pixel in world units at refDist for a perspective camera
			float pixelWorldSize = refDist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f / Screen.height;
			float half = DotPixelSize * pixelWorldSize * 0.5f;
			Vector3 right = ct.right * half;
			Vector3 up = ct.up * half;
			bool inGizmoMode = Window.OperationMode == ShapeEditorWindow.OpMode.Gizmo;
			bool inBrushMode = Window.OperationMode == ShapeEditorWindow.OpMode.Brush;
			HashSet<int> selectedVerts = inGizmoMode && SelectionTool != null ? SelectionTool.SelectedVertices : null;
			bool useGizmoSoftGradient = inGizmoMode && _gizmo != null && _gizmo.SoftSelectionEnabled && _gizmo.HasTarget;
			Dictionary<int, float> primarySoftWeights = useGizmoSoftGradient ? _gizmo.PrimarySoftWeights : null;
			Dictionary<int, float> mirrorSoftWeights = useGizmoSoftGradient && _gizmo.HasMirrorTarget ? _gizmo.MirrorSoftWeights : null;
			Dictionary<int, float> brushAffected = inBrushMode ? _brushAffectedVertices : null;
			Dictionary<int, float> brushMirrorAffected = inBrushMode ? _brushMirrorAffectedVertices : null;
			ShapeEditorWindow.VertexDisplayType displayMode = Window != null ? Window.VertexDisplayMode : ShapeEditorWindow.VertexDisplayType.All;
			bool hideOccludedDots = displayMode == ShapeEditorWindow.VertexDisplayType.BackfaceCulling
				&& _vertexVisible != null && _vertexVisible.Length == _wireVerts.Length;
			bool interactOnly = displayMode == ShapeEditorWindow.VertexDisplayType.Interact;
			GL.Begin(4); // triangles
			for (var i = 0; i < _wireVerts.Length; i++)
			{
				bool isInteracting = (selectedVerts != null && selectedVerts.Contains(i))
					|| i == _hoverVertexIndex
					|| (primarySoftWeights != null && primarySoftWeights.ContainsKey(i))
					|| (mirrorSoftWeights != null && mirrorSoftWeights.ContainsKey(i))
					|| (brushAffected != null && brushAffected.ContainsKey(i))
					|| (brushMirrorAffected != null && brushMirrorAffected.ContainsKey(i));
				if (interactOnly && !isInteracting)
					continue;
				if (hideOccludedDots && !_vertexVisible[i] && !isInteracting)
					continue;
				Vector3 c = _wireVerts[i];
				Color col;
				if (i == _hoverVertexIndex)
					col = BSE.VertexColorHover.Value;
				else if (primarySoftWeights != null && primarySoftWeights.TryGetValue(i, out float gw))
					col = WeightGradient.Evaluate(gw);
				else if (mirrorSoftWeights != null && mirrorSoftWeights.TryGetValue(i, out float mgw))
					col = MirrorWeightGradient.Evaluate(mgw);
				else if (selectedVerts != null && selectedVerts.Contains(i))
					col = BSE.VertexColorSelected.Value;
				else if (brushAffected != null && brushAffected.TryGetValue(i, out float weight))
					col = WeightGradient.Evaluate(weight);
				else if (brushMirrorAffected != null && brushMirrorAffected.TryGetValue(i, out float mWeight))
					col = MirrorWeightGradient.Evaluate(mWeight);
				else
					col = BSE.VertexColorDefault.Value;
				GL.Color(col);
				// Two triangles forming a quad facing the camera
				Vector3 bl = c - right - up;
				Vector3 br = c + right - up;
				Vector3 tl = c - right + up;
				Vector3 tr = c + right + up;
				GL.Vertex(bl); GL.Vertex(br); GL.Vertex(tr);
				GL.Vertex(bl); GL.Vertex(tr); GL.Vertex(tl);
			}
			GL.End();
		}

		private void DrawWireframe()
		{
			if (_edges == null || !_wireLineMesh)
				return;
			Camera main = _camera;
			if (!main)
				return;
			bool useSoftColors = _gizmo != null && _gizmo.SoftSelectionEnabled && _gizmo.HasTarget && Window.OperationMode == ShapeEditorWindow.OpMode.Gizmo;
			bool useBrushColors = Window.OperationMode == ShapeEditorWindow.OpMode.Brush && (_brushAffectedVertices != null || _brushMirrorAffectedVertices != null);
			bool inGizmoMode = Window.OperationMode == ShapeEditorWindow.OpMode.Gizmo;
			HashSet<int> selectedVerts = inGizmoMode && SelectionTool != null ? SelectionTool.SelectedVertices : null;
			HashSet<int> mirrorVerts = inGizmoMode && _symmetryEnabled && _gizmo != null && _gizmo.HasMirrorTarget ? _gizmo.MirrorIndices : null;
			bool hasSelection = selectedVerts != null && selectedVerts.Count > 0;
			bool hasMirror = mirrorVerts != null && mirrorVerts.Count > 0;
			if (_wireColorsDirty || useSoftColors != _prevUseSoftColors || useBrushColors != _prevUseBrushColors || hasMirror != _prevHasMirror || _wireColors == null || _wireColors.Length != _wireVerts.Length)
			{
				RebuildWireColors(useSoftColors, useBrushColors, hasSelection ? selectedVerts : null, hasMirror ? mirrorVerts : null);
				_wireColorsDirty = false;
				_prevUseSoftColors = useSoftColors;
				_prevUseBrushColors = useBrushColors;
				_prevHasMirror = hasMirror;
			}
			// Cull results were computed in LateUpdate; use them directly
			int writeIdx = _culledVisibleCount;
			if (writeIdx != _prevVisibleCount)
			{
				_visibleLineIndices = new int[writeIdx];
				_prevVisibleCount = writeIdx;
			}
			Array.Copy(_lineIndexBuffer, _visibleLineIndices, writeIdx);
			// Only re-upload vertex/color data when something changed
			if (_vertsUploadDirty)
			{
				_wireLineMesh.vertices = _wireVerts;
				_vertsUploadDirty = false;
			}
			_wireLineMesh.colors32 = _wireColors;
			_wireLineMesh.SetIndices(_visibleLineIndices, MeshTopology.Lines, 0);
			_cursorMaterial.SetPass(0);
			Graphics.DrawMeshNow(_wireLineMesh, Matrix4x4.identity);
		}

		private void RebuildWireColors(bool useSoftColors, bool useBrushColors, HashSet<int> selectedVerts, HashSet<int> mirrorVerts)
		{
			int count = _wireVerts.Length;
			if (_wireColors == null || _wireColors.Length != count)
				_wireColors = new Color32[count];
			Color32 defCol = BSE.WireColorDefault.Value;
			if (useSoftColors)
			{
				Dictionary<int, float> primary = _gizmo.PrimarySoftWeights;
				Dictionary<int, float> mirror = _gizmo.HasMirrorTarget ? _gizmo.MirrorSoftWeights : null;
				for (var i = 0; i < count; i++)
				{
					if (primary != null && primary.TryGetValue(i, out float w))
						_wireColors[i] = WeightGradient.Evaluate(w);
					else if (mirror != null && mirror.TryGetValue(i, out float mw))
						_wireColors[i] = MirrorWeightGradient.Evaluate(mw);
					else
						_wireColors[i] = defCol;
				}
				return;
			}
			if (useBrushColors)
			{
				Dictionary<int, float> primary = _brushAffectedVertices;
				Dictionary<int, float> mirror = _brushMirrorAffectedVertices;
				for (var i = 0; i < count; i++)
				{
					if (primary != null && primary.TryGetValue(i, out float w))
						_wireColors[i] = WeightGradient.Evaluate(w);
					else if (mirror != null && mirror.TryGetValue(i, out float mw))
						_wireColors[i] = MirrorWeightGradient.Evaluate(mw);
					else
						_wireColors[i] = defCol;
				}
				return;
			}
			bool hasSelection = selectedVerts != null && selectedVerts.Count > 0;
			bool hasMirror = mirrorVerts != null && mirrorVerts.Count > 0;
			Color32 selCol = BSE.WireColorSelected.Value;
			Color32 mirCol = BSE.WireColorMirror.Value;
			for (var j = 0; j < count; j++)
			{
				if (hasSelection && selectedVerts.Contains(j))
					_wireColors[j] = selCol;
				else if (hasMirror && mirrorVerts.Contains(j))
					_wireColors[j] = mirCol;
				else
					_wireColors[j] = defCol;
			}
		}

		private void DrawBoxSelectRect()
		{
			GL.PushMatrix();
			GL.LoadPixelMatrix();
			// _boxStart/_boxEnd are GUI space (Y=0 at top); GL.LoadPixelMatrix uses screen space (Y=0 at bottom)
			float xMin = Mathf.Min(_boxStart.x, _boxEnd.x);
			float xMax = Mathf.Max(_boxStart.x, _boxEnd.x);
			float yMin = Screen.height - Mathf.Max(_boxStart.y, _boxEnd.y);
			float yMax = Screen.height - Mathf.Min(_boxStart.y, _boxEnd.y);
			GL.Begin(7);
			GL.Color(new Color(0.2f, 0.6f, 1f, 0.15f));
			GL.Vertex3(xMin, yMin, 0f);
			GL.Vertex3(xMax, yMin, 0f);
			GL.Vertex3(xMax, yMax, 0f);
			GL.Vertex3(xMin, yMax, 0f);
			GL.End();
			GL.Begin(1);
			GL.Color(new Color(0.2f, 0.6f, 1f, 0.8f));
			GL.Vertex3(xMin, yMin, 0f);
			GL.Vertex3(xMax, yMin, 0f);
			GL.Vertex3(xMax, yMin, 0f);
			GL.Vertex3(xMax, yMax, 0f);
			GL.Vertex3(xMax, yMax, 0f);
			GL.Vertex3(xMin, yMax, 0f);
			GL.Vertex3(xMin, yMax, 0f);
			GL.Vertex3(xMin, yMin, 0f);
			GL.End();
			GL.PopMatrix();
		}

		private void DrawMeshHighlight()
		{
			if (Window.SelectedRendererIndex < 0 || Window.SelectedRendererIndex >= Window.Renderers.Count)
				return;
			Renderer renderer = Window.Renderers[Window.SelectedRendererIndex];
			if (!renderer)
				return;
			ShapeDeformer deformer = renderer.GetComponent<ShapeDeformer>();
			if (deformer && deformer.DisplayMesh && deformer.DisplayTransform)
			{
				DrawMeshHighlightFromDeformer(deformer, renderer);
				return;
			}
			Mesh poseMesh = null;
			Mesh sourceMesh;
			Matrix4x4 l2w;
			SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
			if (smr)
			{
				if (!smr.sharedMesh)
					return;
				if (!_bakeMeshCache)
					_bakeMeshCache = new Mesh();
				smr.BakeMesh(_bakeMeshCache);
				poseMesh = _bakeMeshCache;
				sourceMesh = smr.sharedMesh;
				Transform xform = renderer.transform;
				l2w = Matrix4x4.TRS(xform.position, xform.rotation, Vector3.one);
			}
			else
			{
				l2w = renderer.localToWorldMatrix;
				MeshFilter mf = renderer.GetComponent<MeshFilter>();
				if (mf)
					poseMesh = mf.sharedMesh;
				sourceMesh = poseMesh;
			}
			if (!poseMesh || !poseMesh.isReadable)
				return;
			if (!Equals(_highlightTrisSource, sourceMesh))
			{
				_highlightTris = poseMesh.triangles;
				_highlightTrisSource = sourceMesh;
			}
			Vector3[] vertices = poseMesh.vertices;
			_highlightMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(l2w);
			GL.Begin(4);
			GL.Color(HighlightColor);
			foreach (int t in _highlightTris)
				GL.Vertex(vertices[t]);

			GL.End();
			GL.PopMatrix();
		}

		private void DrawMeshHighlightFromDeformer(ShapeDeformer deformer, Renderer originalRenderer)
		{
			Mesh displayMesh = deformer.DisplayMesh;
			if (!displayMesh.isReadable)
				return;
			if (_highlightTrisSource != displayMesh)
			{
				_highlightTris = displayMesh.triangles;
				_highlightTrisSource = displayMesh;
			}
			Vector3[] vertices = displayMesh.vertices;
			Matrix4x4 l2w = deformer.DisplayTransform.localToWorldMatrix;
			_highlightMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(l2w);
			GL.Begin(4);
			GL.Color(HighlightColor);
			foreach (int t in _highlightTris)
				GL.Vertex(vertices[t]);

			GL.End();
			GL.PopMatrix();
		}

		public void ActivateHighlight(int vertexCount)
		{
			if (!_deformer || vertexCount <= 0)
				return;
			_highlightVertexCount = vertexCount;
			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (!shader)
				shader = Shader.Find("Sprites/Default");
			_weightMaterial = new Material(shader);
			_weightMaterial.SetInt(SrcBlend, 1);
			_weightMaterial.SetInt(DstBlend, 0);
			_weightMaterial.SetInt(Cull, 0);
			_weightMaterial.SetInt(ZWrite, 1);
			_weightMaterial.SetInt(ZTest, 4);
			_deformer.EnterEditMode(_weightMaterial);
			_highlightActive = true;
		}

		public void DeactivateHighlight()
		{
			_deformer?.ExitEditMode();
			if (_weightMaterial)
			{
				Destroy(_weightMaterial);
				_weightMaterial = null;
			}
			_highlightColors = null;
			_highlightActive = false;
		}

		public void UpdateHighlightColors(float[] weights)
		{
			if (!_highlightActive || !_deformer || weights == null)
				return;
			_highlightColors = new Color[_highlightVertexCount];
			int count = Mathf.Min(weights.Length, _highlightColors.Length);
			for (var i = 0; i < count; i++)
				_highlightColors[i] = WeightGradient.Evaluate(weights[i]);
			_deformer.SetEditColors(_highlightColors);
		}

		public void UpdateHighlightColors(Dictionary<int, float> affectedVertices)
		{
			if (!_highlightActive || _deformer == null || _highlightColors == null)
				return;
			foreach (KeyValuePair<int, float> pair in affectedVertices.Where(pair => pair.Key >= 0 && pair.Key < _highlightColors.Length))
			{
				_highlightColors[pair.Key] = WeightGradient.Evaluate(pair.Value);
			}
			_deformer.SetEditColors(_highlightColors);
		}

		private void RefreshWireframe()
		{
			Mesh mesh = null;
			Matrix4x4 l2w = Matrix4x4.identity;
			if (_deformer && _deformer.DisplayMesh && _deformer.DisplayTransform)
			{
				mesh = _deformer.DisplayMesh;
				l2w = _deformer.DisplayTransform.localToWorldMatrix;
			}
			if (!mesh)
			{
				_wireVerts = null;
				_wireTris = null;
				_edges = null;
				_triFrontFacing = null;
				_vertexVisible = null;
				return;
			}
			int instanceID = mesh.GetInstanceID();
			bool meshChanged = _wireTris == null || _wireMeshId != instanceID;
			// Read vertices into pre-allocated list to avoid allocation from mesh.vertices property
			mesh.GetVertices(_vertexReadback);
			int vCount = _vertexReadback.Count;
			if (_wireVerts == null || _wireVerts.Length != vCount)
			{
				_wireVerts = new Vector3[vCount];
				_vertsUploadDirty = true;
			}
			if (_vertexVisible == null || _vertexVisible.Length != vCount)
				_vertexVisible = new bool[vCount];
			// Always retransform when a live deformer is active (vertices change every frame from brushing/animation).
			// Only skip when the transform hasn't changed and there's no active deformation.
			bool l2wChanged = l2w != _prevL2W;
			bool hasLiveDeformer = _deformer && _deformer.DisplayMesh;
			if (meshChanged || l2wChanged || hasLiveDeformer)
			{
				for (var i = 0; i < vCount; i++)
					_wireVerts[i] = l2w.MultiplyPoint3x4(_vertexReadback[i]);
				_prevL2W = l2w;
				_vertsUploadDirty = true;
				// Rebuild cull results since verts moved
				_culledVisibleCount = 0;
			}
			if (!meshChanged) return;
			_wireTris = mesh.triangles;
			_wireMeshId = instanceID;
			int triCount = _wireTris.Length / 3;
			if (_triFrontFacing == null || _triFrontFacing.Length != triCount)
				_triFrontFacing = new bool[triCount];
			ExtractUniqueEdges(_wireTris);
			if (_wireLineMesh)
				Destroy(_wireLineMesh);
			_wireLineMesh = new Mesh();
			_wireLineMesh.MarkDynamic();
			_prevVisibleCount = -1;
			_wireColorsDirty = true;
		}

		private void OnGUI()
		{
			
			Window?.DrawGUI();

			if (Window == null || !Window.IsEditMode)
				return;

			Event e = Event.current;
			Input.PollKeyboard(e);

			// Hotkeys — in Studio, game's UndoRedoManager handles Ctrl+Z/Y via BlendShapeEditorCommand
			if (e.type == EventType.KeyDown && _undoStack != null && !StudioAPI.InsideStudio)
			{
				if (BlendShapeEditorPlugin.KeyUndo.Value.IsDown() && _undoStack.CanUndo)
				{
					UndoOneStep();
					e.Use();
					return;
				}
				if (BlendShapeEditorPlugin.KeyRedo.Value.IsDown() && _undoStack.CanRedo)
				{
					RedoOneStep();
					e.Use();
					return;
				}
			}

			// Skip mouse interaction when cursor is over the UI panel
			if (ShapeEditorWindow.IsMouseOverUI)
				return;

			Camera main = _camera;
			if (!main || Input == null)
				return;
			
			DeformData activeDeformData = Window.ActiveDeformData;
			if (activeDeformData == null)
				return;
			DeformLayer activeLayer = activeDeformData.ActiveLayer;

			if (Window.OperationMode == ShapeEditorWindow.OpMode.Brush)
				ProcessBrushEvent(e, main, activeLayer);
			else
				ProcessGizmoEvent(e, main, activeLayer);
			
			if (Window.HotkeyUsed) // eat hotkey usage
			{
				//BSE.Logger.LogDebug("NomNomNom");
				UnityEngine.Input.ResetInputAxes();
				Window.HotkeyUsed = false;
			}
		}

		private void ExtractUniqueEdges(int[] triangles)
		{
			var edgeMap = new Dictionary<long, int>();
			var edgeList = new List<WireEdge>();
			int triCount = triangles.Length / 3;
			for (var i = 0; i < triCount; i++)
			{
				int triBase = i * 3;
				int v0 = triangles[triBase];
				int v1 = triangles[triBase + 1];
				int v2 = triangles[triBase + 2];
				AddEdge(edgeMap, edgeList, v0, v1, i);
				AddEdge(edgeMap, edgeList, v1, v2, i);
				AddEdge(edgeMap, edgeList, v2, v0, i);
			}
			_edges = edgeList.ToArray();
			_lineIndexBuffer = new int[_edges.Length * 2];
		}

		private static void AddEdge(Dictionary<long, int> edgeMap, List<WireEdge> edgeList, int v0, int v1, int triIdx)
		{
			int lo = v0 < v1 ? v0 : v1;
			int hi = v0 < v1 ? v1 : v0;
			long key = (long)lo << 32 | (long)(uint)hi;
			if (edgeMap.TryGetValue(key, out int existingIdx))
			{
				WireEdge edge = edgeList[existingIdx];
				edge.tri1 = triIdx;
				edgeList[existingIdx] = edge;
				return;
			}
			edgeMap[key] = edgeList.Count;
			edgeList.Add(new WireEdge
			{
				v0 = lo,
				v1 = hi,
				tri0 = triIdx,
				tri1 = -1
			});
		}

		private void OnDestroy()
		{
			DeactivateHighlight();
			SelectionTool?.CleanupCollider();
			Input?.Cleanup();
			if (_cursorMaterial)
				Destroy(_cursorMaterial);
			if (_highlightMaterial)
				Destroy(_highlightMaterial);
			if (_bakeMeshCache)
				Destroy(_bakeMeshCache);
			if (_wireLineMesh)
				Destroy(_wireLineMesh);
		}

		/*
		private Color WeightColor(float weight)
		{
			return Color.HSVToRGB(weight, 1f, 1f);
		}

		private Color WeightColorMirror(float weight)
		{
			return Color.HSVToRGB(weight, 0.5f, 0.5f);
		}
		*/

		public ShapeEditorWindow Window;
		public SelectionTool SelectionTool;
		public InputHelper Input;
		public Action OnRefreshRenderers;
		public Func<object> GetCurrentSelection;

		private Camera _camera;
		private Renderer _targetRenderer;
		private ShapeDeformer _deformer;
		private MoveTool _moveTool;
		private SmoothTool _smoothTool;
		private InflateTool _inflateTool;
		private DrawTool _drawTool;
		private DrawSharpTool _drawSharpTool;
		private BlobTool _blobTool;
		private ClayTool _clayTool;
		private ClayStripsTool _clayStripsTool;
		private ClayThumbTool _clayThumbTool;
		private CreaseTool _creaseTool;
		private LayerTool _layerTool;
		private FillTool _fillTool;
		private FlattenTool _flattenTool;
		private TransformGizmo _gizmo;
		private Material _cursorMaterial;
		private Vector3 _lastHitPoint;
		private Vector3 _lastHitNormal;
		private bool _hasHit;
		private const int CursorSegments = 32;
		private Material _highlightMaterial;
		private int[] _highlightTris;
		private Mesh _highlightTrisSource;
		private Mesh _bakeMeshCache;
		private static readonly Color HighlightColor = new Color(1f, 0.6f, 0f, 0.35f);
		private object _lastSelection;
		private bool _isBoxSelecting;
		private Vector2 _boxStart;
		private Vector2 _boxEnd;
		private UndoStack _undoStack;
		private bool _isBrushing;
		private Vector3? _lastBrushWorldPosition;
		private Dictionary<int, Vector3> _brushBeforeSnapshot;
		private Dictionary<int, float> _brushAffectedVertices;
		private Dictionary<int, float> _brushMirrorAffectedVertices;
		private Dictionary<int, float> _moveGrabVertices;
		private BrushResult _moveGrabResult;
		private BrushResult _moveGrabMirrorResult;
		private Dictionary<int, Vector3> _gizmoBeforeSnapshot;
		private Vector3[] _wireVerts;
		private int[] _wireTris;
		private bool[] _triFrontFacing;
		private bool[] _vertexVisible;
		private int _wireMeshId;
		private float _refreshTimer;
		private const float ColliderRefreshInterval = 0.5f;
		private WireEdge[] _edges;
		private Mesh _wireLineMesh;
		private int[] _lineIndexBuffer;
		private int[] _visibleLineIndices;
		private int _prevVisibleCount = -1;
		private Color32[] _wireColors;
		private bool _wireColorsDirty = true;
		private bool _vertsUploadDirty = true;
		private bool _prevUseSoftColors;
		private bool _prevUseBrushColors;
		private bool _prevHasMirror;
		// Cull results computed in LateUpdate, consumed in OnRenderObject
		private int _culledVisibleCount;
		// Hover vertex detection
		private int _hoverVertexIndex = -1;
		private List<Vector3> _vertexReadback = new List<Vector3>();
		private Matrix4x4 _prevL2W = Matrix4x4.zero;
		private bool _softWeightsDirty;
		private float _softWeightsDirtyTime;
		private const float SoftWeightsThrottle = 0.15f;
		private bool _symmetryEnabled;
		private int _symmetryAxis;
		private float _symmetryCenter;
		private bool _symmetryCenterSet;
		private Material _weightMaterial;
		private Color[] _highlightColors;
		private int _highlightVertexCount;
		private bool _highlightActive;
		private static readonly Gradient WeightGradient = new Gradient();
		private static readonly Gradient MirrorWeightGradient = new Gradient();
		private bool _deferGizmoCentroidRefresh;
		private static float DotPixelSize => BSE.VertexDotSize.Value;
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");

		private struct WireEdge
		{
			public int v0;
			public int v1;
			public int tri0;
			public int tri1;
		}
	}
}
