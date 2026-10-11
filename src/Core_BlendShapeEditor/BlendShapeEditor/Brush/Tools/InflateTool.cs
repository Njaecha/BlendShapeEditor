using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class InflateTool : IDeformTool
	{
		public float Direction { get; set; }

		public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera, Dictionary<int, Vector3> displacements)
		{
			if (surface == null || brushResult == null || !surface.HasNormals)
				return;

			float direction = Direction;
			if (Mathf.Approximately(direction, 0f))
				return;

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices)
			{
				int vertexIndex = pair.Key;
				float falloff = pair.Value;
				if (surface.IsValid(vertexIndex))
					displacements.AddDisplacement(vertexIndex, surface.GetNormal(vertexIndex) * (direction * falloff * 0.003f));
			}
		}
	}
}
