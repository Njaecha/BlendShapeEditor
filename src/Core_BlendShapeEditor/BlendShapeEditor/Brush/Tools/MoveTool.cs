using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class MoveTool : IDeformTool
	{
		public Vector2 MouseDelta { get; set; }
		public float DragDelta { get; set; }
		public bool UseViewPlane { get; set; }
		public Transform RendererTransform { get; set; }
		public ShapeDeformer Deformer { get; set; }

		public int MirrorAxis { get; set; } = -1;

		public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
		{
			if (layer == null || brushResult == null)
				return;

			Vector3[] deltas = layer.Deltas;
			Vector3 moveDir;

			if (UseViewPlane && camera)
			{
				float x = MouseDelta.x;
				float y = MouseDelta.y;
				if (Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f))
					return;
				moveDir = camera.transform.right * x + camera.transform.up * y;
			}
			else
			{
				float dragDelta = DragDelta;
				if (Mathf.Approximately(dragDelta, 0f))
					return;
				moveDir = brushResult.HitNormal.normalized * dragDelta;
			}

			if (MirrorAxis >= 0 && MirrorAxis <= 2 && RendererTransform)
			{
				Vector3 localDir = RendererTransform.InverseTransformVector(moveDir);
				switch (MirrorAxis)
				{
					case 0:
						localDir.x = -localDir.x;
						break;
					case 1:
						localDir.y = -localDir.y;
						break;
					default:
						localDir.z = -localDir.z;
						break;
				}
				moveDir = RendererTransform.TransformVector(localDir);
			}

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices)
			{
				int vertexIndex = pair.Key;
				float falloff = pair.Value;
				if (vertexIndex < 0 || vertexIndex >= deltas.Length) continue;
				Vector3 bindDelta;
				if (Deformer)
					Deformer.WorldDeltaToBindDelta(vertexIndex, moveDir, out bindDelta);
				else
					bindDelta = (RendererTransform) ? RendererTransform.InverseTransformVector(moveDir) : moveDir;
				deltas[vertexIndex] += bindDelta * falloff;
			}
			layer.Dirty = true;
		}
	}
}
