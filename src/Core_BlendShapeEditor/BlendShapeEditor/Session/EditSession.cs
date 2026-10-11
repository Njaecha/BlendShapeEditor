using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	/// Everything being edited at once, presented as ONE vertex set. Each part's vertices occupy the global
	/// range [part.Offset, part.Offset + part.VertexCount); selection, brush hits, the spatial grid, weld
	/// groups and adjacency all use these global indices, so tools never need to know how many renderers
	/// are involved.
	///
	/// Positions and Normals are expressed in the local space of `Space`: the renderer's own transform when
	/// editing a single renderer (identical to the pre-session behaviour, including mirror axes), and the
	/// owning object's root when editing several, since that's the only frame they all share.
	public class EditSession
	{
		public float Radius { get; set; } = BlendShapeEditorPlugin.DefaultBrushRadius != null ? BlendShapeEditorPlugin.DefaultBrushRadius.Value : 0.05f;
		public float Strength { get; set; } = BlendShapeEditorPlugin.DefaultBrushStrength != null ? BlendShapeEditorPlugin.DefaultBrushStrength.Value : 0.5f;
		public FalloffMode Falloff { get; set; } = FalloffMode.Smooth;
		public float SharpExponent { get; set; } = 3f;

		public IList<EditPart> Parts => _parts;
		public bool HasTarget => _parts.Count > 0;
		public int VertexCount { get; private set; }
		public Transform Space { get; private set; }

		public Vector3[] Positions => _positions;
		public Vector3[] Normals => _normals;
		public SpatialHashGrid Grid => _grid;
		public HashSet<int> SelectedVertices => _selectedVertices;

		/// Combined bind-pose topology (triangles offset into global indices) and the matching bind-pose
		/// positions, used to build adjacency. RestPositions is null if any part's mesh isn't readable, so
		/// adjacency skips position-based linking rather than treating every vertex as coincident.
		public int[] RestTriangles { get; private set; }
		public Vector3[] RestPositions { get; private set; }

		/// Index into Parts for every global vertex.
		public int[] PartIndexOfVertex { get; private set; }

		public bool AllPartsLive
		{
			get
			{
				foreach (EditPart part in _parts)
					if (!part.HasLiveDisplayMesh)
						return false;
				return _parts.Count > 0;
			}
		}

		/// Builds the session from already-initialised parts. `objectRoot` is the shared space used when
		/// there is more than one part.
		public void Begin(IEnumerable<EditPart> parts, Transform objectRoot)
		{
			Clear();
			var offset = 0;
			foreach (EditPart part in parts)
			{
				if (part == null)
					continue;
				part.Offset = offset;
				offset += part.VertexCount;
				_parts.Add(part);
			}
			VertexCount = offset;
			if (_parts.Count == 0)
				return;
			Space = _parts.Count == 1 ? _parts[0].Transform : objectRoot;
			PartIndexOfVertex = new int[VertexCount];
			for (var p = 0; p < _parts.Count; p++)
				for (var i = 0; i < _parts[p].VertexCount; i++)
					PartIndexOfVertex[_parts[p].Offset + i] = p;
			BuildRestTopology();
			RebuildCombined();
		}

		public void RefreshFromDisplayMeshes()
		{
			foreach (EditPart part in _parts)
				part.RefreshFromDisplayMesh();
			RebuildCombined();
		}

		public void RefreshByBaking()
		{
			foreach (EditPart part in _parts)
				part.RefreshByBaking();
			RebuildCombined();
		}

		public void InvalidateDeltaCaches()
		{
			foreach (EditPart part in _parts)
				if (part.Deformer)
					part.Deformer.InvalidateDeltaCache();
		}

		public EditPart GetPart(int globalIndex, out int localIndex)
		{
			for (var i = _parts.Count - 1; i >= 0; i--)
			{
				EditPart part = _parts[i];
				if (globalIndex < part.Offset)
					continue;
				localIndex = globalIndex - part.Offset;
				return localIndex < part.VertexCount ? part : null;
			}
			localIndex = -1;
			return null;
		}

		public bool WorldDeltaToBindDelta(int globalIndex, Vector3 worldDisp, out Vector3 bindDelta)
		{
			EditPart part = GetPart(globalIndex, out int local);
			if (part == null)
			{
				bindDelta = worldDisp;
				return false;
			}
			if (part.Deformer)
				return part.Deformer.WorldDeltaToBindDelta(local, worldDisp, out bindDelta);
			bindDelta = part.Transform ? part.Transform.InverseTransformVector(worldDisp) : worldDisp;
			return false;
		}

		public bool BindDeltaToWorldDelta(int globalIndex, Vector3 bindDelta, out Vector3 worldDisp)
		{
			EditPart part = GetPart(globalIndex, out int local);
			if (part == null)
			{
				worldDisp = bindDelta;
				return false;
			}
			if (part.Deformer)
				return part.Deformer.BindDeltaToWorldDelta(local, bindDelta, out worldDisp);
			worldDisp = part.Transform ? part.Transform.TransformVector(bindDelta) : bindDelta;
			return false;
		}

		public bool Raycast(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
		{
			bool found = RaycastNearest(ray, out RaycastHit hit);
			hitPoint = found ? hit.point : Vector3.zero;
			hitNormal = found ? hit.normal : Vector3.up;
			return found;
		}

		public BrushResult BrushSelect(Ray mouseRay)
		{
			if (!HasTarget || !Space || !RaycastNearest(mouseRay, out RaycastHit hit))
				return null;

			Dictionary<int, float> affected = _brushResultCache.AffectedVertices;
			affected.Clear();
			_brushResultCache.HitPoint = hit.point;
			_brushResultCache.HitNormal = hit.normal;
			CollectInRadius(Space.InverseTransformPoint(hit.point), affected);
			return _brushResultCache;
		}

		public BrushResult BrushSelectAtPoint(Vector3 spacePoint, Vector3 worldPoint, Vector3 worldNormal)
		{
			if (_grid == null || _positions == null || !Space)
				return null;

			var affected = new Dictionary<int, float>();
			CollectInRadius(spacePoint, affected);
			if (affected.Count == 0)
				return null;

			return new BrushResult
			{
				HitPoint = worldPoint,
				HitNormal = worldNormal,
				AffectedVertices = affected
			};
		}

		public void SelectBox(Camera cam, Rect screenRect, bool additive, bool[] vertexVisible = null)
		{
			if (_positions == null || !Space)
				return;

			if (!additive)
				_selectedVertices.Clear();

			Matrix4x4 l2w = Space.localToWorldMatrix;
			bool useMask = vertexVisible != null && vertexVisible.Length == _positions.Length;
			for (int i = 0; i < _positions.Length; i++)
			{
				if (useMask && !vertexVisible[i]) continue;
				Vector3 screenPos = cam.WorldToScreenPoint(l2w.MultiplyPoint3x4(_positions[i]));
				if (screenPos.z > 0f && screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
					_selectedVertices.Add(i);
			}
		}

		public void DeselectBox(Camera cam, Rect screenRect, bool[] vertexVisible = null)
		{
			if (_positions == null || !Space || _selectedVertices.Count == 0)
				return;

			Matrix4x4 l2w = Space.localToWorldMatrix;
			bool useMask = vertexVisible != null && vertexVisible.Length == _positions.Length;
			for (int i = 0; i < _positions.Length; i++)
			{
				if (!_selectedVertices.Contains(i)) continue;
				if (useMask && !vertexVisible[i]) continue;
				Vector3 screenPos = cam.WorldToScreenPoint(l2w.MultiplyPoint3x4(_positions[i]));
				if (screenPos.z > 0f && screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
					_selectedVertices.Remove(i);
			}
		}

		public void ClearSelection()
		{
			_selectedVertices.Clear();
		}

		public float CalculateFalloff(float normalizedDist)
		{
			normalizedDist = Mathf.Clamp01(normalizedDist);
			switch (Falloff)
			{
				case FalloffMode.Linear:
					return 1f - normalizedDist;
				case FalloffMode.Smooth:
				{
					float t = 1f - normalizedDist;
					return t * t * (3f - 2f * t);
				}
				case FalloffMode.Sharp:
					return Mathf.Pow(1f - normalizedDist, SharpExponent);
				default:
					return 1f - normalizedDist;
			}
		}

		public void Clear()
		{
			foreach (EditPart part in _parts)
				part.Cleanup();
			_parts.Clear();
			VertexCount = 0;
			Space = null;
			_positions = null;
			_normals = null;
			_grid = null;
			RestTriangles = null;
			RestPositions = null;
			PartIndexOfVertex = null;
			_selectedVertices.Clear();
		}

		private bool RaycastNearest(Ray ray, out RaycastHit best)
		{
			best = default(RaycastHit);
			var found = false;
			foreach (EditPart part in _parts)
			{
				if (!part.Raycast(ray, out RaycastHit hit))
					continue;
				if (found && hit.distance >= best.distance)
					continue;
				best = hit;
				found = true;
			}
			return found;
		}

		private void CollectInRadius(Vector3 spacePoint, Dictionary<int, float> affected)
		{
			if (_grid == null || _positions == null)
				return;
			float strength = Strength;
			float localRadius = Space.InverseTransformVector(new Vector3(Radius, 0, 0)).magnitude;
			_grid.FindVerticesInRadius(spacePoint, localRadius, (i, distSq) =>
			{
				float falloff = CalculateFalloff(Mathf.Sqrt(distSq) / localRadius);
				affected[i] = falloff * strength;
			});
		}

		private void RebuildCombined()
		{
			if (_parts.Count == 0)
				return;

			if (_positions == null || _positions.Length != VertexCount)
				_positions = new Vector3[VertexCount];
			bool anyNormals = false;
			foreach (EditPart part in _parts)
				anyNormals |= part.LocalNormals != null && part.LocalNormals.Length == part.VertexCount;
			if (!anyNormals)
				_normals = null;
			else if (_normals == null || _normals.Length != VertexCount)
				_normals = new Vector3[VertexCount];

			Matrix4x4 worldToSpace = Space ? Space.worldToLocalMatrix : Matrix4x4.identity;
			foreach (EditPart part in _parts)
			{
				Vector3[] verts = part.LocalVertices;
				Vector3[] norms = part.LocalNormals;
				if (verts == null)
					continue;
				int offset = part.Offset;
				int count = Mathf.Min(verts.Length, part.VertexCount);
				bool hasNormals = _normals != null && norms != null && norms.Length == verts.Length;
				// Single-part sessions use the renderer's own space, so copy verbatim (no float drift).
				if (part.Transform == Space)
				{
					System.Array.Copy(verts, 0, _positions, offset, count);
					if (hasNormals)
						System.Array.Copy(norms, 0, _normals, offset, count);
					continue;
				}
				Matrix4x4 m = worldToSpace * part.ColliderTransform.localToWorldMatrix;
				for (var i = 0; i < count; i++)
				{
					_positions[offset + i] = m.MultiplyPoint3x4(verts[i]);
					if (hasNormals)
						_normals[offset + i] = m.MultiplyVector(norms[i]).normalized;
				}
			}

			if (VertexCount == 0)
			{
				_grid = null;
				return;
			}
			Bounds bounds = ComputeBounds(_positions);
			if (_grid != null)
				_grid.Rebuild(_positions, bounds);
			else
				_grid = new SpatialHashGrid(_positions, bounds);
		}

		private void BuildRestTopology()
		{
			var triangles = new List<int>();
			var rest = new Vector3[VertexCount];
			var restComplete = true;
			foreach (EditPart part in _parts)
			{
				Mesh mesh = MeshHelper.GetMesh(part.Renderer);
				if (!mesh)
				{
					restComplete = false;
					continue;
				}
				int offset = part.Offset;
				foreach (int t in mesh.triangles)
					triangles.Add(t + offset);
				Vector3[] verts = mesh.vertices;
				if (verts == null || verts.Length != part.VertexCount)
				{
					restComplete = false;
					continue;
				}
				System.Array.Copy(verts, 0, rest, offset, verts.Length);
			}
			RestTriangles = triangles.ToArray();
			RestPositions = restComplete ? rest : null;
		}

		private static Bounds ComputeBounds(Vector3[] points)
		{
			Vector3 min = points[0];
			Vector3 max = points[0];
			for (var i = 1; i < points.Length; i++)
			{
				min = Vector3.Min(min, points[i]);
				max = Vector3.Max(max, points[i]);
			}
			var b = new Bounds();
			b.SetMinMax(min, max);
			return b;
		}

		private readonly List<EditPart> _parts = new List<EditPart>();
		private readonly HashSet<int> _selectedVertices = new HashSet<int>();
		private Vector3[] _positions;
		private Vector3[] _normals;
		private SpatialHashGrid _grid;
		private readonly BrushResult _brushResultCache = new BrushResult
		{
			AffectedVertices = new Dictionary<int, float>()
		};
	}
}
