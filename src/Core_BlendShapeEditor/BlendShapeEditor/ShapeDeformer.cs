using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;

namespace BlendShapeEditor
{
	[DefaultExecutionOrder(32000)]
	public class ShapeDeformer : MonoBehaviour
	{
		public DeformData DeformData { get; set; }

		public void InvalidateDeltaCache()
		{
			_cachedFinalDeltas = null;
		}

		public bool StudioMode { get; set; }
		public static bool ForceBakePath = false;

		/// Independent multiplier applied to the combined layer deltas purely for display.
		/// Does not affect layer weights and is not taken into account when baking.
		public float PreviewMultiplier { get; set; } = 1f;

		public Mesh DisplayMesh => _displayMesh;

		public Transform DisplayTransform => _displayGo ? _displayGo.transform : null;
		
		internal List<string> ExistingBlendShapeNames = new List<string>();

		private int _editingExistingShapeIndex = -1;
		private float _editingExistingShapeSavedWeight;

		/// Restores the SMR's weight for the blendshape most recently imported via
		/// ImportBlendShapeAsLayer, if any. Safe to call unconditionally (no-op otherwise).
		public void RestoreEditedShapeWeight()
		{
			if (_editingExistingShapeIndex < 0 || !_smr)
				return;
			_smr.SetBlendShapeWeight(_editingExistingShapeIndex, _editingExistingShapeSavedWeight);
			_editingExistingShapeIndex = -1;
		}

		public void Init(SkinnedMeshRenderer smr)
		{
			int meshId = smr.sharedMesh ? smr.sharedMesh.GetInstanceID() : 0;
			if (_smr == smr && !_isStatic && _sourceMeshId == meshId)
				return;

			bool meshNotReadable = smr.sharedMesh && !smr.sharedMesh.isReadable;
			if (meshNotReadable)
			{
				BSE.Logger.LogInfo("ShapeDeformer.Init: mesh '" + smr.sharedMesh.name + "' is not readable, forcing BakeMesh path");
				StudioMode = false;
			}

			if (_smr == smr && _sourceMeshId != meshId)
				_cachedFinalDeltas = null;

			if (_displayGo)
				DestroyImmediate(_displayGo);

			_smr = smr;
			_sourceMeshId = meshId;
			_originalSMRShadowMode = smr.shadowCastingMode;
			_isStatic = false;
			_sourceMeshFilter = null;
			_sourceMeshRenderer = null;
			_restVertices = null;

			for (var index = 0; index < smr.sharedMesh.blendShapeCount; index++)
			{
				ExistingBlendShapeNames.Add(smr.sharedMesh.GetBlendShapeName(index));
			}
			
			if (StudioMode)
			{
				Mesh sharedMesh = smr.sharedMesh;
				_bindVertices = sharedMesh.vertices;
				_bindNormals = sharedMesh.normals;
				_boneWeights = sharedMesh.boneWeights;
				_bindPoses = sharedMesh.bindposes;
				_smrBones = smr.bones;
				_boneMatrices = new Matrix4x4[_smrBones.Length];
				_workingVerts = new Vector3[_bindVertices.Length];
				if (_bindNormals != null && _bindNormals.Length == _bindVertices.Length)
					_workingNormals = new Vector3[_bindNormals.Length];

				_blendShapeCount = sharedMesh.blendShapeCount;
				if (_blendShapeCount > 0)
				{
					int vertexCount = _bindVertices.Length;
					_bsDeltaVertices = new Vector3[_blendShapeCount][];
					_bsDeltaNormals = new Vector3[_blendShapeCount][];
					var tangentScratch = new Vector3[vertexCount];
					for (var i = 0; i < _blendShapeCount; i++)
					{
						_bsDeltaVertices[i] = new Vector3[vertexCount];
						_bsDeltaNormals[i] = new Vector3[vertexCount];
						int frameCount = sharedMesh.GetBlendShapeFrameCount(i);
						sharedMesh.GetBlendShapeFrameVertices(i, frameCount - 1, _bsDeltaVertices[i], _bsDeltaNormals[i], tangentScratch);
					}
					_blendedVertices = new Vector3[vertexCount];
					_blendedNormals = _bindNormals != null ? new Vector3[vertexCount] : null;
					_bsWeightsCache = new float[_blendShapeCount];
				}
				else
				{
					ClearBlendshapeData();
				}
			}
			else
			{
				ClearCPUSkinningData();
				AcquireSkinningData(smr, meshNotReadable);
			}

			CreateDisplayGO(smr, false);
			if (!_displayMesh)
				_displayMesh = new Mesh();

			if (meshNotReadable || ForceBakePath)
			{
				bool smrWasDisabled = !smr.enabled;
				if (smrWasDisabled)
					smr.enabled = true;
				BSE.Logger.LogDebug($"Baking SkinnedMeshRenderer {smr.name}");
				smr.BakeMesh(_displayMesh);
				if (smrWasDisabled)
					smr.enabled = false;
				UndoBakedScale(_displayMesh, smr.transform.lossyScale);
				_displayMesh.RecalculateBounds();
				_restColors = _displayMesh.colors;
				_workingVerts = new Vector3[_displayMesh.vertexCount];
				_workingNormals = new Vector3[_displayMesh.vertexCount];
				return;
			}

			CopyStaticMeshAttributes(smr.sharedMesh);
			if (!StudioMode)
			{
				bool smrWasDisabled = !smr.enabled;
				if (smrWasDisabled)
					smr.enabled = true;
				Mesh mesh = new Mesh();
				smr.BakeMesh(mesh);
				if (smrWasDisabled)
					smr.enabled = false;
				Vector3[] vertices = mesh.vertices;
				Vector3[] normals = mesh.normals;
				DestroyImmediate(mesh);
				UndoBakedScale(vertices, smr.transform.lossyScale);
				_displayMesh.vertices = vertices;
				if (normals != null && normals.Length != 0)
					_displayMesh.normals = normals;
				_displayMesh.RecalculateBounds();
			}
		}

