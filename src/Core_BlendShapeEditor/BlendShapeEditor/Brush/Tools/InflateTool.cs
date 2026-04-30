using System.Collections.Generic;
using UnityEngine;

namespace KKShapeEditor
{
	public class InflateTool : IDeformTool
	{
		public float Amount { get; set; }

		public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
		{
			if (layer == null || brushResult == null || normals == null)
				return;

			Vector3[] deltas = layer.Deltas;
			float amount = Amount;
			if (Mathf.Approximately(amount, 0f))
				return;

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices)
			{
				int vertexIndex = pair.Key;
				float falloff = pair.Value;
				if (vertexIndex >= 0 && vertexIndex < deltas.Length && vertexIndex < normals.Length)
					deltas[vertexIndex] += normals[vertexIndex].normalized * (amount * falloff);
			}
			layer.Dirty = true;
		}
	}
}
