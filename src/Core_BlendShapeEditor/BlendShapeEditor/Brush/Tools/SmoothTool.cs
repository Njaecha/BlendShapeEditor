using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KKShapeEditor
{
	public class SmoothTool : IDeformTool
	{
		public List<int>[] Adjacency { get; private set; }

		public void BuildAdjacency(int[] triangles, int vertexCount, Vector3[] vertices = null)
		{
			Adjacency = new List<int>[vertexCount];
			for (var i = 0; i < vertexCount; i++)
				Adjacency[i] = new List<int>(6);

			for (var j = 0; j < triangles.Length; j += 3)
			{
				int v0 = triangles[j];
				int v1 = triangles[j + 1];
				int v2 = triangles[j + 2];
				AddEdge(v0, v1);
				AddEdge(v1, v2);
				AddEdge(v2, v0);
			}

			if (vertices != null && vertices.Length == vertexCount)
				LinkColocatedVertices(vertices);
		}

		private void LinkColocatedVertices(Vector3[] vertices)
		{
			var posMap = new Dictionary<long, List<int>>();
			for (var i = 0; i < vertices.Length; i++)
			{
				long key = QuantizePosition(vertices[i]);
				if (!posMap.TryGetValue(key, out List<int> group))
				{
					group = new List<int>(2);
					posMap[key] = group;
				}
				group.Add(i);
			}

			foreach (List<int> group in posMap.Select(pair => pair.Value).Where(group => group.Count >= 2))
			{
				for (var j = 0; j < group.Count; j++)
				for (int k = j + 1; k < group.Count; k++)
					AddEdge(group[j], group[k]);
			}
		}

		private static long QuantizePosition(Vector3 v)
		{
			int x = Mathf.RoundToInt(v.x * 10000f);
			int y = Mathf.RoundToInt(v.y * 10000f);
			int z = Mathf.RoundToInt(v.z * 10000f);
			return (long)(x & 2097151) << 42 | (long)(y & 2097151) << 21 | (long)(z & 2097151);
		}

		private void AddEdge(int a, int b)
		{
			if (!Adjacency[a].Contains(b)) Adjacency[a].Add(b);
			if (!Adjacency[b].Contains(a)) Adjacency[b].Add(a);
		}

		public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
		{
			if (layer == null || brushResult == null || Adjacency == null)
				return;

			Vector3[] deltas = layer.Deltas;
			foreach (KeyValuePair<int, float> pair in brushResult.AffectedVertices)
			{
				int vertexIndex = pair.Key;
				float falloff = pair.Value;
				if (vertexIndex < 0 || vertexIndex >= deltas.Length)
					continue;

				List<int> neighbors = Adjacency[vertexIndex];
				if (neighbors.Count == 0)
					continue;

				Vector3 avg = neighbors.Where(neighbor => neighbor >= 0 && neighbor < deltas.Length).Aggregate(Vector3.zero, (current, neighbor) => current + deltas[neighbor]);
				avg /= neighbors.Count;
				deltas[vertexIndex] = Vector3.Lerp(deltas[vertexIndex], avg, falloff);
			}
			layer.Dirty = true;
		}
	}
}
