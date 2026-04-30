using System.Collections.Generic;
using UnityEngine;

namespace KKShapeEditor
{
	public static class WeightRemapper
	{
		private const int K = 4;

		public static BoneWeight[] ComputeRemappedWeights(
			Vector3[] clothBindVerts, Vector3[] clothDeltas, Transform[] clothBones,
			Vector3[] bodyVerts, BoneWeight[] bodyWeights, BoneWeight[] clothOrigWeights,
			Transform[] bodyBones, int[] bodyTriangles)
		{
			if (clothBindVerts == null || clothDeltas == null || clothBones == null ||
				bodyVerts == null || bodyWeights == null || bodyBones == null)
				return null;
			if (clothBindVerts.Length != clothDeltas.Length)
				return null;

			Dictionary<int, int> boneMap = BuildBoneIndexMap(bodyBones, clothBones);
			Bounds bounds = ComputeBounds(bodyVerts);
			SpatialHashGrid grid = new SpatialHashGrid(bodyVerts, bounds);
			float radius = ComputeAverageEdgeLength(bodyVerts, bodyTriangles) * 3f;

			int clothVertCount = clothBindVerts.Length;
			var result = new BoneWeight[clothVertCount];
			var nearestIndices = new int[K];
			var nearestDistSq = new float[K];
			var nearestWeights = new BoneWeight[K];

			for (var i = 0; i < clothVertCount; i++)
			{
				Vector3 queryPos = clothBindVerts[i] + clothDeltas[i];
				int found = FindKNearest(grid, queryPos, radius, nearestIndices, nearestDistSq);
				if (found == 0)
				{
					if (clothOrigWeights != null && i < clothOrigWeights.Length)
						result[i] = clothOrigWeights[i];
				}
				else
				{
					for (var j = 0; j < found; j++)
						nearestWeights[j] = RemapBoneWeight(bodyWeights[nearestIndices[j]], boneMap);
					result[i] = BlendBoneWeights(nearestWeights, nearestDistSq, found);
				}
			}
			return result;
		}

		private static Dictionary<int, int> BuildBoneIndexMap(Transform[] bodyBones, Transform[] clothBones)
		{
			var clothBoneToIndex = new Dictionary<Transform, int>(clothBones.Length);
			for (var i = 0; i < clothBones.Length; i++)
			{
				if (clothBones[i])
					clothBoneToIndex[clothBones[i]] = i;
			}

			var boneMap = new Dictionary<int, int>(bodyBones.Length);
			for (var j = 0; j < bodyBones.Length; j++)
			{
				if (bodyBones[j] && clothBoneToIndex.TryGetValue(bodyBones[j], out int clothIndex))
					boneMap[j] = clothIndex;
			}
			return boneMap;
		}

		private static BoneWeight BlendBoneWeights(BoneWeight[] weights, float[] distSq, int count)
		{
			if (count == 1)
				return weights[0];

			var invDist = new float[count];
			var totalInvDist = 0f;
			for (var i = 0; i < count; i++)
			{
				float dist = Mathf.Sqrt(distSq[i]);
				if (dist < 1E-08f)
					return weights[i];
				invDist[i] = 1f / dist;
				totalInvDist += invDist[i];
			}

			float invTotal = 1f / totalInvDist;
			for (var j = 0; j < count; j++)
				invDist[j] *= invTotal;

			var boneAccum = new Dictionary<int, float>(count * 4);
			for (var k = 0; k < count; k++)
			{
				float blendWeight = invDist[k];
				BoneWeight bw = weights[k];
				if (bw.weight0 > 0f) AddWeight(boneAccum, bw.boneIndex0, bw.weight0 * blendWeight);
				if (bw.weight1 > 0f) AddWeight(boneAccum, bw.boneIndex1, bw.weight1 * blendWeight);
				if (bw.weight2 > 0f) AddWeight(boneAccum, bw.boneIndex2, bw.weight2 * blendWeight);
				if (bw.weight3 > 0f) AddWeight(boneAccum, bw.boneIndex3, bw.weight3 * blendWeight);
			}

			var topIndices = new int[4];
			var topWeights = new float[4];
			foreach (KeyValuePair<int, float> pair in boneAccum)
			{
				int slot = -1;
				float minWeight = pair.Value;
				for (var l = 0; l < 4; l++)
				{
					if (!(topWeights[l] < minWeight)) continue;
					minWeight = topWeights[l];
					slot = l;
				}

				if (slot < 0) continue;
				topIndices[slot] = pair.Key;
				topWeights[slot] = pair.Value;
			}

			float weightSum = topWeights[0] + topWeights[1] + topWeights[2] + topWeights[3];
			if (weightSum > 0f)
			{
				float invSum = 1f / weightSum;
				topWeights[0] *= invSum;
				topWeights[1] *= invSum;
				topWeights[2] *= invSum;
				topWeights[3] *= invSum;
			}

			BoneWeight result = default(BoneWeight);
			result.boneIndex0 = topIndices[0]; result.weight0 = topWeights[0];
			result.boneIndex1 = topIndices[1]; result.weight1 = topWeights[1];
			result.boneIndex2 = topIndices[2]; result.weight2 = topWeights[2];
			result.boneIndex3 = topIndices[3]; result.weight3 = topWeights[3];
			return result;
		}

