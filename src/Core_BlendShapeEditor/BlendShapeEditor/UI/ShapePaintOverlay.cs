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
		public static void RebuildAllWeldGroups()
		{
			foreach (ShapePaintOverlay overlay in _activeInstances)
				overlay.RebuildWeldGroups();
		}

		public static void MarkAllWireColorsDirty()
		{
			foreach (ShapePaintOverlay overlay in _activeInstances)
				overlay._wireColorsDirty = true;
		}

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

		private void Awake()
		{
			_activeInstances.Add(this);
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
			if (Window.DeferRebuildWeldGroups)
			{
				Window.DeferRebuildWeldGroups = false;
				RebuildWeldGroups();
			}
			if (_adjacencyHasWeldBridges && !Window.WeldEnabled)
				RebuildAdjacency();
			if (Window.DeferEnterEditMode)
			{
				Window.DeferEnterEditMode = false;
				OnRefreshRenderers?.Invoke();
				DoEnterEditMode();
			}
			if (Window.DeferEditExistingShapeName != null)
			{
				string shapeName = Window.DeferEditExistingShapeName;
				Window.DeferEditExistingShapeName = null;
				OnRefreshRenderers?.Invoke();
				DoEnterEditMode();
				DoImportExistingShape(shapeName);
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
					_session.InvalidateDeltaCaches();
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
					_session.InvalidateDeltaCaches();
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

			if (Window.DeferCheckNameAvailability && _session.HasTarget)
			{
				if (Window.ActiveDeformData?.EditingExistingShapeName != null)
				{
					Window.BakeNamingIssues = null;
				}
				else
				{
					BSE.Logger.LogDebug("Checking Name Availability");
					if (!Window.BakeSeparate)
					{
						Window.BakeNamingIssues = AnyPartHasShape(Window.BakeShapeName) ? Window.BakeShapeName : null;
					}
					else
					{
						var badNames = new List<string>();
						foreach (DeformLayer deformLayer in Window.ActiveDeformData.Layers)
						{
							if (AnyPartHasShape(Window.BakeShapeName + "_" + deformLayer.Name))
							{
								badNames.Add(Window.BakeShapeName + "_" + deformLayer.Name);
							}
						}

						Window.BakeNamingIssues = badNames.Count > 0 ? string.Join(", ", badNames.ToArray()) : null;
					}
				}
				Window.DeferCheckNameAvailability = false;
			}
			if (Window.DeferBake)
			{
				Window.DeferBake = false;
				DoBake();
			}

			if (_session.HasTarget)
			{
				bool isEditingExisting = Window.ActiveDeformData?.EditingExistingShapeName != null;
				bool sync = isEditingExisting && Window.SyncPreviewWithLiveWeight;
				// Parts that have the edited shape follow their own live weight (in LateUpdate); the first of
				// them drives the label, and parts without the shape follow the label.
				ShapeDeformer syncSource = null;
				foreach (EditPart part in _session.Parts)
				{
					if (!part.Deformer)
						continue;
					part.Deformer.SyncPreviewWithLiveWeight = sync && part.Deformer.IsEditingExistingShape;
					if (part.Deformer.SyncPreviewWithLiveWeight && !syncSource)
						syncSource = part.Deformer;
				}
				if (syncSource)
					// Label display only; the mesh itself is already synced in LateUpdate.
					Window.PreviewWeight = syncSource.PreviewMultiplier;
				foreach (EditPart part in _session.Parts)
				{
					if (part.Deformer && !part.Deformer.SyncPreviewWithLiveWeight)
						part.Deformer.PreviewMultiplier = Window.PreviewWeight;
				}
			}
		}

		private bool AnyPartHasShape(string shapeName)
		{
			foreach (EditPart part in _session.Parts)
				if (part.Deformer && part.Deformer.HasBlendShape(shapeName))
					return true;
			return false;
		}
		
		// private Animator _animatorTurnedOff = null;
		private void DoEnterEditMode()
		{
			if (Window.Renderers.Count == 0)
				return;
			List<Renderer> selected = Window.GetSelectedRenderers();
			if (selected.Count == 0)
				return;
			EnterEditMode(selected);
		}

		private void EnterEditMode(List<Renderer> renderers)
		{
			// Sorts `renderers` by path; the parts below must stay in that order to match the shared layer layout.
			DeformData deformData = GetDeformDataForRenderers(renderers, out bool studioMode);
			// The layer stack outlives edit sessions; start each one from a freshly summed preview.
			deformData?.InvalidateDisplayDelta();
			renderers[0].TryGetOwningController(out Object controller);
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

			var parts = new List<EditPart>();
			foreach (Renderer renderer in renderers)
			{
				ShapeDeformer deformer = renderer.GetComponent<ShapeDeformer>();
				if (!deformer)
					deformer = renderer.gameObject.AddComponent<ShapeDeformer>();
				if (deformData != null)
				{
					deformer.StudioMode = studioMode;
					deformer.DeformData = deformData;
				}
				deformer.PreviewMultiplier = 1f;
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
				parts.Add(EditPart.Create(renderer, deformer));
			}
			Window.PreviewWeight = 1f;
			Window.SyncPreviewWithLiveWeight = false;
			Window.ActiveDeformData = deformData;
			Window.SetEditMode(true);
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
			_session.Begin(parts, objectRoot);
			if (_session.Parts.Count != renderers.Count)
			{
				// A missing part would shift every later part's slice of the shared layer deltas.
				BSE.Logger.LogError("ShapePaintOverlay.EnterEditMode() -> could not set up every selected renderer");
				DoExitEditMode();
				// DoExitEditMode only removes deformers of parts that made it into the session.
				foreach (Renderer renderer in renderers)
				{
					ShapeDeformer leftover = renderer ? renderer.GetComponent<ShapeDeformer>() : null;
					if (leftover)
						DestroyImmediate(leftover);
				}
				return;
			}
			bool shared = _session.Parts.Count > 1;
			foreach (EditPart part in _session.Parts)
			{
				part.Deformer.DeltaOffset = part.Offset;
				part.Deformer.DeltaTotalCount = shared ? _session.VertexCount : -1;
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
			_gizmo.Session = _session;

			if (!objectRoot)
			{
				// should not happen
				BSE.Logger.LogError("ShapePaintOverlay.EnterEditMode() -> ObjectRoot is null");
				return;
			}
			_gizmo.SetObjectRoot(objectRoot);
			if (_session.RestTriangles == null) return;
			RebuildAdjacency();
			Window.VertexCount = _session.VertexCount;
			ActivateHighlight(_session.VertexCount);
			RebuildWeldGroups();
		}

		private void RebuildWeldGroups()
		{
			if (Window == null || !Window.WeldEnabled || !_session.HasTarget)
				return;
			_weldGroups.Rebuild(_session.Positions, _session.Grid, BSE.WeldRadius.Value);
			int vertexCount = _session.Positions?.Length ?? 0;
			if (_weldBlendedNormalsBuffer == null || _weldBlendedNormalsBuffer.Length != vertexCount)
				_weldBlendedNormalsBuffer = new Vector3[vertexCount];
			_wireColorsDirty = true;
			if (_session.Parts.Count > 1)
				RebuildAdjacency();
		}

		/// Mesh adjacency for Smooth/Fill and surface soft selection. With several renderers, colocated
		/// links stay within each mesh (their bind spaces are unrelated), and while welding is on, weld
		/// groups spanning two meshes become bridge edges so the surface continues across the seam.
		private void RebuildAdjacency()
		{
			if (_smoothTool == null || _session.RestTriangles == null)
				return;
			bool multiPart = _session.Parts.Count > 1;
			_smoothTool.BuildAdjacency(_session.RestTriangles, _session.VertexCount, _session.RestPositions,
				multiPart ? _session.PartIndexOfVertex : null);
			_adjacencyHasWeldBridges = false;
			if (!multiPart)
				return;
			if (Window != null && Window.WeldEnabled)
			{
				int[] partOf = _session.PartIndexOfVertex;
				_weldGroups.ForEachGroup(members =>
				{
					for (var i = 0; i < members.Length; i++)
					for (int j = i + 1; j < members.Length; j++)
						if (partOf[members[i]] != partOf[members[j]])
							_smoothTool.Link(members[i], members[j]);
				});
				_adjacencyHasWeldBridges = true;
			}
			_softWeightsDirty = true;
			_softWeightsDirtyTime = Time.unscaledTime;
		}

		private void ExpandWithWeldedVertices(Dictionary<int, float> affected)
		{
			if (Window == null || !Window.WeldEnabled || affected == null || affected.Count == 0)
				return;

			// Snapshot keys first: the loop below adds entries to `affected`, and mutating a
			// dictionary while enumerating it throws.
			var sourceKeys = new List<int>(affected.Keys);
			foreach (int vertexIndex in sourceKeys)
			{
				if (!_weldGroups.TryGetGroup(vertexIndex, out int[] members))
					continue;
				float falloff = affected[vertexIndex];
				foreach (int member in members)
				{
					if (!affected.TryGetValue(member, out float existing) || falloff > existing)
						affected[member] = falloff;
				}
			}
		}

		private Vector3[] GetBrushNormals(Vector3[] cachedNormals, Dictionary<int, float> affectedVertices)
		{
			if (Window == null || !Window.WeldEnabled || _weldBlendedNormalsBuffer == null
			    || _weldBlendedNormalsBuffer.Length != cachedNormals.Length)
				return cachedNormals;

			Array.Copy(cachedNormals, _weldBlendedNormalsBuffer, cachedNormals.Length);
			foreach (int vertexIndex in affectedVertices.Keys)
			{
				if (_weldGroups.IsWelded(vertexIndex))
					_weldBlendedNormalsBuffer[vertexIndex] = _weldGroups.GetBlendedNormal(vertexIndex, cachedNormals);
			}
			return _weldBlendedNormalsBuffer;
		}

		internal void DoExitEditMode()
		{
			Renderer renderer = Window.SelectedRenderer;
			if (_gizmo != null && _gizmo.IsDragging)
				_gizmo.EndDrag();
			_gizmo?.SetObjectRoot(null);
			DeactivateHighlight();
			foreach (EditPart part in _session.Parts)
			{
				if (!part.Deformer)
					continue;
				part.Deformer.RestoreEditedShapeWeight();
				DestroyImmediate(part.Deformer);
			}
			_session.Clear();
			// Imported layers only make sense while actively editing that specific existing shape —
			// don't let them linger for the next time this renderer enters edit mode.
			if (Window.ActiveDeformData?.EditingExistingShapeName != null)
				Window.ActiveDeformData.ClearLayers();
			Window.ActiveDeformData = null;
			Window.SetEditMode(false);
			_wireVerts = null;
			_wireTris = null;
			_edges = null;
			_wireColors = null;
			_lineIndexBuffer = null;
			_culledVisibleCount = 0;
			_hoverVertexIndex = -1;
			_vertsUploadDirty = true;
			_wireMeshIds = null;
			DestroyWireParts();
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
			DeformData data = Window.ActiveDeformData;
			if (!_session.HasTarget || data == null || !data.HasLayers)
				return;
			string existingShapeName = data.EditingExistingShapeName;
			bool isUpdate = existingShapeName != null;
			string prefix = Window.BakeShapeName;
			if (string.IsNullOrEmpty(prefix))
				prefix = "BSE_Shape";

			if (Window.BakeSeparate && !isUpdate)
			{
				var layers = new List<DeformLayer>(data.Layers);
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
					if (!BakeParts($"{prefix}_{layer.Name}", scaled, false))
						return;
				}
			}
			else if (isUpdate)
			{
				if (!BakeParts(existingShapeName, data.ComputeFinalDelta(), true))
					return;
			}
			else
			{
				if (!BakeParts(prefix, data.ComputeFinalDelta(), false))
					return;
				Window.IncrementBakeShapeName();
			}

			data.ClearLayers();
			DoExitEditMode();
		}

		/// Bakes each skinned part's slice of `combined` under the same shape name, so one slider drives them
		/// all. With several parts, parts the edit never touched are skipped for a new shape. When updating an
		/// existing shape, parts that have it are updated and parts that don't get it registered if edited.
		/// Returns false if registering with BlendShapeCreator failed (the bake is aborted).
		private bool BakeParts(string shapeName, Vector3[] combined, bool isUpdate)
		{
			if (combined == null || combined.Length != _session.VertexCount)
			{
				BSE.Logger.LogWarning(
					$"DoBake: delta length mismatch (delta={combined?.Length ?? 0}, verts={_session.VertexCount}) - not baking '{shapeName}'");
				return false;
			}
			bool multiPart = _session.Parts.Count > 1;
			// A part that gains the shape during an update starts at the weight the shape currently has.
			var newShapeWeight = 100f;
			if (isUpdate)
			{
				foreach (EditPart part in _session.Parts)
				{
					if (!part.Deformer || !part.Deformer.IsEditingExistingShape)
						continue;
					newShapeWeight = part.Deformer.LiveExternalWeight;
					break;
				}
			}
			foreach (EditPart part in _session.Parts)
			{
				if (!(part.Renderer is SkinnedMeshRenderer smr) || !part.Deformer)
					continue;
				var slice = new Vector3[part.VertexCount];
				Array.Copy(combined, part.Offset, slice, 0, slice.Length);
				bool hasShape = isUpdate && part.Deformer.HasBlendShape(shapeName);
				if (multiPart && !hasShape && IsAllZero(slice))
					continue;
				if (!part.Deformer.BakeToBlendShape(shapeName, slice, out Vector3[] deltaVerts, out Vector3[] deltaNormals, Window.BakeCalcNormals))
				{
					BSE.Logger.LogWarning($"DoBake: skipping '{smr.name}' for '{shapeName}' - bake failed");
					continue;
				}
				bool registered = hasShape
					? UpdateBakedShape(smr, shapeName, deltaVerts, deltaNormals)
					: RegisterBakedShape(smr, shapeName, deltaVerts, deltaNormals, isUpdate ? newShapeWeight : 100f);
				if (!registered)
					return false;
			}
			return true;
		}

		private static bool IsAllZero(Vector3[] deltas)
		{
			foreach (Vector3 d in deltas)
				if (d.x != 0f || d.y != 0f || d.z != 0f)
					return false;
			return true;
		}

		private bool RegisterBakedShape(SkinnedMeshRenderer smr, string shapeName, Vector3[] deltaVerts, Vector3[] deltaNormals, float weight)
		{
			if (!smr.TryGetOwningController(out Object controller))
			{
				BSE.Logger.LogError("Could not bake and register Blendshape");
				BSE.Logger.LogError("ShapePaintOverlay.RegisterBakedShape() -> Controller is null");
				return false;
			}
			switch (controller)
			{
				case BlendShapeEditorItemController itemCtrl:
				{
					BlendShapeCreatorBridge.RegisterBlendShapeStudio(itemCtrl.ItemCtrlInfo, smr, shapeName, deltaVerts, deltaNormals, weight);
					RefreshPoseController(itemCtrl.gameObject);
					return true;
				}
				case BlendShapeEditorCharaController charCtrl:
				{
					BlendShapeCreatorBridge.RegisterBlendShapeMaker(charCtrl.ChaControl, smr, shapeName, deltaVerts, deltaNormals, weight);
					RefreshPoseController(charCtrl.gameObject);
					return true;
				}
				default:
					BSE.Logger.LogError("Could not bake and register Blendshape");
					BSE.Logger.LogError("ShapePaintOverlay.RegisterBakedShape() -> Controller is null");
					return false;
			}
		}

		private bool UpdateBakedShape(SkinnedMeshRenderer smr, string shapeName, Vector3[] deltaVerts, Vector3[] deltaNormals)
		{
			if (!smr.TryGetOwningController(out Object controller))
			{
				BSE.Logger.LogError("Could not bake and update Blendshape");
				BSE.Logger.LogError("ShapePaintOverlay.UpdateBakedShape() -> Controller is null");
				return false;
			}
			switch (controller)
			{
				case BlendShapeEditorItemController itemCtrl:
				{
					BlendShapeCreatorBridge.UpdateBlendShapeStudio(itemCtrl.ItemCtrlInfo, smr, shapeName, deltaVerts, deltaNormals);
					RefreshPoseController(itemCtrl.gameObject);
					return true;
				}
				case BlendShapeEditorCharaController charCtrl:
				{
					BlendShapeCreatorBridge.UpdateBlendShapeMaker(charCtrl.ChaControl, smr, shapeName, deltaVerts, deltaNormals);
					RefreshPoseController(charCtrl.gameObject);
					return true;
				}
				default:
					BSE.Logger.LogError("Could not bake and update Blendshape");
					BSE.Logger.LogError("ShapePaintOverlay.UpdateBakedShape() -> Controller is null");
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
			int vertexCount;
			if (_session.HasTarget)
			{
				vertexCount = _session.VertexCount;
			}
			else
			{
				Mesh mesh = MeshHelper.GetMesh(GetCurrentRenderer());
				if (!mesh)
					return;
				vertexCount = mesh.vertexCount;
			}
			DeformLayer layer = Window.ActiveDeformData.AddLayer(vertexCount);
			_undoStack?.Push(new LayerAddUndoEntry(Window.ActiveDeformData, layer, Window.ActiveDeformData.Layers.Count - 1));
			StudioUndoBridge.PushDummy(this);
		}

		private void DoImportExistingShape(string shapeName)
		{
			DeformData data = Window.ActiveDeformData;
			if (!_session.HasTarget || data == null)
				return;
			var layer = new DeformLayer(shapeName, _session.VertexCount) { Weight = 1f };
			ShapeDeformer firstImported = null;
			foreach (EditPart part in _session.Parts)
			{
				if (!part.Deformer || !part.Deformer.HasBlendShape(shapeName))
					continue;
				DeformLayer partLayer = part.Deformer.ImportBlendShapeAsLayer(shapeName);
				if (partLayer == null)
					continue;
				Array.Copy(partLayer.Deltas, 0, layer.Deltas, part.Offset, Mathf.Min(partLayer.Deltas.Length, part.VertexCount));
				if (!firstImported)
					firstImported = part.Deformer;
			}
			if (!firstImported)
			{
				BSE.Logger.LogWarning($"DoImportExistingShape: could not import '{shapeName}'");
				return;
			}
			data.Layers.Add(layer);
			data.ActiveLayerIndex = data.Layers.Count - 1;
			data.EditingExistingShapeName = shapeName;
			_undoStack?.Push(new LayerAddUndoEntry(data, layer, data.Layers.Count - 1));
			StudioUndoBridge.PushDummy(this);
			Window.SetBakeName(shapeName);
			// Start the preview at the shape's pre-edit weight, synced live by default.
			Window.SyncPreviewWithLiveWeight = true;
			Window.PreviewWeight = Mathf.Clamp01(firstImported.LiveExternalWeight / 100f);
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
			_session.InvalidateDeltaCaches();
			if (Window.ActiveDeformData.ActiveLayer == null)
			{
				_gizmo?.SetTarget(null, null, null);
				_session.ClearSelection();
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

		/// Sorts `renderers` by path (the shared layer layout order) and returns their shared layer stack.
		private static DeformData GetDeformDataForRenderers(List<Renderer> renderers, out bool studioMode)
		{
			switch (renderers[0].TryGetOwningController(out Object controller))
			{
				case true when controller is BlendShapeEditorCharaController charaController:
					studioMode = false;
					return charaController.GetOrCreateDeformData(renderers);
				case true when controller is BlendShapeEditorItemController itemController:
					studioMode = true;
					return itemController.GetOrCreateDeformData(renderers);
				default:
					studioMode = false;
					BlendShapeEditorPlugin.Logger.LogError("Could not get deform data for renderers");
					return null;
			}
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

		/// Logs the bind<->posed skinning round-trip for the currently selected vertices.
		/// Probes along the three renderer-local axes so a rotation/scale error shows up
		/// regardless of which direction the vertex was actually dragged.
		private void RunSkinningDiagnostic(Transform targetTransform)
		{
			if (!_session.HasTarget)
			{
				BlendShapeEditorPlugin.Logger.LogWarning("[Diag] no deformer on target renderer");
				return;
			}
			if (_gizmo == null || !_gizmo.HasTarget)
			{
				BlendShapeEditorPlugin.Logger.LogWarning("[Diag] no vertices selected - select a vertex in Gizmo mode first");
				return;
			}

			// Probe magnitude in world units; small enough to stay in the linear regime.
			const float probe = 0.01f;
			var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };

			var logged = 0;
			foreach (int idx in _gizmo.TargetIndices)
			{
				if (logged >= MaxDiagnosticVerts)
				{
					BlendShapeEditorPlugin.Logger.LogInfo(
						$"[Diag] ...{_gizmo.TargetIndices.Count - MaxDiagnosticVerts} more selected vertices not logged");
					break;
				}
				foreach (Vector3 axis in axes)
				{
					Vector3 worldDisp = (targetTransform ? targetTransform.TransformDirection(axis) : axis) * probe;
					EditPart part = _session.GetPart(idx, out int localIdx);
					if (part != null && part.Deformer)
						part.Deformer.LogSkinningDiagnostic(localIdx, worldDisp, part.Transform);
				}
				logged++;
			}
		}

		private const int MaxDiagnosticVerts = 4;

		private void LateUpdate()
		{
			if (!_session.HasTarget)
				return;
			if (Window == null || !Window.IsEditMode)
				return;
			if (_session.AllPartsLive)
			{
				_session.RefreshFromDisplayMeshes();
				RefreshWireframe();
			}
			else
			{
				_refreshTimer += Time.deltaTime;
				if (_refreshTimer >= 0.5f)
				{
					_refreshTimer = 0f;
					_session.RefreshByBaking();
					RefreshWireframe();
				}
			}
			if (_deferGizmoCentroidRefresh)
			{
				_deferGizmoCentroidRefresh = false;
				Vector3[] cachedVertices = _session.Positions;
				if (_gizmo != null && cachedVertices != null)
					_gizmo.UpdateCentroid(cachedVertices);
			}
			Camera main = _camera;
			if (!main || Input == null)
				return;
			_session.Radius = Window.BrushRadius;
			_session.Strength = Window.BrushStrength;
			_session.Falloff = Window.BrushFalloff;
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
					Vector3[] cachedVertices2 = _session.Positions;
					if (cachedVertices2 != null)
					{
						List<int>[] adjacency = _smoothTool?.Adjacency;
						_gizmo.ComputeSoftWeights(cachedVertices2, _session.Grid, adjacency);
						if (_symmetryEnabled && _gizmo.HasMirrorTarget)
							_gizmo.ComputeMirrorSoftWeights(cachedVertices2, _session.Grid, adjacency);
						_wireColorsDirty = true;
					}
				}
				// UpdateHover runs every frame for handle highlight rendering
				Transform targetTransform = _session.Space;
				_gizmo.UpdateHover(InputHelper.MousePosition, main, targetTransform);

				if (Window.DeferSkinningDiagnostic)
				{
					Window.DeferSkinningDiagnostic = false;
					RunSkinningDiagnostic(targetTransform);
				}
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
					Vector3[] cachedVertices3 = _session.Positions;
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
				bool cullWire = Window == null || Window.WireframeDisplayMode == ShapeEditorWindow.WireframeDisplayType.BackfaceCulling;
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
			_hasHit = _session.Raycast(ray, out _lastHitPoint, out _lastHitNormal);

			

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
			bool restrictToVisible = Window != null && Window.WireframeDisplayMode == ShapeEditorWindow.WireframeDisplayType.BackfaceCulling
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
			Vector3[] cachedNormals = _session.Normals;
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
					BrushResult initialResult = _session.BrushSelect(ray);
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
				moveTool.RendererTransform = _session.Space;
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
				brushResult = _session.BrushSelect(ray);
				if (brushResult == null || brushResult.AffectedVertices.Count == 0)
					return;
			}

			ExpandWithWeldedVertices(brushResult.AffectedVertices);

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
			
			ApplyToolToSurface(activeTool, activeLayer, brushResult, cachedNormals, cam);
			_brushAffectedVertices = brushResult.AffectedVertices;
			if (!_symmetryEnabled || !_session.Space)
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
					Transform xform = _session.Space;
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
					mirrorResult = _session.BrushSelectAtPoint(localHit, mirrorWorld, mirrorWorldNormal);
					if (activeTool is MoveTool && mirrorResult != null && _moveGrabMirrorResult == null)
						_moveGrabMirrorResult = mirrorResult;
				}
				if (mirrorResult != null && mirrorResult.AffectedVertices.Count > 0)
				{
					ExpandWithWeldedVertices(mirrorResult.AffectedVertices);
					foreach (KeyValuePair<int, float> pair in mirrorResult.AffectedVertices)
					{
						if (_brushBeforeSnapshot != null && !_brushBeforeSnapshot.ContainsKey(pair.Key))
							_brushBeforeSnapshot[pair.Key] = deltas[pair.Key];
					}
					MoveTool mirrorMoveTool = activeTool as MoveTool;
					if (mirrorMoveTool != null)
						mirrorMoveTool.MirrorAxis = _symmetryAxis;
					ApplyToolToSurface(activeTool, activeLayer, mirrorResult, cachedNormals, cam);
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

		private void ApplyToolToSurface(IDeformTool tool, DeformLayer layer, BrushResult result, Vector3[] cachedNormals, Camera cam)
		{
			WeldGroups welds = Window != null && Window.WeldEnabled ? _weldGroups : null;
			_deformSurface.Bind(_session, layer, GetBrushNormals(cachedNormals, result.AffectedVertices), _smoothTool?.Adjacency, welds);
			_displacementScratch.Clear();
			tool.Apply(_deformSurface, result, cam, _displacementScratch);
			_deformSurface.ApplyDisplacements(_displacementScratch);
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
				_session.ClearSelection();
			}
			else if (_gizmo != null && _gizmo.HasTarget)
			{
				_deferGizmoCentroidRefresh = true;
			}
		}

		public void UndoOneStep()
		{
			if (_undoStack == null || !_undoStack.CanUndo) return;
			UndoContext ctx = new UndoContext { Session = _session, Data = Window?.ActiveDeformData, Window = Window };
			_undoStack.Undo(ctx);
			PostUndoRedoCleanup();
		}

		public void RedoOneStep()
		{
			if (_undoStack == null || !_undoStack.CanRedo) return;
			UndoContext ctx = new UndoContext { Session = _session, Data = Window?.ActiveDeformData, Window = Window };
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
			Transform targetTransform = _session.Space;
			Vector3[] cachedVertices = _session.Positions;

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
				bool[] visMask = (Window != null && Window.VertexDisplayMode == ShapeEditorWindow.VertexDisplayType.BackfaceCulling) ? _vertexVisible : null;
				if ((mods & EventModifiers.Control) != 0)
					_session.DeselectBox(cam, screenRect, visMask);
				else
					_session.SelectBox(cam, screenRect, (mods & EventModifiers.Shift) != 0, visMask);
			}

			UpdateGizmoTarget(cachedVertices);
		}

		private void TrySelectVertexAtHit(EventModifiers mods, Vector3[] cachedVertices)
		{
			// _lastHitPoint is already set by LateUpdate raycast; use it for vertex snap
			if (!_hasHit)
			{
				if ((mods & (EventModifiers.Shift | EventModifiers.Control)) == 0)
					_session.ClearSelection();
				return;
			}
			int bestIdx = _hoverVertexIndex;
			if (bestIdx >= 0)
			{
				if ((mods & EventModifiers.Shift) != 0)
					_session.SelectedVertices.Add(bestIdx);
				else if ((mods & EventModifiers.Control) != 0)
					_session.SelectedVertices.Remove(bestIdx);
				else
				{
					_session.ClearSelection();
					_session.SelectedVertices.Add(bestIdx);
				}
			}
			else if ((mods & (EventModifiers.Shift | EventModifiers.Control)) == 0)
			{
				_session.ClearSelection();
			}
		}

		private void UpdateGizmoTarget(Vector3[] vertices)
		{
			if (_gizmo == null || !_session.HasTarget)
				return;
			Vector3[] cachedNormals = _session.Normals;
			HashSet<int> targetIndices = _session.SelectedVertices;
			if (Window != null && Window.WeldEnabled)
			{
				var expanded = new HashSet<int>(targetIndices);
				foreach (int idx in targetIndices)
				{
					if (_weldGroups.TryGetGroup(idx, out int[] members))
					{
						foreach (int member in members)
							expanded.Add(member);
					}
				}
				targetIndices = expanded;
			}
			_gizmo.SetTarget(targetIndices, vertices, cachedNormals);
			if (_symmetryEnabled && _gizmo.HasTarget && _session.Grid != null)
			{
				var mirrorIndices = new HashSet<int>();
				SpatialHashGrid grid = _session.Grid;
				float symmetryOffset = _symmetryCenterSet ? _symmetryCenter : 0f;
				int axis = _symmetryAxis;
				HashSet<int> selected = targetIndices;
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
				_gizmo.ComputeSoftWeights(vertices, _session.Grid, adjacency);
			if (_symmetryEnabled && _gizmo.HasMirrorTarget && _gizmo.SoftSelectionEnabled)
				_gizmo.ComputeMirrorSoftWeights(vertices, _session.Grid, adjacency);
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
			if (!hasWire && !_hasHit) return;
			if (hasWire) DrawWireframe();
			if (Window.ActiveDeformData == null || Window.ActiveDeformData.Layers.IsNullOrEmpty()) return;
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
			Transform xform = _session.Space;
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
			if (_wireVerts == null || !cam || Window.OverlayHide)
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
			HashSet<int> selectedVerts = inGizmoMode && _session.HasTarget ? _session.SelectedVertices : null;
			bool useGizmoSoftGradient = inGizmoMode && _gizmo != null && _gizmo.SoftSelectionEnabled && _gizmo.HasTarget;
			Dictionary<int, float> primarySoftWeights = useGizmoSoftGradient ? _gizmo.PrimarySoftWeights : null;
			Dictionary<int, float> mirrorSoftWeights = useGizmoSoftGradient && _gizmo.HasMirrorTarget ? _gizmo.MirrorSoftWeights : null;
			Dictionary<int, float> brushAffected = inBrushMode ? _brushAffectedVertices : null;
			Dictionary<int, float> brushMirrorAffected = inBrushMode ? _brushMirrorAffectedVertices : null;
			bool weldEnabled = Window != null && Window.WeldEnabled;
			bool showWeldColor = weldEnabled && BSE.ShowWeldedVertexColor.Value;
			int[] hoverWeldGroup = weldEnabled && _hoverVertexIndex >= 0 && _weldGroups.TryGetGroup(_hoverVertexIndex, out int[] hg) ? hg : null;
			ShapeEditorWindow.VertexDisplayType displayMode = Window != null ? Window.VertexDisplayMode : ShapeEditorWindow.VertexDisplayType.All;
			bool hideOccludedDots = displayMode == ShapeEditorWindow.VertexDisplayType.BackfaceCulling
				&& _vertexVisible != null && _vertexVisible.Length == _wireVerts.Length;
			bool interactOnly = displayMode == ShapeEditorWindow.VertexDisplayType.Interact;
			GL.Begin(4); // triangles
			for (var i = 0; i < _wireVerts.Length; i++)
			{
				bool isHovered = i == _hoverVertexIndex
					|| (hoverWeldGroup != null && Array.IndexOf(hoverWeldGroup, i) >= 0);
				bool isInteracting = (selectedVerts != null && selectedVerts.Contains(i))
					|| isHovered
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
				if (isHovered)
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
				else if (showWeldColor && _weldGroups.IsWelded(i))
					col = BSE.VertexColorWelded.Value;
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
			if (_edges == null || _wireParts.Count == 0 || Window.OverlayHide)
				return;
			Camera main = _camera;
			if (!main)
				return;
			bool useSoftColors = _gizmo != null && _gizmo.SoftSelectionEnabled && _gizmo.HasTarget && Window.OperationMode == ShapeEditorWindow.OpMode.Gizmo;
			bool useBrushColors = Window.OperationMode == ShapeEditorWindow.OpMode.Brush && (_brushAffectedVertices != null || _brushMirrorAffectedVertices != null);
			bool inGizmoMode = Window.OperationMode == ShapeEditorWindow.OpMode.Gizmo;
			HashSet<int> selectedVerts = inGizmoMode && _session.HasTarget ? _session.SelectedVertices : null;
			HashSet<int> mirrorVerts = inGizmoMode && _symmetryEnabled && _gizmo != null && _gizmo.HasMirrorTarget ? _gizmo.MirrorIndices : null;
			bool hasSelection = selectedVerts != null && selectedVerts.Count > 0;
			bool hasMirror = mirrorVerts != null && mirrorVerts.Count > 0;
			bool showWeldColor = Window.WeldEnabled && BSE.ShowWeldedVertexColor.Value;
			ShapeEditorWindow.WireframeDisplayType wireDisplayMode = Window.WireframeDisplayMode;
			if (_wireColorsDirty || useSoftColors != _prevUseSoftColors || useBrushColors != _prevUseBrushColors || hasMirror != _prevHasMirror || wireDisplayMode != _prevWireframeDisplayMode || showWeldColor != _prevShowWeldColor || _wireColors == null || _wireColors.Length != _wireVerts.Length)
			{
				RebuildWireColors(useSoftColors, useBrushColors, showWeldColor, hasSelection ? selectedVerts : null, hasMirror ? mirrorVerts : null);
				_wireColorsDirty = false;
				_prevUseSoftColors = useSoftColors;
				_prevUseBrushColors = useBrushColors;
				_prevHasMirror = hasMirror;
				_prevWireframeDisplayMode = wireDisplayMode;
				_prevShowWeldColor = showWeldColor;
			}
			// Cull results were computed in LateUpdate; use them directly. Edges never cross parts and are
			// extracted part by part, so each part's visible edges form one contiguous run of the buffer.
			int writeIdx = _culledVisibleCount;
			_cursorMaterial.SetPass(0);
			var cursor = 0;
			IList<EditPart> parts = _session.Parts;
			for (var p = 0; p < _wireParts.Count && p < parts.Count; p++)
			{
				WirePartMesh wire = _wireParts[p];
				int offset = parts[p].Offset;
				int end = offset + wire.Verts.Length;
				int start = cursor;
				while (cursor < writeIdx && _lineIndexBuffer[cursor] < end)
					cursor += 2;
				int count = cursor - start;
				if (wire.Indices == null || wire.Indices.Length != count)
					wire.Indices = new int[count];
				for (var k = 0; k < count; k++)
					wire.Indices[k] = _lineIndexBuffer[start + k] - offset;
				// Only re-upload vertex data when something changed
				if (_vertsUploadDirty)
				{
					Array.Copy(_wireVerts, offset, wire.Verts, 0, wire.Verts.Length);
					wire.Mesh.vertices = wire.Verts;
				}
				Array.Copy(_wireColors, offset, wire.Colors, 0, wire.Colors.Length);
				wire.Mesh.colors32 = wire.Colors;
				wire.Mesh.SetIndices(wire.Indices, MeshTopology.Lines, 0);
				Graphics.DrawMeshNow(wire.Mesh, Matrix4x4.identity);
			}
			_vertsUploadDirty = false;
		}

		private void RebuildWireColors(bool useSoftColors, bool useBrushColors, bool showWeldColor, HashSet<int> selectedVerts, HashSet<int> mirrorVerts)
		{
			int count = _wireVerts.Length;
			if (_wireColors == null || _wireColors.Length != count)
				_wireColors = new Color32[count];
			Color32 defCol = BSE.WireColorDefault.Value;
			if (Window != null && Window.WireframeDisplayMode == ShapeEditorWindow.WireframeDisplayType.Interact)
				defCol.a = 0;
			Color32 weldedDefCol = showWeldColor ? (Color32)BSE.VertexColorWelded.Value : defCol;
			if (showWeldColor && Window != null && Window.WireframeDisplayMode == ShapeEditorWindow.WireframeDisplayType.Interact)
				weldedDefCol.a = 0;
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
					else if (showWeldColor && _weldGroups.IsWelded(i))
						_wireColors[i] = weldedDefCol;
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
					else if (showWeldColor && _weldGroups.IsWelded(i))
						_wireColors[i] = weldedDefCol;
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
				else if (showWeldColor && _weldGroups.IsWelded(j))
					_wireColors[j] = weldedDefCol;
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
			foreach (Renderer renderer in Window.GetSelectedRenderers())
				DrawMeshHighlight(renderer);
		}

		private void DrawMeshHighlight(Renderer renderer)
		{
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
			int[] tris = GetHighlightTriangles(sourceMesh, poseMesh);
			Vector3[] vertices = poseMesh.vertices;
			_highlightMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(l2w);
			GL.Begin(4);
			GL.Color(HighlightColor);
			foreach (int t in tris)
				GL.Vertex(vertices[t]);

			GL.End();
			GL.PopMatrix();
		}

		/// Triangles per source mesh, cached so highlighting several renderers doesn't re-read them each frame.
		private int[] GetHighlightTriangles(Mesh key, Mesh readFrom)
		{
			if (_highlightTris.TryGetValue(key, out int[] tris))
				return tris;
			if (_highlightTris.Count > 32)
				_highlightTris.Clear();
			tris = readFrom.triangles;
			_highlightTris[key] = tris;
			return tris;
		}

		private void DrawMeshHighlightFromDeformer(ShapeDeformer deformer, Renderer originalRenderer)
		{
			Mesh displayMesh = deformer.DisplayMesh;
			if (!displayMesh.isReadable)
				return;
			int[] tris = GetHighlightTriangles(displayMesh, displayMesh);
			Vector3[] vertices = displayMesh.vertices;
			Matrix4x4 l2w = deformer.DisplayTransform.localToWorldMatrix;
			_highlightMaterial.SetPass(0);
			GL.PushMatrix();
			GL.MultMatrix(l2w);
			GL.Begin(4);
			GL.Color(HighlightColor);
			foreach (int t in tris)
				GL.Vertex(vertices[t]);

			GL.End();
			GL.PopMatrix();
		}

		public void ActivateHighlight(int vertexCount)
		{
			if (!_session.HasTarget || vertexCount <= 0)
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
			foreach (EditPart part in _session.Parts)
				if (part.Deformer)
					part.Deformer.EnterEditMode(_weightMaterial);
			_highlightActive = true;
		}

		public void DeactivateHighlight()
		{
			foreach (EditPart part in _session.Parts)
				if (part.Deformer)
					part.Deformer.ExitEditMode();
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
			if (!_highlightActive || !_session.HasTarget || weights == null)
				return;
			_highlightColors = new Color[_highlightVertexCount];
			int count = Mathf.Min(weights.Length, _highlightColors.Length);
			for (var i = 0; i < count; i++)
				_highlightColors[i] = WeightGradient.Evaluate(weights[i]);
			PushHighlightColors();
		}

		public void UpdateHighlightColors(Dictionary<int, float> affectedVertices)
		{
			if (!_highlightActive || !_session.HasTarget || _highlightColors == null)
				return;
			foreach (KeyValuePair<int, float> pair in affectedVertices.Where(pair => pair.Key >= 0 && pair.Key < _highlightColors.Length))
			{
				_highlightColors[pair.Key] = WeightGradient.Evaluate(pair.Value);
			}
			PushHighlightColors();
		}

		// Splits the global highlight colour buffer into each part's own vertex range.
		private void PushHighlightColors()
		{
			foreach (EditPart part in _session.Parts)
			{
				if (!part.Deformer)
					continue;
				var colors = new Color[part.VertexCount];
				Array.Copy(_highlightColors, part.Offset, colors, 0, Mathf.Min(colors.Length, _highlightColors.Length - part.Offset));
				part.Deformer.SetEditColors(colors);
			}
		}

		private void RefreshWireframe()
		{
			IList<EditPart> parts = _session.Parts;
			bool allLive = parts.Count > 0;
			foreach (EditPart part in parts)
				allLive &= part.Deformer && part.Deformer.DisplayMesh && part.Deformer.DisplayTransform;
			if (!allLive)
			{
				_wireVerts = null;
				_wireTris = null;
				_edges = null;
				_triFrontFacing = null;
				_vertexVisible = null;
				return;
			}
			bool meshChanged = _wireTris == null || _wireMeshIds == null || _wireMeshIds.Length != parts.Count;
			for (var p = 0; !meshChanged && p < parts.Count; p++)
				meshChanged = _wireMeshIds[p] != parts[p].Deformer.DisplayMesh.GetInstanceID();
			int vCount = _session.VertexCount;
			if (_wireVerts == null || _wireVerts.Length != vCount)
			{
				_wireVerts = new Vector3[vCount];
				_vertsUploadDirty = true;
			}
			if (_vertexVisible == null || _vertexVisible.Length != vCount)
				_vertexVisible = new bool[vCount];
			// Live deformers change every frame (brushing/animation), so always retransform.
			foreach (EditPart part in parts)
			{
				// Read vertices into pre-allocated list to avoid allocation from mesh.vertices property
				part.Deformer.DisplayMesh.GetVertices(_vertexReadback);
				Matrix4x4 l2w = part.Deformer.DisplayTransform.localToWorldMatrix;
				int count = Mathf.Min(_vertexReadback.Count, part.VertexCount);
				for (var i = 0; i < count; i++)
					_wireVerts[part.Offset + i] = l2w.MultiplyPoint3x4(_vertexReadback[i]);
			}
			_vertsUploadDirty = true;
			// Rebuild cull results since verts moved
			_culledVisibleCount = 0;
			if (!meshChanged) return;
			var tris = new List<int>();
			_wireMeshIds = new int[parts.Count];
			for (var p = 0; p < parts.Count; p++)
			{
				Mesh mesh = parts[p].Deformer.DisplayMesh;
				_wireMeshIds[p] = mesh.GetInstanceID();
				int offset = parts[p].Offset;
				foreach (int t in mesh.triangles)
					tris.Add(t + offset);
			}
			_wireTris = tris.ToArray();
			int triCount = _wireTris.Length / 3;
			if (_triFrontFacing == null || _triFrontFacing.Length != triCount)
				_triFrontFacing = new bool[triCount];
			ExtractUniqueEdges(_wireTris);
			DestroyWireParts();
			foreach (EditPart part in parts)
			{
				var mesh = new Mesh();
				mesh.MarkDynamic();
				_wireParts.Add(new WirePartMesh
				{
					Mesh = mesh,
					Verts = new Vector3[part.VertexCount],
					Colors = new Color32[part.VertexCount]
				});
			}
			_vertsUploadDirty = true;
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
			_activeInstances.Remove(this);
			DeactivateHighlight();
			_session.Clear();
			Input?.Cleanup();
			if (_cursorMaterial)
				Destroy(_cursorMaterial);
			if (_highlightMaterial)
				Destroy(_highlightMaterial);
			if (_bakeMeshCache)
				Destroy(_bakeMeshCache);
			DestroyWireParts();
		}

		private void DestroyWireParts()
		{
			foreach (WirePartMesh wire in _wireParts)
				if (wire.Mesh)
					Destroy(wire.Mesh);
			_wireParts.Clear();
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
		public InputHelper Input;
		public Action OnRefreshRenderers;
		public Func<object> GetCurrentSelection;

		private Camera _camera;
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
		private readonly EditSession _session = new EditSession();
		private readonly WeldGroups _weldGroups = new WeldGroups();
		private readonly DeformSurface _deformSurface = new DeformSurface();
		private readonly Dictionary<int, Vector3> _displacementScratch = new Dictionary<int, Vector3>();
		private Vector3[] _weldBlendedNormalsBuffer;
		private static readonly List<ShapePaintOverlay> _activeInstances = new List<ShapePaintOverlay>();
		private Material _cursorMaterial;
		private Vector3 _lastHitPoint;
		private Vector3 _lastHitNormal;
		private bool _hasHit;
		private const int CursorSegments = 32;
		private Material _highlightMaterial;
		private readonly Dictionary<Mesh, int[]> _highlightTris = new Dictionary<Mesh, int[]>();
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
		private int[] _wireMeshIds;
		private float _refreshTimer;
		private const float ColliderRefreshInterval = 0.5f;
		private WireEdge[] _edges;
		private readonly List<WirePartMesh> _wireParts = new List<WirePartMesh>();
		private int[] _lineIndexBuffer;
		private Color32[] _wireColors;
		private bool _wireColorsDirty = true;
		private bool _vertsUploadDirty = true;
		private bool _prevUseSoftColors;
		private bool _prevUseBrushColors;
		private bool _prevHasMirror;
		private bool _prevShowWeldColor;
		private ShapeEditorWindow.WireframeDisplayType _prevWireframeDisplayMode;
		// Cull results computed in LateUpdate, consumed in OnRenderObject
		private int _culledVisibleCount;
		// Hover vertex detection
		private int _hoverVertexIndex = -1;
		private List<Vector3> _vertexReadback = new List<Vector3>();
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
		private bool _adjacencyHasWeldBridges;
		private static float DotPixelSize => BSE.VertexDotSize.Value;
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");

		private class WirePartMesh
		{
			public Mesh Mesh;
			public Vector3[] Verts;
			public Color32[] Colors;
			public int[] Indices;
		}

		private struct WireEdge
		{
			public int v0;
			public int v1;
			public int tri0;
			public int tri1;
		}
	}
}
