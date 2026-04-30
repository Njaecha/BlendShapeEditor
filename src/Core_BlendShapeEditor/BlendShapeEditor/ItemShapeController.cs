using System;
using System.Collections.Generic;
using UnityEngine;

namespace KKShapeEditor
{
	public class ItemShapeController : MonoBehaviour
	{
		private Dictionary<string, DeformData> _deformDataMap = new Dictionary<string, DeformData>();

		public event Action OnDataChanged;

		public Transform RootTransform => transform;

		public DeformData GetOrCreateDeformData(Renderer renderer)
		{
			if (!renderer)
				return null;
			string path = ShapeEditorController.GetRelativePath(RootTransform, renderer.transform);
			if (_deformDataMap.TryGetValue(path, out DeformData data)) return data;
			data = new DeformData(path);
			_deformDataMap[path] = data;
			return data;
		}

		public DeformData GetDeformData(Renderer renderer)
		{
			if (!renderer)
				return null;
			string path = ShapeEditorController.GetRelativePath(RootTransform, renderer.transform);
			_deformDataMap.TryGetValue(path, out DeformData data);
			return data;
		}

		public Dictionary<string, DeformData> GetAllDeformData() => _deformDataMap;

		public List<Renderer> GetAllRenderers() => CollectItemRenderers(transform);

		public void LoadData(Dictionary<string, DeformData> deformData)
		{
			_deformDataMap = deformData ?? new Dictionary<string, DeformData>();
			AttachDeformers();
			OnDataChanged?.Invoke();
		}

		private void AttachDeformers()
		{
			foreach (KeyValuePair<string, DeformData> pair in _deformDataMap)
			{
				if (!pair.Value.HasLayers)
					continue;
				Renderer renderer = ShapeEditorController.FindRendererByPath(RootTransform, pair.Key);
				if (!renderer)
					continue;

				ShapeDeformer deformer = renderer.GetComponent<ShapeDeformer>();
				if (!deformer)
					deformer = renderer.gameObject.AddComponent<ShapeDeformer>();

				deformer.StudioMode = true;
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
			}
		}

		public static List<Renderer> CollectItemRenderers(Transform root)
		{
			var result = new List<Renderer>();
			if (!root)
				return result;

			foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				if (smr.sharedMesh && !smr.sharedMesh.isReadable)
					ShapeEditorPlugin.Logger.LogInfo("CollectItemRenderers: skipping non-readable SMR '" + smr.name + "'");
				else
					result.Add(smr);
			}

			foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
			{
				if (mr.GetComponent<SkinnedMeshRenderer>() || mr.name == "_kkse_deform_display")
					continue;
				MeshFilter mf = mr.GetComponent<MeshFilter>();
				if (mf && mf.sharedMesh && !mf.sharedMesh.isReadable)
					ShapeEditorPlugin.Logger.LogInfo("CollectItemRenderers: skipping non-readable mesh '" + mr.name + "'");
				else
					result.Add(mr);
			}

			return result;
		}

		private void OnDestroy()
		{
			ShapeDeformer[] deformers = GetComponentsInChildren<ShapeDeformer>(true);
			foreach (ShapeDeformer t in deformers)
				DestroyImmediate(t);
		}
	}
}