		/// Reads an existing blendshape's final frame deltas off the SMR's mesh and returns them
		/// as a new, unattached DeformLayer at full weight. Returns null if the shape doesn't exist
		/// or has no frames. Requires Init(SkinnedMeshRenderer) to have already run.
		public DeformLayer ImportBlendShapeAsLayer(string shapeName)
		{
			if (!_smr || !_smr.sharedMesh)
				return null;

			Mesh mesh = _smr.sharedMesh;
			int shapeIndex = mesh.GetBlendShapeIndex(shapeName);
			if (shapeIndex < 0)
			{
				BSE.Logger.LogWarning($"ImportBlendShapeAsLayer: blendshape '{shapeName}' not found on mesh '{mesh.name}'");
				return null;
			}

			int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
			if (frameCount <= 0)
			{
				BSE.Logger.LogWarning($"ImportBlendShapeAsLayer: blendshape '{shapeName}' has no frames");
				return null;
			}

			int vertexCount = mesh.vertexCount;
			var deltaVerts = new Vector3[vertexCount];
			var deltaNormals = new Vector3[vertexCount];
			var deltaTangents = new Vector3[vertexCount];
			mesh.GetBlendShapeFrameVertices(shapeIndex, frameCount - 1, deltaVerts, deltaNormals, deltaTangents);

			var layer = new DeformLayer(shapeName, vertexCount) { Weight = 1f };
			deltaVerts.CopyTo(layer.Deltas, 0);

			// The SMR still has this shape applied at whatever weight it was set to; leaving it
			// active would double-apply the shape (once by skinning, once by our imported layer).
			// Zero it out here and restore it in RestoreEditedShapeWeight() once editing ends.
			_editingExistingShapeSavedWeight = _smr.GetBlendShapeWeight(shapeIndex);
			_smr.SetBlendShapeWeight(shapeIndex, 0f);
			_editingExistingShapeIndex = shapeIndex;

			return layer;
		}

		public void Init(MeshFilter sourceMf, MeshRenderer sourceMr)
		{
			if (sourceMf.sharedMesh && !sourceMf.sharedMesh.isReadable)
			{
				BlendShapeEditorPlugin.Logger.LogWarning("ShapeDeformer.Init: static mesh '" + sourceMf.sharedMesh.name + "' is not readable, skipping");
				return;
			}

			int meshId = sourceMf.sharedMesh ? sourceMf.sharedMesh.GetInstanceID() : 0;
			if (_isStatic && _sourceMeshFilter == sourceMf && _sourceMeshId == meshId)
				return;

			if (_sourceMeshFilter == sourceMf && _sourceMeshId != meshId)
				_cachedFinalDeltas = null;

			_sourceMeshId = meshId;
			if (_displayGo)
				DestroyImmediate(_displayGo);

			_isStatic = true;
			_sourceMeshFilter = sourceMf;
			_sourceMeshRenderer = sourceMr;
			_originalStaticShadowMode = sourceMr.shadowCastingMode;
			_smr = null;
			ClearCPUSkinningData();

			_restVertices = sourceMf.sharedMesh.vertices;
			_restNormals = sourceMf.sharedMesh.normals;
			Color[] colors = sourceMf.sharedMesh.colors;
			_restColors = colors != null && colors.Length != 0 ? colors : null;
			_workingVerts = new Vector3[_restVertices.Length];
			if (_restNormals != null && _restNormals.Length == _restVertices.Length)
				_workingNormals = new Vector3[_restNormals.Length];

			CreateDisplayGO(sourceMr, true);
			if (!_displayMesh)
				_displayMesh = new Mesh();
			CopyStaticMeshAttributes(sourceMf.sharedMesh);
		}

		private void CreateDisplayGO(Renderer source, bool copyLightmap = false)
		{
			_displayGo = new GameObject("_blendshapeeditor_display");
			_displayGo.transform.SetParent(source.transform, false);
			_displayGo.transform.localPosition = Vector3.zero;
			_displayGo.transform.localRotation = Quaternion.identity;
			_displayGo.transform.localScale = Vector3.one;
			_displayGo.layer = source.gameObject.layer;
			_displayMeshFilter = _displayGo.AddComponent<MeshFilter>();
			_displayMeshRenderer = _displayGo.AddComponent<MeshRenderer>();
			_displayMeshRenderer.sharedMaterials = source.sharedMaterials;
			_displayMeshRenderer.shadowCastingMode = source.shadowCastingMode;
			_displayMeshRenderer.receiveShadows = source.receiveShadows;
			_displayMeshRenderer.lightProbeUsage = source.lightProbeUsage;
			_displayMeshRenderer.reflectionProbeUsage = source.reflectionProbeUsage;
			_displayMeshRenderer.probeAnchor = source.probeAnchor;
			if (copyLightmap)
			{
				_displayMeshRenderer.lightmapIndex = source.lightmapIndex;
				_displayMeshRenderer.lightmapScaleOffset = source.lightmapScaleOffset;
				_displayMeshRenderer.realtimeLightmapIndex = source.realtimeLightmapIndex;
				_displayMeshRenderer.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
			}
			_displayMeshRenderer.enabled = false;
		}

