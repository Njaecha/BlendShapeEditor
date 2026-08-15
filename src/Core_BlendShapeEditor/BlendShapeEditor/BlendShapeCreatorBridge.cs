using System.Collections.Generic;
using Studio;
using UnityEngine;
using CharacterController = BlendshapeCreator.CharacterController;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;

namespace BlendShapeEditor
{
	/// Integration with ShalltyB's BlendshapeCreator plugin.
	public static class BlendShapeCreatorBridge
	{
		// Studio: register a baked blendshape with an OCIItem's BSC data store
		public static void RegisterBlendShapeStudio(ObjectCtrlInfo oci, SkinnedMeshRenderer renderer, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals, float weight)
		{
			if (oci == null) return;
			BlendshapeCreator.BlendshapeCreator.BlendShape.BlendShapeDeltas deltas =
				new BlendshapeCreator.BlendshapeCreator.BlendShape.BlendShapeDeltas(deltaVerts, deltaNormals, null);
			if (BlendshapeCreator.BlendshapeCreator.BlendShape.RegisterNewBlendShape(oci, renderer, shapeName,
				    deltas,
				    out BlendshapeCreator.BlendshapeCreator.BlendShape shape, weight))
			{
				BSE.Logger.LogInfo(
					$"BlendShapeCreatorBridge: registered '{shapeName}' on studio item '{oci.treeNodeObject?.textName}'");
			}
			else
			{
				BSE.Logger.LogWarning("Could not register BlendShape.");
			}
		}

		// Maker: register a baked blendshape with a character's BSC controller
		public static void RegisterBlendShapeMaker(ChaControl chaCtrl, SkinnedMeshRenderer renderer, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals, float weight)
		{
			if (!chaCtrl) return;
			BlendshapeCreator.BlendshapeCreator.BlendShape.BlendShapeDeltas deltas = new BlendshapeCreator.BlendshapeCreator.BlendShape.BlendShapeDeltas(deltaVerts, deltaNormals, null);
			if (BlendshapeCreator.BlendshapeCreator.BlendShape.RegisterNewBlendShape(chaCtrl, renderer, shapeName, deltas,
				out BlendshapeCreator.BlendshapeCreator.BlendShape shape, weight))
			{
				BSE.Logger.LogInfo(
					$"BlendShapeCreatorBridge: registered '{shapeName}' on character '{chaCtrl.name}'");
				
			}
			else
			{
				BSE.Logger.LogWarning("Could not register BlendShape.");
			}
		}
	}
}
