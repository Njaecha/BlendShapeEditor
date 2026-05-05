using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlendShapeEditor
{
	public class TransformGizmo
	{
		public GizmoMode Mode { get; set; } = GizmoMode.Translate;
		public GizmoSpace Space { get; set; } = GizmoSpace.World;
		public bool SoftSelectionEnabled { get; set; }
		public float SoftSelectionRadius { get; set; } = 0.1f;
		public FalloffMode SoftFalloff { get; set; } = FalloffMode.Smooth;
		public float SharpExponent { get; set; } = 3f;
		public SoftSelectMode SoftMode { get; set; }
		public GizmoEnums HoveredAxis => _hoveredAxis;
		public bool IsDragging => _isDragging;
		public bool HasTarget => _targetIndices != null && _targetIndices.Count > 0;
		public Vector3 CentroidLocal => _centroid;
		public Dictionary<int, float> SoftWeights => _combinedSoftWeights;
		public bool HasMirrorTarget => _mirrorIndices != null && _mirrorIndices.Count > 0;
		public bool SymmetryEnabled { get; set; }
		public int SymmetryAxis { get; set; }
		public float SymmetryCenter { get; set; }
		public ShapeDeformer Deformer { get; set; }
		public Dictionary<int, Vector3> DragStartDeltas => _dragStartDeltas;

		public void SetObjectRoot(Transform root)
		{
			_objectRoot = root;
		}

		public void SetTarget(HashSet<int> indices, Vector3[] vertices, Vector3[] normals)
		{
			_targetIndices = indices;
			_softWeights.Clear();
			_mirrorIndices = null;
			_mirrorWeights.Clear();
			_combinedSoftWeights.Clear();
			_hoveredAxis = GizmoEnums.None;

			if (indices == null || indices.Count == 0 || vertices == null)
			{
				_centroid = Vector3.zero;
				_localOrientation = Quaternion.identity;
				return;
			}

			Vector3 sum = Vector3.zero;
			var count = 0;
			foreach (int idx in indices.Where(idx => idx >= 0 && idx < vertices.Length))
			{
				sum += vertices[idx];
				count++;
			}
			_centroid = count > 0 ? sum / count : Vector3.zero;

			foreach (int key in indices)
				_softWeights[key] = 1f;

			ComputeLocalOrientation(indices, normals);
			RebuildCombinedWeights();
		}

		public void UpdateCentroid(Vector3[] vertices)
		{
			if (_targetIndices == null || vertices == null)
				return;

			Vector3 sum = Vector3.zero;
			var count = 0;
			foreach (int idx in _targetIndices.Where(idx => idx >= 0 && idx < vertices.Length))
			{
				sum += vertices[idx];
				count++;
			}
			if (count > 0)
				_centroid = sum / count;
		}

		private void ComputeLocalOrientation(HashSet<int> indices, Vector3[] normals)
		{
			if (normals == null)
			{
				_localOrientation = Quaternion.identity;
				return;
			}

			Vector3 avgNormal = indices.Where(idx => idx >= 0 && idx < normals.Length).Aggregate(Vector3.zero, (current, idx) => current + normals[idx]);

			if (avgNormal.sqrMagnitude < 0.001f)
			{
				_localOrientation = Quaternion.identity;
				return;
			}

			avgNormal.Normalize();
			Vector3 tangent = Vector3.Cross(Vector3.up, avgNormal);
			if (tangent.sqrMagnitude < 0.001f)
				tangent = Vector3.Cross(Vector3.right, avgNormal);
			tangent.Normalize();
			Vector3 bitangent = Vector3.Cross(avgNormal, tangent).normalized;
			_localOrientation = Quaternion.LookRotation(bitangent, avgNormal);
		}

		public void SetMirrorTarget(HashSet<int> mirrorIndices)
		{
			_mirrorIndices = mirrorIndices;
			_mirrorWeights.Clear();
			if (mirrorIndices == null || mirrorIndices.Count == 0)
				return;

			foreach (int key in mirrorIndices)
				_mirrorWeights[key] = 1f;

			RebuildCombinedWeights();
		}

		public void ClearMirrorTarget()
		{
			_mirrorIndices = null;
			_mirrorWeights.Clear();
			RebuildCombinedWeights();
		}

		private void RebuildCombinedWeights()
		{
			_combinedSoftWeights.Clear();
			foreach (KeyValuePair<int, float> pair in _softWeights)
				_combinedSoftWeights[pair.Key] = pair.Value;

			foreach (KeyValuePair<int, float> pair in _mirrorWeights)
			{
				if (!_combinedSoftWeights.TryGetValue(pair.Key, out float existing) || pair.Value > existing)
					_combinedSoftWeights[pair.Key] = pair.Value;
			}
		}

		public void ComputeSoftWeights(Vector3[] vertices, SpatialHashGrid grid, List<int>[] adjacency = null)
		{
			_softWeights.Clear();
			if (_targetIndices != null)
			{
				foreach (int key in _targetIndices)
					_softWeights[key] = 1f;
			}

			if (!SoftSelectionEnabled || vertices == null || SoftSelectionRadius <= 0f || _targetIndices == null)
				return;

			if (SoftMode == SoftSelectMode.Surface && adjacency != null)
				ComputeSoftWeightsSurface(vertices, adjacency);
			else if (grid != null)
				ComputeSoftWeightsVolume(vertices, grid);

			RebuildCombinedWeights();
		}

		private void ComputeSoftWeightsVolume(Vector3[] vertices, SpatialHashGrid grid)
		{
			float r = SoftSelectionRadius;
			float rSq = r * r;
			Vector3 boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

			var srcPositions = new Vector3[_targetIndices.Count];
			var srcCount = 0;
			foreach (Vector3 v in from idx in _targetIndices where idx >= 0 && idx < vertices.Length select vertices[idx])
			{
				srcPositions[srcCount++] = v;
				if (v.x < boundsMin.x) boundsMin.x = v.x;
				if (v.y < boundsMin.y) boundsMin.y = v.y;
				if (v.z < boundsMin.z) boundsMin.z = v.z;
				if (v.x > boundsMax.x) boundsMax.x = v.x;
				if (v.y > boundsMax.y) boundsMax.y = v.y;
				if (v.z > boundsMax.z) boundsMax.z = v.z;
			}
			if (srcCount == 0)
				return;

			boundsMin.x -= r; boundsMin.y -= r; boundsMin.z -= r;
			boundsMax.x += r; boundsMax.y += r; boundsMax.z += r;

			int capturedCount = srcCount;
			grid.FindVerticesInBounds(boundsMin, boundsMax, idx =>
			{
				if (_targetIndices.Contains(idx))
					return;

				var bestDistSq = float.MaxValue;
				for (var i = 0; i < capturedCount; i++)
				{
					float dSq = (vertices[idx] - srcPositions[i]).sqrMagnitude;
					if (dSq < bestDistSq)
						bestDistSq = dSq;
				}
				if (bestDistSq > rSq)
					return;

				float falloff = CalculateFalloff(Mathf.Sqrt(bestDistSq) / r);
				if (falloff <= 0.001f)
					return;

				if (!_softWeights.TryGetValue(idx, out float existing) || falloff > existing)
					_softWeights[idx] = falloff;
			});
		}

		private void ComputeSoftWeightsSurface(Vector3[] vertices, List<int>[] adjacency)
		{
			float radius = SoftSelectionRadius;
			var visited = new Dictionary<int, float>();
			var queue = new Queue<KeyValuePair<int, float>>();

			foreach (int idx in _targetIndices.Where(idx => idx >= 0 && idx < vertices.Length))
			{
				visited[idx] = 0f;
				queue.Enqueue(new KeyValuePair<int, float>(idx, 0f));
			}

			while (queue.Count > 0)
			{
				KeyValuePair<int, float> current = queue.Dequeue();
				int node = current.Key;
				float dist = current.Value;

				if (node < 0 || node >= adjacency.Length || adjacency[node] == null)
					continue;

				List<int> neighbors = adjacency[node];
				foreach (int neighbor in neighbors)
				{
					if (neighbor < 0 || neighbor >= vertices.Length)
						continue;

					float edgeDist = Vector3.Distance(vertices[node], vertices[neighbor]);
					float newDist = dist + edgeDist;
					if (!(newDist <= radius) || (visited.TryGetValue(neighbor, out float existingDist) &&
					                             !(existingDist > newDist))) continue;
					visited[neighbor] = newDist;
					if (!_targetIndices.Contains(neighbor))
					{
						float falloff = CalculateFalloff(newDist / radius);
						if (falloff > 0.001f && (!_softWeights.TryGetValue(neighbor, out float existingWeight) || falloff > existingWeight))
							_softWeights[neighbor] = falloff;
					}
					queue.Enqueue(new KeyValuePair<int, float>(neighbor, newDist));
				}
			}
		}

		public void ComputeMirrorSoftWeights(Vector3[] vertices, SpatialHashGrid grid, List<int>[] adjacency = null)
		{
			_mirrorWeights.Clear();
			if (_mirrorIndices == null || _mirrorIndices.Count == 0)
			{
				RebuildCombinedWeights();
				return;
			}

			foreach (int key in _mirrorIndices)
				_mirrorWeights[key] = 1f;

			if (!SoftSelectionEnabled || vertices == null || SoftSelectionRadius <= 0f)
			{
				RebuildCombinedWeights();
				return;
			}

			float r = SoftSelectionRadius;
			if (SoftMode == SoftSelectMode.Surface && adjacency != null)
			{
				var visited = new Dictionary<int, float>();
				var queue = new Queue<KeyValuePair<int, float>>();

				foreach (int idx in _mirrorIndices.Where(idx => idx >= 0 && idx < vertices.Length))
				{
					visited[idx] = 0f;
					queue.Enqueue(new KeyValuePair<int, float>(idx, 0f));
				}

				while (queue.Count > 0)
				{
					KeyValuePair<int, float> current = queue.Dequeue();
					int node = current.Key;
					float dist = current.Value;

					if (node < 0 || node >= adjacency.Length || adjacency[node] == null)
						continue;

					List<int> neighbors = adjacency[node];
					foreach (int neighbor in neighbors)
					{
						if (neighbor < 0 || neighbor >= vertices.Length)
							continue;

						float edgeDist = Vector3.Distance(vertices[node], vertices[neighbor]);
						float newDist = dist + edgeDist;
						if (!(newDist <= r) || (visited.TryGetValue(neighbor, out float existingDist) &&
						                        !(existingDist > newDist))) continue;
						visited[neighbor] = newDist;
						if (!_mirrorIndices.Contains(neighbor))
						{
							float falloff = CalculateFalloff(newDist / r);
							if (falloff > 0.001f && (!_mirrorWeights.TryGetValue(neighbor, out float existingWeight) || falloff > existingWeight))
								_mirrorWeights[neighbor] = falloff;
						}
						queue.Enqueue(new KeyValuePair<int, float>(neighbor, newDist));
					}
				}
			}
			else if (grid != null)
			{
				foreach (Vector3 queryPos in from mirrorIdx in _mirrorIndices where mirrorIdx >= 0 && mirrorIdx < vertices.Length select vertices[mirrorIdx])
				{
					grid.FindVerticesInRadius(queryPos, r, (idx, distSq) =>
					{
						if (_mirrorIndices.Contains(idx))
							return;

						float falloff = CalculateFalloff(Mathf.Sqrt(distSq) / r);
						if (falloff <= 0.001f)
							return;

						if (!_mirrorWeights.TryGetValue(idx, out float existingWeight) || falloff > existingWeight)
							_mirrorWeights[idx] = falloff;
					});
				}
			}

			RebuildCombinedWeights();
		}

		public float CalculateFalloff(float normalizedDist)
		{
			normalizedDist = Mathf.Clamp01(normalizedDist);
			switch (SoftFalloff)
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

		public void UpdateHover(Vector2 mouseScreen, Camera cam, Transform xform)
		{
			if (!HasTarget || !cam)
			{
				_hoveredAxis = GizmoEnums.None;
				return;
			}
			if (_isDragging)
				return;

			Vector3 centroidWorld = ToWorld(xform, _centroid);
			float s = GizmoScale(cam, centroidWorld);

			if (Mode != GizmoMode.Rotate)
			{
				float halfSize = 0.09f * s;
				Vector3 freeCorner = centroidWorld + cam.transform.right * halfSize + cam.transform.up * halfSize;
				float freeRadius = Vector2.Distance(WorldToScreen(cam, centroidWorld), WorldToScreen(cam, freeCorner));
				if (Vector2.Distance(mouseScreen, WorldToScreen(cam, centroidWorld)) <= freeRadius)
				{
					_hoveredAxis = GizmoEnums.Free;
					return;
				}
			}

			if (Mode == GizmoMode.Translate)
			{
				GizmoEnums planeHit = TestPlaneHandleHit(mouseScreen, cam, centroidWorld, xform, s);
				if (planeHit != GizmoEnums.None)
				{
					_hoveredAxis = planeHit;
					return;
				}
			}

			var bestDist = 14f;
			GizmoEnums hoveredAxis = GizmoEnums.None;
			if (Mode == GizmoMode.Rotate)
			{
				TestRingHit(mouseScreen, cam, centroidWorld, cam.transform.forward, s, ref bestDist, ref hoveredAxis, GizmoEnums.ViewRotate, 1.1f);
				TestRingHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.X, xform), s, ref bestDist, ref hoveredAxis, GizmoEnums.X, 0.85f);
				TestRingHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Y, xform), s, ref bestDist, ref hoveredAxis, GizmoEnums.Y, 0.85f);
				TestRingHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Z, xform), s, ref bestDist, ref hoveredAxis, GizmoEnums.Z, 0.85f);
			}
			else
			{
				TestAxisHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.X, xform), s, ref bestDist, ref hoveredAxis, GizmoEnums.X);
				TestAxisHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Y, xform), s, ref bestDist, ref hoveredAxis, GizmoEnums.Y);
				TestAxisHit(mouseScreen, cam, centroidWorld, AxisW(GizmoEnums.Z, xform), s, ref bestDist, ref hoveredAxis, GizmoEnums.Z);
			}
			_hoveredAxis = hoveredAxis;
		}

		private GizmoEnums TestPlaneHandleHit(Vector2 mp, Camera cam, Vector3 wc, Transform xform, float s)
		{
			var planes = new GizmoEnums[] { GizmoEnums.XY, GizmoEnums.XZ, GizmoEnums.YZ };
			for (var i = 0; i < 3; i++)
			{
				GetPlaneAxes(planes[i], xform, out Vector3 ax1, out Vector3 ax2);
				Vector2 a = WorldToScreen(cam, wc + 0.25f * s * ax1 + 0.25f * s * ax2);
				Vector2 b = WorldToScreen(cam, wc + 0.5f * s * ax1 + 0.25f * s * ax2);
				Vector2 c = WorldToScreen(cam, wc + 0.5f * s * ax1 + 0.5f * s * ax2);
				Vector2 d = WorldToScreen(cam, wc + 0.25f * s * ax1 + 0.5f * s * ax2);
				if (PointInQuad(mp, a, b, c, d))
					return planes[i];
			}
			return GizmoEnums.None;
		}

		private void GetPlaneAxes(GizmoEnums plane, Transform xform, out Vector3 ax1, out Vector3 ax2)
		{
			switch (plane)
			{
				case GizmoEnums.XY:
					ax1 = AxisW(GizmoEnums.X, xform);
					ax2 = AxisW(GizmoEnums.Y, xform);
					break;
				case GizmoEnums.XZ:
					ax1 = AxisW(GizmoEnums.X, xform);
					ax2 = AxisW(GizmoEnums.Z, xform);
					break;
				case GizmoEnums.None:
				case GizmoEnums.X:
				case GizmoEnums.Y:
				case GizmoEnums.Z:
				case GizmoEnums.YZ:
				case GizmoEnums.Free:
				case GizmoEnums.ViewRotate:
				default:
					ax1 = AxisW(GizmoEnums.Y, xform);
					ax2 = AxisW(GizmoEnums.Z, xform);
					break;
			}
		}

		private static bool PointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
		{
			return PointInTriangle(p, a, b, c) || PointInTriangle(p, a, c, d);
		}

		private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
		{
			float d0 = Cross2D(p - a, b - a);
			float d1 = Cross2D(p - b, c - b);
			float d2 = Cross2D(p - c, a - c);
			bool hasNeg = d0 < 0f || d1 < 0f || d2 < 0f;
			bool hasPos = d0 > 0f || d1 > 0f || d2 > 0f;
			return !hasNeg || !hasPos;
		}

		private static float Cross2D(Vector2 a, Vector2 b)
		{
			return a.x * b.y - a.y * b.x;
		}

		private void TestAxisHit(Vector2 mp, Camera cam, Vector3 c, Vector3 dir, float s, ref float best, ref GizmoEnums bestA, GizmoEnums axis)
		{
			Vector2 a = WorldToScreen(cam, c);
			Vector2 b = WorldToScreen(cam, c + AXIS_LEN * s * dir);
			float dist = PointToSegmentDist(mp, a, b);
			if (!(dist < best)) return;
			best = dist;
			bestA = axis;
		}

		private void TestRingHit(Vector2 mp, Camera cam, Vector3 c, Vector3 normal, float s, ref float best, ref GizmoEnums bestA, GizmoEnums axis, float ringRadius)
		{
			Perpendiculars(normal, out Vector3 tan, out Vector3 bin);
			float r = ringRadius * s;
			var bestDist = float.MaxValue;
			for (var i = 0; i < RING_SEG; i++)
			{
				float angle = (float)i / RING_SEG * Mathf.PI * 2f;
				Vector3 p = c + (tan * Mathf.Cos(angle) + bin * Mathf.Sin(angle)) * r;
				float d = Vector2.Distance(mp, WorldToScreen(cam, p));
				if (d < bestDist)
					bestDist = d;
			}

			if (!(bestDist < best)) return;
			best = bestDist;
			bestA = axis;
		}

		public bool BeginDrag(Vector2 mouseScreen, Camera cam, Transform xform, DeformLayer layer, Vector3[] vertices)
		{
			if (_hoveredAxis == GizmoEnums.None || !HasTarget || layer == null)
				return false;

			_activeAxis = _hoveredAxis;
			_isDragging = true;
			_dragStartScreen = mouseScreen;
			_dragStartCentroidWorld = ToWorld(xform, _centroid);
			_dragStartDeltas.Clear();
			_dragStartPositions.Clear();

			Vector3[] deltas = layer.Deltas;
			foreach (int key in _combinedSoftWeights.Select(pair => pair.Key).Where(key => key >= 0 && key < deltas.Length && key < vertices.Length))
			{
				_dragStartDeltas[key] = deltas[key];
				_dragStartPositions[key] = vertices[key];
			}

			if (!HasMirrorTarget || !xform) return true;
			Vector3 mirrorCentroid = _centroid;
			switch (SymmetryAxis)
			{
				case 0:
					mirrorCentroid.x = SymmetryCenter * 2f - mirrorCentroid.x;
					break;
				case 1:
					mirrorCentroid.y = SymmetryCenter * 2f - mirrorCentroid.y;
					break;
				default:
					mirrorCentroid.z = SymmetryCenter * 2f - mirrorCentroid.z;
					break;
			}
			_dragStartMirrorCentroidWorld = ToWorld(xform, mirrorCentroid);
			_mirrorCentroid = mirrorCentroid;
			return true;
		}

		public void UpdateDrag(Vector2 mouseScreen, Camera cam, Transform xform, DeformLayer layer, Vector3[] vertices)
		{
			if (!_isDragging || layer == null || !cam)
				return;

			switch (Mode)
			{
				case GizmoMode.Translate:
					ApplyTranslate(mouseScreen, cam, xform, layer.Deltas);
					break;
				case GizmoMode.Rotate:
					ApplyRotate(mouseScreen, cam, xform, layer.Deltas);
					break;
				case GizmoMode.Scale:
					ApplyScale(mouseScreen, cam, xform, layer.Deltas);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			layer.Dirty = true;
			UpdateCentroid(vertices);
		}

		public void EndDrag()
		{
			_isDragging = false;
			_activeAxis = GizmoEnums.None;
		}

		private void ApplyTranslate(Vector2 ms, Camera cam, Transform xform, Vector3[] deltas)
		{
			Vector3 dispWorld;
			switch (_activeAxis)
			{
				case GizmoEnums.Free:
				{
					Vector3 startHit = RayPlaneIntersect(cam, _dragStartScreen, _dragStartCentroidWorld, cam.transform.forward);
					dispWorld = RayPlaneIntersect(cam, ms, _dragStartCentroidWorld, cam.transform.forward) - startHit;
					break;
				}
				case GizmoEnums.XY:
				case GizmoEnums.XZ:
				case GizmoEnums.YZ:
				{
					Vector3 planeNormal = GetPlaneNormal(_activeAxis, xform);
					Vector3 startHit = RayPlaneIntersect(cam, _dragStartScreen, _dragStartCentroidWorld, planeNormal);
					dispWorld = RayPlaneIntersect(cam, ms, _dragStartCentroidWorld, planeNormal) - startHit;
					break;
				}
				case GizmoEnums.None:
				case GizmoEnums.X:
				case GizmoEnums.Y:
				case GizmoEnums.Z:
				case GizmoEnums.ViewRotate:
				default:
				{
					Vector3 axisDir = AxisW(_activeAxis, xform);
					Vector3 startPt = ClosestPointOnAxis(cam, _dragStartScreen, _dragStartCentroidWorld, axisDir);
					dispWorld = ClosestPointOnAxis(cam, ms, _dragStartCentroidWorld, axisDir) - startPt;
					break;
				}
			}

			foreach (KeyValuePair<int, float> pair in _softWeights)
			{
				int key = pair.Key;
				if (!_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) || key < 0 ||
				    key >= deltas.Length) continue;
				Vector3 bindDelta = WorldToBindDelta(key, dispWorld, xform);
				deltas[key] = startDelta + bindDelta * pair.Value;
			}

			if (HasMirrorTarget && SymmetryEnabled)
				ApplyMirrorTranslate(dispWorld, xform, deltas);
		}

		private Vector3 WorldToBindDelta(int vertexIdx, Vector3 worldDisp, Transform xform)
		{
			if (!Deformer) return xform ? xform.InverseTransformVector(worldDisp) : worldDisp;
			Deformer.WorldDeltaToBindDelta(vertexIdx, worldDisp, out Vector3 result);
			return result;
		}

		private static Vector3 ClosestPointOnAxis(Camera cam, Vector2 screenPos, Vector3 axisOrigin, Vector3 axisDir)
		{
			Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
			Vector3 w = axisOrigin - ray.origin;
			float a = Vector3.Dot(axisDir, axisDir);
			float b = Vector3.Dot(axisDir, ray.direction);
			float c = Vector3.Dot(ray.direction, ray.direction);
			float d = Vector3.Dot(axisDir, w);
			float denom = a * c - b * b;
			if (Mathf.Abs(denom) < 0.0001f)
				return axisOrigin;
			float t = (b * Vector3.Dot(ray.direction, w) - c * d) / denom;
			return axisOrigin + axisDir * t;
		}

		private void ApplyRotate(Vector2 ms, Camera cam, Transform xform, Vector3[] deltas)
		{
			if (_activeAxis == GizmoEnums.Free || _activeAxis == GizmoEnums.None)
				return;

			Vector2 centroidScreen = WorldToScreen(cam, _dragStartCentroidWorld);
			Vector2 fromVec = _dragStartScreen - centroidScreen;
			Vector2 toVec = ms - centroidScreen;
			if (fromVec.sqrMagnitude < 1f || toVec.sqrMagnitude < 1f)
				return;

			float angle = Mathf.Atan2(toVec.y, toVec.x) - Mathf.Atan2(fromVec.y, fromVec.x);
			angle *= -Mathf.Rad2Deg;

			Vector3 axisWorld = _activeAxis == GizmoEnums.ViewRotate ? cam.transform.forward : AxisW(_activeAxis, xform);
			if (Vector3.Dot(axisWorld, cam.transform.forward) > 0f)
				angle = -angle;

			Quaternion rot = Quaternion.AngleAxis(angle, axisWorld);
			foreach (KeyValuePair<int, float> pair in _softWeights)
			{
				int key = pair.Key;
				if (!_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) ||
				    !_dragStartPositions.TryGetValue(key, out Vector3 startPos) || key < 0 ||
				    key >= deltas.Length) continue;
				Vector3 fromCenter = (xform ? xform.TransformPoint(startPos) : startPos) - _dragStartCentroidWorld;
				Vector3 worldDisp = Quaternion.Slerp(Quaternion.identity, rot, pair.Value) * fromCenter - fromCenter;
				deltas[key] = startDelta + WorldToBindDelta(key, worldDisp, xform);
			}

			if (HasMirrorTarget && SymmetryEnabled)
				ApplyMirrorRotate(angle, axisWorld, cam, xform, deltas);
		}

		private void ApplyScale(Vector2 ms, Camera cam, Transform xform, Vector3[] deltas)
		{
			float factor;
			bool uniform;
			if (_activeAxis == GizmoEnums.Free)
			{
				Vector2 centroidScreen = WorldToScreen(cam, _dragStartCentroidWorld);
				float startDist = Mathf.Max(Vector2.Distance(_dragStartScreen, centroidScreen), 1f);
				factor = Vector2.Distance(ms, centroidScreen) / startDist;
				uniform = true;
			}
			else
			{
				Vector3 axisDir = AxisW(_activeAxis, xform);
				Vector2 centroidScreen = WorldToScreen(cam, _dragStartCentroidWorld);
				Vector2 axisScreen = WorldToScreen(cam, _dragStartCentroidWorld + axisDir) - centroidScreen;
				if (axisScreen.sqrMagnitude < 0.01f)
					return;
				axisScreen.Normalize();
				float startProj = Vector2.Dot(_dragStartScreen - centroidScreen, axisScreen);
				float curProj = Vector2.Dot(ms - centroidScreen, axisScreen);
				if (Mathf.Abs(startProj) < 1f)
					return;
				factor = curProj / startProj;
				uniform = false;
			}

			Vector3 scaleAxis = uniform ? Vector3.zero : AxisW(_activeAxis, xform);
			foreach (KeyValuePair<int, float> pair in _softWeights)
			{
				int key = pair.Key;
				if (!_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) ||
				    !_dragStartPositions.TryGetValue(key, out Vector3 startPos) || key < 0 ||
				    key >= deltas.Length) continue;
				Vector3 fromCenter = (xform ? xform.TransformPoint(startPos) : startPos) - _dragStartCentroidWorld;
				float scaleFactor = Mathf.Lerp(1f, factor, pair.Value);
				Vector3 scaled;
				if (uniform)
				{
					scaled = fromCenter * scaleFactor;
				}
				else
				{
					float proj = Vector3.Dot(fromCenter, scaleAxis);
					scaled = fromCenter - scaleAxis * proj + scaleAxis * (proj * scaleFactor);
				}
				Vector3 worldDisp = scaled - fromCenter;
				deltas[key] = startDelta + WorldToBindDelta(key, worldDisp, xform);
			}

			if (HasMirrorTarget && SymmetryEnabled)
				ApplyMirrorScale(factor, uniform, scaleAxis, xform, deltas);
		}

		private void ApplyMirrorTranslate(Vector3 dispWorld, Transform xform, Vector3[] deltas)
		{
			Vector3 mirroredDisp = MirrorWorldVector(dispWorld, xform);
			foreach (KeyValuePair<int, float> pair in _mirrorWeights)
			{
				int key = pair.Key;
				if (_softWeights.ContainsKey(key) || !_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) ||
				    key < 0 || key >= deltas.Length) continue;
				Vector3 bindDelta = WorldToBindDelta(key, mirroredDisp, xform);
				deltas[key] = startDelta + bindDelta * pair.Value;
			}
		}

		private Vector3 MirrorWorldVector(Vector3 worldVec, Transform xform)
		{
			if (!xform)
				return worldVec;

			Vector3 local = xform.InverseTransformVector(worldVec);
			switch (SymmetryAxis)
			{
				case 0:
					local.x = -local.x;
					break;
				case 1:
					local.y = -local.y;
					break;
				default:
					local.z = -local.z;
					break;
			}
			return xform.TransformVector(local);
		}

		private void ApplyMirrorRotate(float angle, Vector3 axW, Camera cam, Transform xform, Vector3[] deltas)
		{
			Vector3 symAxis = SymmetryAxis == 0 ? Vector3.right : SymmetryAxis == 1 ? Vector3.up : Vector3.forward;
			float mirrorAngle = Mathf.Abs(Vector3.Dot(axW.normalized, symAxis)) > 0.9f ? angle : -angle;

			Quaternion rot = Quaternion.AngleAxis(mirrorAngle, axW);
			foreach (KeyValuePair<int, float> pair in _mirrorWeights)
			{
				int key = pair.Key;
				if (_softWeights.ContainsKey(key) || !_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) ||
				    !_dragStartPositions.TryGetValue(key, out Vector3 startPos) || key < 0 ||
				    key >= deltas.Length) continue;
				Vector3 fromCenter = (xform ? xform.TransformPoint(startPos) : startPos) - _dragStartMirrorCentroidWorld;
				Vector3 worldDisp = Quaternion.Slerp(Quaternion.identity, rot, pair.Value) * fromCenter - fromCenter;
				deltas[key] = startDelta + WorldToBindDelta(key, worldDisp, xform);
			}
		}

		private void ApplyMirrorScale(float factor, bool uniform, Vector3 scaleAxisWorld, Transform xform, Vector3[] deltas)
		{
			foreach (KeyValuePair<int, float> pair in _mirrorWeights)
			{
				int key = pair.Key;
				if (_softWeights.ContainsKey(key) || !_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) ||
				    !_dragStartPositions.TryGetValue(key, out Vector3 startPos) || key < 0 ||
				    key >= deltas.Length) continue;
				Vector3 fromCenter = (xform ? xform.TransformPoint(startPos) : startPos) - _dragStartMirrorCentroidWorld;
				float scaleFactor = Mathf.Lerp(1f, factor, pair.Value);
				Vector3 scaled;
				if (uniform)
				{
					scaled = fromCenter * scaleFactor;
				}
				else
				{
					float proj = Vector3.Dot(fromCenter, scaleAxisWorld);
					scaled = fromCenter - scaleAxisWorld * proj + scaleAxisWorld * (proj * scaleFactor);
				}
				Vector3 worldDisp = scaled - fromCenter;
				deltas[key] = startDelta + WorldToBindDelta(key, worldDisp, xform);
			}
		}

		public void Render(Camera cam, Transform xform)
		{
			if (!HasTarget || cam == null || !EnsureMaterial())
				return;

			_glMaterial.SetPass(0);
			Vector3 centroidWorld = ToWorld(xform, _centroid);
			float scale = GizmoScale(cam, centroidWorld);
			Quaternion gizmoRot = GizmoRotation(xform);

			switch (Mode)
			{
				case GizmoMode.Translate:
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.X, xform), scale, GetColor(GizmoEnums.X));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Y, xform), scale, GetColor(GizmoEnums.Y));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Z, xform), scale, GetColor(GizmoEnums.Z));
					DrawArrowHead(centroidWorld, AxisW(GizmoEnums.X, xform), scale, GetColor(GizmoEnums.X));
					DrawArrowHead(centroidWorld, AxisW(GizmoEnums.Y, xform), scale, GetColor(GizmoEnums.Y));
					DrawArrowHead(centroidWorld, AxisW(GizmoEnums.Z, xform), scale, GetColor(GizmoEnums.Z));
					DrawPlaneHandle(centroidWorld, xform, scale, GizmoEnums.XY);
					DrawPlaneHandle(centroidWorld, xform, scale, GizmoEnums.XZ);
					DrawPlaneHandle(centroidWorld, xform, scale, GizmoEnums.YZ);
					DrawCenterCube(centroidWorld, gizmoRot, scale, GetColor(GizmoEnums.Free));
					break;
				case GizmoMode.Rotate:
					DrawRing(centroidWorld, AxisW(GizmoEnums.X, xform), scale, GetColor(GizmoEnums.X), RING_RAD);
					DrawRing(centroidWorld, AxisW(GizmoEnums.Y, xform), scale, GetColor(GizmoEnums.Y), RING_RAD);
					DrawRing(centroidWorld, AxisW(GizmoEnums.Z, xform), scale, GetColor(GizmoEnums.Z), RING_RAD);
					DrawRing(centroidWorld, cam.transform.forward, scale, GetColor(GizmoEnums.ViewRotate), VIEW_RING_RAD);
					break;
				case GizmoMode.Scale:
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.X, xform), scale, GetColor(GizmoEnums.X));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Y, xform), scale, GetColor(GizmoEnums.Y));
					DrawAxisLine(centroidWorld, AxisW(GizmoEnums.Z, xform), scale, GetColor(GizmoEnums.Z));
					DrawCubeEnd(centroidWorld, GizmoEnums.X, gizmoRot, scale, GetColor(GizmoEnums.X));
					DrawCubeEnd(centroidWorld, GizmoEnums.Y, gizmoRot, scale, GetColor(GizmoEnums.Y));
					DrawCubeEnd(centroidWorld, GizmoEnums.Z, gizmoRot, scale, GetColor(GizmoEnums.Z));
					DrawCenterCube(centroidWorld, gizmoRot, scale, GetColor(GizmoEnums.Free));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void RenderSoftRadius(Camera cam, Transform xform)
		{
			if (!HasTarget || !SoftSelectionEnabled || cam == null || SoftSelectionRadius <= 0f || !EnsureMaterial())
				return;

			_glMaterial.SetPass(0);
			Vector3 center = ToWorld(xform, _centroid);
			float r = SoftSelectionRadius;
			if (xform != null)
				r *= xform.lossyScale.x;
			DrawWireSphere(center, r, COL_SOFT);
		}

		private void DrawAxisLine(Vector3 center, Vector3 dir, float scale, Color col)
		{
			GL.Begin(1);
			GL.Color(col);
			GL.Vertex(center);
			GL.Vertex(center + dir * AXIS_LEN * scale);
			GL.End();
		}

		private void DrawArrowHead(Vector3 center, Vector3 dir, float scale, Color col)
		{
			Vector3 tip = center + dir * AXIS_LEN * scale;
			Vector3 base_ = center + dir * (AXIS_LEN - ARROW_LEN) * scale;
			float r = ARROW_RAD * scale;
			Perpendiculars(dir, out Vector3 tan, out Vector3 bin);
			GL.Begin(4);
			GL.Color(col);
			for (var i = 0; i < ARROW_SEG; i++)
			{
				float a0 = (float)i / ARROW_SEG * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / ARROW_SEG * Mathf.PI * 2f;
				Vector3 p0 = base_ + (tan * Mathf.Cos(a0) + bin * Mathf.Sin(a0)) * r;
				Vector3 p1 = base_ + (tan * Mathf.Cos(a1) + bin * Mathf.Sin(a1)) * r;
				GL.Vertex(tip);
				GL.Vertex(p0);
				GL.Vertex(p1);
			}
			GL.End();
		}

		private void DrawRing(Vector3 center, Vector3 normal, float scale, Color col, float ringRadius)
		{
			Perpendiculars(normal, out Vector3 tan, out Vector3 bin);
			float r = ringRadius * scale;
			GL.Begin(1);
			GL.Color(col);
			for (var i = 0; i < RING_SEG; i++)
			{
				float a0 = (float)i / RING_SEG * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / RING_SEG * Mathf.PI * 2f;
				GL.Vertex(center + (tan * Mathf.Cos(a0) + bin * Mathf.Sin(a0)) * r);
				GL.Vertex(center + (tan * Mathf.Cos(a1) + bin * Mathf.Sin(a1)) * r);
			}
			GL.End();
		}

		private void DrawPlaneHandle(Vector3 wc, Transform xform, float scale, GizmoEnums plane)
		{
			GetPlaneAxes(plane, xform, out Vector3 ax1, out Vector3 ax2);
			Color col = GetColor(plane);
			col.a = PLANE_ALPHA;
			Vector3 p0 = wc + PLANE_OFFSET * scale * ax1 + PLANE_OFFSET * scale * ax2;
			Vector3 p1 = wc + (PLANE_OFFSET + PLANE_SIZE) * scale * ax1 + PLANE_OFFSET * scale * ax2;
			Vector3 p2 = wc + (PLANE_OFFSET + PLANE_SIZE) * scale * ax1 + (PLANE_OFFSET + PLANE_SIZE) * scale * ax2;
			Vector3 p3 = wc + PLANE_OFFSET * scale * ax1 + (PLANE_OFFSET + PLANE_SIZE) * scale * ax2;

			GL.Begin(7);
			GL.Color(col);
			GL.Vertex(p0); GL.Vertex(p1); GL.Vertex(p2); GL.Vertex(p3);
			GL.End();

			Color edgeCol = col;
			edgeCol.a = 0.8f;
			GL.Begin(1);
			GL.Color(edgeCol);
			GLEdge(p0, p1); GLEdge(p1, p2); GLEdge(p2, p3); GLEdge(p3, p0);
			GL.End();
		}

		private Vector3 GetPlaneNormal(GizmoEnums plane, Transform xform)
		{
			switch (plane)
			{
				case GizmoEnums.XY: return AxisW(GizmoEnums.Z, xform);
				case GizmoEnums.XZ: return AxisW(GizmoEnums.Y, xform);
				case GizmoEnums.YZ: return AxisW(GizmoEnums.X, xform);
				default: return Vector3.up;
			}
		}

		private void DrawCubeEnd(Vector3 center, GizmoEnums axis, Quaternion gizmoRot, float scale, Color col)
		{
			Vector3 axisDir = gizmoRot * BaseDir(axis);
			Vector3 cubeCenter = center + AXIS_LEN * scale * axisDir;
			float h = CUBE_HALF * scale;
			Vector3 rx = gizmoRot * Vector3.right * h;
			Vector3 ry = gizmoRot * Vector3.up * h;
			Vector3 rz = gizmoRot * Vector3.forward * h;
			var corners = new Vector3[8];
			for (var i = 0; i < 8; i++)
			{
				float sx = (i & 1) == 0 ? -1f : 1f;
				float sy = (i & 2) == 0 ? -1f : 1f;
				float sz = (i & 4) == 0 ? -1f : 1f;
				corners[i] = cubeCenter + rx * sx + ry * sy + rz * sz;
			}
			GL.Begin(1);
			GL.Color(col);
			GLEdge(corners[0], corners[1]); GLEdge(corners[2], corners[3]);
			GLEdge(corners[4], corners[5]); GLEdge(corners[6], corners[7]);
			GLEdge(corners[0], corners[2]); GLEdge(corners[1], corners[3]);
			GLEdge(corners[4], corners[6]); GLEdge(corners[5], corners[7]);
			GLEdge(corners[0], corners[4]); GLEdge(corners[1], corners[5]);
			GLEdge(corners[2], corners[6]); GLEdge(corners[3], corners[7]);
			GL.End();
		}

		private void DrawCenterCube(Vector3 center, Quaternion gizmoRot, float scale, Color col)
		{
			float h = CENTER_HALF * scale;
			Vector3 rx = gizmoRot * Vector3.right * h;
			Vector3 ry = gizmoRot * Vector3.up * h;
			Vector3 rz = gizmoRot * Vector3.forward * h;
			var corners = new Vector3[8];
			for (var i = 0; i < 8; i++)
			{
				float sx = (i & 1) == 0 ? -1f : 1f;
				float sy = (i & 2) == 0 ? -1f : 1f;
				float sz = (i & 4) == 0 ? -1f : 1f;
				corners[i] = center + rx * sx + ry * sy + rz * sz;
			}
			GL.Begin(1);
			GL.Color(col);
			GLEdge(corners[0], corners[1]); GLEdge(corners[1], corners[3]);
			GLEdge(corners[3], corners[2]); GLEdge(corners[2], corners[0]);
			GLEdge(corners[4], corners[5]); GLEdge(corners[5], corners[7]);
			GLEdge(corners[7], corners[6]); GLEdge(corners[6], corners[4]);
			GLEdge(corners[0], corners[4]); GLEdge(corners[1], corners[5]);
			GLEdge(corners[2], corners[6]); GLEdge(corners[3], corners[7]);
			GL.End();
		}

		private void DrawWireSphere(Vector3 center, float radius, Color col)
		{
			GL.Begin(1);
			GL.Color(col);
			DrawGLCircle(center, Vector3.right, Vector3.up, radius);
			DrawGLCircle(center, Vector3.up, Vector3.forward, radius);
			DrawGLCircle(center, Vector3.right, Vector3.forward, radius);
			GL.End();
		}

		private void DrawGLCircle(Vector3 center, Vector3 ax1, Vector3 ax2, float r)
		{
			for (var i = 0; i < SPHERE_SEG; i++)
			{
				float a0 = (float)i / SPHERE_SEG * Mathf.PI * 2f;
				float a1 = (float)(i + 1) / SPHERE_SEG * Mathf.PI * 2f;
				GL.Vertex(center + (ax1 * Mathf.Cos(a0) + ax2 * Mathf.Sin(a0)) * r);
				GL.Vertex(center + (ax1 * Mathf.Cos(a1) + ax2 * Mathf.Sin(a1)) * r);
			}
		}

		private static void GLEdge(Vector3 a, Vector3 b)
		{
			GL.Vertex(a);
			GL.Vertex(b);
		}

		private Quaternion GizmoRotation(Transform xform)
		{
			if (Space == GizmoSpace.Object && _objectRoot)
				return _objectRoot.rotation;
			if (Space == GizmoSpace.Normal)
				return (xform ? xform.rotation : Quaternion.identity) * _localOrientation;
			return Quaternion.identity;
		}

		private Vector3 AxisW(GizmoEnums axis, Transform xform)
		{
			return GizmoRotation(xform) * BaseDir(axis);
		}

		private Vector3 AxisL(GizmoEnums axis, Transform xform)
		{
			Vector3 dir = BaseDir(axis);
			switch (Space)
			{
				case GizmoSpace.Normal:
					return _localOrientation * dir;
				case GizmoSpace.Object when _objectRoot:
				{
					Vector3 worldDir = _objectRoot.rotation * dir;
					return xform ? xform.InverseTransformDirection(worldDir) : worldDir;
				}
				case GizmoSpace.World:
				default:
					return xform ? xform.InverseTransformDirection(dir) : dir;
			}
		}

		private bool EnsureMaterial()
		{
			if (_glMaterial)
				return true;

			Shader shader = Shader.Find("Hidden/Internal-Colored");
			if (!shader)
				return false;

			_glMaterial = new Material(shader);
			_glMaterial.hideFlags = (HideFlags)61;
			_glMaterial.SetInt(SrcBlend, 5);
			_glMaterial.SetInt(DstBlend, 10);
			_glMaterial.SetInt(Cull, 0);
			_glMaterial.SetInt(ZWrite, 0);
			_glMaterial.SetInt(ZTest, 8);
			return true;
		}

		private Color GetColor(GizmoEnums axis)
		{
			if (axis == _hoveredAxis || axis == _activeAxis)
				return COL_HOVER;

			switch (axis)
			{
				case GizmoEnums.X: return COL_X;
				case GizmoEnums.Y: return COL_Y;
				case GizmoEnums.Z:
				case GizmoEnums.XY: return COL_Z;
				case GizmoEnums.XZ: return COL_Y;
				case GizmoEnums.YZ: return COL_X;
				case GizmoEnums.ViewRotate: return COL_VIEW;
				case GizmoEnums.None:
				case GizmoEnums.Free:
				default: return COL_FREE;
			}
		}

		private static float GizmoScale(Camera cam, Vector3 worldPos)
		{
			return Vector3.Distance(cam.transform.position, worldPos) * SCREEN_SCALE;
		}

		private static Vector3 BaseDir(GizmoEnums axis)
		{
			switch (axis)
			{
				case GizmoEnums.X: return Vector3.right;
				case GizmoEnums.Y: return Vector3.up;
				case GizmoEnums.Z: return Vector3.forward;
				case GizmoEnums.None:
				case GizmoEnums.XY:
				case GizmoEnums.XZ:
				case GizmoEnums.YZ:
				case GizmoEnums.Free:
				case GizmoEnums.ViewRotate:
				default: return Vector3.zero;
			}
		}

		private static Vector3 ToWorld(Transform xform, Vector3 local)
		{
			return xform ? xform.TransformPoint(local) : local;
		}

		private static Vector2 WorldToScreen(Camera cam, Vector3 worldPos)
		{
			Vector3 s = cam.WorldToScreenPoint(worldPos);
			return s;
		}

		private static Vector3 RayPlaneIntersect(Camera cam, Vector2 screenPos, Vector3 planePoint, Vector3 planeNormal)
		{
			Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
			float denom = Vector3.Dot(ray.direction, planeNormal);
			if (Mathf.Abs(denom) < 0.0001f)
				return planePoint;
			float t = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
			return ray.origin + ray.direction * t;
		}

		private static float PointToSegmentDist(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float sqLen = ab.sqrMagnitude;
			if (sqLen < 0.001f)
				return Vector2.Distance(p, a);
			float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqLen);
			return Vector2.Distance(p, a + ab * t);
		}

		private static void Perpendiculars(Vector3 dir, out Vector3 tan, out Vector3 bin)
		{
			Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.up : Vector3.right;
			tan = Vector3.Cross(dir, up).normalized;
			bin = Vector3.Cross(dir, tan).normalized;
		}

		private HashSet<int> _targetIndices;
		private readonly Dictionary<int, float> _softWeights = new Dictionary<int, float>();
		private readonly Dictionary<int, float> _combinedSoftWeights = new Dictionary<int, float>();
		private Vector3 _centroid;
		private HashSet<int> _mirrorIndices;
		private readonly Dictionary<int, float> _mirrorWeights = new Dictionary<int, float>();
		private Vector3 _mirrorCentroid;
		private Vector3 _dragStartMirrorCentroidWorld;
		private Quaternion _localOrientation = Quaternion.identity;
		private Transform _objectRoot;
		private GizmoEnums _hoveredAxis;
		private GizmoEnums _activeAxis;
		private bool _isDragging;
		private Vector2 _dragStartScreen;
		private Vector3 _dragStartCentroidWorld;
		private readonly Dictionary<int, Vector3> _dragStartDeltas = new Dictionary<int, Vector3>();
		private readonly Dictionary<int, Vector3> _dragStartPositions = new Dictionary<int, Vector3>();
		private Material _glMaterial;

		private const float AXIS_LEN = 1f;
		private const float ARROW_LEN = 0.18f;
		private const float ARROW_RAD = 0.055f;
		private const float CUBE_HALF = 0.055f;
		private const float CENTER_HALF = 0.09f;
		private const float RING_RAD = 0.85f;
		private const float HIT_PX = 14f;
		private const int RING_SEG = 64;
		private const int ARROW_SEG = 8;
		private const int SPHERE_SEG = 48;
		private const float PLANE_OFFSET = 0.25f;
		private const float PLANE_SIZE = 0.25f;
		private const float PLANE_ALPHA = 0.35f;
		private const float VIEW_RING_RAD = 1.1f;
		private const float SCREEN_SCALE = 0.12f;

		private static readonly Color COL_X = new Color(0.9f, 0.2f, 0.2f);
		private static readonly Color COL_Y = new Color(0.2f, 0.9f, 0.2f);
		private static readonly Color COL_Z = new Color(0.3f, 0.3f, 0.95f);
		private static readonly Color COL_HOVER = Color.yellow;
		private static readonly Color COL_FREE = new Color(0.85f, 0.85f, 0.85f);
		private static readonly Color COL_VIEW = new Color(0.8f, 0.8f, 0.8f);
		private static readonly Color COL_SOFT = new Color(0.4f, 0.7f, 1f, 0.3f);
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int Cull = Shader.PropertyToID("_Cull");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTest = Shader.PropertyToID("_ZTest");
	}
}
