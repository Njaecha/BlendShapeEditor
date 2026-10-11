using System.Collections.Generic;
using System.Linq;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlendShapeEditor
{
	public class BlendShapeEditorCharaController : CharaCustomFunctionController
	{
		private readonly Dictionary<string, DeformData> _sessionData = new Dictionary<string, DeformData>();

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
			string path = RootTransform.GetPathToChild(renderer.transform);
			if (_sessionData.TryGetValue(path, out DeformData data)) return data;
			data = new DeformData(path);
			_sessionData[path] = data;
			return data;
		}

		/// Layer stack shared by a set of renderers edited together. Sorts `renderers` in place by path:
		/// the shared deltas are laid out part after part in that order, so callers must use it as-is.
		public DeformData GetOrCreateDeformData(List<Renderer> renderers)
		{
			if (renderers == null || renderers.Count == 0 || renderers.Any(r => !r) || !RootTransform)
				return null;
			if (renderers.Count == 1)
				return GetOrCreateDeformData(renderers[0]);
			string key = DeformDataKeys.SortAndBuildKey(RootTransform, renderers);
			if (_sessionData.TryGetValue(key, out DeformData data)) return data;
			data = new DeformData(key);
			_sessionData[key] = data;
			return data;
		}

		public DeformData GetDeformData(Renderer renderer)
		{
			if (!renderer || !RootTransform)
				return null;
			string path = RootTransform.GetPathToChild(renderer.transform);
			_sessionData.TryGetValue(path, out DeformData data);
			return data;
		}

		public List<Renderer> GetAllRenderers() => CollectCharacterRenderers(ChaControl, RootTransform);

		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{
			if (!maintainState)
				_sessionData.Clear();
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode) { }

		public static string GetRelativePath(Transform root, Transform target)
		{
			if (!root || !target)
				return target ? target.name : "";
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

		public List<Renderer> CollectCharacterRenderers(ChaControl chaCtrl, Transform root)
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
				result.AddRange(go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
					.Where(renderer => renderer.TryGetOwningController(out Object controller) && controller is BlendShapeEditorCharaController ctrl && ctrl == this)
					.Where(seen.Add).Cast<Renderer>());
				result.AddRange(go.GetComponentsInChildren<MeshRenderer>(true)
					.Where(renderer => renderer.TryGetOwningController(out Object controller) && controller is BlendShapeEditorCharaController ctrl && ctrl == this)
					.Where(mr => !mr.GetComponent<SkinnedMeshRenderer>() && mr.name != "_kkse_deform_display" && seen.Add(mr)).Cast<Renderer>());
			}
		}
	}
}
