using System.Collections.Generic;

namespace KKShapeEditor
{
	public class ItemSaveData
	{
		public Dictionary<string, DeformData> DeformDataMap;
		public Dictionary<string, int> SubdividedMeshes;
		public Dictionary<string, List<int[]>> SubdividedFaces;
	}
}
