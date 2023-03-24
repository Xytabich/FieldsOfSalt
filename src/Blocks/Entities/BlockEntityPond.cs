using FieldsOfSalt.Recipes;
using FieldsOfSalt.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FieldsOfSalt.Blocks.Entities
{
	public class BlockEntityPond : BlockEntity, ILiquidSink
	{
		private const string SIZE_X_ATTR = "width";
		private const string SIZE_Y_ATTR = "length";
		private const string GRID_ATTR = "grid";
		private const string BLOCK_IDS_ATTR = "blocks";
		private const string LAYERS_PACKED_ATTR = "layers";
		private const string PREV_CALENDAR_HOUR_ATTR = "prevHour";
		private const string LAYER_PROGRESS_ATTR = "progress";
		private const string CURRENT_LIQUID_ATTR = "liquid";
		private const string RECIPE_LIQUID_ATTR = "recipe";

		private Vec2i size = null;
		private ushort[] grid = null;
		private int[] blockIds = null;//TODO: CollectibleMappings

		private byte[] layersPacked = null;//4 bits (15 items) per block
		private double prevCalendarHour = 0;
		private double layerProgress = 0;

		// Calculated
		private int evaporationArea = 0;
		private int liquidCapacity = 0;

		private bool markDirtyNext = false;
		private bool removeInvalidStructure = false;

		private ItemStack currentLiquidStack = null;
		private EvaporationRecipe recipe = null;

		private FieldsOfSaltMod mod;

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			mod = api.ModLoader.GetModSystem<FieldsOfSaltMod>();
			if(removeInvalidStructure)
			{
				mod.RemoveReferenceToMainBlock(Pos, Pos);
				Api.Logger.Warning("Received invalid multiblock structure from tree attributes, block will be removed");
				Api.World.BlockAccessor.SetBlock(0, Pos);
				return;
			}
			var inputToResolve = currentLiquidStack ?? recipe?.Input.ResolvedItemstack;
			if(inputToResolve != null)
			{
				if(!((BlockPond)Block).TryGetRecipe(api.World.BlockAccessor, Pos, inputToResolve, out recipe))
				{
					recipe = null;
					currentLiquidStack = null;
				}
			}
			if(recipe != null)
			{
				CalculateAreaAndCapacity();
			}
			if(api.Side == EnumAppSide.Server)
			{
				RegisterGameTickListener(OnTick, 1000);
			}
		}

		public override void OnBlockPlaced(ItemStack byItemStack = null)
		{
			try
			{
				if(byItemStack == null) throw new InvalidOperationException("Trying to place multiblock without structure info");

				size = new Vec2i(byItemStack.Attributes.GetInt(SIZE_X_ATTR, 5), byItemStack.Attributes.GetInt(SIZE_Y_ATTR, 5));
				grid = byItemStack.Attributes.ReadUShortArray(GRID_ATTR);
				blockIds = ((IntArrayAttribute)byItemStack.Attributes[BLOCK_IDS_ATTR]).value;
				layersPacked = new byte[(((size.X - 2) * (size.Y - 2)) >> 1) + 1];
				prevCalendarHour = Api.World.Calendar.TotalHours;
				layerProgress = 0;

				int yStep = size.X;
				int xLast = size.X - 1;
				int yLast = size.X * (size.Y - 1);
				int xPosStart = Pos.X - (size.X >> 1);

				var tmpPos = Pos.Copy();
				for(int y = 0, zp = (Pos.Z - (size.Y >> 1)); y <= yLast; y += yStep, zp++)
				{
					tmpPos.Z = zp;
					if(y == 0 || y == yLast)// Ignore corners
					{
						for(int x = 1, xp = xPosStart; x < xLast; x++, xp++)
						{
							tmpPos.X = xp;
							mod.AddReferenceToMainBlock(tmpPos, Pos);
						}
					}
					else
					{
						for(int x = 0, xp = xPosStart; x <= xLast; x++, xp++)
						{
							tmpPos.X = xp;
							mod.AddReferenceToMainBlock(tmpPos, Pos);
						}
					}
				}

				base.OnBlockPlaced(byItemStack);
			}
			catch(Exception e)
			{
				Api.Logger.Warning("Exception when trying to read multiblock structure, placement will be canceled\n{0}", e);
				Api.World.BlockAccessor.SetBlock(0, Pos);
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			base.ToTreeAttributes(tree);
			tree.SetInt(SIZE_X_ATTR, size.X);
			tree.SetInt(SIZE_Y_ATTR, size.Y);
			tree.WriteUShortArray(GRID_ATTR, grid);
			tree[BLOCK_IDS_ATTR] = new IntArrayAttribute(blockIds);
			tree[LAYERS_PACKED_ATTR] = new ByteArrayAttribute(layersPacked);
			tree.SetDouble(PREV_CALENDAR_HOUR_ATTR, prevCalendarHour);
			tree.SetDouble(LAYER_PROGRESS_ATTR, layerProgress);
			tree.SetItemstack(CURRENT_LIQUID_ATTR, currentLiquidStack);
			if(currentLiquidStack == null)
			{
				tree.SetItemstack(RECIPE_LIQUID_ATTR, recipe?.Input.ResolvedItemstack);
			}
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
		{
			base.FromTreeAttributes(tree, worldAccessForResolve);
			size = new Vec2i(tree.GetInt(SIZE_X_ATTR, 5), tree.GetInt(SIZE_Y_ATTR, 5));
			grid = tree.ReadUShortArray(GRID_ATTR);
			blockIds = (tree[BLOCK_IDS_ATTR] as IntArrayAttribute)?.value;
			layersPacked = (tree[LAYERS_PACKED_ATTR] as ByteArrayAttribute)?.value;
			prevCalendarHour = tree.GetDouble(PREV_CALENDAR_HOUR_ATTR);
			layerProgress = tree.GetDouble(LAYER_PROGRESS_ATTR);
			currentLiquidStack = tree.GetItemstack(CURRENT_LIQUID_ATTR);
			if(currentLiquidStack != null && !currentLiquidStack.ResolveBlockOrItem(worldAccessForResolve))
			{
				currentLiquidStack = null;
			}
			var recipeLiquid = tree.GetItemstack(RECIPE_LIQUID_ATTR);
			if(recipeLiquid != null && !recipeLiquid.ResolveBlockOrItem(worldAccessForResolve))
			{
				recipeLiquid = null;
			}
			if(grid == null)
			{
				removeInvalidStructure = true;
				if(Api != null)
				{
					mod.RemoveReferenceToMainBlock(Pos, Pos);
					Api.Logger.Warning("Received invalid multiblock structure from tree attributes, block will be removed");
					Api.World.BlockAccessor.SetBlock(0, Pos);
					return;
				}
			}
			if(Api == null)
			{
				recipe = recipeLiquid == null ? null : new EvaporationRecipe() { Input = new JsonItemStack() { ResolvedItemstack = recipeLiquid } };
			}
			else
			{
				var inputToResolve = currentLiquidStack ?? recipe?.Input.ResolvedItemstack;
				if(inputToResolve != null)
				{
					if(!((BlockPond)Block).TryGetRecipe(Api.World.BlockAccessor, Pos, inputToResolve, out recipe))
					{
						recipe = null;
						currentLiquidStack = null;
					}
				}
				if(recipe != null)
				{
					CalculateAreaAndCapacity();
				}
			}
		}

		public void DisassembleMultiblock()
		{
			layersPacked = null;//TODO: drop all items

			int yStep = size.X;
			int xLast = size.X - 1;
			int yLast = size.X * (size.Y - 1);
			int xPosStart = Pos.X - (size.X >> 1);

			var accessor = Api.World.GetBlockAccessorBulkUpdate(false, false);
			var tmpPos = Pos.Copy();
			for(int y = 0, zp = (Pos.Z - (size.Y >> 1)); y <= yLast; y += yStep, zp++)
			{
				tmpPos.Z = zp;
				for(int x = 0, xp = xPosStart; x <= xLast; x++, xp++)
				{
					tmpPos.X = xp;

					int index = grid[y + x];
					if(mod.RemoveReferenceToMainBlock(tmpPos, Pos) && index != ushort.MaxValue)
					{
						if(accessor.GetBlock(tmpPos) is IMultiblockPhantomBlock)
						{
							accessor.SetBlock(blockIds[index], tmpPos);
						}
					}
				}
			}
			accessor.Commit();
		}

		public ItemStack[] GetDrops(BlockPos partPos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
		{
			return GetPartBlockAt(partPos)?.GetDrops(Api.World, partPos, byPlayer, dropQuantityMultiplier) ?? Array.Empty<ItemStack>();
		}

		public Block GetPartBlockAt(BlockPos partPos)
		{
			int index = (partPos.X - (Pos.X - (size.X >> 1))) + (partPos.Z - (Pos.Z - (size.Y >> 1))) * size.X;
			if(index < 0 || index >= grid.Length) return null;
			return Api.World.GetBlock(grid[index]);
		}

		public void OnBlockInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			//TODO: pick result
		}

		public WorldInteraction[] GetInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
		{
			return Array.Empty<WorldInteraction>();//TODO: show help & amount of salt
		}

		public bool TryAccept(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ref ItemStack liquid)
		{
			if(liquidCapacity <= 0) return false;
			if(currentLiquidStack == null)
			{
				if(((BlockPond)Block).TryGetRecipe(blockAccessor, pos, liquid, out var evaporationRecipe) &&
					(recipe == null || recipe == evaporationRecipe || recipe.Output.ResolvedItemstack.Equals(
						Api.World, evaporationRecipe.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
				{
					if(recipe == null)
					{
						Buffer.SetByte(layersPacked, 0, 0);
						layerProgress = 0;
						prevCalendarHour = Api.World.Calendar.TotalHours;
						evaporationArea = (size.X - 2) * (size.Y - 2);
						CalculateLiquidCapacity(0);
					}
					recipe = evaporationRecipe;
					currentLiquidStack = liquid.GetEmptyClone();
					PickLiquid(liquid);
					return true;
				}
			}
			else if(liquid.Equals(Api.World, currentLiquidStack, GlobalConstants.IgnoredStackAttributes))
			{
				PickLiquid(liquid);
				return true;
			}
			return false;
		}

		public void SetLiquidLevel(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, int level, WaterTightContainableProps liquidProps)
		{
		}

		private void PickLiquid(ItemStack liquid)
		{
			if(liquid.StackSize > liquidCapacity)
			{
				liquid.StackSize = liquidCapacity;
			}
			currentLiquidStack.StackSize += liquid.StackSize;
			markDirtyNext = true;
		}

		private void OnTick(float dt)
		{
			if(recipe == null) return;

			if(currentLiquidStack != null)
			{
				var temperature = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
					prevCalendarHour / Api.World.Calendar.HoursPerDay).Temperature;
				var timeProgress = recipe.GetProgress(prevCalendarHour - Api.World.Calendar.TotalHours, temperature);

				double unitsInLayer = recipe.Input.StackSize * evaporationArea;
				int prevStepEvaporatedTotal = (int)(layerProgress * unitsInLayer);
				int evaporatedUnits = (int)((layerProgress + timeProgress) * unitsInLayer) - prevStepEvaporatedTotal;
				if(evaporatedUnits > 0)
				{
					evaporatedUnits = Math.Min(evaporatedUnits, currentLiquidStack.StackSize);
					currentLiquidStack.StackSize -= evaporatedUnits;
					if(currentLiquidStack.StackSize == 0) currentLiquidStack = null;
					markDirtyNext = true;//TODO: maybe use small network messages instead? if only need to update liquid amount

					layerProgress += (double)evaporatedUnits / unitsInLayer;
					if(layerProgress >= 1)
					{
						int addLayers = (int)layerProgress;
						layerProgress -= addLayers;
						int addOutputUnitsAmount = addLayers * recipe.Output.StackSize;

						int count = ((size.X - 2) * (size.Y - 2)) >> 1;
						int usedLayers = 0;
						for(int i = 0; i < count; i++)
						{
							uint value = layersPacked[i];
							uint newValue = 0;

							int size = (int)(value & 0b00001111);
							if(size < 15)
							{
								size += addLayers;
								if(size > 15)
								{
									newValue = (uint)15;
									addOutputUnitsAmount -= 15 - addLayers;
									addLayers = addOutputUnitsAmount / recipe.Output.StackSize;
									evaporationArea--;
									usedLayers += 15;
								}
								else
								{
									newValue = (uint)size;
									addOutputUnitsAmount -= addLayers;
									if(size == 15) evaporationArea--;
									usedLayers += size;
								}
							}
							else
							{
								usedLayers += 15;
							}

							size = (int)(value >> 4);
							if(size < 15)
							{
								size += addLayers;
								if(size > 15)
								{
									newValue |= (uint)0b11110000;
									addOutputUnitsAmount -= 15 - addLayers;
									addLayers = addOutputUnitsAmount / recipe.Output.StackSize;
									evaporationArea--;
									usedLayers += 15;
								}
								else
								{
									newValue |= (uint)size << 4;
									addOutputUnitsAmount -= addLayers;
									if(size == 15) evaporationArea--;
									usedLayers += size;
								}
							}
							else
							{
								usedLayers += 15;
							}

							layersPacked[i] = (byte)newValue;
						}
						{
							int size = addOutputUnitsAmount + layersPacked[count];
							if(size >= 15)
							{
								layersPacked[count] = 15;
								usedLayers += 15;
								evaporationArea--;
							}
							else
							{
								layersPacked[count] = (byte)size;
								usedLayers += size;
							}
						}
						CalculateLiquidCapacity(usedLayers);
						markDirtyNext = true;
					}
				}
			}
			prevCalendarHour = Api.World.Calendar.TotalHours;
			if(markDirtyNext)
			{
				markDirtyNext = false;
				MarkDirty(true);
			}
		}

		private void CalculateAreaAndCapacity()
		{
			evaporationArea = (size.X - 2) * (size.Y - 2);
			int usedLayers = 0;
			int count = ((size.X - 2) * (size.Y - 2)) >> 1;
			for(int i = 0; i < count; i++)
			{
				uint value = layersPacked[i];

				int size = (int)(value & 0b00001111);
				usedLayers += size;
				if(size == 15) evaporationArea--;

				size = (int)(value >> 4);
				usedLayers += size;
				if(size == 15) evaporationArea--;
			}
			{
				int size = layersPacked[count];
				usedLayers += size;
				if(size == 15) evaporationArea--;
			}
			CalculateLiquidCapacity(usedLayers);
		}

		private void CalculateLiquidCapacity(int layersUsedByOutput)
		{
			double volumeForLiquid = ((size.X - 2) * (size.Y - 2) * 15 - layersUsedByOutput) * (1.0 / 30);
			liquidCapacity = (int)Math.Ceiling(volumeForLiquid * (double)recipe.InputProps.ItemsPerLitre);
		}

		/// <summary>
		/// Creates a structure in the specified area.
		/// The structure must be validated beforehand.
		/// </summary>
		public static void CreateStructure(IWorldAccessor world, BlockPos fromPos, BlockPos toPos, ItemStack mainBlock, ItemStack surrogateBlock)
		{
			var size = new Vec2i(toPos.X - fromPos.X, toPos.Z - fromPos.Z);

			var blockId2index = new Dictionary<int, int>();
			var grid = new ushort[size.X * size.Y];

			int yStep = size.X;
			int xLast = size.X - 1;
			int yLast = size.X * (size.Y - 1);

			ushort GetBlockIndex(int id)
			{
				if(!blockId2index.TryGetValue(id, out var index))
				{
					index = blockId2index.Count;
					blockId2index[id] = index;
				}
				return (ushort)index;
			}

			var blockAccessor = world.GetBlockAccessorBulkUpdate(false, false);

			var tmpPos = fromPos.Copy();
			for(int y = 0, zp = fromPos.Z; y <= yLast; y += yStep, zp++)//TODO: collect connectors to add sink to sources later
			{
				tmpPos.Z = zp;
				if(y == 0 || y == yLast)// Ignore corners
				{
					grid[y] = ushort.MaxValue;
					grid[y + xLast] = ushort.MaxValue;
					for(int x = 1, xp = fromPos.X + 1; x < xLast; x++, xp++)
					{
						tmpPos.X = xp;

						var block = blockAccessor.GetBlock(tmpPos);
						if(block is IMultiblockPartBlock)
						{
							grid[y + x] = ushort.MaxValue;
						}
						else
						{
							grid[y + x] = GetBlockIndex(block.Id);
							blockAccessor.SetBlock(surrogateBlock.Id, tmpPos, surrogateBlock);
						}
					}
				}
				else
				{
					for(int x = 0, xp = fromPos.X; x <= xLast; x++, xp++)
					{
						tmpPos.X = xp;

						var block = blockAccessor.GetBlock(tmpPos);
						if(block is IMultiblockPartBlock)
						{
							grid[y + x] = ushort.MaxValue;
						}
						else
						{
							grid[y + x] = GetBlockIndex(block.Id);
							blockAccessor.SetBlock(surrogateBlock.Id, tmpPos, surrogateBlock);
						}
					}
				}
			}

			var stack = mainBlock.Clone();
			stack.Attributes.SetInt(SIZE_X_ATTR, size.X);
			stack.Attributes.SetInt(SIZE_Y_ATTR, size.Y);
			stack.Attributes.WriteUShortArray(GRID_ATTR, grid);

			var blockIds = new int[blockId2index.Count];
			foreach(var pair in blockId2index) blockIds[pair.Value] = pair.Key;
			stack.Attributes[BLOCK_IDS_ATTR] = new IntArrayAttribute(blockIds);

			tmpPos.X = size.X >> 1;
			tmpPos.Z = size.Y >> 1;
			blockAccessor.SetBlock(mainBlock.Id, tmpPos, stack);

			blockAccessor.Commit();
		}
	}
}