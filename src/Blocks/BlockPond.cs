﻿using FieldsOfSalt.Blocks.Entities;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public class BlockPond : Block, IMultiblockMainBlock, IMultiblockPhantomBlock
	{
		public double CellCapacity;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			CellCapacity = Attributes?["cellLiquidCapacity"].AsDouble(0.5) ?? 0.5;
		}

		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			return OnBlockInteractStart(world, byPlayer, blockSel.Position, blockSel);
		}

		public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel)
		{
			if(api.World.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				pond.OnBlockInteract(world, byPlayer, blockSel);
			}
			return api.Side == EnumAppSide.Client;
		}

		public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			return false;
		}

		public bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel)
		{
			return false;
		}

		public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
		}

		public void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel)
		{
		}

		public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
		{
			return true;
		}

		public bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockPos mainPos, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
		{
			return true;
		}

		public void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos mainPos, BlockPos partPos, Block[] chunkExtBlocks, int extIndex3d)
		{
			if(api.World.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				var block = pond.GetPartBlockAt(partPos);
				if(block != null) sourceMesh = ((ICoreClientAPI)api).TesselatorManager.GetDefaultBlockMesh(block);
			}
		}

		public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			OnBlockBroken(world, pos, pos, byPlayer, dropQuantityMultiplier);
		}

		public void OnBlockBroken(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			if(world.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				var block = pond.GetPartBlockAt(partPos);
				if(block != null)
				{
					pond.DisassembleMultiblock();

					block.OnBlockBroken(world, partPos, byPlayer, dropQuantityMultiplier);
					return;
				}
			}
			world.BlockAccessor.SetBlock(0, partPos);
		}

		public void OnPartRemoved(IWorldAccessor world, BlockPos mainPos, BlockPos partPos)
		{
			if(world.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				pond.DisassembleMultiblock();
			}
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			return GetDrops(world, pos, pos, byPlayer, dropQuantityMultiplier);
		}

		public ItemStack[] GetDrops(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			if(world.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				return pond.GetDrops(partPos, byPlayer, dropQuantityMultiplier);
			}
			return Array.Empty<ItemStack>();
		}

		public ItemStack OnPickBlock(IWorldAccessor world, BlockPos mainPos, BlockPos partPos)
		{
			return OnPickBlock(world, mainPos);
		}

		public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
		{
			return new ItemStack(world.GetItem(AssetLocation.Create(Attributes["templateItem"].AsString(), Code.Domain)));
		}

		public string GetPlacedBlockName(IWorldAccessor world, BlockPos mainPos, BlockPos partPos)
		{
			return GetPlacedBlockName(world, mainPos);
		}

		public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
		{
			var sb = new StringBuilder();
			sb.Append(new ItemStack(this).GetName());
			foreach(var bb in BlockBehaviors)
			{
				bb.GetPlacedBlockName(sb, world, pos);
			}

			return sb.ToString().TrimEnd();
		}

		public string GetPlacedBlockInfo(IWorldAccessor world, BlockPos mainPos, BlockPos partPos, IPlayer forPlayer)
		{
			return GetPlacedBlockInfo(world, mainPos, forPlayer);
		}

		public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
		{
			return GetPlacedBlockInteractionHelp(world, selection.Position, selection, forPlayer);
		}

		public WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockPos mainPos, BlockSelection selection, IPlayer forPlayer)
		{
			if(api.World.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				return pond.GetInteractionHelp(world, selection, forPlayer);
			}
			return Array.Empty<WorldInteraction>();
		}

		public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
		{
			return GetColorWithoutTint(capi, pos, pos);
		}

		public int GetColorWithoutTint(ICoreClientAPI capi, BlockPos mainPos, BlockPos partPos)
		{
			if(capi.World.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				var color = pond.GetColorWithoutTint(partPos);
				if(color.HasValue) return color.Value;
			}
			return base.GetColorWithoutTint(capi, mainPos);
		}

		public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
		{
			return GetCollisionBoxes(blockAccessor, pos, pos);
		}

		public Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos mainPos, BlockPos partPos)
		{
			if(blockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				var block = pond.GetPartBlockAt(partPos);
				if(block != null) return block.GetCollisionBoxes(blockAccessor, partPos);
			}
			return base.GetCollisionBoxes(blockAccessor, mainPos);
		}

		public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
		{
			return GetRandomColor(capi, pos, pos, facing, rndIndex);
		}

		public int GetRandomColor(ICoreClientAPI capi, BlockPos mainPos, BlockPos partPos, BlockFacing facing, int rndIndex = -1)
		{
			if(capi.World.BlockAccessor.GetBlockEntity(mainPos) is BlockEntityPond pond)
			{
				var block = pond.GetPartBlockAt(partPos);
				if(block != null) return block.GetRandomColor(capi, partPos, facing, rndIndex);
			}
			return base.GetRandomColor(capi, mainPos, facing, rndIndex);
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