		private void CopyStaticMeshAttributes(Mesh sourceMesh)
		{
			if (!sourceMesh || !_displayMesh)
				return;

			_displayMesh.Clear();
			_displayMesh.vertices = sourceMesh.vertices;
			_displayMesh.normals = sourceMesh.normals;
			_displayMesh.uv = sourceMesh.uv;
			if (sourceMesh.uv2 != null && sourceMesh.uv2.Length != 0)
				_displayMesh.uv2 = sourceMesh.uv2;
			_displayMesh.tangents = sourceMesh.tangents;
			if (sourceMesh.colors != null && sourceMesh.colors.Length != 0)
				_displayMesh.colors = sourceMesh.colors;
			_displayMesh.subMeshCount = sourceMesh.subMeshCount;
			for (var i = 0; i < sourceMesh.subMeshCount; i++)
				_displayMesh.SetTriangles(sourceMesh.GetTriangles(i), i);
			_displayMesh.RecalculateBounds();
		}

		public void EnterEditMode(Material editMaterial)
		{
			_editMode = true;
			_editMaterial = editMaterial;
		}

		public void ExitEditMode()
		{
			_editMode = false;
			_editColors = null;
			_editMaterial = null;
			if (_displayMesh && _restColors != null)
				_displayMesh.colors = _restColors;
		}

		public void SetEditColors(Color[] colors)
		{
			_editColors = colors;
		}

		private void LateUpdate()
		{
			// Nothing stops the game (PoseController, animations, character reload, etc.) from
			// raising this back up mid-edit, so re-force it to 0 every frame while we're editing it.
			if (_editingExistingShapeIndex >= 0 && _smr)
				_smr.SetBlendShapeWeight(_editingExistingShapeIndex, 0f);
			DoDeformation();
		}

		internal void DoDeformation()
		{
			bool isSkinned = _smr;
			bool isStaticMesh = _isStatic && _sourceMeshFilter;
			switch (isSkinned)
			{
				case false when !isStaticMesh:
					return;
				case true when _smr.sharedMesh && _sourceMeshId != 0 && _smr.sharedMesh.GetInstanceID() != _sourceMeshId:
				{
					BlendShapeEditorPlugin.Logger.LogInfo(
						$"DoDeformation: mesh replaced (stored={_sourceMeshId}, current={_smr.sharedMesh.GetInstanceID()}), re-initializing");
					SkinnedMeshRenderer smr = _smr;
					smr.enabled = true;
					smr.shadowCastingMode = _originalSMRShadowMode;
					Init(smr);
					OnMeshReplaced?.Invoke();
					return;
				}
				default:
				{
					if (isStaticMesh && _sourceMeshFilter.sharedMesh && _sourceMeshId != 0 && _sourceMeshFilter.sharedMesh.GetInstanceID() != _sourceMeshId)
					{
						BlendShapeEditorPlugin.Logger.LogInfo("DoDeformation: static mesh replaced, re-initializing");
						if (_sourceMeshRenderer)
						{
							_sourceMeshRenderer.enabled = true;
							_sourceMeshRenderer.shadowCastingMode = _originalStaticShadowMode;
						}
						Init(_sourceMeshFilter, _sourceMeshRenderer);
						OnMeshReplaced?.Invoke();
						return;
					}

					break;
				}
			}

			bool hasLayers = DeformData != null && DeformData.HasLayers;
			if (!hasLayers && !_editMode)
			{
				if (_smr)
				{
					_smr.enabled = true;
					_smr.shadowCastingMode = _originalSMRShadowMode;
				}
				if (_sourceMeshRenderer)
				{
					_sourceMeshRenderer.enabled = true;
					_sourceMeshRenderer.shadowCastingMode = _originalStaticShadowMode;
				}
				if (_displayMeshRenderer)
					_displayMeshRenderer.enabled = false;
				return;
			}

			Transform transform_ = isSkinned ? _smr.transform : _sourceMeshFilter.transform;
			Vector3[] workingVerts;
			Vector3[] workingNormals;

			if (isSkinned)
			{
				if (StudioMode && _bindVertices != null && _boneWeights != null)
				{
					ComputeCPUSkinning();
					workingVerts = _workingVerts;
					workingNormals = _workingNormals;
				}
				else
				{
					bool smrWasDisabled = !_smr.enabled;
					if (smrWasDisabled)
						_smr.enabled = true;
					_smr.BakeMesh(_displayMesh);
					if (smrWasDisabled)
						_smr.enabled = false;

					if (_bakedVertsList == null)
						_bakedVertsList = new List<Vector3>();
					_displayMesh.GetVertices(_bakedVertsList);
					int count = _bakedVertsList.Count;
					if (_workingVerts == null || _workingVerts.Length != count)
						_workingVerts = new Vector3[count];
					_bakedVertsList.CopyTo(_workingVerts);
					workingVerts = _workingVerts;

					if (_bakedNormalsList == null)
						_bakedNormalsList = new List<Vector3>();
					_displayMesh.GetNormals(_bakedNormalsList);
					if (_bakedNormalsList.Count == count)
					{
						if (_workingNormals == null || _workingNormals.Length != count)
							_workingNormals = new Vector3[count];
						_bakedNormalsList.CopyTo(_workingNormals);
						workingNormals = _workingNormals;
					}
					else
					{
						workingNormals = null;
					}

					UndoBakedScale(workingVerts, transform_.lossyScale);
				}
			}
			else
			{
				if (_workingVerts == null || _workingVerts.Length != _restVertices.Length)
					_workingVerts = new Vector3[_restVertices.Length];
				Array.Copy(_restVertices, _workingVerts, _restVertices.Length);
				workingVerts = _workingVerts;

				if (_restNormals != null && _restNormals.Length == _restVertices.Length)
				{
					if (_workingNormals == null || _workingNormals.Length != _restNormals.Length)
						_workingNormals = new Vector3[_restNormals.Length];
					Array.Copy(_restNormals, _workingNormals, _restNormals.Length);
					workingNormals = _workingNormals;
				}
				else
				{
					workingNormals = null;
				}
			}

			if (hasLayers)
			{
				if (DeformData.AnyDirty() || _cachedFinalDeltas == null)
					_cachedFinalDeltas = DeformData.ComputeFinalDelta();

				if (_cachedFinalDeltas != null && _cachedFinalDeltas.Length == workingVerts.Length)
				{
					if (isSkinned && _boneWeights != null && _boneMatrices != null)
					{
						if (!StudioMode)
							ComputeBoneMatrices();
						ApplySkinnedDeltas(workingVerts, _cachedFinalDeltas, PreviewMultiplier);
					}
					else
					{
						for (int i = 0; i < workingVerts.Length; i++)
							workingVerts[i] += _cachedFinalDeltas[i] * PreviewMultiplier;
					}
				}
				else if (_cachedFinalDeltas != null && !_loggedDeltaMismatch)
				{
					_loggedDeltaMismatch = true;
					string meshName = isSkinned ? _smr ? _smr.name : "?" : _sourceMeshFilter ? _sourceMeshFilter.name : "?";
					BlendShapeEditorPlugin.Logger.LogWarning(
						$"DoDeformation: delta length mismatch (deltaLen={_cachedFinalDeltas.Length}, vertsLen={workingVerts.Length}, mesh='{meshName}')");
				}
			}

			bool hasValidNormals = workingNormals != null && workingNormals.Length == workingVerts.Length;
			_displayMesh.vertices = workingVerts;
			if (hasValidNormals)
				_displayMesh.normals = workingNormals;
			else
				_displayMesh.RecalculateNormals();
			_displayMesh.RecalculateBounds();

			if (_editMode && _editColors != null)
				_displayMesh.colors = _editColors;

			if (_editMode && _editColors != null)
				ApplyEditMaterial();
			else
				SyncMaterials();

			if (isSkinned)
				_smr.enabled = false;
			else if (_sourceMeshRenderer)
				_sourceMeshRenderer.enabled = false;

			_displayMeshRenderer.enabled = true;
			_displayMeshFilter.sharedMesh = _displayMesh;
			OnDeformationApplied?.Invoke();
		}

