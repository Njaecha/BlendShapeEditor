using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class BrushResult
	{
		public Vector3 HitPoint;
		public Vector3 HitNormal;
		public Dictionary<int, float> AffectedVertices;
	}
}
