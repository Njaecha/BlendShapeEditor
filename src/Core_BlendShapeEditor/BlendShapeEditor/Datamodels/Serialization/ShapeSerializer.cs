using System;
using System.Collections.Generic;
using System.IO;

namespace KKShapeEditor
{
	public static class ShapeSerializer
	{
		private const byte Magic0 = 75; // 'K'
		private const byte Magic1 = 83; // 'S'
		private const byte FormatVersion = 2;

		public static byte[] SerializeAllLayers(Dictionary<string, DeformData> dataMap)
		{
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(ms))
			{
				WriteHeader(w);
				w.Write(dataMap.Count);
				foreach (KeyValuePair<string, DeformData> pair in dataMap)
				{
					w.Write(pair.Key ?? "");
					WriteDeformData(w, pair.Value);
				}
				return ms.ToArray();
			}
		}

		public static Dictionary<string, DeformData> DeserializeAllLayers(byte[] data)
		{
			if (data == null || data.Length < 3 || !HasMagicHeader(data))
				return null;
			try
			{
				using (MemoryStream ms = new MemoryStream(data))
				using (BinaryReader r = new BinaryReader(ms))
				{
					r.ReadByte(); r.ReadByte();
					byte version = r.ReadByte();
					int count = r.ReadInt32();
					Dictionary<string, DeformData> result = new Dictionary<string, DeformData>(count);
					for (int i = 0; i < count; i++)
					{
						string rendererPath = r.ReadString();
						DeformData deformData = ReadDeformData(r, version, rendererPath);
						if (deformData != null)
							result[rendererPath] = deformData;
					}
					return result;
				}
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("ShapeSerializer.DeserializeAllLayers failed: " + ex.Message);
				return null;
			}
		}

		private static void WriteDeformData(BinaryWriter w, DeformData data)
		{
			w.Write(data.Layers.Count);
			w.Write(data.ActiveLayerIndex);
			foreach (DeformLayer layer in data.Layers)
			{
				w.Write(layer.Name ?? "");
				w.Write(layer.Weight);
				w.Write(layer.Deltas.Length);
				for (var i = 0; i < layer.Deltas.Length; i++)
				{
					w.Write(layer.Deltas[i].x);
					w.Write(layer.Deltas[i].y);
					w.Write(layer.Deltas[i].z);
				}
			}
			w.Write(data.WeightRemapped);
		}

		private static DeformData ReadDeformData(BinaryReader r, byte version, string path)
		{
			int layerCount = r.ReadInt32();
			int savedActiveIndex = r.ReadInt32();
			DeformData data = new DeformData(path);
			for (var i = 0; i < layerCount; i++)
			{
				string name = r.ReadString();
				float weight = r.ReadSingle();
				int vertexCount = r.ReadInt32();
				DeformLayer layer = new DeformLayer(name, vertexCount);
				layer.Weight = weight;
				for (var j = 0; j < vertexCount; j++)
				{
					layer.Deltas[j].x = r.ReadSingle();
					layer.Deltas[j].y = r.ReadSingle();
					layer.Deltas[j].z = r.ReadSingle();
				}
				data.Layers.Add(layer);
			}
			data.ActiveLayerIndex = (savedActiveIndex < data.Layers.Count) ? savedActiveIndex : -1;
			if (version >= 2)
				data.WeightRemapped = r.ReadBoolean();
			return data;
		}

