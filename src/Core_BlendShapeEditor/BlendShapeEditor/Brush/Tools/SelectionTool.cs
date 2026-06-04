using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public class SelectionTool
	{
		public float Radius { get; set; } = BlendShapeEditorPlugin.DefaultBrushRadius != null ? BlendShapeEditorPlugin.DefaultBrushRadius.Value : 0.05f;
		public float Strength { get; set; } = BlendShapeEditorPlugin.DefaultBrushStrength != null ? BlendShapeEditorPlugin.DefaultBrushStrength.Value : 0.5f;
		public FalloffMode Falloff { get; set; } = FalloffMode.Smooth;
		public SelectionToolMode ToolMode { get; set; }
		public float SharpExponent { get; set; } = 3f;

		public HashSet<int> SelectedVertices => _selectedVertices;

		public SpatialHashGrid Grid => _grid;

		public Transform ColliderTransform => _tempCollider != null ? _tempCollider.transform : null;

		public Mesh BakedMesh => _bakedMesh;

		public SkinnedMeshRenderer TargetRenderer => _targetRenderer;

		public Vector3[] CachedVertices => _cachedVertices;

		public Vector3[] CachedNormals => _cachedNormals;

		public Transform TargetTransform
		{
			get
			{
				if (_targetRenderer)
					return _targetRenderer.transform;
				return _targetMeshFilter ? _targetMeshFilter.transform : null;
			}
		}

		public bool HasTarget => _targetRenderer != null || _targetMeshFilter != null;

		public void SetTarget(SkinnedMeshRenderer renderer)
		{
			CleanupCollider();
			_targetRenderer = renderer;
			_targetMeshFilter = null;
			_isStatic = false;
			if (!renderer)
				return;

			_bakedMesh = new Mesh();
			renderer.BakeMesh(_bakedMesh);
			UndoBakedScale(_bakedMesh, renderer);
			SetupCollider(renderer.transform);
			RebuildGrid();
		}

		public void SetTarget(MeshFilter meshFilter)
		{
			CleanupCollider();
			_targetRenderer = null;
			_targetMeshFilter = meshFilter;
			_isStatic = true;
			if (!meshFilter)
				return;

			Mesh sharedMesh = meshFilter.sharedMesh;
			_bakedMesh = new Mesh
			{
				vertices = sharedMesh.vertices,
				normals = sharedMesh.normals,
				uv = sharedMesh.uv,
				triangles = sharedMesh.triangles
			};
			_bakedMesh.RecalculateBounds();
			SetupCollider(meshFilter.transform);
			RebuildGrid();
		}

		private void SetupCollider(Transform parent)
		{
			if (!_layerIsolated)
			{
				for (var i = 0; i < 32; i++)
					Physics.IgnoreLayerCollision(KkseColliderLayer, i, true);
				_layerIsolated = true;
			}

			GameObject go = new GameObject("_kkse_selectionCollider");
			go.layer = KkseColliderLayer;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = Vector3.zero;
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = Vector3.one;
			_tempCollider = go.AddComponent<MeshCollider>();
			_tempCollider.sharedMesh = _bakedMesh;
		}

		public void RefreshCollider()
		{
			if (!_tempCollider || _isStatic || !_targetRenderer)
				return;

			bool rendererWasDisabled = !_targetRenderer.enabled;
			if (rendererWasDisabled)
				_targetRenderer.enabled = true;
			_targetRenderer.BakeMesh(_bakedMesh);
			if (rendererWasDisabled)
				_targetRenderer.enabled = false;

			UndoBakedScale(_bakedMesh, _targetRenderer);
			_tempCollider.sharedMesh = null;
			_tempCollider.sharedMesh = _bakedMesh;
			RebuildGrid();
		}

		public void RefreshCollider(Mesh deformedMesh)
		{
			if (!_tempCollider || !deformedMesh)
				return;

			_bakedMesh.vertices = deformedMesh.vertices;
			_bakedMesh.RecalculateBounds();
			_tempCollider.sharedMesh = null;
			_tempCollider.sharedMesh = _bakedMesh;
			RebuildGrid();
		}

		private void RebuildGrid()
		{
			if (!_bakedMesh)
			{
				_cachedVertices = null;
				_grid = null;
				return;
			}

			_cachedVertices = _bakedMesh.vertices;
			_cachedNormals = _bakedMesh.normals;
			if (_cachedVertices.Length == 0)
			{
				_grid = null;
				return;
			}

			if (_grid != null)
				_grid.Rebuild(_cachedVertices, _bakedMesh.bounds);
			else
				_grid = new SpatialHashGrid(_cachedVertices, _bakedMesh.bounds);
		}

		public bool Raycast(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
		{
			hitPoint = Vector3.zero;
			hitNormal = Vector3.up;
			if (!_tempCollider)
				return false;

			if (_tempCollider.Raycast(ray, out RaycastHit hit, 1000f))
			{
				hitPoint = hit.point;
				hitNormal = hit.normal;
				return true;
			}
			return false;
		}

		public BrushResult BrushSelect(Ray mouseRay)
		{
			if (!_tempCollider || (!_targetRenderer && !_targetMeshFilter))
				return null;

			if (!_tempCollider.Raycast(mouseRay, out RaycastHit hit, 1000f))
				return null;

			Vector3 localPoint = _tempCollider.transform.InverseTransformPoint(hit.point);
			Dictionary<int, float> affected = _brushResultCache.AffectedVertices;
			affected.Clear();
			_brushResultCache.HitPoint = hit.point;
			_brushResultCache.HitNormal = hit.normal;
			float radius = Radius;
			float strength = Strength;

			float localRadius = _tempCollider.transform.InverseTransformVector(new Vector3(radius, 0, 0)).magnitude;
			if (_grid != null && _cachedVertices != null)
			{
				_grid.FindVerticesInRadius(localPoint, localRadius, (i, distSq) =>
				{
					float dist = Mathf.Sqrt(distSq);
					float falloff = CalculateFalloff(dist / localRadius);
					affected[i] = falloff * strength;
				});
			}
			else
			{
				Vector3[] verts = _cachedVertices ?? (_bakedMesh ? _bakedMesh.vertices : null);
				if (verts == null)
					return null;

				float radiusSq = localRadius * localRadius;
				for (int j = 0; j < verts.Length; j++)
				{
					float sqDist = (verts[j] - localPoint).sqrMagnitude;
					if (sqDist <= radiusSq)
					{
						float dist = Mathf.Sqrt(sqDist);
						float falloff = CalculateFalloff(dist / localRadius);
						affected[j] = falloff * strength;
					}
				}
			}

			return _brushResultCache;
		}

		public BrushResult BrushSelectAtPoint(Vector3 localPoint, Vector3 worldPoint, Vector3 worldNormal)
		{
			if (_grid == null || _cachedVertices == null)
				return null;

			Dictionary<int, float> affected = new Dictionary<int, float>();
			float radius = Radius;
			float strength = Strength;
			float localRadius = _tempCollider.transform.InverseTransformVector(new Vector3(radius, 0, 0)).magnitude;
			_grid.FindVerticesInRadius(localPoint, localRadius, (i, distSq) =>
			{
				float dist = Mathf.Sqrt(distSq);
				float falloff = CalculateFalloff(dist / localRadius);
				affected[i] = falloff * strength;
			});

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
			if (!_tempCollider)
				return;

			Vector3[] verts = _cachedVertices ?? (_bakedMesh ? _bakedMesh.vertices : null);
			if (verts == null)
				return;

			if (!additive)
				_selectedVertices.Clear();

			Matrix4x4 l2w = _tempCollider.transform.localToWorldMatrix;
			bool useMask = vertexVisible != null && vertexVisible.Length == verts.Length;
			for (int i = 0; i < verts.Length; i++)
			{
				if (useMask && !vertexVisible[i]) continue;
				Vector3 worldPos = l2w.MultiplyPoint3x4(verts[i]);
				Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
				if (screenPos.z > 0f && screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
					_selectedVertices.Add(i);
			}
		}

		public void DeselectBox(Camera cam, Rect screenRect, bool[] vertexVisible = null)
		{
			if (!_tempCollider || _selectedVertices.Count == 0)
				return;

			Vector3[] verts = _cachedVertices ?? (_bakedMesh ? _bakedMesh.vertices : null);
			if (verts == null)
				return;

			Matrix4x4 l2w = _tempCollider.transform.localToWorldMatrix;
			bool useMask = vertexVisible != null && vertexVisible.Length == verts.Length;
			for (int i = 0; i < verts.Length; i++)
			{
				if (_selectedVertices.Contains(i))
				{
					if (useMask && !vertexVisible[i]) continue;
					Vector3 worldPos = l2w.MultiplyPoint3x4(verts[i]);
					Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
					if (screenPos.z > 0f && screenRect.Contains(new Vector2(screenPos.x, screenPos.y)))
						_selectedVertices.Remove(i);
				}
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

		public void CleanupCollider()
		{
			if (_tempCollider)
			{
				UnityEngine.Object.Destroy(_tempCollider.gameObject);
				_tempCollider = null;
			}
			if (_bakedMesh)
			{
				UnityEngine.Object.Destroy(_bakedMesh);
				_bakedMesh = null;
			}
			_targetRenderer = null;
			_targetMeshFilter = null;
			_isStatic = false;
			_cachedVertices = null;
			_cachedNormals = null;
			_grid = null;
			_selectedVertices.Clear();
		}

		private static void UndoBakedScale(Mesh mesh, SkinnedMeshRenderer smr)
		{
			Vector3 scale = smr.transform.lossyScale;
			if (Mathf.Approximately(scale.x, 1f) && Mathf.Approximately(scale.y, 1f) && Mathf.Approximately(scale.z, 1f))
				return;

			Vector3[] vertices = mesh.vertices;
			for (int i = 0; i < vertices.Length; i++)
			{
				vertices[i].x /= scale.x;
				vertices[i].y /= scale.y;
				vertices[i].z /= scale.z;
			}
			mesh.vertices = vertices;
		}

		private HashSet<int> _selectedVertices = new HashSet<int>();
		private MeshCollider _tempCollider;
		private SkinnedMeshRenderer _targetRenderer;
		private MeshFilter _targetMeshFilter;
		private bool _isStatic;
		private Mesh _bakedMesh;
		private SpatialHashGrid _grid;
		private Vector3[] _cachedVertices;
		private Vector3[] _cachedNormals;
		private readonly BrushResult _brushResultCache = new BrushResult
		{
			AffectedVertices = new Dictionary<int, float>()
		};
		private const int KkseColliderLayer = 29;
		private static bool _layerIsolated;
	}
}
