using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace KKShapeEditor
{
	public class ShapeDeformer : MonoBehaviour
	{
		public DeformData DeformData { get; set; }

		public void InvalidateDeltaCache()
		{
			_cachedFinalDeltas = null;
		}

		public bool StudioMode { get; set; }

		public Mesh DisplayMesh => _bakedMesh;

		public Transform DisplayTransform => _displayGo != null ? _displayGo.transform : null;

		public bool IsEditMode => _editMode;

		public BoneWeight[] RemappedBoneWeights
		{
			get => _remappedBoneWeights;
			set
			{
				_remappedBoneWeights = value;
				InvalidateDeltaCache();
			}
		}

		public bool HasRemappedWeights => _remappedBoneWeights != null;

		public void ClearRemappedWeights()
		{
			_remappedBoneWeights = null;
			InvalidateDeltaCache();
		}

		public Vector3[] BindVertices => _bindVertices;

		public BoneWeight[] OriginalBoneWeights => _boneWeights;

		public Transform[] SmrBones => _smrBones;

		public void Init(SkinnedMeshRenderer smr)
		{
			int meshId = smr.sharedMesh ? smr.sharedMesh.GetInstanceID() : 0;
			if (_smr == smr && !_isStatic && _sourceMeshId == meshId)
				return;

			bool meshNotReadable = smr.sharedMesh && !smr.sharedMesh.isReadable;
			if (meshNotReadable)
			{
				ShapeEditorPlugin.Logger.LogInfo("ShapeDeformer.Init: mesh '" + smr.sharedMesh.name + "' is not readable, forcing BakeMesh path");
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
				if (!meshNotReadable && smr.sharedMesh)
				{
					_boneWeights = smr.sharedMesh.boneWeights;
					_bindPoses = smr.sharedMesh.bindposes;
					_smrBones = smr.bones;
					if (_smrBones != null && _smrBones.Length != 0)
						_boneMatrices = new Matrix4x4[_smrBones.Length];
				}
			}

			EnsureRunner();
			CreateDisplayGO(smr, false);
			if (!_bakedMesh)
				_bakedMesh = new Mesh();

			if (meshNotReadable)
			{
				bool smrWasDisabled = !smr.enabled;
				if (smrWasDisabled)
					smr.enabled = true;
				smr.BakeMesh(_bakedMesh);
				if (smrWasDisabled)
					smr.enabled = false;
				UndoBakedScale(_bakedMesh, smr.transform.lossyScale);
				_bakedMesh.RecalculateBounds();
				_restColors = _bakedMesh.colors;
				_workingVerts = new Vector3[_bakedMesh.vertexCount];
				_workingNormals = new Vector3[_bakedMesh.vertexCount];
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
				_bakedMesh.vertices = vertices;
				if (normals != null && normals.Length != 0)
					_bakedMesh.normals = normals;
				_bakedMesh.RecalculateBounds();
			}
		}

		public void Init(MeshFilter sourceMf, MeshRenderer sourceMr)
		{
			if (sourceMf.sharedMesh && !sourceMf.sharedMesh.isReadable)
			{
				ShapeEditorPlugin.Logger.LogWarning("ShapeDeformer.Init: static mesh '" + sourceMf.sharedMesh.name + "' is not readable, skipping");
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
			DestroyRunner();

			_restVertices = sourceMf.sharedMesh.vertices;
			_restNormals = sourceMf.sharedMesh.normals;
			Color[] colors = sourceMf.sharedMesh.colors;
			_restColors = colors != null && colors.Length != 0 ? colors : null;
			_workingVerts = new Vector3[_restVertices.Length];
			if (_restNormals != null && _restNormals.Length == _restVertices.Length)
				_workingNormals = new Vector3[_restNormals.Length];

			CreateDisplayGO(sourceMr, true);
			if (!_bakedMesh)
				_bakedMesh = new Mesh();
			CopyStaticMeshAttributes(sourceMf.sharedMesh);
		}

		private void CreateDisplayGO(Renderer source, bool copyLightmap = false)
		{
			_displayGo = new GameObject("_kkse_deform_display");
			_displayGo.transform.SetParent(source.transform, false);
			_displayGo.transform.localPosition = Vector3.zero;
			_displayGo.transform.localRotation = Quaternion.identity;
			_displayGo.transform.localScale = Vector3.one;
			_displayGo.layer = source.gameObject.layer;
			_meshFilter = _displayGo.AddComponent<MeshFilter>();
			_meshRenderer = _displayGo.AddComponent<MeshRenderer>();
			_meshRenderer.sharedMaterials = source.sharedMaterials;
			_meshRenderer.shadowCastingMode = source.shadowCastingMode;
			_meshRenderer.receiveShadows = source.receiveShadows;
			_meshRenderer.lightProbeUsage = source.lightProbeUsage;
			_meshRenderer.reflectionProbeUsage = source.reflectionProbeUsage;
			_meshRenderer.probeAnchor = source.probeAnchor;
			if (copyLightmap)
			{
				_meshRenderer.lightmapIndex = source.lightmapIndex;
				_meshRenderer.lightmapScaleOffset = source.lightmapScaleOffset;
				_meshRenderer.realtimeLightmapIndex = source.realtimeLightmapIndex;
				_meshRenderer.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
			}
			_meshRenderer.enabled = false;
		}

		private void CopyStaticMeshAttributes(Mesh sourceMesh)
		{
			if (!sourceMesh || !_bakedMesh)
				return;

			_bakedMesh.Clear();
			_bakedMesh.vertices = sourceMesh.vertices;
			_bakedMesh.normals = sourceMesh.normals;
			_bakedMesh.uv = sourceMesh.uv;
			if (sourceMesh.uv2 != null && sourceMesh.uv2.Length != 0)
				_bakedMesh.uv2 = sourceMesh.uv2;
			_bakedMesh.tangents = sourceMesh.tangents;
			if (sourceMesh.colors != null && sourceMesh.colors.Length != 0)
				_bakedMesh.colors = sourceMesh.colors;
			_bakedMesh.subMeshCount = sourceMesh.subMeshCount;
			for (var i = 0; i < sourceMesh.subMeshCount; i++)
				_bakedMesh.SetTriangles(sourceMesh.GetTriangles(i), i);
			_bakedMesh.RecalculateBounds();
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
			if (_bakedMesh && _restColors != null)
				_bakedMesh.colors = _restColors;
		}

		public void SetEditColors(Color[] colors)
		{
			_editColors = colors;
		}

		private void LateUpdate()
		{
			if (_runner && !_isStatic)
				return;
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
					ShapeEditorPlugin.Logger.LogInfo(
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
						ShapeEditorPlugin.Logger.LogInfo("DoDeformation: static mesh replaced, re-initializing");
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
				if (_meshRenderer)
					_meshRenderer.enabled = false;
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
					_smr.BakeMesh(_bakedMesh);
					if (smrWasDisabled)
						_smr.enabled = false;

					if (_bakedVertsList == null)
						_bakedVertsList = new List<Vector3>();
					_bakedMesh.GetVertices(_bakedVertsList);
					int count = _bakedVertsList.Count;
					if (_workingVerts == null || _workingVerts.Length != count)
						_workingVerts = new Vector3[count];
					_bakedVertsList.CopyTo(_workingVerts);
					workingVerts = _workingVerts;

					if (_bakedNormalsList == null)
						_bakedNormalsList = new List<Vector3>();
					_bakedMesh.GetNormals(_bakedNormalsList);
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
						ApplySkinnedDeltas(workingVerts, _cachedFinalDeltas);
					}
					else
					{
						for (int i = 0; i < workingVerts.Length; i++)
							workingVerts[i] += _cachedFinalDeltas[i];
					}
				}
				else if (_cachedFinalDeltas != null && !_loggedDeltaMismatch)
				{
					_loggedDeltaMismatch = true;
					string meshName = isSkinned ? _smr ? _smr.name : "?" : _sourceMeshFilter ? _sourceMeshFilter.name : "?";
					ShapeEditorPlugin.Logger.LogWarning(
						$"DoDeformation: delta length mismatch (deltaLen={_cachedFinalDeltas.Length}, vertsLen={workingVerts.Length}, mesh='{meshName}')");
				}
			}

			bool hasValidNormals = workingNormals != null && workingNormals.Length == workingVerts.Length;
			_bakedMesh.vertices = workingVerts;
			if (hasValidNormals)
				_bakedMesh.normals = workingNormals;
			else
				_bakedMesh.RecalculateNormals();
			_bakedMesh.RecalculateBounds();

			if (_editMode && _editColors != null)
				_bakedMesh.colors = _editColors;

			if (_editMode && _editColors != null)
				ApplyEditMaterial();
			else
				SyncMaterials();

			if (isSkinned)
				_smr.enabled = false;
			else if (_sourceMeshRenderer)
				_sourceMeshRenderer.enabled = false;

			_meshRenderer.enabled = true;
			_meshFilter.sharedMesh = _bakedMesh;
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

		private void ApplySkinnedDeltas(Vector3[] verts, Vector3[] deltas)
		{
			if (_boneWeights == null || _boneMatrices == null)
			{
				for (var i = 0; i < verts.Length; i++)
					verts[i] += deltas[i];
				return;
			}

			for (var j = 0; j < verts.Length; j++)
			{
				Vector3 delta = deltas[j];
				if (delta.x == 0f && delta.y == 0f && delta.z == 0f)
					continue;

				BoneWeight bw = _remappedBoneWeights != null ? _remappedBoneWeights[j] : _boneWeights[j];
				Vector3 skinned = Vector3.zero;
				if (bw.weight0 > 0f)
					skinned += _boneMatrices[bw.boneIndex0].MultiplyVector(delta) * bw.weight0;
				if (bw.weight1 > 0f)
					skinned += _boneMatrices[bw.boneIndex1].MultiplyVector(delta) * bw.weight1;
				if (bw.weight2 > 0f)
					skinned += _boneMatrices[bw.boneIndex2].MultiplyVector(delta) * bw.weight2;
				if (bw.weight3 > 0f)
					skinned += _boneMatrices[bw.boneIndex3].MultiplyVector(delta) * bw.weight3;
				verts[j] += skinned;
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

			BoneWeight bw = _remappedBoneWeights != null ? _remappedBoneWeights[vertexIdx] : _boneWeights[vertexIdx];
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
				ShapeEditorPlugin.Logger.LogInfo($"[Diag] v={vertexIdx}: no bone data (static / non-readable)");
				return;
			}
			if (vertexIdx < 0 || vertexIdx >= _boneWeights.Length)
			{
				ShapeEditorPlugin.Logger.LogInfo($"[Diag] v={vertexIdx}: out of range ({_boneWeights.Length})");
				return;
			}

			BoneWeight bw = _remappedBoneWeights != null ? _remappedBoneWeights[vertexIdx] : _boneWeights[vertexIdx];
			Matrix4x4 M = Matrix4x4.zero;
			AccumulateBoneMatrix(ref M, bw.boneIndex0, bw.weight0);
			AccumulateBoneMatrix(ref M, bw.boneIndex1, bw.weight1);
			AccumulateBoneMatrix(ref M, bw.boneIndex2, bw.weight2);
			AccumulateBoneMatrix(ref M, bw.boneIndex3, bw.weight3);

			Vector3 boneX = M.MultiplyVector(Vector3.right);
			Vector3 boneY = M.MultiplyVector(Vector3.up);
			Vector3 boneZ = M.MultiplyVector(Vector3.forward);
			Vector3 localDisp = xform ? xform.InverseTransformVector(worldDispSample) : worldDispSample;
			Vector3 appliedDisp = M.MultiplyVector(localDisp);
			float dispLen = localDisp.magnitude;
			float scaleRatio = dispLen > 1E-06f ? appliedDisp.magnitude / dispLen : 0f;
			float angleDeg = Vector3.Angle(appliedDisp, localDisp);

			ShapeEditorPlugin.Logger.LogInfo(string.Concat(new[]
			{
				$"[Diag] v={vertexIdx} renderer={(_smr != null ? _smr.name : "?")} studio={StudioMode}\n",
				$"  bones: {BoneName(bw.boneIndex0)}({bw.weight0:F2}) {BoneName(bw.boneIndex1)}({bw.weight1:F2}) {BoneName(bw.boneIndex2)}({bw.weight2:F2}) {BoneName(bw.boneIndex3)}({bw.weight3:F2})\n",
				$"  M*X={boneX} len={boneX.magnitude:F3}\n",
				$"  M*Y={boneY} len={boneY.magnitude:F3}\n",
				$"  M*Z={boneZ} len={boneZ.magnitude:F3}\n",
				$"  worldDisp={worldDispSample} len={worldDispSample.magnitude:F4}\n",
				$"  storedDelta(smrLocal)={localDisp} len={dispLen:F4}\n",
				$"  applied(M*delta)={appliedDisp} len={appliedDisp.magnitude:F4}\n",
				$"  applied/stored: ratio={scaleRatio:F3} angleDeg={angleDeg:F1}"
			}));
		}

		private string BoneName(int boneIdx)
		{
			if (_smrBones == null || boneIdx < 0 || boneIdx >= _smrBones.Length)
				return "?";
			return _smrBones[boneIdx] ? _smrBones[boneIdx].name : "?";
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
			if (!_editMaterial || !_meshRenderer)
				return;

			Renderer source = SourceRenderer;
			if (!source)
				return;

			Material[] sourceMats = source.sharedMaterials;
			Material[] displayMats = _meshRenderer.sharedMaterials;
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
			_meshRenderer.sharedMaterials = newMats;
		}

		private void SyncMaterials()
		{
			if (!_meshRenderer)
				return;

			Renderer source = SourceRenderer;
			if (!source)
				return;

			Material[] sourceMats = source.sharedMaterials;
			Material[] displayMats = _meshRenderer.sharedMaterials;
			bool materialsChanged = sourceMats.Length != displayMats.Length;
			if (!materialsChanged)
			{
				if (sourceMats.Where((t, i) => t != displayMats[i]).Any())
				{
					materialsChanged = true;
				}
			}
			if (materialsChanged)
				_meshRenderer.sharedMaterials = sourceMats;
		}

		private Renderer SourceRenderer => _smr ? (Renderer)_smr : _sourceMeshRenderer;

		private void EnsureRunner()
		{
			if (_runner)
				return;

			GameObject runnerGo = new GameObject("_kkse_deformer_runner");
			DontDestroyOnLoad(runnerGo);
			_runner = runnerGo.AddComponent<ShapeDeformerRunner>();
			_runner.Deformer = this;
		}

		private void DestroyRunner()
		{
			if (!_runner) return;
			DestroyImmediate(_runner.gameObject);
			_runner = null;
		}

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
			DestroyRunner();
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
			if (_bakedMesh)
				DestroyImmediate(_bakedMesh);
		}

		private SkinnedMeshRenderer _smr;
		private GameObject _displayGo;
		private MeshFilter _meshFilter;
		private MeshRenderer _meshRenderer;
		private Mesh _bakedMesh;
		private MeshFilter _sourceMeshFilter;
		private MeshRenderer _sourceMeshRenderer;
		private Vector3[] _restVertices;
		private Vector3[] _restNormals;
		private Color[] _restColors;
		private bool _isStatic;
		private Vector3[] _workingVerts;
		private Vector3[] _workingNormals;
		private ShadowCastingMode _originalSMRShadowMode;
		private ShadowCastingMode _originalStaticShadowMode;
		private Vector3[] _bindVertices;
		private Vector3[] _bindNormals;
		private BoneWeight[] _boneWeights;
		private BoneWeight[] _remappedBoneWeights;
		private Matrix4x4[] _bindPoses;
		private Transform[] _smrBones;
		private Matrix4x4[] _boneMatrices;
		private int _blendShapeCount;
		private Vector3[][] _bsDeltaVertices;
		private Vector3[][] _bsDeltaNormals;
		private Vector3[] _blendedVertices;
		private Vector3[] _blendedNormals;
		private float[] _bsWeightsCache;
		private int _sourceMeshId;
		private ShapeDeformerRunner _runner;
		private List<Vector3> _bakedVertsList;
		private List<Vector3> _bakedNormalsList;
		private Vector3[] _cachedFinalDeltas;
		private bool _loggedDeltaMismatch;
		private bool _editMode;
		private Material _editMaterial;
		private Color[] _editColors;

		public Action OnDeformationApplied;
		public Action OnMeshReplaced;
	}
}
