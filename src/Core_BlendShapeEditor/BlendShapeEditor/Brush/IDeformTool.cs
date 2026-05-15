using UnityEngine;

namespace BlendShapeEditor
{
	public interface IDeformTool
	{
		float Direction { get; set; }
		void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera);
	}
}