		private void ComputeCPUSkinning()
		{
			int boneCount = _smrBones.Length;
			Matrix4x4 worldToLocalMatrix = _smr.transform.worldToLocalMatrix;
			for (var i = 0; i < boneCount; i++)
			{
				_boneMatrices[i] = _smrBones[i]
					? _smrBones[i].localToWorldMatrix * _bindPoses[i]
					: _bindPoses[i];
			}

			int vertexCount = _bindVertices.Length;
			bool hasNormals = _bindNormals != null && _bindNormals.Length == vertexCount;
			Vector3[] baseVertices = _bindVertices;
			Vector3[] baseNormals = _bindNormals;

			if (_blendShapeCount > 0 && _bsDeltaVertices != null && _bsWeightsCache != null)
			{
				var anyBlendActive = false;
				for (var j = 0; j < _blendShapeCount; j++)
				{
					float bsWeight = _smr.GetBlendShapeWeight(j) / 100f;
					_bsWeightsCache[j] = bsWeight;
					if (Mathf.Abs(bsWeight) > 1E-05f)
						anyBlendActive = true;
				}

				if (anyBlendActive)
				{
					Array.Copy(_bindVertices, _blendedVertices, vertexCount);
					if (hasNormals && _blendedNormals != null)
						Array.Copy(_bindNormals, _blendedNormals, vertexCount);

					for (var k = 0; k < _blendShapeCount; k++)
					{
						float bsWeight = _bsWeightsCache[k];
						if (!(Mathf.Abs(bsWeight) >= 1E-05f)) continue;
						Vector3[] bsDeltaVerts = _bsDeltaVertices[k];
						for (var l = 0; l < vertexCount; l++)
							_blendedVertices[l] += bsDeltaVerts[l] * bsWeight;

						if (!hasNormals || _blendedNormals == null) continue;
						Vector3[] bsDeltaNormals = _bsDeltaNormals[k];
						for (var m = 0; m < vertexCount; m++)
							_blendedNormals[m] += bsDeltaNormals[m] * bsWeight;
					}

					baseVertices = _blendedVertices;
					if (hasNormals && _blendedNormals != null)
						baseNormals = _blendedNormals;
				}
			}

			for (var n = 0; n < vertexCount; n++)
			{
				BoneWeight bw = _boneWeights[n];
				Vector3 bindPos = baseVertices[n];
				Vector3 skinnedPos = Vector3.zero;
				if (bw.weight0 > 0f)
					skinnedPos += _boneMatrices[bw.boneIndex0].MultiplyPoint3x4(bindPos) * bw.weight0;
				if (bw.weight1 > 0f)
					skinnedPos += _boneMatrices[bw.boneIndex1].MultiplyPoint3x4(bindPos) * bw.weight1;
				if (bw.weight2 > 0f)
					skinnedPos += _boneMatrices[bw.boneIndex2].MultiplyPoint3x4(bindPos) * bw.weight2;
				if (bw.weight3 > 0f)
					skinnedPos += _boneMatrices[bw.boneIndex3].MultiplyPoint3x4(bindPos) * bw.weight3;
				_workingVerts[n] = worldToLocalMatrix.MultiplyPoint3x4(skinnedPos);

				if (!hasNormals) continue;
				Vector3 bindNormal = baseNormals[n];
				Vector3 skinnedNormal = Vector3.zero;
				if (bw.weight0 > 0f)
					skinnedNormal += _boneMatrices[bw.boneIndex0].MultiplyVector(bindNormal) * bw.weight0;
				if (bw.weight1 > 0f)
					skinnedNormal += _boneMatrices[bw.boneIndex1].MultiplyVector(bindNormal) * bw.weight1;
				if (bw.weight2 > 0f)
					skinnedNormal += _boneMatrices[bw.boneIndex2].MultiplyVector(bindNormal) * bw.weight2;
				if (bw.weight3 > 0f)
					skinnedNormal += _boneMatrices[bw.boneIndex3].MultiplyVector(bindNormal) * bw.weight3;
				_workingNormals[n] = worldToLocalMatrix.MultiplyVector(skinnedNormal).normalized;
			}
		}

