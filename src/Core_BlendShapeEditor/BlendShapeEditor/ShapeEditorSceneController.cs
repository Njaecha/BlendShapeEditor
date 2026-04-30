using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace KKShapeEditor
{
	public class ShapeEditorSceneController : SceneCustomFunctionController
	{
		protected override void OnSceneSave()
		{
			var itemDict = new Dictionary<int, ItemSaveData>();
			var mapItemDict = new Dictionary<string, ItemSaveData>();

			Studio.Studio studio = Singleton<Studio.Studio>.Instance;
			if (!studio)
				return;

			foreach (KeyValuePair<int, ObjectCtrlInfo> pair in studio.dicObjectCtrl)
			{
				if (!(pair.Value is OCIItem ociItem) || ociItem.objectItem == null)
					continue;

				ItemShapeController controller = ociItem.objectItem.GetComponent<ItemShapeController>();
				if (!controller)
					continue;

				Dictionary<string, DeformData> allData = controller.GetAllDeformData();
				bool hasLayers = allData.Values.Any(data => data.HasLayers);

				var subLevels = new Dictionary<string, int>();
				var subFaces = new Dictionary<string, List<int[]>>();
				foreach (Renderer renderer in controller.GetAllRenderers())
				{
					int level = MeshHelper.GetSubdivisionLevel(renderer);
					if (level <= 0) continue;
					string path = ShapeEditorController.GetRelativePath(controller.RootTransform, renderer.transform);
					subLevels[path] = level;
					List<int[]> faces = MeshHelper.GetSubdivisionFaces(renderer);
					if (faces != null)
						subFaces[path] = faces;
				}

				if (!hasLayers && subLevels.Count == 0)
					continue;

				ItemSaveData saveData = new ItemSaveData
				{
					DeformDataMap = hasLayers ? allData : null,
					SubdividedMeshes = subLevels.Count > 0 ? subLevels : null,
					SubdividedFaces = subFaces.Count > 0 ? subFaces : null
				};

				if (ociItem.treeNodeObject && !ociItem.treeNodeObject.enableCopy)
					mapItemDict[GetHierarchyPath(ociItem.objectItem.transform)] = saveData;
				else
					itemDict[pair.Key] = saveData;
			}

			PluginData pluginData = new PluginData();
			if (itemDict.Count > 0)
				pluginData.data["items"] = ShapeSerializer.SerializeItemDict(itemDict);
			if (mapItemDict.Count > 0)
				pluginData.data["mapItems"] = ShapeSerializer.SerializeMapItemDict(mapItemDict);

			SetExtendedData(pluginData.data.Count > 0 ? pluginData : null);
		}

		protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
		{
			if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
			{
				foreach (ItemShapeController ctrl in FindObjectsOfType<ItemShapeController>())
					Destroy(ctrl);
				MeshHelper.PurgeDestroyed();
			}

			if (operation == SceneOperationKind.Clear)
				return;

			PluginData extendedData = GetExtendedData();
			if (extendedData == null)
				return;

			if (extendedData.data.TryGetValue("items", out object obj))
			{
				if (obj is byte[] bytes)
				{
					Dictionary<int, ItemSaveData> dict = ShapeSerializer.DeserializeItemDict(bytes);
					if (dict != null)
					{
						foreach (KeyValuePair<int, ItemSaveData> pair in dict)
						{
							if (loadedItems.TryGetValue(pair.Key, out ObjectCtrlInfo info))
								RestoreItem(info as OCIItem, pair.Value);
						}
					}
				}
			}

			object obj2;
			if (!extendedData.data.TryGetValue("mapItems", out obj2)) return;
			if (!(obj2 is byte[] bytes2)) return;
			Dictionary<string, ItemSaveData> dict2 = ShapeSerializer.DeserializeMapItemDict(bytes2);
			if (dict2 != null && dict2.Count > 0)
				StartCoroutine(RestoreMapItemsDelayed(dict2));
		}

		private static void RestoreItem(OCIItem ociItem, ItemSaveData saveData)
		{
			if (ociItem == null || !ociItem.objectItem || saveData == null)
				return;

			ItemShapeController controller = ociItem.objectItem.GetComponent<ItemShapeController>();
			if (!controller)
				controller = ociItem.objectItem.AddComponent<ItemShapeController>();

			if (saveData.SubdividedMeshes != null && saveData.SubdividedMeshes.Count > 0)
			{
				Dictionary<string, List<int[]>> faceMap = saveData.SubdividedFaces ?? new Dictionary<string, List<int[]>>();
				var processedMeshes = new HashSet<int>();

				foreach (KeyValuePair<string, int> pair in saveData.SubdividedMeshes)
				{
					Renderer renderer = ShapeEditorController.FindRendererByPath(controller.RootTransform, pair.Key);
					if (!renderer)
						continue;

					MeshHelper.CloneMeshIfShared(renderer);
					Mesh mesh = MeshHelper.GetMesh(renderer);
					if (!mesh)
						continue;

					int meshId = mesh.GetInstanceID();
					if (processedMeshes.Contains(meshId))
						continue;

					faceMap.TryGetValue(pair.Key, out List<int[]> faces);
					if (faces != null && faces.Count > 0)
						MeshHelper.SubdivideReplay(mesh, faces);
					processedMeshes.Add(meshId);
				}
			}

			controller.LoadData(saveData.DeformDataMap);
		}

		private IEnumerator RestoreMapItemsDelayed(Dictionary<string, ItemSaveData> mapItemData)
		{
			yield return null;
			foreach (ItemShapeController controller in FindObjectsOfType<ItemShapeController>())
			{
				string path = GetHierarchyPath(controller.transform);
				if (mapItemData.TryGetValue(path, out ItemSaveData saveData))
					controller.LoadData(saveData.DeformDataMap);
			}
		}

		private static string GetHierarchyPath(Transform t)
		{
			var parts = new List<string>();
			while (t)
			{
				parts.Add(t.name);
				t = t.parent;
			}
			parts.Reverse();
			return string.Join("/", parts.ToArray());
		}
	}
}
