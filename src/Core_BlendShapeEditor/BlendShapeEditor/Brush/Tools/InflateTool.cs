using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class InflateTool : IDeformTool
	{
		public float Direction { get; set; }

		public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
		{
			if (layer == null || brushResult == null || normals == null)
				return;

			Vector3[] deltas = layer.Deltas;
			float direction = Direction;
			if (Mathf.Approximately(direction, 0f))
				return;

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices)
			{
				int vertexIndex = pair.Key;
				float falloff = pair.Value;
				if (vertexIndex >= 0 && vertexIndex < deltas.Length && vertexIndex < normals.Length)
					deltas[vertexIndex] += normals[vertexIndex].normalized * (direction * falloff * 0.003f);
			}
			layer.Dirty = true;
		}
	}
}
