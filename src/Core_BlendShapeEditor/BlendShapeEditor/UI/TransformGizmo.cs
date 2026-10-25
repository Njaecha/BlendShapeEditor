using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlendShapeEditor
{
	/// Vertex transform gizmo: owns the selection's soft/mirror weights and applies the drags of its
	/// GizmoHandle to layer deltas.
	public class TransformGizmo
	{
		public GizmoMode Mode { get => _handle.Mode; set => _handle.Mode = value; }
		public GizmoSpace Space { get; set; } = GizmoSpace.World;
		public bool SoftSelectionEnabled { get; set; }
		public float SoftSelectionRadius { get; set; } = 0.1f;
		public float SizeFactor { get => _handle.SizeFactor; set => _handle.SizeFactor = value; }
		public FalloffMode SoftFalloff { get; set; } = FalloffMode.Smooth;
		public float SharpExponent { get; set; } = 3f;
		public SoftSelectMode SoftMode { get; set; }
		public GizmoEnums HoveredAxis => _handle.HoveredAxis;
		public bool IsDragging => _handle.IsDragging;
		public bool HasTarget => _targetIndices != null && _targetIndices.Count > 0;
		public Vector3 CentroidLocal => _centroid;
		public Dictionary<int, float> SoftWeights => _combinedSoftWeights;
		public Dictionary<int, float> PrimarySoftWeights => _softWeights;
		public Dictionary<int, float> MirrorSoftWeights => _mirrorWeights;
		public bool HasMirrorTarget => _mirrorIndices != null && _mirrorIndices.Count > 0;
		public HashSet<int> MirrorIndices => _mirrorIndices;
		public HashSet<int> TargetIndices => _targetIndices;
		public bool SymmetryEnabled { get; set; }
		public MirrorPlane Mirror { get; set; }
		public EditSession Session { get; set; }
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
			_handle.ClearHover();

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
				_handle.ClearHover();
				return;
			}
			SyncHandle(xform);
			_handle.UpdateHover(mouseScreen, cam);
		}

		public void ClearHover()
		{
			_handle.ClearHover();
		}

		public bool BeginDrag(Vector2 mouseScreen, Camera cam, Transform xform, DeformLayer layer, Vector3[] vertices)
		{
			if (_handle.HoveredAxis == GizmoEnums.None || !HasTarget || layer == null)
				return false;

			SyncHandle(xform);
			_handle.BeginDrag(mouseScreen);
			_dragStartCentroidWorld = _handle.DragStartPivot;
			_dragStartDeltas.Clear();
			_dragStartPositions.Clear();

			Vector3[] deltas = layer.Deltas;
			foreach (int key in _combinedSoftWeights.Select(pair => pair.Key).Where(key => key >= 0 && key < deltas.Length && key < vertices.Length))
			{
				_dragStartDeltas[key] = deltas[key];
				_dragStartPositions[key] = vertices[key];
			}

			if (!HasMirrorTarget || !xform || Mirror == null) return true;
			_dragStartMirrorCentroidWorld = Mirror.ReflectPoint(_dragStartCentroidWorld);
			return true;
		}

		public void UpdateDrag(Vector2 mouseScreen, Camera cam, Transform xform, DeformLayer layer, Vector3[] vertices)
		{
			if (!_handle.IsDragging || layer == null || !cam)
				return;

			SyncHandle(xform);
			if (_handle.UpdateDrag(mouseScreen, cam, out GizmoDragDelta drag))
			{
				switch (Mode)
				{
					case GizmoMode.Translate:
						ApplyTranslate(drag.Translation, xform, layer.Deltas);
						break;
					case GizmoMode.Rotate:
						ApplyRotate(drag.Angle, drag.RotationAxis, xform, layer.Deltas);
						break;
					case GizmoMode.Scale:
						ApplyScale(drag.ScaleFactor, drag.Uniform, drag.ScaleAxis, xform, layer.Deltas);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			layer.Dirty = true;
			UpdateCentroid(vertices);
		}

		public void EndDrag()
		{
			_handle.EndDrag();
		}

		private void ApplyTranslate(Vector3 dispWorld, Transform xform, Vector3[] deltas)
		{
			foreach (KeyValuePair<int, float> pair in _softWeights)
			{
				int key = pair.Key;
				if (!_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) || key < 0 ||
				    key >= deltas.Length) continue;
				Vector3 bindDelta = WorldToBindDelta(key, dispWorld, xform);
				deltas[key] = startDelta + bindDelta * pair.Value;
			}

			if (HasMirrorTarget && SymmetryEnabled && Mirror != null)
				ApplyMirrorTranslate(dispWorld, xform, deltas);
		}

		private Vector3 WorldToBindDelta(int vertexIdx, Vector3 worldDisp, Transform xform)
		{
			if (Session == null) return xform ? xform.InverseTransformVector(worldDisp) : worldDisp;
			Session.WorldDeltaToBindDelta(vertexIdx, worldDisp, out Vector3 result);
			return result;
		}

		private void ApplyRotate(float angle, Vector3 axisWorld, Transform xform, Vector3[] deltas)
		{
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

			if (HasMirrorTarget && SymmetryEnabled && Mirror != null)
				ApplyMirrorRotate(angle, axisWorld, xform, deltas);
		}

		private void ApplyScale(float factor, bool uniform, Vector3 scaleAxis, Transform xform, Vector3[] deltas)
		{
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

			if (HasMirrorTarget && SymmetryEnabled && Mirror != null)
				ApplyMirrorScale(factor, uniform, uniform ? Vector3.zero : Mirror.ReflectVector(scaleAxis), xform, deltas);
		}

		private void ApplyMirrorTranslate(Vector3 dispWorld, Transform xform, Vector3[] deltas)
		{
			Vector3 mirroredDisp = Mirror.ReflectVector(dispWorld);
			foreach (KeyValuePair<int, float> pair in _mirrorWeights)
			{
				int key = pair.Key;
				if (_softWeights.ContainsKey(key) || !_dragStartDeltas.TryGetValue(key, out Vector3 startDelta) ||
				    key < 0 || key >= deltas.Length) continue;
				Vector3 bindDelta = WorldToBindDelta(key, mirroredDisp, xform);
				deltas[key] = startDelta + bindDelta * pair.Value;
			}
		}

		private void ApplyMirrorRotate(float angle, Vector3 axW, Transform xform, Vector3[] deltas)
		{
			Mirror.ReflectRotation(axW, angle, out Vector3 mirrorAxis, out float mirrorAngle);
			Quaternion rot = Quaternion.AngleAxis(mirrorAngle, mirrorAxis);
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
			if (!HasTarget || !cam)
				return;
			SyncHandle(xform);
			_handle.Render(cam);
		}

		public void RenderSoftRadius(Camera cam, Transform xform)
		{
			if (!HasTarget || !SoftSelectionEnabled || cam == null || SoftSelectionRadius <= 0f)
				return;

			Vector3 center = ToWorld(xform, _centroid);
			float r = SoftSelectionRadius;
			if (xform != null)
				r *= xform.lossyScale.x;
			_handle.RenderWireSphere(center, r, COL_SOFT);
		}

		/// The handle sits on the selection's centroid, oriented by the chosen gizmo space.
		private void SyncHandle(Transform xform)
		{
			_handle.Pivot = ToWorld(xform, _centroid);
			_handle.Orientation = GizmoRotation(xform);
		}

		private Quaternion GizmoRotation(Transform xform)
		{
			if (Space == GizmoSpace.Object && _objectRoot)
				return _objectRoot.rotation;
			if (Space == GizmoSpace.Normal)
				return (xform ? xform.rotation : Quaternion.identity) * _localOrientation;
			return Quaternion.identity;
		}

		private static Vector3 ToWorld(Transform xform, Vector3 local)
		{
			return xform ? xform.TransformPoint(local) : local;
		}

		private readonly GizmoHandle _handle = new GizmoHandle();
		private HashSet<int> _targetIndices;
		private readonly Dictionary<int, float> _softWeights = new Dictionary<int, float>();
		private readonly Dictionary<int, float> _combinedSoftWeights = new Dictionary<int, float>();
		private Vector3 _centroid;
		private HashSet<int> _mirrorIndices;
		private readonly Dictionary<int, float> _mirrorWeights = new Dictionary<int, float>();
		private Vector3 _dragStartMirrorCentroidWorld;
		private Quaternion _localOrientation = Quaternion.identity;
		private Transform _objectRoot;
		private Vector3 _dragStartCentroidWorld;
		private readonly Dictionary<int, Vector3> _dragStartDeltas = new Dictionary<int, Vector3>();
		private readonly Dictionary<int, Vector3> _dragStartPositions = new Dictionary<int, Vector3>();

		private static readonly Color COL_SOFT = new Color(0.4f, 0.7f, 1f, 0.3f);
	}
}
