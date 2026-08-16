using System.Collections.Generic;
using Studio;
using UnityEngine;
using CharacterController = BlendshapeCreator.CharacterController;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;
using BSC = BlendshapeCreator.BlendshapeCreator;

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
			BSC.BlendShape.BlendShapeDeltas deltas =
				new BSC.BlendShape.BlendShapeDeltas(deltaVerts, deltaNormals, null);
			if (BSC.BlendShape.RegisterNewBlendShape(oci, renderer, shapeName,
				    deltas,
				    out BSC.BlendShape shape, weight))
			{
				BSE.Logger.LogInfo(
					$"BlendShapeCreatorBridge: registered '{shapeName}' on studio item '{oci.treeNodeObject?.textName}'");
			}
			else
			{
				BSE.Logger.LogError("Could not register BlendShape.");
			}
		}

		// Maker: register a baked blendshape with a character's BSC controller
		public static void RegisterBlendShapeMaker(ChaControl chaCtrl, SkinnedMeshRenderer renderer, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals, float weight)
		{
			if (!chaCtrl) return;
			BSC.BlendShape.BlendShapeDeltas deltas = new BSC.BlendShape.BlendShapeDeltas(deltaVerts, deltaNormals, null);
			if (BSC.BlendShape.RegisterNewBlendShape(chaCtrl, renderer, shapeName, deltas,
				out BSC.BlendShape shape, weight))
			{
				BSE.Logger.LogInfo(
					$"BlendShapeCreatorBridge: registered '{shapeName}' on character '{chaCtrl.name}'");
				
			}
			else
			{
				BSE.Logger.LogError("Could not register BlendShape.");
			}
		}

		public static void UpdateBlendShapeMaker(ChaControl chaCtrl, SkinnedMeshRenderer renderer, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals)
		{
			if (!chaCtrl) return;
			
			BSC.BlendShape.BlendShapeDeltas deltas = new BSC.BlendShape.BlendShapeDeltas(deltaVerts, deltaNormals, null);
			if (BSC.BlendShape.UpdateBlendShape(chaCtrl, renderer, shapeName, deltas, out BSC.BlendShape shape))
			{
				BSE.Logger.LogInfo("BlendShape successfully updated.");
			}
			else
			{
				BSE.Logger.LogError("Could not update BlendShape.");
			}
		}
		
		public static void UpdateBlendShapeStudio(ObjectCtrlInfo oci, SkinnedMeshRenderer renderer, string shapeName,
			Vector3[] deltaVerts, Vector3[] deltaNormals)
		{
			if (oci == null) return;
			
			BSC.BlendShape.BlendShapeDeltas deltas = new BSC.BlendShape.BlendShapeDeltas(deltaVerts, deltaNormals, null);
			if (BSC.BlendShape.UpdateBlendShape(oci, renderer, shapeName, deltas, out BSC.BlendShape shape))
			{
				BSE.Logger.LogInfo("BlendShape successfully updated.");
			}
			else
			{
				BSE.Logger.LogError("Could not update BlendShape.");
			}
		}
	}
}