		public static byte[] SerializeSingleRenderer(DeformData data)
		{
			if (data == null || data.Layers.Count == 0)
				return null;
			int vertexCount = data.Layers[0].Deltas.Length;
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(ms))
			{
				WriteHeader(w);
				w.Write(vertexCount);
				w.Write(data.Layers.Count);
				w.Write(data.ActiveLayerIndex);
				foreach (DeformLayer layer in data.Layers)
				{
					w.Write(layer.Name ?? "");
					w.Write(layer.Weight);
					w.Write(layer.Deltas.Length);
					for (var i = 0; i < layer.Deltas.Length; i++)
					{
						w.Write(layer.Deltas[i].x);
						w.Write(layer.Deltas[i].y);
						w.Write(layer.Deltas[i].z);
					}
				}
				return ms.ToArray();
			}
		}

		public static List<DeformLayer> DeserializeSingleRenderer(byte[] data, out int fileVertexCount)
		{
			fileVertexCount = 0;
			if (data == null || data.Length < 3 || !HasMagicHeader(data))
				return null;
			try
			{
				using (MemoryStream ms = new MemoryStream(data))
				using (BinaryReader r = new BinaryReader(ms))
				{
					r.ReadByte(); r.ReadByte();
					if (r.ReadByte() > 2)
						return null;

					fileVertexCount = r.ReadInt32();
					if (fileVertexCount < 0 || fileVertexCount > 10000000)
						return null;

					int layerCount = r.ReadInt32();
					if (layerCount < 0 || layerCount > 256)
						return null;

					r.ReadInt32(); // active layer index (not used when importing individual renderer)
					var layers = new List<DeformLayer>(layerCount);
					for (var i = 0; i < layerCount; i++)
					{
						string name = r.ReadString();
						float weight = r.ReadSingle();
						int vertexCount = r.ReadInt32();
						if (vertexCount != fileVertexCount)
							return null;
						DeformLayer layer = new DeformLayer(name, vertexCount);
						layer.Weight = weight;
						for (var j = 0; j < vertexCount; j++)
						{
							layer.Deltas[j].x = r.ReadSingle();
							layer.Deltas[j].y = r.ReadSingle();
							layer.Deltas[j].z = r.ReadSingle();
						}
						layers.Add(layer);
					}
					return layers;
				}
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("ShapeSerializer.DeserializeSingleRenderer failed: " + ex.Message);
				return null;
			}
		}

		public static byte[] SerializeSubdivisionInfo(Dictionary<string, int> levels, Dictionary<string, List<int[]>> faces)
		{
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(ms))
			{
				WriteHeader(w);
				WriteSubdivisionLevels(w, levels);
				WriteSubdivisionFaces(w, faces);
				return ms.ToArray();
			}
		}

		public static void DeserializeSubdivisionInfo(byte[] data, out Dictionary<string, int> levels, out Dictionary<string, List<int[]>> faces)
		{
			levels = null;
			faces = null;
			if (data == null || data.Length < 3 || !HasMagicHeader(data))
				return;
			try
			{
				using (MemoryStream ms = new MemoryStream(data))
				using (BinaryReader r = new BinaryReader(ms))
				{
					r.ReadByte(); r.ReadByte(); r.ReadByte();
					levels = ReadSubdivisionLevels(r);
					faces = ReadSubdivisionFaces(r);
				}
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("ShapeSerializer.DeserializeSubdivisionInfo failed: " + ex.Message);
			}
		}

		private static void WriteSubdivisionLevels(BinaryWriter w, Dictionary<string, int> levels)
		{
			if (levels == null)
			{
				w.Write(0);
				return;
			}
			w.Write(levels.Count);
			foreach (KeyValuePair<string, int> pair in levels)
			{
				w.Write(pair.Key ?? "");
				w.Write(pair.Value);
			}
		}

		private static Dictionary<string, int> ReadSubdivisionLevels(BinaryReader r)
		{
			int count = r.ReadInt32();
			Dictionary<string, int> result = new Dictionary<string, int>(count);
			for (int i = 0; i < count; i++)
				result[r.ReadString()] = r.ReadInt32();
			return result;
		}

		private static void WriteSubdivisionFaces(BinaryWriter w, Dictionary<string, List<int[]>> faces)
		{
			if (faces == null)
			{
				w.Write(0);
				return;
			}
			w.Write(faces.Count);
			foreach (KeyValuePair<string, List<int[]>> pair in faces)
			{
				w.Write(pair.Key ?? "");
				List<int[]> faceList = pair.Value;
				w.Write(faceList.Count);
				foreach (int[] face in faceList)
				{
					if (face == null)
					{
						w.Write(-1);
					}
					else
					{
						w.Write(face.Length);
						foreach (int t in face) w.Write(t);
					}
				}
			}
		}

		private static Dictionary<string, List<int[]>> ReadSubdivisionFaces(BinaryReader r)
		{
			int rendererCount = r.ReadInt32();
			Dictionary<string, List<int[]>> result = new Dictionary<string, List<int[]>>(rendererCount);
			for (int i = 0; i < rendererCount; i++)
			{
				string key = r.ReadString();
				int faceCount = r.ReadInt32();
				List<int[]> faceList = new List<int[]>(faceCount);
				for (int j = 0; j < faceCount; j++)
				{
					int faceLen = r.ReadInt32();
					if (faceLen < 0)
					{
						faceList.Add(null);
					}
					else
					{
						int[] face = new int[faceLen];
						for (int k = 0; k < faceLen; k++)
							face[k] = r.ReadInt32();
						faceList.Add(face);
					}
				}
				result[key] = faceList;
			}
			return result;
		}

		public static byte[] SerializeItemDict(Dictionary<int, ItemSaveData> dict)
		{
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(ms))
			{
				WriteHeader(w);
				w.Write(dict.Count);
				foreach (KeyValuePair<int, ItemSaveData> pair in dict)
				{
					w.Write(pair.Key);
					WriteItemSaveData(w, pair.Value);
				}
				return ms.ToArray();
			}
		}

		public static Dictionary<int, ItemSaveData> DeserializeItemDict(byte[] data)
		{
			if (data == null || data.Length < 3 || !HasMagicHeader(data))
				return null;
			try
			{
				using (MemoryStream ms = new MemoryStream(data))
				using (BinaryReader r = new BinaryReader(ms))
				{
					r.ReadByte(); r.ReadByte();
					byte version = r.ReadByte();
					int count = r.ReadInt32();
					Dictionary<int, ItemSaveData> result = new Dictionary<int, ItemSaveData>(count);
					for (int i = 0; i < count; i++)
					{
						int key = r.ReadInt32();
						result[key] = ReadItemSaveData(r, version);
					}
					return result;
				}
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("ShapeSerializer.DeserializeItemDict failed: " + ex.Message);
				return null;
			}
		}

		public static byte[] SerializeMapItemDict(Dictionary<string, ItemSaveData> dict)
		{
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(ms))
			{
				WriteHeader(w);
				w.Write(dict.Count);
				foreach (KeyValuePair<string, ItemSaveData> pair in dict)
				{
					w.Write(pair.Key ?? "");
					WriteItemSaveData(w, pair.Value);
				}
				return ms.ToArray();
			}
		}

		public static Dictionary<string, ItemSaveData> DeserializeMapItemDict(byte[] data)
		{
			if (data == null || data.Length < 3 || !HasMagicHeader(data))
				return null;
			try
			{
				using (MemoryStream ms = new MemoryStream(data))
				using (BinaryReader r = new BinaryReader(ms))
				{
					r.ReadByte(); r.ReadByte();
					byte version = r.ReadByte();
					int count = r.ReadInt32();
					Dictionary<string, ItemSaveData> result = new Dictionary<string, ItemSaveData>(count);
					for (int i = 0; i < count; i++)
					{
						string key = r.ReadString();
						result[key] = ReadItemSaveData(r, version);
					}
					return result;
				}
			}
			catch (Exception ex)
			{
				ShapeEditorPlugin.Logger.LogWarning("ShapeSerializer.DeserializeMapItemDict failed: " + ex.Message);
				return null;
			}
		}

		private static void WriteItemSaveData(BinaryWriter w, ItemSaveData saveData)
		{
			bool hasDeformData = saveData.DeformDataMap != null && saveData.DeformDataMap.Count > 0;
			w.Write(hasDeformData);
			if (hasDeformData)
			{
				w.Write(saveData.DeformDataMap.Count);
				foreach (KeyValuePair<string, DeformData> pair in saveData.DeformDataMap)
				{
					w.Write(pair.Key ?? "");
					WriteDeformData(w, pair.Value);
				}
			}
			WriteSubdivisionLevels(w, saveData.SubdividedMeshes);
			WriteSubdivisionFaces(w, saveData.SubdividedFaces);
		}

		private static ItemSaveData ReadItemSaveData(BinaryReader r, byte version)
		{
			ItemSaveData saveData = new ItemSaveData();
			if (r.ReadBoolean())
			{
				int count = r.ReadInt32();
				saveData.DeformDataMap = new Dictionary<string, DeformData>(count);
				for (int i = 0; i < count; i++)
				{
					string rendererPath = r.ReadString();
					DeformData deformData = ReadDeformData(r, version, rendererPath);
					if (deformData != null)
						saveData.DeformDataMap[rendererPath] = deformData;
				}
			}
			saveData.SubdividedMeshes = ReadSubdivisionLevels(r);
			saveData.SubdividedFaces = ReadSubdivisionFaces(r);
			return saveData;
		}

		private static void WriteHeader(BinaryWriter w)
		{
			w.Write(Magic0);
			w.Write(Magic1);
			w.Write(FormatVersion);
		}

		private static bool HasMagicHeader(byte[] data)
		{
			return data.Length >= 2 && data[0] == Magic0 && data[1] == Magic1;
		}
	}
}
