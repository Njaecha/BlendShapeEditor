using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	/// The brush tools' only view of the mesh being sculpted. Everything a tool reads or writes is in
	/// world space and indexed by EditSession global vertex index; the session owns the conversion to/from
	/// each vertex's bind-space delta, so tools never have to know which renderer (or which bone weights)
	/// a vertex belongs to.
	public class DeformSurface
	{
		public List<int>[] Adjacency { get; private set; }

		public void Bind(EditSession session, DeformLayer layer, Vector3[] normals, List<int>[] adjacency,
			WeldGroups weldGroups)
		{
			_session = session;
			_vertices = session?.Positions;
			_normals = normals;
			_layer = layer;
			_weldGroups = weldGroups;
			Adjacency = adjacency;
			Transform space = session?.Space;
			_localToWorld = space ? space.localToWorldMatrix : Matrix4x4.identity;
		}

		public bool IsValid(int vertexIndex)
		{
			return _layer != null && _vertices != null && vertexIndex >= 0
				&& vertexIndex < _vertices.Length && vertexIndex < _layer.Deltas.Length;
		}

		public bool HasNormals => _normals != null;

		/// Current world position, already including every layer's deformation.
		public Vector3 GetPosition(int vertexIndex)
		{
			return _localToWorld.MultiplyPoint3x4(_vertices[vertexIndex]);
		}

		public Vector3 GetNormal(int vertexIndex)
		{
			if (_normals == null || vertexIndex < 0 || vertexIndex >= _normals.Length)
				return Vector3.up;
			return _localToWorld.MultiplyVector(_normals[vertexIndex]).normalized;
		}

		/// World displacement the active layer currently contributes at this vertex (layer weight not applied).
		public Vector3 GetLayerDisplacement(int vertexIndex)
		{
			_session.BindDeltaToWorldDelta(vertexIndex, _layer.Deltas[vertexIndex], out Vector3 worldDisp);
			return worldDisp;
		}

		/// Converts each world displacement to its vertex's bind space and adds it to the active layer.
		/// Welded vertices get their group's average displacement first, so they move as one point even
		/// when a tool (e.g. Smooth) computed slightly different values for each member.
		public void ApplyDisplacements(Dictionary<int, Vector3> worldDisplacements)
		{
			if (_layer == null || _session == null || worldDisplacements == null || worldDisplacements.Count == 0)
				return;

			if (_weldGroups != null)
				AverageWeldedDisplacements(worldDisplacements);

			Vector3[] deltas = _layer.Deltas;
			foreach (KeyValuePair<int, Vector3> pair in worldDisplacements)
			{
				int vertexIndex = pair.Key;
				if (vertexIndex < 0 || vertexIndex >= deltas.Length)
					continue;
				_session.WorldDeltaToBindDelta(vertexIndex, pair.Value, out Vector3 bindDelta);
				deltas[vertexIndex] += bindDelta;
			}
			_layer.Dirty = true;
		}

		private void AverageWeldedDisplacements(Dictionary<int, Vector3> worldDisplacements)
		{
			_weldScratch.Clear();
			foreach (int vertexIndex in worldDisplacements.Keys)
			{
				if (!_weldGroups.TryGetGroup(vertexIndex, out int[] members))
					continue;
				Vector3 sum = Vector3.zero;
				var count = 0;
				foreach (int member in members)
				{
					if (!worldDisplacements.TryGetValue(member, out Vector3 d))
						continue;
					sum += d;
					count++;
				}
				if (count > 1)
					_weldScratch[vertexIndex] = sum / count;
			}
			foreach (KeyValuePair<int, Vector3> pair in _weldScratch)
				worldDisplacements[pair.Key] = pair.Value;
		}

		private EditSession _session;
		private Vector3[] _vertices;
		private Vector3[] _normals;
		private DeformLayer _layer;
		private WeldGroups _weldGroups;
		private Matrix4x4 _localToWorld;
		private readonly Dictionary<int, Vector3> _weldScratch = new Dictionary<int, Vector3>();
	}
}