		/// Populates the bone data used to convert deltas between bind space and the current pose.
		/// bindposes and smr.bones are always accessible; only boneWeights is gated by isReadable,
		/// so for a non-readable mesh we recover the weights via GetAllBoneWeights (KKS/Unity 2019+).
		/// Without this the deltas are stored in posed space and the bake silently comes out wrong.
		private void AcquireSkinningData(SkinnedMeshRenderer smr, bool meshNotReadable)
		{
			Mesh mesh = smr.sharedMesh;
			if (!mesh)
				return;

			_bindPoses = mesh.bindposes;
			_smrBones = smr.bones;

			_boneWeights = meshNotReadable ? ReadBoneWeightsUnreadable(mesh) : mesh.boneWeights;

			if (_boneWeights == null || _boneWeights.Length != mesh.vertexCount)
			{
				// No usable weights: every skinning-aware path falls back to treating deltas as
				// posed-space vectors, which bakes incorrectly. Make that loud rather than silent.
				_boneWeights = null;
				_bindPoses = null;
				_smrBones = null;
				_boneMatrices = null;
				BSE.Logger.LogWarning(
					$"ShapeDeformer: could not obtain bone weights for '{mesh.name}' - deltas cannot be converted to " +
					"bind space and baking to a blendshape will be inaccurate in poses away from the bind pose.");
				return;
			}

			if (_smrBones != null && _smrBones.Length != 0)
				_boneMatrices = new Matrix4x4[_smrBones.Length];

			if (meshNotReadable)
				BSE.Logger.LogInfo($"ShapeDeformer: recovered bone weights for non-readable mesh '{mesh.name}'");
		}

		/// Reads bone weights from a mesh whose isReadable flag is false.
		private static BoneWeight[] ReadBoneWeightsUnreadable(Mesh mesh)
		{
#if KKS
			try
			{
				// GetAllBoneWeights returns a flat, vertex-major stream; bonesPerVertex says how
				// many entries belong to each vertex. Unity's legacy BoneWeight holds at most 4,
				// so extra influences are dropped and the kept ones renormalized.
				Unity.Collections.NativeArray<byte> bonesPerVertex = mesh.GetBonesPerVertex();
				Unity.Collections.NativeArray<BoneWeight1> allWeights = mesh.GetAllBoneWeights();
				if (bonesPerVertex.Length == 0 || allWeights.Length == 0)
					return null;

				var result = new BoneWeight[bonesPerVertex.Length];
				var cursor = 0;
				for (var v = 0; v < bonesPerVertex.Length; v++)
				{
					int influences = bonesPerVertex[v];
					var bw = new BoneWeight();
					// GetAllBoneWeights is sorted by descending weight per vertex, so the first
					// four are the most significant.
					int kept = Mathf.Min(influences, 4);
					var sum = 0f;
					for (var i = 0; i < kept; i++)
					{
						BoneWeight1 w = allWeights[cursor + i];
						switch (i)
						{
							case 0: bw.boneIndex0 = w.boneIndex; bw.weight0 = w.weight; break;
							case 1: bw.boneIndex1 = w.boneIndex; bw.weight1 = w.weight; break;
							case 2: bw.boneIndex2 = w.boneIndex; bw.weight2 = w.weight; break;
							case 3: bw.boneIndex3 = w.boneIndex; bw.weight3 = w.weight; break;
						}
						sum += w.weight;
					}
					// Renormalize so dropped influences don't shrink the delta.
					if (sum > 1E-06f && Mathf.Abs(sum - 1f) > 1E-06f)
					{
						bw.weight0 /= sum;
						bw.weight1 /= sum;
						bw.weight2 /= sum;
						bw.weight3 /= sum;
					}
					result[v] = bw;
					cursor += influences;
				}
				return result;
			}
			catch (Exception ex)
			{
				BSE.Logger.LogWarning("ShapeDeformer.ReadBoneWeightsUnreadable failed: " + ex.Message);
				return null;
			}
#else
			// KK targets Unity 5.6, which has neither GetAllBoneWeights nor BoneWeight1.
			// No non-readable meshes have been observed on that target.
			return null;
#endif
		}

		private void ComputeBoneMatrices()
		{
			if (_smrBones == null || _bindPoses == null || _boneMatrices == null)
				return;

			Matrix4x4 worldToLocalMatrix = _smr.transform.worldToLocalMatrix;
			int boneCount = _smrBones.Length;
			for (var i = 0; i < boneCount; i++)
			{
				_boneMatrices[i] = _smrBones[i]
					? worldToLocalMatrix * _smrBones[i].localToWorldMatrix * _bindPoses[i]
					: _bindPoses[i];
			}
		}

		private void ApplySkinnedDeltas(Vector3[] verts, Vector3[] deltas, float scale = 1f)
		{
			if (_boneWeights == null || _boneMatrices == null)
			{
				for (var i = 0; i < verts.Length; i++)
					verts[i] += deltas[i] * scale;
				return;
			}

			for (var j = 0; j < verts.Length; j++)
			{
				Vector3 delta = deltas[j];
				if (delta.x == 0f && delta.y == 0f && delta.z == 0f)
					continue;

				BoneWeight bw = _boneWeights[j];
				Vector3 skinned = Vector3.zero;
				if (bw.weight0 > 0f)
					skinned += _boneMatrices[bw.boneIndex0].MultiplyVector(delta) * bw.weight0;
				if (bw.weight1 > 0f)
					skinned += _boneMatrices[bw.boneIndex1].MultiplyVector(delta) * bw.weight1;
				if (bw.weight2 > 0f)
					skinned += _boneMatrices[bw.boneIndex2].MultiplyVector(delta) * bw.weight2;
				if (bw.weight3 > 0f)
					skinned += _boneMatrices[bw.boneIndex3].MultiplyVector(delta) * bw.weight3;
				verts[j] += skinned * scale;
			}
		}

