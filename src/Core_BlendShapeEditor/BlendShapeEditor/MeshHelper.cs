using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlendShapeEditor
{
	public static class MeshHelper
	{
		private static readonly HashSet<int> _clonedMeshIds = new HashSet<int>();
		private static readonly Dictionary<int, Mesh> _originalMeshes = new Dictionary<int, Mesh>();

		public static void CloneMeshIfShared(SkinnedMeshRenderer renderer)
		{
			if (!renderer || !renderer.sharedMesh)
				return;
			Mesh clone = CloneMeshCore(renderer.sharedMesh);
			if (clone)
				renderer.sharedMesh = clone;
		}

		public static void CloneMeshIfShared(MeshFilter meshFilter)
		{
			if (!meshFilter || !meshFilter.sharedMesh)
				return;
			Mesh clone = CloneMeshCore(meshFilter.sharedMesh);
			if (clone)
				meshFilter.sharedMesh = clone;
		}

		public static void CloneMeshIfShared(Renderer renderer)
		{
			SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
			if (smr)
			{
				CloneMeshIfShared(smr);
				return;
			}
			MeshFilter mf = renderer.GetComponent<MeshFilter>();
			if (mf)
				CloneMeshIfShared(mf);
		}

		private static Mesh CloneMeshCore(Mesh original)
		{
			int originalId = original.GetInstanceID();
			if (_clonedMeshIds.Contains(originalId))
				return null;
			Mesh clone = UnityEngine.Object.Instantiate<Mesh>(original);
			clone.name = original.name + "_kkse";
			int cloneId = clone.GetInstanceID();
			_clonedMeshIds.Add(cloneId);
			_originalMeshes[cloneId] = original;
			return clone;
		}

		public static bool HasOriginal(Renderer r)
		{
			Mesh mesh = GetMesh(r);
			return mesh && _originalMeshes.ContainsKey(mesh.GetInstanceID());
		}

		public static bool RestoreOriginal(SkinnedMeshRenderer smr)
		{
			if (!smr || !smr.sharedMesh)
				return false;
			if (!RestoreOriginalCore(smr.sharedMesh, out Mesh original))
				return false;
			smr.sharedMesh = original;
			return true;
		}

		public static bool RestoreOriginal(MeshFilter mf)
		{
			if (!mf || !mf.sharedMesh)
				return false;
			if (!RestoreOriginalCore(mf.sharedMesh, out Mesh original))
				return false;
			mf.sharedMesh = original;
			return true;
		}

		private static bool RestoreOriginalCore(Mesh cloned, out Mesh original)
		{
			int cloneId = cloned.GetInstanceID();
			if (!_originalMeshes.TryGetValue(cloneId, out original))
				return false;
			_clonedMeshIds.Remove(cloneId);
			_originalMeshes.Remove(cloneId);
			UnityEngine.Object.DestroyImmediate(cloned);
			return true;
		}

		public static void PurgeDestroyed()
		{
			List<int> toRemove = null;
			foreach (KeyValuePair<int, Mesh> pair in _originalMeshes.Where(pair => !pair.Value || !_clonedMeshIds.Contains(pair.Key)))
			{
				if (toRemove == null)
					toRemove = new List<int>();
				toRemove.Add(pair.Key);
			}
			if (toRemove == null)
				return;
			foreach (int id in toRemove)
			{
				_clonedMeshIds.Remove(id);
				_originalMeshes.Remove(id);
			}
		}

		public static Mesh GetMesh(Renderer r)
		{
			SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
			if (smr)
				return smr.sharedMesh;
			MeshFilter mf = r.GetComponent<MeshFilter>();
			return mf ? mf.sharedMesh : null;
		}

		/// Computes delta normals for baking to a blendshape.
		/// Only recalculates normals for vertices that have non-zero deltas (affected vertices).
		/// Unaffected vertices get delta normal = Vector3.zero, avoiding seams from realigned normals.
		public static Vector3[] ComputePartialDeltaNormals(Vector3[] bindVerts, Vector3[] bindNormals,
			Vector3[] combinedDelta, int[] triangles)
		{
			int vertCount = bindVerts.Length;
			var deltaNormals = new Vector3[vertCount];

			if (bindNormals == null || bindNormals.Length != vertCount || triangles == null)
				return deltaNormals;

			// Identify affected vertices
			var affected = new bool[vertCount];
			for (int i = 0; i < vertCount; i++)
				affected[i] = combinedDelta[i] != Vector3.zero;

			// Compute target positions for all verts (needed for face normals)
			var targetPositions = new Vector3[vertCount];
			for (int i = 0; i < vertCount; i++)
				targetPositions[i] = bindVerts[i] + combinedDelta[i];

			// Accumulate face normals into affected vertices only
			var accum = new Vector3[vertCount];
			int triCount = triangles.Length / 3;
			for (int i = 0; i < triCount; i++)
			{
				int i0 = triangles[i * 3];
				int i1 = triangles[i * 3 + 1];
				int i2 = triangles[i * 3 + 2];

				if (!affected[i0] && !affected[i1] && !affected[i2])
					continue;

				Vector3 v0 = targetPositions[i0];
				Vector3 v1 = targetPositions[i1];
				Vector3 v2 = targetPositions[i2];
				// Area-weighted face normal (cross product magnitude = 2 * area)
				Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0);

				if (affected[i0]) accum[i0] += faceNormal;
				if (affected[i1]) accum[i1] += faceNormal;
				if (affected[i2]) accum[i2] += faceNormal;
			}

			// Normalize and compute delta vs bind normal
			for (int i = 0; i < vertCount; i++)
			{
				if (!affected[i]) continue;
				Vector3 newNormal = accum[i].normalized;
				deltaNormals[i] = newNormal - bindNormals[i];
			}

			return deltaNormals;
		}
	}
}
