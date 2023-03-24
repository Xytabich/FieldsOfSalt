﻿using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public class BlockConnector : Block, ILiquidSinkConnector, IMultiblockPartBlock
	{
		private EnumAxis axis;

		private FieldsOfSaltMod mod;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			mod = api.ModLoader.GetModSystem<FieldsOfSaltMod>();
			axis = Variant["side"] == "we" ? EnumAxis.X : EnumAxis.Z;
		}

		public bool CanConnect(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face)
		{
			return axis == EnumAxis.X ? face.IsAxisWE : face.IsAxisNS;
		}

		public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
		{
			base.OnBlockRemoved(world, pos);

			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(world.BlockAccessor.GetBlock(mainPos) is IMultiblockMainBlock main)
				{
					main.OnPartRemoved(world, mainPos, pos);
				}
			}
		}

		public ILiquidSink GetLiquidSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				if(blockAccessor.GetBlockEntity(mainPos) is ILiquidSink sink)
				{
					return sink;
				}
			}
			return null;
		}

		public override bool DisplacesLiquids(IBlockAccessor blockAccess, BlockPos pos)
		{
			return true;
		}

		public override float LiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
		{
			return 1f;
		}
	}
}