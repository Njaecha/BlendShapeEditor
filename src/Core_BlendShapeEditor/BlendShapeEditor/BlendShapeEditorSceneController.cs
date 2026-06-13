using System.Collections.Generic;
using KKAPI.Studio;
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

		protected override void OnObjectDeleted(ObjectCtrlInfo objectCtrlInfo)
		{
			if (!StudioUI.Window.IsEditMode) return;
			string objectPath;
			switch (objectCtrlInfo)
			{
				case OCIChar cha:
					objectPath = cha.charInfo.gameObject.GetFullPath();
					break;
				case OCIItem item:
					objectPath = item.objectItem.GetFullPath();
					break;
				default:
					return; // BSE only works on Characters and Items
			}
			if (StudioUI.Overlay.GetCurrentRenderer().GetFullPath().StartsWith(objectPath)) // simple parent check
				StudioUI.Overlay.DoExitEditMode();
			base.OnObjectDeleted(objectCtrlInfo);
		}
	}
}
