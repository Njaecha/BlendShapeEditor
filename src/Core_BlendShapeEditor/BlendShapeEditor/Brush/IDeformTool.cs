using UnityEngine;

namespace KKShapeEditor
{
	public interface IDeformTool
	{
		void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera);
	}
}
