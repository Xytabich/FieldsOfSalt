using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public interface IMultiblockMainBlock
	{
		void OnPartRemoved(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		void OnBlockBroken(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1);

		ItemStack[] GetDrops(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1);

		void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos mainPos, BlockPos partPos, Block[] chunkExtBlocks, int extIndex3d);

		ItemStack OnPickBlock(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		string GetPlacedBlockInfo(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer forPlayer);

		string GetPlacedBlockName(IWorldAccessor world, BlockPos mainPos, BlockPos partPos);

		WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockPos mainPos, BlockSelection selection, IPlayer forPlayer);

		bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel);

		bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel);

		void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel);

		bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel, EnumItemUseCancelReason cancelReason);
	}
}