using System.Collections.Generic;
using UnityEngine;

namespace KKShapeEditor
{
	public class BrushResult
	{
		public Vector3 HitPoint;
		public Vector3 HitNormal;
		public Dictionary<int, float> AffectedVertices;
	}
}
