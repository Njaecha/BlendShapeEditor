using System;
using System.Collections.Generic;
using UnityEngine;

namespace KKShapeEditor
{
	public class SpatialHashGrid
	{
		private Dictionary<long, List<int>> _grid;
		private Vector3[] _vertices;
		private float _invCell;

		public SpatialHashGrid(Vector3[] vertices, Bounds bounds)
		{
			Rebuild(vertices, bounds);
		}

		public void Rebuild(Vector3[] vertices, Bounds bounds)
		{
			_vertices = vertices;
			if (vertices == null || vertices.Length == 0)
			{
				_grid = null;
				return;
			}

			int vertexCount = vertices.Length;
			float cellSize = Mathf.Max(bounds.size.magnitude / Mathf.Sqrt((float)vertexCount), 0.0001f);
			_invCell = 1f / cellSize;

			if (_grid != null)
			{
				foreach (List<int> cell in _grid.Values)
					cell.Clear();
				_grid.Clear();
			}
			else
			{
				_grid = new Dictionary<long, List<int>>();
			}

			for (var i = 0; i < vertexCount; i++)
			{
				long key = HashKey(vertices[i], _invCell);
				List<int> cell;
				if (!_grid.TryGetValue(key, out cell))
				{
					cell = new List<int>();
					_grid[key] = cell;
				}
				cell.Add(i);
			}
		}

		public void FindVerticesInRadius(Vector3 queryPos, float radius, Action<int, float> callback)
		{
			if (_grid == null || _vertices == null)
				return;

			float radiusSq = radius * radius;
			int cellRadius = Mathf.CeilToInt(radius * _invCell);
			int cellX = Mathf.FloorToInt(queryPos.x * _invCell);
			int cellY = Mathf.FloorToInt(queryPos.y * _invCell);
			int cellZ = Mathf.FloorToInt(queryPos.z * _invCell);

			for (int i = -cellRadius; i <= cellRadius; i++)
			{
				for (int j = -cellRadius; j <= cellRadius; j++)
				{
					for (int k = -cellRadius; k <= cellRadius; k++)
					{
						long key = (cellX + i) * 73856093L ^ (cellY + j) * 19349663L ^ (cellZ + k) * 83492791L;
						if (!_grid.TryGetValue(key, out List<int> cell)) continue;
						foreach (int vertexIndex in cell)
						{
							float distSq = (_vertices[vertexIndex] - queryPos).sqrMagnitude;
							if (distSq <= radiusSq)
								callback(vertexIndex, distSq);
						}
					}
				}
			}
		}

		public int FindNearest(Vector3 queryPos)
		{
			if (_grid == null || _vertices == null)
				return -1;

			int result = -1;
			var bestDistSq = float.MaxValue;
			int cellX = Mathf.FloorToInt(queryPos.x * _invCell);
			int cellY = Mathf.FloorToInt(queryPos.y * _invCell);
			int cellZ = Mathf.FloorToInt(queryPos.z * _invCell);

			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						long key = (cellX + i) * 73856093L ^ (cellY + j) * 19349663L ^ (cellZ + k) * 83492791L;
						if (!_grid.TryGetValue(key, out List<int> cell)) continue;
						foreach (int vertexIndex in cell)
						{
							float distSq = (_vertices[vertexIndex] - queryPos).sqrMagnitude;
							if (!(distSq < bestDistSq)) continue;
							bestDistSq = distSq;
							result = vertexIndex;
						}
					}
				}
			}
			return result;
		}

		public void FindVerticesInBounds(Vector3 min, Vector3 max, Action<int> callback)
		{
			if (_grid == null || _vertices == null)
				return;

			int minCellX = Mathf.FloorToInt(min.x * _invCell);
			int minCellY = Mathf.FloorToInt(min.y * _invCell);
			int minCellZ = Mathf.FloorToInt(min.z * _invCell);
			int maxCellX = Mathf.FloorToInt(max.x * _invCell);
			int maxCellY = Mathf.FloorToInt(max.y * _invCell);
			int maxCellZ = Mathf.FloorToInt(max.z * _invCell);

			for (int i = minCellX; i <= maxCellX; i++)
			{
				for (int j = minCellY; j <= maxCellY; j++)
				{
					for (int k = minCellZ; k <= maxCellZ; k++)
					{
						long key = i * 73856093L ^ j * 19349663L ^ k * 83492791L;
						if (!_grid.TryGetValue(key, out List<int> cell)) continue;
						foreach (int t in cell)
							callback(t);
					}
				}
			}
		}

		private static long HashKey(Vector3 pos, float invCell)
		{
			var cx = (long)Mathf.FloorToInt(pos.x * invCell);
			int cy = Mathf.FloorToInt(pos.y * invCell);
			int cz = Mathf.FloorToInt(pos.z * invCell);
			return cx * 73856093L ^ cy * 19349663L ^ cz * 83492791L;
		}
	}
}