		public bool WorldDeltaToBindDelta(int vertexIdx, Vector3 worldDisp, out Vector3 bindDelta)
		{
			Transform transform = _smr ? _smr.transform
				: _sourceMeshFilter ? _sourceMeshFilter.transform
				: _sourceMeshRenderer ? _sourceMeshRenderer.transform
				: null;

			if (_boneWeights == null || _boneMatrices == null || vertexIdx < 0 || vertexIdx >= _boneWeights.Length)
			{
				bindDelta = transform ? transform.InverseTransformVector(worldDisp) : worldDisp;
				return false;
			}

			BoneWeight bw = _boneWeights[vertexIdx];
			Matrix4x4 M = Matrix4x4.zero;
			AccumulateBoneMatrix(ref M, bw.boneIndex0, bw.weight0);
			AccumulateBoneMatrix(ref M, bw.boneIndex1, bw.weight1);
			AccumulateBoneMatrix(ref M, bw.boneIndex2, bw.weight2);
			AccumulateBoneMatrix(ref M, bw.boneIndex3, bw.weight3);
			Vector3 localDisp = StudioMode ? worldDisp : (transform ? transform.InverseTransformVector(worldDisp) : worldDisp);
			bindDelta = M.inverse.MultiplyVector(localDisp);
			return true;
		}

		public void LogSkinningDiagnostic(int vertexIdx, Vector3 worldDispSample, Transform xform)
		{
			if (_boneWeights == null || _boneMatrices == null)
			{
				BSE.Logger.LogInfo($"[Diag] v={vertexIdx}: no bone data (static / non-readable)");
				return;
			}
			if (vertexIdx < 0 || vertexIdx >= _boneWeights.Length)
			{
				BSE.Logger.LogInfo($"[Diag] v={vertexIdx}: out of range ({_boneWeights.Length})");
				return;
			}

			BoneWeight bw = _boneWeights[vertexIdx];
			Matrix4x4 M = Matrix4x4.zero;
			AccumulateBoneMatrix(ref M, bw.boneIndex0, bw.weight0);
			AccumulateBoneMatrix(ref M, bw.boneIndex1, bw.weight1);
			AccumulateBoneMatrix(ref M, bw.boneIndex2, bw.weight2);
			AccumulateBoneMatrix(ref M, bw.boneIndex3, bw.weight3);

			Vector3 boneX = M.MultiplyVector(Vector3.right);
			Vector3 boneY = M.MultiplyVector(Vector3.up);
			Vector3 boneZ = M.MultiplyVector(Vector3.forward);
			Vector3 localDisp = StudioMode ? worldDispSample : (xform ? xform.InverseTransformVector(worldDispSample) : worldDispSample);

			// The round-trip that actually matters: what the editor stores (M.inverse) vs what
			// Unity/ApplySkinnedDeltas replays on top of it (sum of w*M). If these disagree, the
			// baked blendshape will not match what was previewed.
			WorldDeltaToBindDelta(vertexIdx, worldDispSample, out Vector3 storedBindDelta);
			Vector3 replayed = Vector3.zero;
			AccumulateSkinnedVector(ref replayed, bw.boneIndex0, bw.weight0, storedBindDelta);
			AccumulateSkinnedVector(ref replayed, bw.boneIndex1, bw.weight1, storedBindDelta);
			AccumulateSkinnedVector(ref replayed, bw.boneIndex2, bw.weight2, storedBindDelta);
			AccumulateSkinnedVector(ref replayed, bw.boneIndex3, bw.weight3, storedBindDelta);

			float dispLen = localDisp.magnitude;
			float scaleRatio = dispLen > 1E-06f ? replayed.magnitude / dispLen : 0f;
			float angleDeg = Vector3.Angle(replayed, localDisp);
			float weightSum = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
			Vector3 residual = replayed - localDisp;

			BSE.Logger.LogInfo(string.Concat(new[]
			{
				$"[Diag] v={vertexIdx} renderer={(_smr != null ? _smr.name : "?")} studio={StudioMode}\n",
				$"  bones: {BoneName(bw.boneIndex0)}({bw.weight0:F2}) {BoneName(bw.boneIndex1)}({bw.weight1:F2}) {BoneName(bw.boneIndex2)}({bw.weight2:F2}) {BoneName(bw.boneIndex3)}({bw.weight3:F2}) sum={weightSum:F4}\n",
				$"  M*X={boneX} len={boneX.magnitude:F3}\n",
				$"  M*Y={boneY} len={boneY.magnitude:F3}\n",
				$"  M*Z={boneZ} len={boneZ.magnitude:F3}\n",
				$"  worldDisp={worldDispSample} len={worldDispSample.magnitude:F4}\n",
				$"  wanted(smrLocal)={localDisp} len={dispLen:F4}\n",
				$"  storedBindDelta={storedBindDelta} len={storedBindDelta.magnitude:F4}\n",
				$"  replayed(sum w*M*stored)={replayed} len={replayed.magnitude:F4}\n",
				$"  ROUNDTRIP: ratio={scaleRatio:F4} angleDeg={angleDeg:F2} residualLen={residual.magnitude:F6}"
			}));
		}

		private string BoneName(int boneIdx)
		{
			if (_smrBones == null || boneIdx < 0 || boneIdx >= _smrBones.Length)
				return "?";
			return _smrBones[boneIdx] ? _smrBones[boneIdx].name : "?";
		}

