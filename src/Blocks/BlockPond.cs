using FieldsOfSalt.Blocks.Entities;
using FieldsOfSalt.Recipes;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public class BlockPond : Block, IMultiblockMainBlock, IMultiblockPhantomBlock
	{
		private EvaporationRecipe[] recipes = null;

		public bool TryGetRecipe(IBlockAccessor blockAccessor, BlockPos pos, ItemStack forLiquid, out EvaporationRecipe evaporationRecipe)
		{
			EnsureRecipes();

			int index = Array.FindIndex(recipes, r => r.Input.ResolvedItemstack.Satisfies(forLiquid));
			if(index >= 0)
			{
				evaporationRecipe = recipes[index];
				return true;
			}

			evaporationRecipe = null;
			return false;
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
				sourceMesh = ((ICoreClientAPI)api).TesselatorManager.GetDefaultBlockMesh(pond.GetPartBlockAt(partPos));
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

		public string GetPlacedBlockName(IWorldAccessor world, BlockPos mainPos, BlockPos partPos)
		{
			return GetPlacedBlockName(world, mainPos);
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

		private void EnsureRecipes()
		{
			if(recipes == null)
			{
				var recipes = new List<EvaporationRecipe>();
				try
				{
					var list = Attributes?["recipes"].AsArray<EvaporationRecipe>(null, Code.Domain);
					if(list != null)
					{
						for(int i = 0; i < list.Length; i++)
						{
							if(list[i].Input.Resolve(api.World, "evaporation recipe input"))
							{
								if(list[i].Output.Resolve(api.World, "evaporation recipe output"))
								{
									recipes.Add(list[i]);
								}
							}
						}
					}
				}
				catch(Exception e)
				{
					api.Logger.Warning("Exception when trying to parse evaporation recipes:\n", e);
				}
				this.recipes = recipes.ToArray();
			}
		}
	}
}