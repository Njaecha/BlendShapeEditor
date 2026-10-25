using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class MoveTool : IDeformTool
	{
		public float Direction { get; set; }
		public Vector2 MouseDelta { get; set; }
		public float DragDelta { get; set; }
		public bool UseViewPlane { get; set; }
		/// When set, the move direction is reflected through this plane (the mirrored half of a stroke).
		public MirrorPlane Mirror { get; set; }

		public void Apply(DeformSurface surface, BrushResult brushResult, Camera camera, Dictionary<int, Vector3> displacements)
		{
			if (surface == null || brushResult == null)
				return;

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

			if (Mirror != null)
				moveDir = Mirror.ReflectVector(moveDir);

			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices)
			{
				int vertexIndex = pair.Key;
				float falloff = pair.Value;
				if (!surface.IsValid(vertexIndex)) continue;
				displacements.AddDisplacement(vertexIndex, moveDir * falloff);
			}
		}
	}
}