		/// Mirrors one term of ApplySkinnedDeltas so the diagnostic replays exactly what the
		/// forward path (and Unity's blendshape skinning) will do to a stored bind-space delta.
		private void AccumulateSkinnedVector(ref Vector3 acc, int boneIdx, float w, Vector3 v)
		{
			if (w <= 0f || _boneMatrices == null || boneIdx < 0 || boneIdx >= _boneMatrices.Length)
				return;
			acc += _boneMatrices[boneIdx].MultiplyVector(v) * w;
		}

		private void AccumulateBoneMatrix(ref Matrix4x4 M, int boneIdx, float w)
		{
			if (w <= 0f || _boneMatrices == null || boneIdx < 0 || boneIdx >= _boneMatrices.Length)
				return;

			Matrix4x4 bm = _boneMatrices[boneIdx];
			M.m00 += bm.m00 * w;
			M.m01 += bm.m01 * w;
			M.m02 += bm.m02 * w;
			M.m03 += bm.m03 * w;
			M.m10 += bm.m10 * w;
			M.m11 += bm.m11 * w;
			M.m12 += bm.m12 * w;
			M.m13 += bm.m13 * w;
			M.m20 += bm.m20 * w;
			M.m21 += bm.m21 * w;
			M.m22 += bm.m22 * w;
			M.m23 += bm.m23 * w;
			M.m30 += bm.m30 * w;
			M.m31 += bm.m31 * w;
			M.m32 += bm.m32 * w;
			M.m33 += bm.m33 * w;
		}

		private void ApplyEditMaterial()
		{
			if (!_editMaterial || !_displayMeshRenderer)
				return;

			Renderer source = SourceRenderer;
			if (!source)
				return;

			Material[] sourceMats = source.sharedMaterials;
			Material[] displayMats = _displayMeshRenderer.sharedMaterials;
			bool materialsChanged = sourceMats.Length != displayMats.Length;
			if (!materialsChanged)
			{
				if (displayMats.Any(t => t != _editMaterial))
				{
					materialsChanged = true;
				}
			}

			if (!materialsChanged) return;
			var newMats = new Material[sourceMats.Length];
			for (var j = 0; j < newMats.Length; j++)
				newMats[j] = _editMaterial;
			_displayMeshRenderer.sharedMaterials = newMats;
		}

		private void SyncMaterials()
		{
			if (!_displayMeshRenderer)
				return;

			Renderer source = SourceRenderer;
			if (!source)
				return;

			Material[] sourceMats = source.sharedMaterials;
			Material[] displayMats = _displayMeshRenderer.sharedMaterials;
			bool materialsChanged = sourceMats.Length != displayMats.Length;
			if (!materialsChanged)
			{
				if (sourceMats.Where((t, i) => t != displayMats[i]).Any())
				{
					materialsChanged = true;
				}
			}
			if (materialsChanged)
				_displayMeshRenderer.sharedMaterials = sourceMats;
		}

		private Renderer SourceRenderer => _smr ? (Renderer)_smr : _sourceMeshRenderer;

		private void ClearCPUSkinningData()
		{
			_bindVertices = null;
			_bindNormals = null;
			_boneWeights = null;
			_bindPoses = null;
			_smrBones = null;
			_boneMatrices = null;
			ClearBlendshapeData();
		}

		private void ClearBlendshapeData()
		{
			_blendShapeCount = 0;
			_bsDeltaVertices = null;
			_bsDeltaNormals = null;
			_blendedVertices = null;
			_blendedNormals = null;
			_bsWeightsCache = null;
		}

		private static void UndoBakedScale(Mesh mesh, Vector3 scale)
		{
			Vector3[] vertices = mesh.vertices;
			UndoBakedScale(vertices, scale);
			if (vertices.Length != 0)
				mesh.vertices = vertices;
		}

		private static void UndoBakedScale(Vector3[] verts, Vector3 scale)
		{
			if (!(Mathf.Abs(scale.x - 1f) > 0.001f) && !(Mathf.Abs(scale.y - 1f) > 0.001f) &&
			    !(Mathf.Abs(scale.z - 1f) > 0.001f)) return;
			for (var i = 0; i < verts.Length; i++)
			{
				verts[i].x /= scale.x;
				verts[i].y /= scale.y;
				verts[i].z /= scale.z;
			}
		}

		private void OnDestroy()
		{
			if (_smr)
			{
				_smr.enabled = true;
				_smr.shadowCastingMode = _originalSMRShadowMode;
			}
			if (_sourceMeshRenderer)
			{
				_sourceMeshRenderer.enabled = true;
				_sourceMeshRenderer.shadowCastingMode = _originalStaticShadowMode;
			}
			_editMode = false;
			_editMaterial = null;
			_editColors = null;
			if (_displayGo)
				DestroyImmediate(_displayGo);
			if (_displayMesh)
				DestroyImmediate(_displayMesh);
		}

		// --- Target renderer (one of _smr or _sourceMeshFilter/_sourceMeshRenderer is set, not both) ---
		private SkinnedMeshRenderer _smr;              // target SMR; null when targeting a static mesh
		private MeshFilter _sourceMeshFilter;          // static mesh source; null when targeting an SMR
		private MeshRenderer _sourceMeshRenderer;      // renderer paired with _sourceMeshFilter

		// --- Display GO: child GameObject with a plain MeshRenderer that shows the deformed result ---
		private GameObject _displayGo;                 // parent of the display MeshFilter/MeshRenderer
		private MeshFilter _displayMeshFilter;         // MeshFilter on _displayGo; receives _bakedMesh each frame
		private MeshRenderer _displayMeshRenderer;     // MeshRenderer on _displayGo; shown while original is hidden
		private Mesh _displayMesh;                     // reused mesh object written to every frame

