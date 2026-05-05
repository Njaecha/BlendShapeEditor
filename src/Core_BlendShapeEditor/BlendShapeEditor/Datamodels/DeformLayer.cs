using System.Linq;
using UnityEngine;

namespace BlendShapeEditor
{
	public class DeformLayer
	{
		public string Name { get; set; }
		public Vector3[] Deltas { get; set; }
		public float Weight { get; set; }
		public bool Dirty { get; set; }

		public DeformLayer(string name, int vertexCount)
		{
			Name = name;
			Deltas = new Vector3[vertexCount];
			Weight = 1f;
			Dirty = false;
		}

		public void Reset(int newVertexCount)
		{
			Deltas = new Vector3[newVertexCount];
			Dirty = true;
		}

		public bool IsEmpty()
		{
			return Deltas.All(t => !(t.sqrMagnitude > 0f));
		}
	}
}
