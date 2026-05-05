using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlendShapeEditor
{
	public class DeformData
	{
		public string RendererPath { get; set; }
		public List<DeformLayer> Layers { get; private set; }
		public int ActiveLayerIndex { get; set; }

		public DeformData(string rendererPath)
		{
			RendererPath = rendererPath;
			Layers = new List<DeformLayer>();
			ActiveLayerIndex = -1;
		}

		public DeformLayer ActiveLayer
		{
			get
			{
				if (ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count)
					return Layers[ActiveLayerIndex];
				return null;
			}
		}

		public bool HasLayers => Layers.Count > 0;

		public bool CanSculpt => ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count;

		public void SetActiveLayer(int index)
		{
			if (index < -1 || index >= Layers.Count)
				return;
			ActiveLayerIndex = index;
		}

		public Vector3[] ComputeFinalDelta()
		{
			if (Layers.Count == 0)
				return null;

			int vertexCount = Layers[0].Deltas.Length;
			if (_finalDeltas == null || _finalDeltas.Length != vertexCount)
				_finalDeltas = new Vector3[vertexCount];

			for (var i = 0; i < vertexCount; i++)
				_finalDeltas[i] = Vector3.zero;

			foreach (DeformLayer layer in Layers)
			{
				float weight = layer.Weight;
				if (!(weight > 0f) || layer.Deltas.Length != vertexCount) continue;
				Vector3[] deltas = layer.Deltas;
				for (var k = 0; k < vertexCount; k++)
					_finalDeltas[k] += deltas[k] * weight;
			}

			foreach (DeformLayer t in Layers) t.Dirty = false;

			return _finalDeltas;
		}

		public bool AnyDirty()
		{
			return Layers.Any(t => t.Dirty);
		}

		public DeformLayer AddLayer(int vertexCount)
		{
			DeformLayer layer = new DeformLayer(string.Format(L.LayerDefaultNameFmt, Layers.Count + 1), vertexCount);
			Layers.Add(layer);
			ActiveLayerIndex = Layers.Count - 1;
			return layer;
		}

		public void SetLayerWeight(int index, float weight)
		{
			if (index < 0 || index >= Layers.Count)
				return;
			float clamped = Mathf.Clamp01(weight);
			if (Mathf.Approximately(Layers[index].Weight, clamped))
				return;
			Layers[index].Weight = clamped;
			Layers[index].Dirty = true;
		}

		public void RenameLayer(int index, string newName)
		{
			if (index < 0 || index >= Layers.Count)
				return;
			Layers[index].Name = newName;
		}

		public void RemoveLayer(int index)
		{
			if (index < 0 || index >= Layers.Count)
				return;
			Layers.RemoveAt(index);
			if (Layers.Count == 0)
			{
				ActiveLayerIndex = -1;
				return;
			}
			if (ActiveLayerIndex >= Layers.Count)
			{
				ActiveLayerIndex = Layers.Count - 1;
				return;
			}
			if (ActiveLayerIndex > index)
				ActiveLayerIndex--;
		}

		public void ClearLayers()
		{
			Layers.Clear();
			ActiveLayerIndex = -1;
		}

		public void MoveLayerUp(int index)
		{
			if (index <= 0 || index >= Layers.Count)
				return;
			DeformLayer temp = Layers[index];
			Layers[index] = Layers[index - 1];
			Layers[index - 1] = temp;
			if (ActiveLayerIndex == index)
				ActiveLayerIndex--;
			else if (ActiveLayerIndex == index - 1)
				ActiveLayerIndex++;
		}

		public void MoveLayerDown(int index)
		{
			if (index < 0 || index >= Layers.Count - 1)
				return;
			DeformLayer temp = Layers[index];
			Layers[index] = Layers[index + 1];
			Layers[index + 1] = temp;
			if (ActiveLayerIndex == index)
				ActiveLayerIndex++;
			else if (ActiveLayerIndex == index + 1)
				ActiveLayerIndex--;
		}

		public void ResetAllLayers(int newVertexCount)
		{
			for (int i = 0; i < Layers.Count; i++)
				Layers[i].Reset(newVertexCount);
			_finalDeltas = null;
		}

		private Vector3[] _finalDeltas;
	}
}
