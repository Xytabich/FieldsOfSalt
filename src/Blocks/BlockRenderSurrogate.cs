﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public class BlockRenderSurrogate : Block, IMultiblockPhantomBlock
	{
		private FieldsOfSaltMod mod;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			mod = api.ModLoader.GetModSystem<FieldsOfSaltMod>();
		}

		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(blockSel.Position, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.OnBlockInteractStart(world, byPlayer, mainPos, blockSel);
				}
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}

		public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(blockSel.Position, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.OnBlockInteractStep(secondsUsed, world, byPlayer, mainPos, blockSel);
				}
			}
			return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
		}

		public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(blockSel.Position, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					main.OnBlockInteractStop(secondsUsed, world, byPlayer, mainPos, blockSel);
					return;
				}
			}
			base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
		}

		public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(blockSel.Position, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.OnBlockInteractCancel(secondsUsed, world, byPlayer, mainPos, blockSel, cancelReason);
				}
			}
			return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
		}

		public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					main.OnBlockBroken(world, mainPos, pos, byPlayer, dropQuantityMultiplier);
					return;
				}
			}
			base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
		}

		public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					main.OnPartRemoved(world, mainPos, pos);
				}
			}
			base.OnBlockRemoved(world, pos);
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetDrops(world, mainPos, pos, byPlayer, dropQuantityMultiplier);
				}
			}
			return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
		}

		public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(api.World.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					main.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, mainPos, pos, chunkExtBlocks, extIndex3d);
					return;
				}
			}
			base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);
		}

		public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.OnPickBlock(world, mainPos, pos);
				}
			}
			return base.OnPickBlock(world, pos);
		}

		public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetPlacedBlockInfo(world, mainPos, pos, forPlayer);
				}
			}
			return base.GetPlacedBlockInfo(world, pos, forPlayer);
		}

		public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetPlacedBlockName(world, mainPos, pos);
				}
			}
			return base.GetPlacedBlockName(world, pos);
		}

		public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(capi.World.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetColorWithoutTint(capi, mainPos, pos);
				}
			}
			return base.GetColorWithoutTint(capi, pos);
		}

		public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(selection.Position, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetPlacedBlockInteractionHelp(world, mainPos, selection, forPlayer);
				}
			}
			return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
		}

		public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(blockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetCollisionBoxes(blockAccessor, mainPos, pos);
				}
			}
			return base.GetCollisionBoxes(blockAccessor, pos);
		}

		public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(capi.World.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					return main.GetRandomColor(capi, mainPos, pos, facing, rndIndex);
				}
			}
			return base.GetRandomColor(capi, pos, facing, rndIndex);
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