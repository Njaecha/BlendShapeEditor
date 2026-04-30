using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using StrayTech;
using UnityEngine;

namespace KKShapeEditor
{
	public class ShapeEditorController : CharaCustomFunctionController
	{
		private Dictionary<string, DeformData> _deformDataMap = new Dictionary<string, DeformData>();
		private Dictionary<string, int> _pendingSubLevels;
		private Dictionary<string, List<int[]>> _pendingSubFaces;
		private int _lastCoordinateType = -1;

		public event Action OnDataChanged;

		public Transform RootTransform
		{
			get
			{
				ChaControl ctrl = ChaControl;
				return ctrl ? ctrl.transform : null;
			}
		}

		public DeformData GetOrCreateDeformData(Renderer renderer)
		{
			if (!renderer || !RootTransform)
				return null;
			string path = GetRelativePath(RootTransform, renderer.transform);
			if (_deformDataMap.TryGetValue(path, out DeformData data)) return data;
			data = new DeformData(path);
			_deformDataMap[path] = data;
			return data;
		}

		public DeformData GetDeformData(Renderer renderer)
		{
			if (!renderer || !RootTransform)
				return null;
			string path = GetRelativePath(RootTransform, renderer.transform);
			_deformDataMap.TryGetValue(path, out DeformData data);
			return data;
		}

		public Dictionary<string, DeformData> GetAllDeformData() => _deformDataMap;

		public SkinnedMeshRenderer GetBodySmr()
		{
			if (!ChaControl || !ChaControl.objBody)
				return null;
			SkinnedMeshRenderer smr = ChaControl.objBody.GetComponentInChildren<SkinnedMeshRenderer>();
			if (!smr || !smr.sharedMesh || !smr.sharedMesh.isReadable)
				return null;
			return smr;
		}

		public List<Renderer> GetAllRenderers() => CollectCharacterRenderers(ChaControl, RootTransform);

		protected override void Update()
		{
			if (ChaControl)
			{
				int coordinateType = ChaControl.fileStatus.coordinateType;
				if (_lastCoordinateType >= 0 && coordinateType != _lastCoordinateType)
				{
					_lastCoordinateType = coordinateType;
					OnCoordinateTypeChanged();
				}
				else
				{
					_lastCoordinateType = coordinateType;
				}
			}
			base.Update();
		}

		private void OnCoordinateTypeChanged()
		{
			if (_deformDataMap.Count == 0)
				return;
			if (RootTransform)
				CleanupDeformers();
			ShapeEditorPlugin.Instance.StartCoroutine(DelayedAttachCoroutine());
		}

		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{
			if (maintainState)
				return;

			_deformDataMap.Clear();
			_pendingSubLevels = null;
			_pendingSubFaces = null;

			PluginData extendedData = GetExtendedData();
			if (extendedData != null)
			{
				if (extendedData.data.TryGetValue("layers", out object obj))
				{
					if (obj is byte[] bytes)
					{
						_deformDataMap = ShapeSerializer.DeserializeAllLayers(bytes) ?? new Dictionary<string, DeformData>();
					}
				}

				if (extendedData.data.TryGetValue("subdivision", out object obj2))
				{
					if (obj2 is byte[] bytes2)
						ShapeSerializer.DeserializeSubdivisionInfo(bytes2, out _pendingSubLevels, out _pendingSubFaces);
				}
			}

			if (RootTransform)
				CleanupDeformers();

			if (_pendingSubLevels != null && _pendingSubLevels.Count > 0)
				ShapeEditorPlugin.Instance.StartCoroutine(DelayedRestoreCoroutine());
			else
				ShapeEditorPlugin.Instance.StartCoroutine(DelayedAttachCoroutine());

			OnDataChanged?.Invoke();
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode)
		{
			PluginData pluginData = new PluginData();

			var layersToSave = new Dictionary<string, DeformData>();
			foreach (KeyValuePair<string, DeformData> pair in _deformDataMap.Where(pair => pair.Value.HasLayers))
			{
				layersToSave[pair.Key] = pair.Value;
			}
			if (layersToSave.Count > 0)
				pluginData.data["layers"] = ShapeSerializer.SerializeAllLayers(layersToSave);

			var subLevels = new Dictionary<string, int>();
			var subFaces = new Dictionary<string, List<int[]>>();
			foreach (Renderer renderer in GetAllRenderers())
			{
				int level = MeshHelper.GetSubdivisionLevel(renderer);
				if (level <= 0) continue;
				string path = GetRelativePath(RootTransform, renderer.transform);
				subLevels[path] = level;
				List<int[]> faces = MeshHelper.GetSubdivisionFaces(renderer);
				if (faces != null)
					subFaces[path] = faces;
			}
			if (subLevels.Count > 0)
				pluginData.data["subdivision"] = ShapeSerializer.SerializeSubdivisionInfo(subLevels, subFaces);

			SetExtendedData(pluginData.data.Count > 0 ? pluginData : null);
		}

		protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
		{
			if (RootTransform)
				CleanupDeformers();
			ShapeEditorPlugin.Instance.StartCoroutine(DelayedAttachCoroutine());
		}

		private IEnumerator DelayedRestoreCoroutine()
		{
			yield return null;
			foreach (KeyValuePair<string, int> pair in _pendingSubLevels)
			{
				string path = pair.Key;
				Renderer renderer = FindRendererByPath(RootTransform, path);
				if (!renderer)
					continue;
				if (_pendingSubFaces != null && _pendingSubFaces.TryGetValue(path, out List<int[]> faces))
					MeshHelper.CloneAndReplaySubdivision(renderer, faces);
			}
			_pendingSubLevels = null;
			_pendingSubFaces = null;
			AttachDeformers();
		}

		private IEnumerator DelayedAttachCoroutine()
		{
			yield return null;
			AttachDeformers();
		}

		private void AttachDeformers()
		{
			if (!RootTransform)
				return;

			foreach (KeyValuePair<string, DeformData> pair in _deformDataMap)
			{
				if (!pair.Value.HasLayers)
					continue;
				Renderer renderer = FindRendererByPath(RootTransform, pair.Key);
				if (!renderer)
					continue;

				ShapeDeformer deformer = renderer.GetComponent<ShapeDeformer>();
				if (!deformer)
					deformer = renderer.gameObject.AddComponent<ShapeDeformer>();

				deformer.StudioMode = false;
				deformer.DeformData = pair.Value;

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

				if (pair.Value.WeightRemapped && smr)
					RecomputeRemappedWeights(deformer, smr);
			}
		}

		private void RecomputeRemappedWeights(ShapeDeformer deformer, SkinnedMeshRenderer clothSmr)
		{
			SkinnedMeshRenderer bodySmr = GetBodySmr();
			if (!bodySmr || !clothSmr.sharedMesh)
				return;

			Mesh bodyMesh = bodySmr.sharedMesh;
			Mesh clothMesh = clothSmr.sharedMesh;
			Vector3[] bindVerts = deformer.BindVertices ?? clothMesh.vertices;
			Vector3[] deltas = deformer.DeformData?.ComputeFinalDelta();
			if (deltas == null || deltas.Length != bindVerts.Length)
				return;

			BoneWeight[] remapped = WeightRemapper.ComputeRemappedWeights(
				bindVerts, deltas, clothSmr.bones,
				bodyMesh.vertices, bodyMesh.boneWeights, deformer.OriginalBoneWeights,
				bodySmr.bones, bodyMesh.triangles);

			if (remapped != null)
				deformer.RemappedBoneWeights = remapped;
		}

		private void CleanupDeformers()
		{
			if (!RootTransform)
				return;
			ShapeDeformer[] deformers = RootTransform.GetComponentsInChildren<ShapeDeformer>(true);
			foreach (ShapeDeformer t in deformers)
				DestroyImmediate(t);

			MeshHelper.PurgeDestroyed();
		}

		public static List<Renderer> CollectCharacterRenderers(ChaControl chaCtrl, Transform root)
		{
			var result = new List<Renderer>();
			if (!chaCtrl) return result;

			var seen = new HashSet<Renderer>();

			if (chaCtrl.objClothes != null)
				foreach (GameObject go in chaCtrl.objClothes) Collect(go);
			if (chaCtrl.objAccessory != null)
				foreach (GameObject go in chaCtrl.objAccessory) Collect(go);
			Collect(chaCtrl.objBody);
			Collect(chaCtrl.objHead);
			return result;

			void Collect(GameObject go)
			{
				if (!go) return;
				result.AddRange((IEnumerable<Renderer>)go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(seen.Add));
				result.AddRange((IEnumerable<Renderer>)go.GetComponentsInChildren<MeshRenderer>(true).Where(mr => !mr.GetComponent<SkinnedMeshRenderer>() && mr.name != "_kkse_deform_display").Where(seen.Add));
			}
		}

		public static string GetRelativePath(Transform root, Transform target)
		{
			if (!root || !target)
				return (target) ? target.name : "";
			if (target == root)
				return "";

			var parts = new List<string>();
			Transform t = target;
			while (t && t != root)
			{
				parts.Add(t.name);
				t = t.parent;
			}
			if (t != root)
				return target.name;

			parts.Reverse();
			return string.Join("/", parts.ToArray());
		}

		public static Renderer FindRendererByPath(Transform root, string path)
		{
			if (!root)
				return null;
			Transform t = string.IsNullOrEmpty(path) ? root : root.Find(path);
			if (!t)
				return null;
			SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
			if (smr)
				return smr;
			return t.GetComponent<MeshRenderer>();
		}
	}
}