		private static void AddWeight(Dictionary<int, float> accum, int boneIndex, float weight)
		{
			if (accum.TryGetValue(boneIndex, out float existing))
				accum[boneIndex] = existing + weight;
			else
				accum[boneIndex] = weight;
		}

		private static BoneWeight RemapBoneWeight(BoneWeight bodyBw, Dictionary<int, int> boneMap)
		{
			var indices = new int[4];
			var weights = new float[4];
			var totalWeight = 0f;
			var slotCount = 0;

			float w = bodyBw.weight0;
			if (w > 0f && boneMap.TryGetValue(bodyBw.boneIndex0, out int clothIdx))
			{ indices[slotCount] = clothIdx; weights[slotCount] = w; totalWeight += w; slotCount++; }

			w = bodyBw.weight1;
			if (w > 0f && boneMap.TryGetValue(bodyBw.boneIndex1, out clothIdx))
			{ indices[slotCount] = clothIdx; weights[slotCount] = w; totalWeight += w; slotCount++; }

			w = bodyBw.weight2;
			if (w > 0f && boneMap.TryGetValue(bodyBw.boneIndex2, out clothIdx))
			{ indices[slotCount] = clothIdx; weights[slotCount] = w; totalWeight += w; slotCount++; }

			w = bodyBw.weight3;
			if (w > 0f && boneMap.TryGetValue(bodyBw.boneIndex3, out clothIdx))
			{ indices[slotCount] = clothIdx; weights[slotCount] = w; totalWeight += w; slotCount++; }

			if (slotCount == 0)
				return default;

			float invTotal = (totalWeight > 0f) ? (1f / totalWeight) : 0f;
			BoneWeight result = default;
			if (slotCount > 0) { result.boneIndex0 = indices[0]; result.weight0 = weights[0] * invTotal; }
			if (slotCount > 1) { result.boneIndex1 = indices[1]; result.weight1 = weights[1] * invTotal; }
			if (slotCount > 2) { result.boneIndex2 = indices[2]; result.weight2 = weights[2] * invTotal; }
			if (slotCount > 3) { result.boneIndex3 = indices[3]; result.weight3 = weights[3] * invTotal; }
			return result;
		}

		private static int FindKNearest(SpatialHashGrid grid, Vector3 queryPos, float radius, int[] outIndices, float[] outDistSq)
		{
			var count = 0;
			grid.FindVerticesInRadius(queryPos, radius, (idx, dSq) =>
			{
				int insertAt = count < K ? count : K;
				for (var i = 0; i < count && i < K; i++)
				{
					if (!(dSq < outDistSq[i])) continue;
					insertAt = i;
					break;
				}
				if (insertAt >= K)
					return;
				for (int i = count < K ? count : K - 1; i > insertAt; i--)
				{
					outIndices[i] = outIndices[i - 1];
					outDistSq[i] = outDistSq[i - 1];
				}
				outIndices[insertAt] = idx;
				outDistSq[insertAt] = dSq;
				if (count < K)
					count++;
			});
			return count;
		}

		private static float ComputeAverageEdgeLength(Vector3[] vertices, int[] triangles)
		{
			if (vertices == null || triangles == null || triangles.Length < 3)
				return 0.1f;

			var total = 0.0;
			var edgeCount = 0;
			for (var i = 0; i < triangles.Length; i += 3)
			{
				int v0 = triangles[i];
				int v1 = triangles[i + 1];
				int v2 = triangles[i + 2];
				total += Vector3.Distance(vertices[v0], vertices[v1]);
				total += Vector3.Distance(vertices[v1], vertices[v2]);
				total += Vector3.Distance(vertices[v2], vertices[v0]);
				edgeCount += 3;
			}
			return edgeCount > 0 ? (float)(total / edgeCount) : 0.1f;
		}

		private static Bounds ComputeBounds(Vector3[] vertices)
		{
			if (vertices == null || vertices.Length == 0)
				return new Bounds(Vector3.zero, Vector3.zero);

			Vector3 min = vertices[0];
			Vector3 max = vertices[0];
			for (var i = 1; i < vertices.Length; i++)
			{
				Vector3 v = vertices[i];
				if (v.x < min.x) min.x = v.x;
				if (v.y < min.y) min.y = v.y;
				if (v.z < min.z) min.z = v.z;
				if (v.x > max.x) max.x = v.x;
				if (v.y > max.y) max.y = v.y;
				if (v.z > max.z) max.z = v.z;
			}
			Bounds bounds = default(Bounds);
			bounds.SetMinMax(min, max);
			return bounds;
		}
	}
}
