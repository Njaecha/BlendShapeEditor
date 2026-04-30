using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KKShapeEditor
{
	[DefaultExecutionOrder(32001)]
	public class ShapePaintOverlay : MonoBehaviour
	{
		static ShapePaintOverlay()
		{
			WeightGradient = new Gradient();
			WeightGradient.SetKeys(new GradientColorKey[]
			{
				new GradientColorKey(new Color(0f, 0f, 1f), 0f),
				new GradientColorKey(new Color(0f, 1f, 0f), 0.33f),
				new GradientColorKey(new Color(1f, 1f, 0f), 0.66f),
				new GradientColorKey(new Color(1f, 0f, 0f), 1f)
			}, new GradientAlphaKey[]
			{
				new GradientAlphaKey(1f, 0f),
				new GradientAlphaKey(1f, 1f)
			});
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
			if (ShapeEditorPlugin.StudioToggleKey.Value.IsDown())
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
			Input?.PollInput();
			Input?.UpdateCameraIsolation(Window != null && Window.IsEditMode);
		}

		private void ProcessDeferredActions()
		{
			if (Window == null)
				return;
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
			if (Window.DeferSubdivide)
			{
				Window.DeferSubdivide = false;
				DoSubdivide();
				_undoStack?.Clear();
			}
			if (Window.DeferRestore)
			{
				Window.DeferRestore = false;
				DoRestore();
			}
			if (Window.DeferLayerMoveUp >= 0)
			{
				int layerMoveUpIdx = Window.DeferLayerMoveUp;
				Window.DeferLayerMoveUp = -1;
				if (Window.ActiveDeformData != null)
				{
					Window.ActiveDeformData.MoveLayerUp(layerMoveUpIdx);
					_undoStack?.Push(new LayerReorderUndoEntry(Window.ActiveDeformData, true));
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
					_deformer?.InvalidateDeltaCache();
				}
			}
			if (Window.WeightUndoLayer >= 0)
			{
				int weightUndoLayer = Window.WeightUndoLayer;
				Window.WeightUndoLayer = -1;
				if (_undoStack != null && Window.ActiveDeformData != null && weightUndoLayer < Window.ActiveDeformData.Layers.Count)
					_undoStack.Push(new LayerWeightUndoEntry(Window.ActiveDeformData.Layers[weightUndoLayer], Window.WeightUndoBefore, Window.WeightUndoAfter));
			}
			if (Window.DeferFaceSelectAll)
			{
				Window.DeferFaceSelectAll = false;
				Window.FaceSelect?.SelectAll();
			}
			if (Window.DeferFaceSelectNone)
			{
				Window.DeferFaceSelectNone = false;
				Window.FaceSelect?.ClearSelection();
			}
			if (Window.DeferFaceSelectInvert)
			{
				Window.DeferFaceSelectInvert = false;
				Window.FaceSelect?.InvertSelection();
			}
			if (Window.DeferSetSymmetryCenter)
			{
				Window.DeferSetSymmetryCenter = false;
				if (_gizmo != null && _gizmo.HasTarget)
				{
					Vector3 centroidLocal = _gizmo.CentroidLocal;
					int axisIdx = Window.SymmetryAxisIndex;
					float center = axisIdx == 0 ? centroidLocal.x : (axisIdx == 1 ? centroidLocal.y : centroidLocal.z);
					Window.SymmetryCenter = center;
					Window.SymmetryCenterSet = true;
					_symmetryCenter = center;
					_symmetryCenterSet = true;
				}
			}
			if (Window.DeferClearSymmetryCenter)
			{
				Window.DeferClearSymmetryCenter = false;
				Window.SymmetryCenter = 0f;
				Window.SymmetryCenterSet = false;
				_symmetryCenter = 0f;
				_symmetryCenterSet = false;
			}
			if (Window.DeferRemapWeights)
			{
				Window.DeferRemapWeights = false;
				DoRemapWeights();
			}
			if (Window.DeferRestoreWeights)
			{
				Window.DeferRestoreWeights = false;
				DoRestoreWeights();
			}
			if (Window.DeferExport)
			{
				Window.DeferExport = false;
				DoExportDeform();
			}
			if (Window.DeferImport)
			{
				Window.DeferImport = false;
				DoImportDeform();
			}
		}

		private void DoEnterEditMode()
		{
			if (Window.Renderers.Count == 0)
				return;
			int index = Mathf.Clamp(Window.SelectedRendererIndex, 0, Window.Renderers.Count - 1);
			Renderer renderer = Window.Renderers[index];
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
			SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
			if (smr)
			{
				deformer.Init(smr);
			}
			else
			{
				MeshFilter mf = renderer.GetComponent<MeshFilter>();
				MeshRenderer mr = renderer as MeshRenderer;
				if (mf && mr)
					deformer.Init(mf, mr);
			}
			SetTarget(renderer);
			Window.ActiveDeformData = deformData;
			Window.SetEditMode(true);
			if (SelectionTool != null)
			{
				SkinnedMeshRenderer smr2 = renderer as SkinnedMeshRenderer;
				if (smr2)
				{
					SelectionTool.SetTarget(smr2);
				}
				else
				{
					MeshFilter mf2 = renderer.GetComponent<MeshFilter>();
					if (mf2)
						SelectionTool.SetTarget(mf2);
				}
			}
			_undoStack = new UndoStack(ShapeEditorPlugin.UndoMaxSteps.Value);
			if (_moveTool == null)
				_moveTool = new MoveTool();
			if (_smoothTool == null)
				_smoothTool = new SmoothTool();
			if (_inflateTool == null)
				_inflateTool = new InflateTool();
			if (_gizmo == null)
				_gizmo = new TransformGizmo();
			_moveTool.Deformer = _deformer;
			_gizmo.Deformer = _deformer;
			Transform objectRoot = null;
			ShapeEditorController charCtrl = renderer.GetComponentInParent<ShapeEditorController>();
			if (charCtrl)
			{
				objectRoot = charCtrl.RootTransform;
			}
			else
			{
				ItemShapeController itemCtrl = renderer.GetComponentInParent<ItemShapeController>();
				if (itemCtrl)
					objectRoot = itemCtrl.RootTransform;
			}
			_gizmo.SetObjectRoot(objectRoot);
			Mesh mesh = MeshHelper.GetMesh(renderer);
			if (!mesh) return;
			_smoothTool.BuildAdjacency(mesh.triangles, mesh.vertexCount, mesh.vertices);
			Window.VertexCount = mesh.vertexCount;
			ActivateHighlight(mesh.vertexCount);
		}

		private void DoExitEditMode()
		{
			if (_gizmo != null && _gizmo.IsDragging)
				_gizmo.EndDrag();
			_gizmo?.SetObjectRoot(null);
			DeactivateHighlight();
			SelectionTool?.CleanupCollider();
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
			if (_wireLineMesh)
			{
				Destroy(_wireLineMesh);
				_wireLineMesh = null;
			}
			_undoStack = null;
			_isBrushing = false;
		}

		private void DoRemapWeights()
		{
			if (!_deformer || !_targetRenderer)
				return;
			if (Window.ActiveDeformData == null)
				return;
			SkinnedMeshRenderer smr = _targetRenderer as SkinnedMeshRenderer;
			if (!smr)
				return;
			ShapeEditorController charCtrl = _targetRenderer.GetComponentInParent<ShapeEditorController>();
			if (!charCtrl)
				return;
			SkinnedMeshRenderer bodySmr = charCtrl.GetBodySmr();
			if (!bodySmr)
				return;
			Mesh bodyMesh = bodySmr.sharedMesh;
			Mesh targetMesh = smr.sharedMesh;
			if (!targetMesh)
				return;
			Vector3[] bindVerts = _deformer.BindVertices ?? targetMesh.vertices;
			Vector3[] finalDelta = Window.ActiveDeformData.ComputeFinalDelta();
			if (finalDelta == null || finalDelta.Length != bindVerts.Length)
				return;
			BoneWeight[] remapped = WeightRemapper.ComputeRemappedWeights(bindVerts, finalDelta, smr.bones, bodyMesh.vertices, bodyMesh.boneWeights, _deformer.OriginalBoneWeights, bodySmr.bones, bodyMesh.triangles);
			if (remapped == null) return;
			_deformer.RemappedBoneWeights = remapped;
			Window.ActiveDeformData.WeightRemapped = true;
		}

		private void DoRestoreWeights()
		{
			if (!_deformer)
				return;
			_deformer.ClearRemappedWeights();
			if (Window.ActiveDeformData != null)
				Window.ActiveDeformData.WeightRemapped = false;
		}

		private void DoExportDeform()
		{
			if (Window.Renderers.Count == 0)
				return;
			int index = Mathf.Clamp(Window.SelectedRendererIndex, 0, Window.Renderers.Count - 1);
			Renderer renderer = Window.Renderers[index];
			if (!renderer)
				return;
			DeformData deformData = GetExistingDeformData(renderer);
			if (deformData == null || deformData.Layers.Count == 0)
				return;
			string path = FileDialogHelper.ShowSaveDialog(L.ExportDeform, "deform", L.DeformFileFilter, "kksd");
			if (string.IsNullOrEmpty(path))
				return;
			byte[] bytes = ShapeSerializer.SerializeSingleRenderer(deformData);
			if (bytes == null)
				return;
			try
			{
				File.WriteAllBytes(path, bytes);
				ShapeEditorPlugin.Logger.LogInfo(L.ExportSuccess);
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("Export failed: " + ex.Message);
			}
		}

		private void DoImportDeform()
		{
			if (Window.Renderers.Count == 0)
				return;
			int index = Mathf.Clamp(Window.SelectedRendererIndex, 0, Window.Renderers.Count - 1);
			Renderer renderer = Window.Renderers[index];
			if (!renderer)
				return;
			string path = FileDialogHelper.ShowOpenDialog(L.ImportDeform, L.DeformFileFilter);
			if (string.IsNullOrEmpty(path))
				return;
			byte[] data;
			try
			{
				data = File.ReadAllBytes(path);
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("Import failed: " + ex.Message);
				return;
			}

			List<DeformLayer> layers = ShapeSerializer.DeserializeSingleRenderer(data, out int vertexCount);
			if (layers == null)
			{
				ShapeEditorPlugin.Logger.LogWarning(L.ImportInvalidFile);
				return;
			}
			Mesh mesh = MeshHelper.GetMesh(renderer);
			if (!mesh)
				return;
			if (vertexCount != mesh.vertexCount)
			{
				ShapeEditorPlugin.Logger.LogWarning(string.Format(L.ImportVertexMismatchFmt, vertexCount, mesh.vertexCount));
				return;
			}
			bool studioMode;
			DeformData deformData = GetDeformDataForRenderer(renderer, out studioMode);
			if (deformData == null)
				return;
			Window.ActiveDeformData = deformData;
			foreach (DeformLayer layer in layers)
			{
				layer.Dirty = true;
				deformData.Layers.Add(layer);
			}
			deformData.ActiveLayerIndex = deformData.Layers.Count - 1;
			_deformer?.InvalidateDeltaCache();
			ShapeEditorPlugin.Logger.LogInfo(L.ImportSuccess);
		}

		private void DoLayerAdd()
		{
			if (Window.ActiveDeformData == null)
			{
				if (Window.Renderers.Count == 0)
					return;
				int index = Mathf.Clamp(Window.SelectedRendererIndex, 0, Window.Renderers.Count - 1);
				Renderer renderer = Window.Renderers[index];
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
				_undoStack.Push(new LayerRemoveUndoEntry(data, removedLayer, layerIndex, prevActiveIdx));
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

		private void DoSubdivide()
		{
			Renderer currentRenderer = GetCurrentRenderer();
			if (!currentRenderer)
				return;
			MeshHelper.CloneMeshIfShared(currentRenderer);
			Mesh mesh = MeshHelper.GetMesh(currentRenderer);
			if (!mesh)
				return;
			HashSet<int> selectedFaces = null;
			int[] faceArray = null;
			if (Window.FaceSelect && Window.FaceSelect.SelectedFaces.Count > 0)
			{
				selectedFaces = Window.FaceSelect.SelectedFaces;
				faceArray = new int[selectedFaces.Count];
				selectedFaces.CopyTo(faceArray);
			}
			int subdivideLevel = Window.SubdivideLevel;
			MeshHelper.Subdivide(mesh, subdivideLevel, selectedFaces);
			MeshHelper.AppendSubdivisionFaces(mesh, faceArray, subdivideLevel);
			if (Window.ActiveDeformData != null)
				MeshHelper.ResetLayersForNewVertexCount(Window.ActiveDeformData, mesh.vertexCount);
			if (!Window.FaceSelect) return;
			Destroy(Window.FaceSelect.gameObject);
			Window.FaceSelect = FaceSelectOverlay.Create(currentRenderer);
		}

		private void DoRestore()
		{
			Renderer currentRenderer = GetCurrentRenderer();
			if (!currentRenderer)
				return;
			SkinnedMeshRenderer smr = currentRenderer as SkinnedMeshRenderer;
			if (smr)
			{
				MeshHelper.RestoreOriginal(smr);
			}
			else
			{
				MeshFilter mf = currentRenderer.GetComponent<MeshFilter>();
				if (mf)
					MeshHelper.RestoreOriginal(mf);
			}
			Mesh mesh = MeshHelper.GetMesh(currentRenderer);
			if (mesh && Window.ActiveDeformData != null)
				MeshHelper.ResetLayersForNewVertexCount(Window.ActiveDeformData, mesh.vertexCount);
			if (!Window.FaceSelect) return;
			Destroy(Window.FaceSelect.gameObject);
			Window.FaceSelect = FaceSelectOverlay.Create(currentRenderer);
		}

		private Renderer GetCurrentRenderer()
		{
			if (Window == null || Window.Renderers.Count == 0)
				return null;
			int index = Mathf.Clamp(Window.SelectedRendererIndex, 0, Window.Renderers.Count - 1);
			return Window.Renderers[index];
		}

		private static DeformData GetExistingDeformData(Renderer renderer)
		{
			ShapeEditorController charCtrl = renderer.GetComponentInParent<ShapeEditorController>();
			if (charCtrl)
				return charCtrl.GetDeformData(renderer);
			ItemShapeController itemCtrl = renderer.GetComponentInParent<ItemShapeController>();
			return itemCtrl ? itemCtrl.GetDeformData(renderer) : null;
		}

		private static DeformData GetDeformDataForRenderer(Renderer renderer, out bool studioMode)
		{
			studioMode = false;
			ShapeEditorController charCtrl = renderer.GetComponentInParent<ShapeEditorController>();
			if (charCtrl)
				return charCtrl.GetOrCreateDeformData(renderer);
			ItemShapeController itemCtrl = renderer.GetComponentInParent<ItemShapeController>();
			if (!itemCtrl) return null;
			studioMode = true;
			return itemCtrl.GetOrCreateDeformData(renderer);
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
			Camera main = Camera.main;
			if (!main || Input == null)
				return;
			if (_targetRenderer)
			{
				ShapeEditorController charCtrl = _targetRenderer.GetComponentInParent<ShapeEditorController>();
				Window.IsOnCharacter = charCtrl;
				Window.BodyMeshReadable = charCtrl && charCtrl.GetBodySmr();
			}
			else
			{
				Window.IsOnCharacter = false;
				Window.BodyMeshReadable = false;
			}
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
			}
			bool prevSymEnabled = _symmetryEnabled;
			int prevSymAxis = _symmetryAxis;
			float prevSymCenter = _symmetryCenter;
			bool prevSymCenterSet = _symmetryCenterSet;
			_symmetryEnabled = Window.SymmetryEnabled;
			_symmetryAxis = Window.SymmetryAxisIndex;
			_symmetryCenter = Window.SymmetryCenter;
			_symmetryCenterSet = Window.SymmetryCenterSet;
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
			if (_undoStack != null && Window.IsEditMode)
			{
				if (Input.UndoPressed && _undoStack.CanUndo)
				{
					UndoContext ctx = new UndoContext
					{
						Deformer = _deformer,
						Data = Window.ActiveDeformData,
						Window = Window
					};
					_undoStack.Undo(ctx);
					PostUndoRedoCleanup();
				}
				else if (Input.RedoPressed && _undoStack.CanRedo)
				{
					UndoContext ctx = new UndoContext
					{
						Deformer = _deformer,
						Data = Window.ActiveDeformData,
						Window = Window
					};
					_undoStack.Redo(ctx);
					PostUndoRedoCleanup();
				}
			}
			if (ShapeEditorWindow.IsMouseOverUI)
			{
				_hasHit = false;
				return;
			}
			Vector3 mousePosition = Input.MousePosition;
			Ray ray = main.ScreenPointToRay(mousePosition);
			_hasHit = SelectionTool.Raycast(ray, out _lastHitPoint, out _lastHitNormal);
			DeformData activeDeformData = Window.ActiveDeformData;
			if (activeDeformData == null)
				return;
			DeformLayer activeLayer = activeDeformData.ActiveLayer;
			if (Window.OperationMode == ShapeEditorWindow.OpMode.Brush)
			{
				ProcessBrushInteraction(main, ray, mousePosition, activeLayer);
				if (_isBrushing && !Input.MouseButton)
				{
					_isBrushing = false;
					_moveGrabVertices = null;
					_moveGrabResult = null;
					_moveGrabMirrorResult = null;
					CommitBrushUndoEntry(activeLayer);
				}
			}
			else
			{
				if (_isBrushing)
				{
					_isBrushing = false;
					_moveGrabVertices = null;
					_moveGrabResult = null;
					_moveGrabMirrorResult = null;
					CommitBrushUndoEntry(activeLayer);
				}
				ProcessGizmoInteraction(main, mousePosition, activeLayer);
			}
			_prevMousePos = mousePosition;
		}

		private void ProcessBrushInteraction(Camera cam, Ray ray, Vector3 mousePos, DeformLayer activeLayer)
		{
			if (activeLayer == null || !Input.MouseButton || !_hasHit || Input.CtrlHeld)
				return;
			if (!_isBrushing)
			{
				_isBrushing = true;
				_brushBeforeSnapshot = new Dictionary<int, Vector3>();
			}
			BrushResult brushResult = SelectionTool.BrushSelect(ray);
			if (brushResult == null || brushResult.AffectedVertices.Count == 0)
				return;
			Vector3[] deltas = activeLayer.Deltas;
			Vector3[] cachedVertices = SelectionTool.CachedVertices;
			Vector3[] cachedNormals = SelectionTool.CachedNormals;
			IDeformTool activeTool = GetActiveBrushTool();
			switch (activeTool)
			{
				case null:
					return;
				case MoveTool moveTool:
				{
					if (_moveGrabVertices == null)
					{
						_moveGrabVertices = new Dictionary<int, float>(brushResult.AffectedVertices);
						_moveGrabResult = new BrushResult
						{
							HitPoint = brushResult.HitPoint,
							HitNormal = brushResult.HitNormal,
							AffectedVertices = _moveGrabVertices
						};
					}
					brushResult = _moveGrabResult;
					moveTool.RendererTransform = _targetRenderer ? _targetRenderer.transform : null;
					moveTool.UseViewPlane = !Input.ShiftHeld;
					Vector3 hitScreen = cam.WorldToScreenPoint(brushResult.HitPoint);
					Vector3 hitWorld = cam.ScreenToWorldPoint(hitScreen);
					Vector3 hitWorldOffset = cam.ScreenToWorldPoint(new Vector3(hitScreen.x + 1f, hitScreen.y, hitScreen.z));
					float pixelSize = Vector3.Distance(hitWorld, hitWorldOffset);
					float dx = mousePos.x - _prevMousePos.x;
					float dy = mousePos.y - _prevMousePos.y;
					if (moveTool.UseViewPlane)
						moveTool.MouseDelta = new Vector2(dx * pixelSize, dy * pixelSize);
					else
						moveTool.DragDelta = dy * pixelSize;
					break;
				}
				case InflateTool inflateTool:
					inflateTool.Amount = Input.AltHeld ? -0.005f : 0.005f;
					break;
			}

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices.Where(pair => !_brushBeforeSnapshot.ContainsKey(pair.Key)))
			{
				_brushBeforeSnapshot[pair.Key] = deltas[pair.Key];
			}
			activeTool.Apply(activeLayer, brushResult, cachedVertices, cachedNormals, cam);
			if (!_symmetryEnabled || !_targetRenderer) return;
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
					Vector3 colliderLocalPoint = SelectionTool.ColliderTransform != null ? SelectionTool.ColliderTransform.InverseTransformPoint(mirrorWorld) : localHit;
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
				}
			}
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
				_undoStack.Push(new DeltaUndoEntry(layer, indices, before, after));
			_gizmoBeforeSnapshot = null;
		}

		private void ProcessGizmoInteraction(Camera cam, Vector2 mousePos, DeformLayer activeLayer)
		{
			if (_gizmo == null || activeLayer == null)
				return;
			Transform targetTransform = SelectionTool.TargetTransform;
			Vector3[] cachedVertices = SelectionTool.CachedVertices;
			if (Input.MouseButtonR && !Input.CtrlHeld && !_gizmo.IsDragging)
			{
				if (!_isBoxSelecting)
				{
					_isBoxSelecting = true;
					_boxStart = mousePos;
				}
				_boxEnd = mousePos;
			}
			else if (_isBoxSelecting)
			{
				_isBoxSelecting = false;
				if (!Input.CtrlHeld)
				{
					float xMin = Mathf.Min(_boxStart.x, _boxEnd.x);
					float xMax = Mathf.Max(_boxStart.x, _boxEnd.x);
					float yMin = Mathf.Min(_boxStart.y, _boxEnd.y);
					float yMax = Mathf.Max(_boxStart.y, _boxEnd.y);
					Rect screenRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
					if (Input.AltHeld)
						SelectionTool.DeselectBox(cam, screenRect);
					else
						SelectionTool.SelectBox(cam, screenRect, Input.ShiftHeld);
					UpdateGizmoTarget(cachedVertices);
				}
			}
			_gizmo.UpdateHover(mousePos, cam, targetTransform);
			if (Input.MouseButtonDown && _gizmo.HoveredAxis != GizmoAxis.None)
			{
				if (!_gizmo.BeginDrag(mousePos, cam, targetTransform, activeLayer, cachedVertices)) return;
				_gizmoBeforeSnapshot = new Dictionary<int, Vector3>(_gizmo.DragStartDeltas);
				return;
			}
			else
			{
				if (Input.MouseButton && _gizmo.IsDragging)
				{
					_gizmo.UpdateDrag(mousePos, cam, targetTransform, activeLayer, cachedVertices);
					return;
				}

				if (!Input.MouseButtonUp || !_gizmo.IsDragging) return;
				_gizmo.EndDrag();
				CommitGizmoUndoEntry(activeLayer);
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

		private IDeformTool GetActiveBrushTool()
		{
			switch (Window.SelectedBrushTool)
			{
			case ShapeEditorWindow.BrushToolType.Move:
				return _moveTool;
			case ShapeEditorWindow.BrushToolType.Smooth:
				return _smoothTool;
			case ShapeEditorWindow.BrushToolType.Inflate:
				return _inflateTool;
			default:
				return _moveTool;
			}
		}

		private void OnRenderObject()
		{
			if (Camera.current != Camera.main)
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
			if (!hasWire && !_hasHit)
				return;
			if (hasWire)
				DrawWireframe();
			_cursorMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(Matrix4x4.identity);
			if (Window.OperationMode == ShapeEditorWindow.OpMode.Brush)
			{
				if (_hasHit)
					DrawBrushCircle(_lastHitPoint, _lastHitNormal, Window.BrushRadius);
			}
			else if (hasWire && SelectionTool != null && SelectionTool.SelectedVertices.Count > 0)
			{
				DrawSelectedVertices();
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
			GL.Color(new Color(1f, 1f, 0f, 0.9f));
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
			GL.Color(new Color(1f, 1f, 0f, 0.9f));
			GL.Vertex(center - tangent * crossSize);
			GL.Vertex(center + tangent * crossSize);
			GL.Vertex(center - bitangent * crossSize);
			GL.Vertex(center + bitangent * crossSize);
			GL.End();
		}

		private void DrawSelectedVertices()
		{
			var size = 0.002f;
			GL.Begin(1);
			GL.Color(new Color(0f, 1f, 0.5f, 0.9f));
			foreach (Vector3 v in from idx in SelectionTool.SelectedVertices where idx >= 0 && idx < _wireVerts.Length select _wireVerts[idx])
			{
				GL.Vertex(v + Vector3.left * size);
				GL.Vertex(v + Vector3.right * size);
				GL.Vertex(v + Vector3.up * size);
				GL.Vertex(v + Vector3.down * size);
			}
			GL.End();
		}

		private void DrawWireframe()
		{
			if (_edges == null || !_wireLineMesh)
				return;
			Camera main = Camera.main;
			if (!main)
				return;
			Vector3 camPos = main.transform.position;
			bool useSoftColors = _gizmo != null && _gizmo.SoftSelectionEnabled && _gizmo.HasTarget && Window.OperationMode == ShapeEditorWindow.OpMode.Gizmo;
			if (_wireColorsDirty || useSoftColors != _prevUseSoftColors || _wireColors == null || _wireColors.Length != _wireVerts.Length)
			{
				RebuildWireColors(useSoftColors);
				_wireColorsDirty = false;
				_prevUseSoftColors = useSoftColors;
			}
			int edgeCount = _edges.Length;
			var writeIdx = 0;
			for (var i = 0; i < edgeCount; i++)
			{
				int tri0 = _edges[i].tri0;
				int tri1 = _edges[i].tri1;
				bool front0 = IsTriangleFrontFacing(tri0, camPos);
				bool front1 = tri1 >= 0 && IsTriangleFrontFacing(tri1, camPos);
				if (!front0 && !front1) continue;
				_lineIndexBuffer[writeIdx++] = _edges[i].v0;
				_lineIndexBuffer[writeIdx++] = _edges[i].v1;
			}
			_wireLineMesh.vertices = _wireVerts;
			_wireLineMesh.colors32 = _wireColors;
			if (writeIdx != _prevVisibleCount)
			{
				_visibleLineIndices = new int[writeIdx];
				_prevVisibleCount = writeIdx;
			}
			Array.Copy(_lineIndexBuffer, _visibleLineIndices, writeIdx);
			_wireLineMesh.SetIndices(_visibleLineIndices, MeshTopology.Lines, 0);
			_cursorMaterial.SetPass(0);
			Graphics.DrawMeshNow(_wireLineMesh, Matrix4x4.identity);
		}

		private bool IsTriangleFrontFacing(int triIdx, Vector3 camPos)
		{
			int base3 = triIdx * 3;
			if (base3 + 2 >= _wireTris.Length)
				return false;
			Vector3 v0 = _wireVerts[_wireTris[base3]];
			Vector3 v1 = _wireVerts[_wireTris[base3 + 1]];
			Vector3 v2 = _wireVerts[_wireTris[base3 + 2]];
			return Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), v0 - camPos) <= 0f;
		}

		private void RebuildWireColors(bool useSoftColors)
		{
			int count = _wireVerts.Length;
			if (_wireColors == null || _wireColors.Length != count)
				_wireColors = new Color32[count];
			if (useSoftColors)
			{
				Dictionary<int, float> softWeights = _gizmo.SoftWeights;
				for (var i = 0; i < count; i++)
				{
					if (softWeights.TryGetValue(i, out float weight))
					{
						if (weight <= 0.5f)
						{
							var r = (byte)(weight * 2f * 255f);
							_wireColors[i] = new Color32(r, 0, 0, byte.MaxValue);
						}
						else
						{
							var g = (byte)((weight - 0.5f) * 2f * 255f);
							_wireColors[i] = new Color32(byte.MaxValue, g, 0, byte.MaxValue);
						}
					}
					else
					{
						_wireColors[i] = WireDefaultColor32;
					}
				}
				return;
			}
			for (var j = 0; j < count; j++)
				_wireColors[j] = WireDefaultColor32;
		}

		private void DrawBoxSelectRect()
		{
			GL.PushMatrix();
			GL.LoadPixelMatrix();
			float xMin = Mathf.Min(_boxStart.x, _boxEnd.x);
			float xMax = Mathf.Max(_boxStart.x, _boxEnd.x);
			float yMin = Mathf.Min(_boxStart.y, _boxEnd.y);
			float yMax = Mathf.Max(_boxStart.y, _boxEnd.y);
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
				return;
			}
			Vector3[] vertices = mesh.vertices;
			if (_wireVerts == null || _wireVerts.Length != vertices.Length)
				_wireVerts = new Vector3[vertices.Length];
			for (int i = 0; i < vertices.Length; i++)
				_wireVerts[i] = l2w.MultiplyPoint3x4(vertices[i]);
			int instanceID = mesh.GetInstanceID();
			if (_wireTris != null && _wireMeshId == instanceID) return;
			_wireTris = mesh.triangles;
			_wireMeshId = instanceID;
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
			if (Window != null && Window.IsEditMode)
			{
				DrawEditModeHud();
				Input?.ResetGameInput();
			}
		}

		private void DrawEditModeHud()
		{
			if (_hudStyle == null)
			{
				_hudStyle = new GUIStyle(GUI.skin.box)
				{
					alignment = TextAnchor.UpperLeft,
					fontSize = 13,
					normal =
					{
						textColor = Color.green
					},
					padding = new RectOffset(8, 8, 6, 6)
				};
			}
			string modeLine;
			if (Window.OperationMode == ShapeEditorWindow.OpMode.Brush)
			{
				string toolName;
				switch (Window.SelectedBrushTool)
				{
				case ShapeEditorWindow.BrushToolType.Move:
					toolName = L.MoveTool;
					break;
				case ShapeEditorWindow.BrushToolType.Smooth:
					toolName = L.SmoothTool;
					break;
				case ShapeEditorWindow.BrushToolType.Inflate:
					toolName = L.InflateTool;
					break;
				default:
					toolName = "?";
					break;
				}
				modeLine = L.BrushMode + ": " + toolName;
			}
			else
			{
				modeLine = L.GizmoMode;
			}
			DeformData activeData = Window.ActiveDeformData;
			DeformLayer activeLayer = activeData?.ActiveLayer;
			string layerLine = activeLayer != null ? string.Format(L.HudLayerFmt, activeLayer.Name) : L.HudNoLayer;
			string brushLine = $"R:{Window.BrushRadius:F2}  S:{Window.BrushStrength:F2}  F:{Window.BrushFalloff}";
			string hudText = string.Concat(modeLine, "\n", layerLine, "\n", brushLine, "\n", L.HudShortcuts);
			float hudWidth = 250f;
			float hudX = (float)Screen.width - hudWidth - 360f;
			GUIContent content = new GUIContent(hudText);
			float hudHeight = _hudStyle.CalcHeight(content, hudWidth);
			GUI.Box(new Rect(hudX, 10f, hudWidth, hudHeight), hudText, _hudStyle);
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

		public ShapeEditorWindow Window;
		public SelectionTool SelectionTool;
		public InputHelper Input;
		public Action OnRefreshRenderers;
		public Func<object> GetCurrentSelection;

		private Renderer _targetRenderer;
		private ShapeDeformer _deformer;
		private MoveTool _moveTool;
		private SmoothTool _smoothTool;
		private InflateTool _inflateTool;
		private TransformGizmo _gizmo;
		private Vector2 _prevMousePos;
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
		private Dictionary<int, Vector3> _brushBeforeSnapshot;
		private Dictionary<int, float> _moveGrabVertices;
		private BrushResult _moveGrabResult;
		private BrushResult _moveGrabMirrorResult;
		private Dictionary<int, Vector3> _gizmoBeforeSnapshot;
		private Vector3[] _wireVerts;
		private int[] _wireTris;
		private int _wireMeshId;
		private float _refreshTimer;
		private const float ColliderRefreshInterval = 0.5f;
		private WireEdge[] _edges;
		private Mesh _wireLineMesh;
		private int[] _lineIndexBuffer;
		private int[] _visibleLineIndices;
		private int _prevVisibleCount;
		private Color32[] _wireColors;
		private bool _wireColorsDirty = true;
		private bool _prevUseSoftColors;
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
		private static readonly Gradient WeightGradient;
		private bool _deferGizmoCentroidRefresh;
		private static readonly Color WireDefaultColor = new Color(0f, 0f, 0f, 0.3f);
		private static readonly Color32 WireDefaultColor32 = new Color32(0, 0, 0, 77);
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");
		private GUIStyle _hudStyle;

		private struct WireEdge
		{
			public int v0;
			public int v1;
			public int tri0;
			public int tri1;
		}
	}
}
