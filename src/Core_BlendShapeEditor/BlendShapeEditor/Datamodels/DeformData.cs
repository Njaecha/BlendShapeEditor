using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlendShapeEditor
{
	public class DeformData
	{
		/// Renderer path, or several renderer paths joined in sorted order when the layer stack is shared by
		/// a multi-renderer session. Layer deltas are laid out part after part in that same order.
		public string Key { get; set; }
		public List<DeformLayer> Layers { get; private set; }
		public int ActiveLayerIndex { get; set; }
		public string EditingExistingShapeName { get; set; }

		public DeformData(string key)
		{
			Key = key;
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

		/// Combined deltas for display (hidden layers skipped). Shared by every deformer reading this data, so
		/// the cache lives here rather than per deformer: clearing layers' Dirty flags inside one deformer
		/// would otherwise hide the change from the others. Returns the cached buffer; do not modify it.
		/// This data outlives edit sessions and Layers is mutated directly (import, undo entries), so the
		/// cache is also rebuilt whenever the layer list itself differs from the one it was summed from.
		public Vector3[] GetDisplayDelta()
		{
			if (Layers.Count == 0)
				return null;
			if (_displayDeltas == null || _displayInvalid || AnyDirty() || !LayerListMatchesCache())
			{
				_displayDeltas = SumLayers(true, _displayDeltas);
				_displayInvalid = false;
				foreach (DeformLayer t in Layers) t.Dirty = false;
				_displayLayers.Clear();
				_displayLayers.AddRange(Layers);
			}
			return _displayDeltas;
		}

		private bool LayerListMatchesCache()
		{
			if (_displayLayers.Count != Layers.Count)
				return false;
			for (var i = 0; i < Layers.Count; i++)
				if (!ReferenceEquals(_displayLayers[i], Layers[i]))
					return false;
			return true;
		}

		/// Forces GetDisplayDelta to recompute, for changes that don't mark a layer dirty (removal, reorder, undo).
		public void InvalidateDisplayDelta()
		{
			_displayInvalid = true;
		}

		/// Fresh array of the combined deltas, for baking. Leaves the display cache and Dirty flags alone.
		public Vector3[] ComputeFinalDelta(bool respectHidden = false)
		{
			return Layers.Count == 0 ? null : SumLayers(respectHidden, null);
		}

		private Vector3[] SumLayers(bool respectHidden, Vector3[] buffer)
		{
			int vertexCount = Layers[0].Deltas.Length;
			if (buffer == null || buffer.Length != vertexCount)
				buffer = new Vector3[vertexCount];
			else
				System.Array.Clear(buffer, 0, vertexCount);

			foreach (DeformLayer layer in Layers)
			{
				float weight = layer.Weight;
				if (!(weight > 0f) || layer.Deltas.Length != vertexCount) continue;
				if (respectHidden && layer.Hidden) continue;
				Vector3[] deltas = layer.Deltas;
				for (var k = 0; k < vertexCount; k++)
					buffer[k] += deltas[k] * weight;
			}
			return buffer;
		}

		public bool AnyDirty()
		{
			return Layers.Any(t => t.Dirty);
		}

		public DeformLayer AddLayer(int vertexCount)
		{
			DeformLayer layer = new DeformLayer(string.Format(i18n.LayerDefaultNameFmt, Layers.Count + 1), vertexCount);
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

		public void SetLayerHidden(int index, bool hidden)
		{
			if (index < 0 || index >= Layers.Count)
				return;
			if (Layers[index].Hidden == hidden)
				return;
			Layers[index].Hidden = hidden;
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
			_displayDeltas = null;
			_displayLayers.Clear();
			ActiveLayerIndex = -1;
			EditingExistingShapeName = null;
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
			_displayDeltas = null;
		}

		private Vector3[] _displayDeltas;
		private bool _displayInvalid;
		private readonly List<DeformLayer> _displayLayers = new List<DeformLayer>();
	}

	public static class DeformDataKeys
	{
		/// Sorts renderers by their path under `root` (ordinal) and returns the paths joined with '|'.
		public static string SortAndBuildKey(Transform root, List<Renderer> renderers)
		{
			renderers.Sort((a, b) => string.CompareOrdinal(root.GetPathToChild(a.transform), root.GetPathToChild(b.transform)));
			return string.Join("|", renderers.Select(r => root.GetPathToChild(r.transform)).ToArray());
		}
	}
}
