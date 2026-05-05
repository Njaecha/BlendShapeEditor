using System.Collections.Generic;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace BlendShapeEditor
{
	public class BlendShapeEditorSceneController : SceneCustomFunctionController
	{
		protected override void OnSceneSave() { }

		protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
		{
			if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
			{
				foreach (BlendShapeEditorItemController ctrl in FindObjectsOfType<BlendShapeEditorItemController>())
					Destroy(ctrl);
				MeshHelper.PurgeDestroyed();
			}
		}
	}
}