		// --- Static-mesh rest state (only valid when _isStatic == true) ---
		private bool _isStatic;                        // true when the target is a MeshFilter/MeshRenderer pair
		private Vector3[] _restVertices;               // original vertex positions from sourceMeshFilter.sharedMesh
		private Vector3[] _restNormals;                // original normals from sourceMeshFilter.sharedMesh
		private Color[] _restColors;                   // original vertex colors; restored when exiting edit mode

		// --- Per-frame working buffers (reused to avoid GC each LateUpdate) ---
		private Vector3[] _workingVerts;               // scratch buffer for this frame's final vertex positions
		private Vector3[] _workingNormals;             // scratch buffer for this frame's final normals

		// --- Shadow mode saved at Init time so OnDestroy can restore it ---
		private ShadowCastingMode _originalSMRShadowMode;
		private ShadowCastingMode _originalStaticShadowMode;

		// --- CPU skinning data (Studio mode only; pre-sampled from sharedMesh at Init time) ---
		private Vector3[] _bindVertices;               // bind-pose vertex positions from sharedMesh.vertices
		private Vector3[] _bindNormals;                // bind-pose normals from sharedMesh.normals
		private BoneWeight[] _boneWeights;             // per-vertex bone weights; also used for delta conversion in Maker
		private Matrix4x4[] _bindPoses;                // inverse bind-pose matrices from sharedMesh.bindposes
		private Transform[] _smrBones;                 // bone transforms sampled from smr.bones at Init time
		private Matrix4x4[] _boneMatrices;             // composed bone matrices (world-to-local * boneL2W * bindPose) per frame

		// --- BlendShape baking during CPU skinning (Studio mode) ---
		private int _blendShapeCount;                  // number of blendshapes on sharedMesh at Init time
		private Vector3[][] _bsDeltaVertices;          // [blendshapeIndex][vertexIndex] delta verts at 100% weight
		private Vector3[][] _bsDeltaNormals;           // [blendshapeIndex][vertexIndex] delta normals at 100% weight
		private Vector3[] _blendedVertices;            // bind verts + active blendshape contributions (scratch)
		private Vector3[] _blendedNormals;             // bind normals + active blendshape contributions (scratch)
		private float[] _bsWeightsCache;               // per-frame snapshot of smr.GetBlendShapeWeight() / 100f

		// --- Misc ---
		private int _sourceMeshId;                     // GetInstanceID() of the mesh at Init time; detects hot-swap
		private List<Vector3> _bakedVertsList;         // reused list for BakeMesh vertex readback (avoids array alloc)
		private List<Vector3> _bakedNormalsList;       // reused list for BakeMesh normal readback
		private Vector3[] _cachedFinalDeltas;          // result of DeformData.ComputeFinalDelta(), invalidated on dirty
		private bool _loggedDeltaMismatch;             // rate-limit for the delta-length mismatch warning

		// --- Edit-mode overlay ---
		private bool _editMode;                        // true while the editor has called EnterEditMode
		private Material _editMaterial;                // solid-color material used to tint the display mesh in edit mode
		private Color[] _editColors;                   // per-vertex colors written to _bakedMesh to show brush influence
		
		/// <summary>
		/// Bakes the current combined deformation into a new blendshape on the SMR's mesh.
		/// Returns the blendshape index, or -1 on failure.
		/// Also returns the delta arrays used for BSC registration.
		/// </summary>
		public bool BakeToBlendShape(string shapeName, out Vector3[] outDeltaVerts, out Vector3[] outDeltaNormals,
			bool computeDeltaNormals = true)
		{
			outDeltaVerts = null;
			outDeltaNormals = null;

			if (DeformData == null || !DeformData.HasLayers)
			{
				BSE.Logger.LogWarning("BakeToBlendShape: no layers to bake");
				return false;
			}

			BakeToBlendShape(shapeName, DeformData.ComputeFinalDelta(), out outDeltaVerts, out outDeltaNormals, computeDeltaNormals);
			return true;
		}

		public bool BakeToBlendShape(string shapeName, Vector3[] delta, out Vector3[] outDeltaVerts, out Vector3[] outDeltaNormals,
			bool computeDeltaNormals = true)
		{
			outDeltaVerts = null;
			outDeltaNormals = null;

			if (!_smr || !_smr.sharedMesh)
			{
				BSE.Logger.LogWarning("BakeToBlendShape: no SMR or mesh");
				return false;
			}

			Mesh mesh = _smr.sharedMesh;
			// A non-readable mesh returns empty geometry arrays, so fall back to vertexCount
			// (always valid) rather than reporting a bogus length mismatch.
			Vector3[] bindVerts = mesh.vertices;
			int vertCount = bindVerts != null && bindVerts.Length != 0 ? bindVerts.Length : mesh.vertexCount;

			if (delta == null || delta.Length != vertCount)
			{
				BSE.Logger.LogWarning(
					$"BakeToBlendShape: delta length mismatch (delta={delta?.Length ?? 0}, verts={vertCount})");
				return false;
			}

			outDeltaVerts = delta;

			if (computeDeltaNormals)
			{
				Vector3[] bindNormals = mesh.normals;
				int[] triangles = mesh.triangles;
				// ComputePartialDeltaNormals needs real geometry; skip it when the mesh is
				// non-readable rather than feeding it empty arrays.
				if (bindVerts != null && bindVerts.Length == vertCount && triangles != null && triangles.Length != 0)
					outDeltaNormals = MeshHelper.ComputePartialDeltaNormals(bindVerts, bindNormals, delta, triangles);
				else
					BSE.Logger.LogInfo(
						$"BakeToBlendShape: '{mesh.name}' has no readable geometry, baking without delta normals");
			}

			BSE.Logger.LogInfo($"BakeToBlendShape: baked '{shapeName}' to blendshape deltas");
			return true;
		}

		public Action OnDeformationApplied;
		public Action OnMeshReplaced;
	}
}
