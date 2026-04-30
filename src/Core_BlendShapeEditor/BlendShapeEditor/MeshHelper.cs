using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace KKShapeEditor
{
	public static class MeshHelper
	{
		private static readonly HashSet<int> _clonedMeshIds = new HashSet<int>();
		private static readonly Dictionary<int, Mesh> _originalMeshes = new Dictionary<int, Mesh>();
		private static readonly Dictionary<int, int> _subdivisionLevels = new Dictionary<int, int>();
		private static readonly Dictionary<int, List<int[]>> _subdivisionFaces = new Dictionary<int, List<int[]>>();

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
			_subdivisionLevels.Remove(cloneId);
			_subdivisionFaces.Remove(cloneId);
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
				_subdivisionLevels.Remove(id);
				_subdivisionFaces.Remove(id);
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

		public static void CloneAndReplaySubdivision(Renderer renderer, List<int[]> facesPerLevel)
		{
			if (!renderer)
				return;
			CloneMeshIfShared(renderer);
			Mesh mesh = GetMesh(renderer);
			if (mesh && facesPerLevel != null && facesPerLevel.Count > 0)
				SubdivideReplay(mesh, facesPerLevel);
		}

		public static List<SubmeshInfo> GetSubmeshInfo(Mesh mesh)
		{
			var result = new List<SubmeshInfo>();
			if (!mesh)
				return result;
			for (var i = 0; i < mesh.subMeshCount; i++)
			{
				int[] triangles = mesh.GetTriangles(i);
				var uniqueVerts = new HashSet<int>();
				foreach (int t in triangles)
					uniqueVerts.Add(t);

				result.Add(new SubmeshInfo { TriangleCount = triangles.Length / 3, VertexCount = uniqueVerts.Count });
			}
			return result;
		}

		public static int GetSubdivisionLevel(Renderer r)
		{
			Mesh mesh = GetMesh(r);
			if (!mesh)
				return 0;
			return _subdivisionLevels.TryGetValue(mesh.GetInstanceID(), out int level) ? level : 0;
		}

		public static List<int[]> GetSubdivisionFaces(Renderer r)
		{
			Mesh mesh = GetMesh(r);
			if (!mesh)
				return null;
			return _subdivisionFaces.TryGetValue(mesh.GetInstanceID(), out List<int[]> faces) ? faces : null;
		}

		public static int GetTotalFaceCount(Mesh mesh)
		{
			if (!mesh)
				return 0;
			var total = 0;
			for (var i = 0; i < mesh.subMeshCount; i++)
				total += mesh.GetTriangles(i).Length / 3;
			return total;
		}

		public static bool Subdivide(Mesh mesh, int levels = 1)
		{
			return Subdivide(mesh, levels, null);
		}

		public static bool Subdivide(Mesh mesh, int levels, HashSet<int> selectedFaces)
		{
			if (!mesh)
				return false;

			var successCount = 0;
			var levelIndex = 0;
			while (levelIndex < levels && SubdivideOnce(mesh, selectedFaces, out HashSet<int> nextFaces))
			{
				successCount++;
				selectedFaces = nextFaces;
				levelIndex++;
			}

			if (successCount <= 0) return successCount == levels;
			int id = mesh.GetInstanceID();
			_subdivisionLevels.TryGetValue(id, out int prevLevel);
			_subdivisionLevels[id] = prevLevel + successCount;
			return successCount == levels;
		}

		public static void AppendSubdivisionFaces(Mesh mesh, int[] faces, int levels = 1)
		{
			if (!mesh || levels <= 0)
				return;
			int id = mesh.GetInstanceID();
			if (!_subdivisionFaces.TryGetValue(id, out List<int[]> facesList))
			{
				facesList = new List<int[]>();
				_subdivisionFaces[id] = facesList;
			}
			facesList.Add(faces);
			for (var i = 1; i < levels; i++)
				facesList.Add(new int[0]);
		}

		public static void SetSubdivisionFaces(Mesh mesh, List<int[]> facesList)
		{
			if (!mesh)
				return;
			int id = mesh.GetInstanceID();
			if (facesList != null && facesList.Count > 0)
				_subdivisionFaces[id] = facesList;
			else
				_subdivisionFaces.Remove(id);
		}

		public static bool SubdivideReplay(Mesh mesh, List<int[]> facesPerLevel)
		{
			if (!mesh || facesPerLevel == null || facesPerLevel.Count == 0)
				return false;

			var successCount = 0;
			HashSet<int> prevOutputFaces = null;
			foreach (int[] entry in facesPerLevel)
			{
				HashSet<int> inputFaces;
				if (entry != null && entry.Length != 0)
				{
					inputFaces = new HashSet<int>();
					foreach (int t in entry)
						inputFaces.Add(t);
				}
				else if (entry != null && entry.Length == 0)
				{
					inputFaces = prevOutputFaces;
				}
				else
				{
					inputFaces = null;
				}

				if (!SubdivideOnce(mesh, inputFaces, out HashSet<int> outputFaces))
					break;
				successCount++;
				prevOutputFaces = outputFaces;
			}

			if (successCount <= 0) return successCount == facesPerLevel.Count;
			int id = mesh.GetInstanceID();
			_subdivisionLevels.TryGetValue(id, out int prevLevel);
			_subdivisionLevels[id] = prevLevel + successCount;
			_subdivisionFaces[id] = new List<int[]>(facesPerLevel);
			return successCount == facesPerLevel.Count;
		}

		private static bool SubdivideOnce(Mesh mesh, HashSet<int> selectedFaces, out HashSet<int> outputSelectedFaces)
		{
			outputSelectedFaces = null;
			Vector3[] vertices = mesh.vertices;
			if (vertices.Length == 0)
				return false;

			Vector3[] normals = mesh.normals;
			Vector2[] uv = mesh.uv;
			Vector2[] uv2 = mesh.uv2;
			Vector4[] tangents = mesh.tangents;
			BoneWeight[] boneWeights = mesh.boneWeights;
			Color32[] colors = mesh.colors32;
			Matrix4x4[] bindposes = mesh.bindposes;

			bool hasNormals = normals != null && normals.Length == vertices.Length;
			bool hasUVs = uv != null && uv.Length == vertices.Length;
			bool hasUV2s = uv2 != null && uv2.Length == vertices.Length;
			bool hasTangents = tangents != null && tangents.Length == vertices.Length;
			bool hasBoneWeights = boneWeights != null && boneWeights.Length == vertices.Length;
			bool hasColors = colors != null && colors.Length == vertices.Length;

			List<Vector3> newVerts = new List<Vector3>(vertices);
			List<Vector3> newNormals = hasNormals ? new List<Vector3>(normals) : null;
			List<Vector2> newUVs = hasUVs ? new List<Vector2>(uv) : null;
			List<Vector2> newUV2s = hasUV2s ? new List<Vector2>(uv2) : null;
			List<Vector4> newTangents = hasTangents ? new List<Vector4>(tangents) : null;
			List<BoneWeight> newBoneWeights = hasBoneWeights ? new List<BoneWeight>(boneWeights) : null;
			List<Color32> newColors = hasColors ? new List<Color32>(colors) : null;

			Dictionary<long, int> midpointCache = new Dictionary<long, int>();
			int subMeshCount = mesh.subMeshCount;
			List<int>[] newTriangles = new List<int>[subMeshCount];

			if (selectedFaces != null)
			{
				var globalFaceIndex = 0;
				for (var i = 0; i < subMeshCount; i++)
				{
					int[] tris = mesh.GetTriangles(i);
					for (var j = 0; j < tris.Length; j += 3)
					{
						if (selectedFaces.Contains(globalFaceIndex))
						{
							GetOrCreateMidpoint(tris[j], tris[j + 1], midpointCache, newVerts, newNormals, newUVs, newUV2s, newTangents, newBoneWeights, newColors, vertices, normals, uv, uv2, tangents, boneWeights, colors, hasNormals, hasUVs, hasUV2s, hasTangents, hasBoneWeights, hasColors);
							GetOrCreateMidpoint(tris[j + 1], tris[j + 2], midpointCache, newVerts, newNormals, newUVs, newUV2s, newTangents, newBoneWeights, newColors, vertices, normals, uv, uv2, tangents, boneWeights, colors, hasNormals, hasUVs, hasUV2s, hasTangents, hasBoneWeights, hasColors);
							GetOrCreateMidpoint(tris[j + 2], tris[j], midpointCache, newVerts, newNormals, newUVs, newUV2s, newTangents, newBoneWeights, newColors, vertices, normals, uv, uv2, tangents, boneWeights, colors, hasNormals, hasUVs, hasUV2s, hasTangents, hasBoneWeights, hasColors);
						}
						globalFaceIndex++;
					}
				}
			}

			var inputFaceIndex = 0;
			var outputFaceIndex = 0;
			HashSet<int> nextSelectedFaces = (selectedFaces != null) ? new HashSet<int>() : null;

			for (var k = 0; k < subMeshCount; k++)
			{
				int[] tris = mesh.GetTriangles(k);
				var outTris = new List<int>(tris.Length * 4);
				for (var l = 0; l < tris.Length; l += 3)
				{
					int v0 = tris[l];
					int v1 = tris[l + 1];
					int v2 = tris[l + 2];

					if (selectedFaces != null && !selectedFaces.Contains(inputFaceIndex))
					{
						long key01 = MakeEdgeKey(v0, v1);
						long key12 = MakeEdgeKey(v1, v2);
						long key20 = MakeEdgeKey(v2, v0);
						bool has01 = midpointCache.TryGetValue(key01, out int m01);
						bool has12 = midpointCache.TryGetValue(key12, out int m12);
						bool has20 = midpointCache.TryGetValue(key20, out int m20);
						if (!has01 && !has12 && !has20)
						{
							outTris.Add(v0); outTris.Add(v1); outTris.Add(v2);
							outputFaceIndex++;
						}
						else
						{
							outputFaceIndex += EmitBorderTriangles(outTris, v0, v1, v2, has01, has12, has20, m01, m12, m20);
						}
						inputFaceIndex++;
					}
					else
					{
						int m01 = GetOrCreateMidpoint(v0, v1, midpointCache, newVerts, newNormals, newUVs, newUV2s, newTangents, newBoneWeights, newColors, vertices, normals, uv, uv2, tangents, boneWeights, colors, hasNormals, hasUVs, hasUV2s, hasTangents, hasBoneWeights, hasColors);
						int m12 = GetOrCreateMidpoint(v1, v2, midpointCache, newVerts, newNormals, newUVs, newUV2s, newTangents, newBoneWeights, newColors, vertices, normals, uv, uv2, tangents, boneWeights, colors, hasNormals, hasUVs, hasUV2s, hasTangents, hasBoneWeights, hasColors);
						int m20 = GetOrCreateMidpoint(v2, v0, midpointCache, newVerts, newNormals, newUVs, newUV2s, newTangents, newBoneWeights, newColors, vertices, normals, uv, uv2, tangents, boneWeights, colors, hasNormals, hasUVs, hasUV2s, hasTangents, hasBoneWeights, hasColors);
						outTris.Add(v0); outTris.Add(m01); outTris.Add(m20);
						outTris.Add(m01); outTris.Add(v1); outTris.Add(m12);
						outTris.Add(m20); outTris.Add(m12); outTris.Add(v2);
						outTris.Add(m01); outTris.Add(m12); outTris.Add(m20);
						if (nextSelectedFaces != null)
						{
							nextSelectedFaces.Add(outputFaceIndex);
							nextSelectedFaces.Add(outputFaceIndex + 1);
							nextSelectedFaces.Add(outputFaceIndex + 2);
							nextSelectedFaces.Add(outputFaceIndex + 3);
						}
						inputFaceIndex++;
						outputFaceIndex += 4;
					}
				}
				newTriangles[k] = outTris;
			}

			outputSelectedFaces = nextSelectedFaces;
			int newVertCount = newVerts.Count;

#if KK
			if (newVertCount > 65535)
			{
				ShapeEditorPlugin.Logger.LogWarning(string.Format("Subdivide would produce {0} vertices (>65535 limit on KK), aborting", newVertCount));
				return false;
			}
#endif

			try
			{
				mesh.Clear();
#if KKS
				if (newVertCount > 65535)
					mesh.indexFormat = IndexFormat.UInt32;
#endif
				mesh.vertices = newVerts.ToArray();
				if (newNormals != null) mesh.normals = newNormals.ToArray();
				if (newUVs != null) mesh.uv = newUVs.ToArray();
				if (newUV2s != null) mesh.uv2 = newUV2s.ToArray();
				if (newTangents != null) mesh.tangents = newTangents.ToArray();
				if (newBoneWeights != null) mesh.boneWeights = newBoneWeights.ToArray();
				if (newColors != null) mesh.colors32 = newColors.ToArray();
				if (bindposes != null && bindposes.Length != 0) mesh.bindposes = bindposes;
				mesh.subMeshCount = subMeshCount;
				for (var n = 0; n < subMeshCount; n++)
					mesh.SetTriangles(newTriangles[n].ToArray(), n);
				mesh.RecalculateBounds();
				if (newNormals == null)
					mesh.RecalculateNormals();
				return true;
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogError("Subdivide failed during mesh write: " + ex.Message);
				return false;
			}
		}

		private static int EmitBorderTriangles(List<int> tris, int v0, int v1, int v2, bool has01, bool has12, bool has20, int m01, int m12, int m20)
		{
			int edgeCount = (has01 ? 1 : 0) + (has12 ? 1 : 0) + (has20 ? 1 : 0);
			switch (edgeCount)
			{
				case 3:
					tris.Add(v0); tris.Add(m01); tris.Add(m20);
					tris.Add(m01); tris.Add(v1); tris.Add(m12);
					tris.Add(m20); tris.Add(m12); tris.Add(v2);
					tris.Add(m01); tris.Add(m12); tris.Add(m20);
					return 4;
				case 1:
				{
					if (has01)
					{
						tris.Add(v0); tris.Add(m01); tris.Add(v2);
						tris.Add(m01); tris.Add(v1); tris.Add(v2);
					}
					else if (has12)
					{
						tris.Add(v0); tris.Add(v1); tris.Add(m12);
						tris.Add(v0); tris.Add(m12); tris.Add(v2);
					}
					else
					{
						tris.Add(v0); tris.Add(v1); tris.Add(m20);
						tris.Add(m20); tris.Add(v1); tris.Add(v2);
					}
					return 2;
				}
			}

			if (has01 && has12)
			{
				tris.Add(v0); tris.Add(m01); tris.Add(m12);
				tris.Add(v0); tris.Add(m12); tris.Add(v2);
				tris.Add(m01); tris.Add(v1); tris.Add(m12);
				return 3;
			}
			if (has12 && has20)
			{
				tris.Add(v0); tris.Add(v1); tris.Add(m12);
				tris.Add(v0); tris.Add(m12); tris.Add(m20);
				tris.Add(m12); tris.Add(v2); tris.Add(m20);
				return 3;
			}
			// has01 && has20
			tris.Add(v0); tris.Add(m01); tris.Add(m20);
			tris.Add(m01); tris.Add(v1); tris.Add(v2);
			tris.Add(m01); tris.Add(v2); tris.Add(m20);
			return 3;
		}

		private static long MakeEdgeKey(int a, int b)
		{
			long lo = a < b ? a : b;
			int hi = a < b ? b : a;
			return lo << 32 | (uint)hi;
		}

		private static int GetOrCreateMidpoint(
			int a, int b, Dictionary<long, int> cache,
			List<Vector3> newVerts, List<Vector3> newNormals, List<Vector2> newUVs, List<Vector2> newUV2s,
			List<Vector4> newTangents, List<BoneWeight> newBoneWeights, List<Color32> newColors,
			Vector3[] verts, Vector3[] normals, Vector2[] uvs, Vector2[] uv2s, Vector4[] tangents,
			BoneWeight[] boneWeights, Color32[] colors,
			bool hasNormals, bool hasUVs, bool hasUV2s, bool hasTangents, bool hasBoneWeights, bool hasColors)
		{
			long key = MakeEdgeKey(a, b);
			if (cache.TryGetValue(key, out int midIndex))
				return midIndex;

			if (a >= 0 && a < verts.Length && b >= 0 && b < verts.Length)
			{
				midIndex = newVerts.Count;
				newVerts.Add((verts[a] + verts[b]) * 0.5f);
				if (hasNormals) newNormals.Add(((normals[a] + normals[b]) * 0.5f).normalized);
				if (hasUVs) newUVs.Add((uvs[a] + uvs[b]) * 0.5f);
				if (hasUV2s) newUV2s.Add((uv2s[a] + uv2s[b]) * 0.5f);
				if (hasTangents)
				{
					Vector4 t = (tangents[a] + tangents[b]) * 0.5f;
					Vector3 tDir = new Vector3(t.x, t.y, t.z).normalized;
					newTangents.Add(new Vector4(tDir.x, tDir.y, tDir.z, tangents[a].w));
				}
				if (hasBoneWeights) newBoneWeights.Add(LerpBoneWeight(boneWeights[a], boneWeights[b]));
				if (hasColors) newColors.Add(LerpColor32(colors[a], colors[b]));
				cache[key] = midIndex;
				return midIndex;
			}

			ShapeEditorPlugin.Logger.LogWarning(
				$"Subdivide: vertex index out of range (a={a}, b={b}, verts={verts.Length}), skipping midpoint");
			if (a >= 0 && a < verts.Length) return a;
			if (b >= 0 && b < verts.Length) return b;
			return 0;
		}

		private static Color32 LerpColor32(Color32 a, Color32 b)
		{
			return new Color32((byte)((a.r + b.r) / 2), (byte)((a.g + b.g) / 2), (byte)((a.b + b.b) / 2), (byte)((a.a + b.a) / 2));
		}

		private static BoneWeight LerpBoneWeight(BoneWeight a, BoneWeight b)
		{
			Dictionary<int, float> accum = new Dictionary<int, float>();
			AddSlot(accum, a.boneIndex0, a.weight0 * 0.5f);
			AddSlot(accum, a.boneIndex1, a.weight1 * 0.5f);
			AddSlot(accum, a.boneIndex2, a.weight2 * 0.5f);
			AddSlot(accum, a.boneIndex3, a.weight3 * 0.5f);
			AddSlot(accum, b.boneIndex0, b.weight0 * 0.5f);
			AddSlot(accum, b.boneIndex1, b.weight1 * 0.5f);
			AddSlot(accum, b.boneIndex2, b.weight2 * 0.5f);
			AddSlot(accum, b.boneIndex3, b.weight3 * 0.5f);

			List<KeyValuePair<int, float>> sorted = new List<KeyValuePair<int, float>>(accum);
			sorted.Sort((x, y) => y.Value.CompareTo(x.Value));

			BoneWeight result = default(BoneWeight);
			var total = 0f;
			if (sorted.Count > 0) { result.boneIndex0 = sorted[0].Key; result.weight0 = sorted[0].Value; total += result.weight0; }
			if (sorted.Count > 1) { result.boneIndex1 = sorted[1].Key; result.weight1 = sorted[1].Value; total += result.weight1; }
			if (sorted.Count > 2) { result.boneIndex2 = sorted[2].Key; result.weight2 = sorted[2].Value; total += result.weight2; }
			if (sorted.Count > 3) { result.boneIndex3 = sorted[3].Key; result.weight3 = sorted[3].Value; total += result.weight3; }

			if (!(total > 0f)) return result;
			float invTotal = 1f / total;
			result.weight0 *= invTotal;
			result.weight1 *= invTotal;
			result.weight2 *= invTotal;
			result.weight3 *= invTotal;
			return result;
		}

		private static void AddSlot(Dictionary<int, float> dict, int boneIndex, float weight)
		{
			if (weight < 0.0001f)
				return;
			if (dict.TryGetValue(boneIndex, out float existing))
				dict[boneIndex] = existing + weight;
			else
				dict[boneIndex] = weight;
		}

		public static void ResetLayersForNewVertexCount(DeformData deformData, int newVertexCount)
		{
			if (deformData == null)
				return;

			var hadData = false;
			foreach (DeformLayer layer in deformData.Layers)
			{
				if (layer.Deltas != null && layer.Deltas.Length != 0)
				{
					if (layer.Deltas.Any(t => t != Vector3.zero))
					{
						hadData = true;
					}
				}
				if (hadData)
					break;
			}

			foreach (DeformLayer layer in deformData.Layers)
			{
				layer.Deltas = new Vector3[newVertexCount];
				layer.Dirty = true;
			}

			if (hadData)
				ShapeEditorPlugin.Logger.LogWarning("Subdivision changed vertex count — all layer deltas have been reset");
		}
	}
}
