using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public interface IMultiblockRenderMain
	{
		void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos mainPos, BlockPos partPos, Block[] chunkExtBlocks, int extIndex3d);
	}
}