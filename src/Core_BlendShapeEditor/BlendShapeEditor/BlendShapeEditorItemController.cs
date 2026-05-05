using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace BlendShapeEditor
{
	public class BlendShapeEditorItemController : MonoBehaviour
	{
		private readonly Dictionary<string, DeformData> _deformDataMap = new Dictionary<string, DeformData>();
		public OCIItem ItemCtrlInfo { get; internal set; }

		public Transform RootTransform => transform;

		public DeformData GetOrCreateDeformData(Renderer renderer)
		{
			if (!renderer)
				return null;
			string path = RootTransform.GetPathToChild(renderer.transform);
			if (_deformDataMap.TryGetValue(path, out DeformData data)) return data;
			data = new DeformData(path);
			_deformDataMap[path] = data;
			return data;
		}

		public DeformData GetDeformData(Renderer renderer)
		{
			if (!renderer)
				return null;
			string path = RootTransform.GetPathToChild(renderer.transform);
			_deformDataMap.TryGetValue(path, out DeformData data);
			return data;
		}

		public List<Renderer> GetAllRenderers() => CollectItemRenderers(transform);

		public List<Renderer> CollectItemRenderers(Transform root)
		{
			var result = new List<Renderer>();
			if (!root)
				return result;

			foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				if (smr.sharedMesh && !smr.sharedMesh.isReadable)
					BlendShapeEditorPlugin.Logger.LogDebug($"CollectItemRenderers: skipping non-readable SMR '{smr.name}'");
				else
				{
					smr.TryGetOwningController(out Object controller);
					// skip skinned mesh renderers that do not actually belong to this renderer.
					if (!(controller is BlendShapeEditorItemController foundCtrl && foundCtrl == this))
					{
						BlendShapeEditorPlugin.Logger.LogDebug($"CollectItemRenderers: SMR '{smr.name}' because it does not belong to '{gameObject.name}'");
						continue;
					}
					result.Add(smr);
				}
			}

			foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
			{
				if (mr.GetComponent<SkinnedMeshRenderer>() || mr.name == "_kkse_deform_display")
					continue;
				MeshFilter mf = mr.GetComponent<MeshFilter>();
				if (mf && mf.sharedMesh && !mf.sharedMesh.isReadable)
					BlendShapeEditorPlugin.Logger.LogDebug($"CollectItemRenderers: skipping non-readable mesh '{mr.name}'");
				else
				{
					mr.TryGetOwningController(out Object controller);
					// skip mesh renderers that do not actually belong to this renderer.
					if (!(controller is BlendShapeEditorItemController foundCtrl && foundCtrl == this))
					{
						BlendShapeEditorPlugin.Logger.LogDebug($"CollectItemRenderers: MR '{mr.name}' because it does not belong to '{gameObject.name}'");
						continue;
					}
					result.Add(mr);
				}
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
