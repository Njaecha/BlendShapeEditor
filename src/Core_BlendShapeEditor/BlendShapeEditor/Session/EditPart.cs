using UnityEngine;

namespace BlendShapeEditor
{
	/// One renderer taking part in an EditSession: its picking collider, the baked mesh feeding it, its
	/// deformer, and where its vertices live in the session's global index range
	/// (global index = Offset + local index).
	public class EditPart
	{
		public Renderer Renderer { get; private set; }
		public ShapeDeformer Deformer { get; private set; }
		public int Offset { get; internal set; }
		public int VertexCount => _localVertices?.Length ?? 0;

		/// Vertices / normals in the collider's (= renderer's) local space, as last refreshed.
		public Vector3[] LocalVertices => _localVertices;
		public Vector3[] LocalNormals => _localNormals;

		public Transform Transform => Renderer ? Renderer.transform : null;
		public Transform ColliderTransform => _collider ? _collider.transform : null;
		public bool HasLiveDisplayMesh => Deformer && Deformer.DisplayMesh;

		public static EditPart Create(Renderer renderer, ShapeDeformer deformer)
		{
			var part = new EditPart { Renderer = renderer, Deformer = deformer };
			switch (renderer)
			{
				case SkinnedMeshRenderer smr:
					part._smr = smr;
					part._bakedMesh = new Mesh();
					smr.BakeMesh(part._bakedMesh);
					UndoBakedScale(part._bakedMesh, smr);
					break;
				case MeshRenderer _:
				{
					MeshFilter mf = renderer.GetComponent<MeshFilter>();
					if (!mf || !mf.sharedMesh)
						return null;
					Mesh sharedMesh = mf.sharedMesh;
					part._bakedMesh = new Mesh
					{
						vertices = sharedMesh.vertices,
						normals = sharedMesh.normals,
						uv = sharedMesh.uv,
						triangles = sharedMesh.triangles
					};
					part._bakedMesh.RecalculateBounds();
					break;
				}
				default:
					return null;
			}
			part.SetupCollider(renderer.transform);
			part.ReadBack();
			return part;
		}

		public bool Raycast(Ray ray, out RaycastHit hit)
		{
			hit = default(RaycastHit);
			return _collider && _collider.Raycast(ray, out hit, 1000f);
		}

		/// Live path: copy the deformer's display mesh (already includes all layer deltas).
		public void RefreshFromDisplayMesh()
		{
			if (!_collider || !HasLiveDisplayMesh)
				return;
			_bakedMesh.vertices = Deformer.DisplayMesh.vertices;
			_bakedMesh.RecalculateBounds();
			_collider.sharedMesh = null;
			_collider.sharedMesh = _bakedMesh;
			ReadBack();
		}

		/// Fallback path when there is no live display mesh: re-bake the SMR in its current pose.
		public void RefreshByBaking()
		{
			if (!_collider || !_smr)
				return;
			bool rendererWasDisabled = !_smr.enabled;
			if (rendererWasDisabled)
				_smr.enabled = true;
			_smr.BakeMesh(_bakedMesh);
			if (rendererWasDisabled)
				_smr.enabled = false;
			UndoBakedScale(_bakedMesh, _smr);
			_collider.sharedMesh = null;
			_collider.sharedMesh = _bakedMesh;
			ReadBack();
		}

		public void Cleanup()
		{
			if (_collider)
				Object.Destroy(_collider.gameObject);
			if (_bakedMesh)
				Object.Destroy(_bakedMesh);
			_collider = null;
			_bakedMesh = null;
			_localVertices = null;
			_localNormals = null;
		}

		private void ReadBack()
		{
			_localVertices = _bakedMesh.vertices;
			_localNormals = _bakedMesh.normals;
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
			_collider = go.AddComponent<MeshCollider>();
			_collider.sharedMesh = _bakedMesh;
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

		private SkinnedMeshRenderer _smr;
		private MeshCollider _collider;
		private Mesh _bakedMesh;
		private Vector3[] _localVertices;
		private Vector3[] _localNormals;
		private const int KkseColliderLayer = 29;
		private static bool _layerIsolated;
	}
}
