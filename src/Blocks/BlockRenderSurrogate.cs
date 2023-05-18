using FieldsOfSalt.Multiblock;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public class BlockRenderSurrogate : BlockMultiblockSurrogate, IMultiblockPhantomBlock
	{
		private MultiblockManager manager;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			manager = api.ModLoader.GetModSystem<MultiblockManager>();
		}

		public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
		{
			var mainPos = new BlockPos();
			if(manager.GetReferenceToMainBlock(pos, mainPos))
			{
				if(api.World.BlockAccessor.GetBlock(mainPos) is IMultiblockRenderMain main)
				{
					main.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, mainPos, pos, chunkExtBlocks, extIndex3d);
					return;
				}
			}
			base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);
		}

		public override bool DisplacesLiquids(IBlockAccessor blockAccess, BlockPos pos)
		{
			return true;
		}

		public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
		{
			return 1f;
		}
	}
}