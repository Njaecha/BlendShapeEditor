using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	// Groups vertices that are within WeldRadius of each other in bind-pose space so that
	// deforming one member of a seam can be mirrored onto the rest. Keyed by plain vertex
	// index rather than a renderer-qualified id, since detection/propagation is currently
	// scoped to a single renderer's own mesh; a future multi-renderer weld could extend the
	// member list to carry (rendererId, vertexIndex) pairs without changing how callers use
	// TryGetGroup/GetBlendedNormal.
	public class WeldGroups
	{
		public void Rebuild(Vector3[] bindVertices, SpatialHashGrid grid, float weldRadius)
		{
			_vertexToGroup.Clear();

			if (bindVertices == null || bindVertices.Length == 0 || grid == null || weldRadius <= 0f)
				return;

			int count = bindVertices.Length;
			var parent = new int[count];
			for (var i = 0; i < count; i++)
				parent[i] = i;

			for (var i = 0; i < count; i++)
			{
				Vector3 pos = bindVertices[i];
				grid.FindVerticesInRadius(pos, weldRadius, (other, distSq) =>
				{
					if (other != i)
						Union(parent, i, other);
				});
			}

			var rootToGroup = new Dictionary<int, List<int>>();
			for (var i = 0; i < count; i++)
			{
				int root = Find(parent, i);
				if (!rootToGroup.TryGetValue(root, out List<int> members))
				{
					members = new List<int>();
					rootToGroup[root] = members;
				}
				members.Add(i);
			}

			foreach (List<int> members in rootToGroup.Values)
			{
				if (members.Count < 2)
					continue;

				int[] memberArray = members.ToArray();
				foreach (int idx in memberArray)
					_vertexToGroup[idx] = memberArray;
			}
		}

		public bool IsWelded(int vertexIndex)
		{
			return _vertexToGroup.ContainsKey(vertexIndex);
		}

		public bool TryGetGroup(int vertexIndex, out int[] members)
		{
			return _vertexToGroup.TryGetValue(vertexIndex, out members);
		}

		/// Calls `callback` once per weld group (each group's member array).
		public void ForEachGroup(System.Action<int[]> callback)
		{
			var visited = new HashSet<int[]>();
			foreach (int[] members in _vertexToGroup.Values)
				if (visited.Add(members))
					callback(members);
		}

		public Vector3 GetBlendedNormal(int vertexIndex, Vector3[] normals)
		{
			if (normals == null || vertexIndex < 0 || vertexIndex >= normals.Length)
				return Vector3.up;

			if (!_vertexToGroup.TryGetValue(vertexIndex, out int[] group))
				return normals[vertexIndex];

			Vector3 sum = Vector3.zero;
			foreach (int idx in group)
			{
				if (idx >= 0 && idx < normals.Length)
					sum += normals[idx];
			}

			return sum.sqrMagnitude > 0.0001f ? sum.normalized : normals[vertexIndex];
		}

		private static int Find(int[] parent, int i)
		{
			while (parent[i] != i)
			{
				parent[i] = parent[parent[i]];
				i = parent[i];
			}
			return i;
		}

		private static void Union(int[] parent, int a, int b)
		{
			int rootA = Find(parent, a);
			int rootB = Find(parent, b);
			if (rootA != rootB)
				parent[rootA] = rootB;
		}

		private readonly Dictionary<int, int[]> _vertexToGroup = new Dictionary<int, int[]>();
	}
}
