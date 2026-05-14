using System.Collections.Generic;
using Studio;
using UnityEngine;
using CharacterController = BlendshapeCreator.CharacterController;

namespace BlendShapeEditor
{
	/// Integration with ShalltyB's BlendshapeCreator plugin.
	public static class BlendShapeCreatorBridge
	{
		// Studio: register a baked blendshape with an OCIItem's BSC data store
		public static void RegisterBlendShapeStudio(ObjectCtrlInfo oci, string rendererPath, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals)
		{
			if (oci == null) return;
			try
			{
				var deltaVertsStr = BlendshapeCreator.BlendshapeCreator.Vector3Array.ToString(deltaVerts);
				string deltaNormalsStr = deltaNormals != null
					? BlendshapeCreator.BlendshapeCreator.Vector3Array.ToString(deltaNormals)
					: null;

				BlendshapeCreator.BlendshapeCreator.BlendShape shape = new BlendshapeCreator.BlendshapeCreator.BlendShape(
					rendererPath, shapeName, deltaVertsStr, deltaNormalsStr, null);

				Dictionary<ObjectCtrlInfo, BlendshapeCreator.BlendshapeCreator.OCIBlendShapeData> dataStore = BlendshapeCreator.BlendshapeCreator.ociBlendShapesData;
				if (dataStore.TryGetValue(oci, out BlendshapeCreator.BlendshapeCreator.OCIBlendShapeData existing))
				{
					existing.blendShapes.Add(shape);
				}
				else
				{
					BlendshapeCreator.BlendshapeCreator.OCIBlendShapeData newData = new BlendshapeCreator.BlendshapeCreator.OCIBlendShapeData();
					newData.blendShapes.Add(shape);
					dataStore[oci] = newData;
				}
				BlendShapeEditorPlugin.Logger.LogInfo(
					$"BlendShapeCreatorBridge: registered '{shapeName}' on studio item '{oci.treeNodeObject?.textName}'");
			}
			catch (System.Exception ex)
			{
				BlendShapeEditorPlugin.Logger.LogWarning("BlendShapeCreatorBridge.RegisterBlendShapeStudio error: " + ex.Message);
			}
		}

		// Maker: register a baked blendshape with a character's BSC controller
		public static void RegisterBlendShapeMaker(ChaControl chaCtrl, string rendererPath, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals)
		{
			if (!chaCtrl) return;
			try
			{
				CharacterController controller = chaCtrl.GetComponent<CharacterController>();
				if (!controller)
				{
					BlendShapeEditorPlugin.Logger.LogWarning(
						"BlendShapeCreatorBridge: BlendshapeCreator CharacterController not found on character");
					return;
				}

				var deltaVertsStr = BlendshapeCreator.BlendshapeCreator.Vector3Array.ToString(deltaVerts);
				string deltaNormalsStr = deltaNormals != null
					? BlendshapeCreator.BlendshapeCreator.Vector3Array.ToString(deltaNormals)
					: null;

				BlendshapeCreator.BlendshapeCreator.BlendShape shape = new BlendshapeCreator.BlendshapeCreator.BlendShape(
					rendererPath, shapeName, deltaVertsStr, deltaNormalsStr, null);

				controller.CharaBlendShapesData.blendShapes.Add(shape);
				BlendShapeEditorPlugin.Logger.LogInfo(
					$"BlendShapeCreatorBridge: registered '{shapeName}' on character '{chaCtrl.name}'");
			}
			catch (System.Exception ex)
			{
				BlendShapeEditorPlugin.Logger.LogWarning("BlendShapeCreatorBridge.RegisterBlendShapeMaker error: " + ex.Message);
			}
		}
	}
}
