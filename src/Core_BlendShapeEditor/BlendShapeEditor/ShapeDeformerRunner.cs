using UnityEngine;

namespace KKShapeEditor
{
	[DefaultExecutionOrder(32000)]
	internal class ShapeDeformerRunner : MonoBehaviour
	{
		internal ShapeDeformer Deformer;

		private void LateUpdate()
		{
			Deformer?.DoDeformation();
		}
	}
}
