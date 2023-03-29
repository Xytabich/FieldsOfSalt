using FieldsOfSalt.Recipes;
using FieldsOfSalt.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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
		private int[] blockIds = null;

		private byte[] layersPacked = null;//4 bits (15 items) per block
		private double prevCalendarHour = 0;
		private double layerProgress = 0;

		private List<ConnectorInfo> connectors = new List<ConnectorInfo>();

		// Calculated
		private int evaporationArea = 0;
		private int liquidCapacity = 0;

		private bool markDirtyNext = false;
		private bool removeInvalidStructure = false;

		private TextureAtlasPosition liquidTexture = null;
		private WaterTightContainableProps liquidProps = null;
		private ItemStack currentLiquidStack = null;
		private EvaporationRecipe recipe = null;

		private MeshData mesh = null;

		private bool multiblockRegistered = false;
		private FieldsOfSaltMod mod;

		private WorldInteraction[] pickInteractionHelp = null;

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			mod = api.ModLoader.GetModSystem<FieldsOfSaltMod>();
			if(removeInvalidStructure)
			{
				mod.RemoveReferenceToMainBlock(Pos, Pos);
				Api.Logger.Warning("Received invalid multiblock structure from tree attributes, block at {0} will be removed", Pos);
				Api.World.BlockAccessor.SetBlock(0, Pos);
				return;
			}
			RegisterMultiblock();
			var inputToResolve = currentLiquidStack ?? recipe?.Input.ResolvedItemstack;
			if(inputToResolve != null)
			{
				if(!mod.TryGetRecipe(inputToResolve, out recipe))
				{
					recipe = null;
					currentLiquidStack = null;
				}
				if(api is ICoreClientAPI capi)
				{
					liquidProps = BlockLiquidContainerBase.GetContainableProps(inputToResolve);
					GraphicUtil.BakeTexture(capi, liquidProps.Texture, "Evaporation pond", out liquidTexture);
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
			if(Api.Side == EnumAppSide.Client)
			{
				base.OnBlockPlaced(byItemStack);
				return;
			}
			try
			{
				if(byItemStack == null) throw new InvalidOperationException("Trying to place multiblock without structure info");

				size = new Vec2i(byItemStack.Attributes.GetInt(SIZE_X_ATTR, 5), byItemStack.Attributes.GetInt(SIZE_Y_ATTR, 5));
				blockIds = ((IntArrayAttribute)byItemStack.Attributes[BLOCK_IDS_ATTR]).value;
				grid = byItemStack.Attributes.ReadPackedUShortArray(GRID_ATTR, (ushort)(blockIds.Length - 1), size.X * size.Y);
				layersPacked = new byte[(((size.X - 2) * (size.Y - 2)) >> 1) + 1];
				prevCalendarHour = Api.World.Calendar.TotalHours;
				layerProgress = 0;

				RegisterMultiblock();

				base.OnBlockPlaced(byItemStack);
			}
			catch(Exception e)
			{
				Api.Logger.Warning("Exception when trying to read multiblock structure, placement will be canceled\n{0}", e);
				Api.World.BlockAccessor.SetBlock(0, Pos);
			}
		}

		public override void OnBlockRemoved()
		{
			base.OnBlockRemoved();
			UnregisterMultiblock();
		}

		public override void OnBlockUnloaded()
		{
			base.OnBlockUnloaded();
			UnregisterMultiblock();
		}

		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			base.ToTreeAttributes(tree);
			tree.SetInt(SIZE_X_ATTR, size.X);
			tree.SetInt(SIZE_Y_ATTR, size.Y);
			tree.WritePackedUShortArray(GRID_ATTR, grid, (ushort)(blockIds.Length - 1), size.X * size.Y);
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
			blockIds = (tree[BLOCK_IDS_ATTR] as IntArrayAttribute)?.value;
			grid = tree.ReadPackedUShortArray(GRID_ATTR, (ushort)(blockIds == null ? 0 : (blockIds.Length - 1)), size.X * size.Y);
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
					Api.Logger.Warning("Received invalid multiblock structure from tree attributes, block at {0} will be removed", Pos);
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
				RegisterMultiblock();
				var inputToResolve = currentLiquidStack ?? recipe?.Input.ResolvedItemstack;
				if(inputToResolve != null)
				{
					if(!mod.TryGetRecipe(inputToResolve, out recipe))
					{
						recipe = null;
						currentLiquidStack = null;
					}
					if(Api is ICoreClientAPI capi)
					{
						liquidProps = BlockLiquidContainerBase.GetContainableProps(inputToResolve);
						GraphicUtil.BakeTexture(capi, liquidProps.Texture, "Evaporation pond", out liquidTexture);
					}
				}
				if(recipe != null)
				{
					CalculateAreaAndCapacity();
				}
			}
		}

		public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			var capi = (ICoreClientAPI)Api;

			var recipe = this.recipe;
			var layersPacked = this.layersPacked;
			if(recipe != null && layersPacked != null)
			{
				if(mesh == null) mesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();
				int sx = size.X - 2;
				int sz = size.Y - 2;
				int cx = sx >> 1;
				int cz = sz >> 1;

				var liquidTexture = this.liquidTexture ?? capi.BlockTextureAtlas.UnknownTexturePosition;
				int liquidColor = -1;//TODO: color doesn't work?
				int liquidGlow = liquidProps.GlowLevel;

				var bakedCT = recipe.OutputTexture?.Baked;
				var contentTexture = bakedCT == null ? capi.BlockTextureAtlas.UnknownTexturePosition : capi.BlockTextureAtlas.Positions[bakedCT.TextureSubId];
				int contentColor = -1;//TODO: color doesn't work?
				int contentGlow = 0;

				mesh.Clear();
				var offset = new Vec3f();
				float liquidLevel = (currentLiquidStack == null || liquidCapacity <= 0) ? 0f : (Math.Min(currentLiquidStack.StackSize / (float)liquidCapacity, 1f) * 0.8f);
				for(int z = 0; z < sz; z++)
				{
					int oz = z * sx;
					for(int x = 0; x < sx; x++)
					{
						offset.X = (x - cx) + 0.5f;
						offset.Z = (z - cz) + 0.5f;
						int i = oz + x;
						const float m = 1f / 15f;
						float level = ((i & 1) == 0 ? (layersPacked[i >> 1] & 15) : (layersPacked[i >> 1] >> 4)) * m;
						if(liquidLevel > level)
						{
							offset.Y = 0.5f + liquidLevel * 0.5f;
							GraphicUtil.AddLiquidBlockMesh(mesh, offset, liquidTexture, liquidColor, (short)EnumChunkRenderPass.Transparent, liquidGlow);
						}
						if(level > 0)
						{
							offset.Y = 0.5f;
							GraphicUtil.AddContentMesh(mesh, offset, level * 0.5f, contentTexture, contentColor, contentGlow);
						}
					}
				}
				if(mesh.VerticesCount > 0)
				{
					mesher.AddMeshData(mesh);
				}
			}
			var block = GetPartBlockAt(Pos);
			if(block != null) mesher.AddMeshData(capi.TesselatorManager.GetDefaultBlockMesh(block));
			return true;
		}

		public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
		{
			base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
			if(blockIds != null)
			{
				for(int i = 0; i < blockIds.Length; i++)
				{
					if(blockIds[i] == 0) continue;
					if(!oldBlockIdMapping.TryGetValue(blockIds[i], out var code))
					{
						blockIds[i] = 0;
						continue;
					}
					var block = worldForNewMappings.GetBlock(code);
					if(block == null)
					{
						blockIds[i] = 0;
						continue;
					}
					blockIds[i] = block.Id;
				}
			}
		}

		public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
		{
			base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
			if(blockIds != null)
			{
				for(int i = 0; i < blockIds.Length; i++)
				{
					if(blockIds[i] == 0) continue;
					blockIdMapping[blockIds[i]] = Api.World.GetBlock(blockIds[i]).Code;
				}
			}
		}

		public void DisassembleMultiblock()
		{
			if(layersPacked == null) return;
			if(Api.Side != EnumAppSide.Server) return;

			var tmpPos = Pos.Copy();
			var tmpDropPos = new Vec3d();
			if(recipe != null)
			{
				int fromX = (Pos.X - (size.X >> 1)) + 1;
				int toX = fromX + (size.X - 3);
				int fromZ = (Pos.Z - (size.Y >> 1)) + 1;
				int toZ = fromZ + (size.Y - 3);
				int sizeX = size.X - 2;

				for(int z = fromZ, gz = 0; z <= toZ; z++, gz += sizeX)
				{
					tmpPos.Z = z;
					for(int x = fromX, gx = 0; x <= toX; x++, gx++)
					{
						tmpPos.X = x;
						int i = gz + gx;
						int amount = (i & 1) == 0 ? (layersPacked[i >> 1] & 15) : (layersPacked[i >> 1] >> 4);
						if(amount > 0)
						{
							tmpDropPos.Set(tmpPos);
							tmpDropPos.Add(0.5, 1, 0.5);

							var output = recipe.Output.ResolvedItemstack.Clone();
							output.StackSize = amount;
							Api.World.SpawnItemEntity(output, tmpDropPos);
						}
					}
				}
			}
			layersPacked = null;

			int yStep = size.X;
			int xLast = size.X - 1;
			int yLast = size.X * (size.Y - 1);
			int xPosStart = Pos.X - (size.X >> 1);

			var accessor = Api.World.GetBlockAccessorBulkUpdate(true, false);
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
			if(index < 0 || index >= grid.Length || grid[index] == ushort.MaxValue) return null;
			var id = blockIds[grid[index]];
			if(id == 0) return null;
			return Api.World.GetBlock(id);
		}

		public void OnBlockInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			if(Api.Side != EnumAppSide.Server) return;
			if(byPlayer == null) return;

			var slot = byPlayer.Entity.ActiveHandItemSlot;
			if(slot?.Itemstack?.Collectible.Tool == EnumTool.Hoe)
			{
				if(recipe == null) return;
				int index = (blockSel.Position.X - (Pos.X - ((size.X - 2) >> 1))) + (blockSel.Position.Z - (Pos.Z - ((size.Y - 2) >> 1))) * (size.X - 2);
				if(index < 0 || index >= ((size.X - 2) * (size.Y - 2))) return;
				int value = layersPacked[index >> 1];
				int amount = (index & 1) == 0 ? (value & 15) : (value >> 4);
				if(amount > 0)
				{
					layersPacked[index >> 1] = (byte)((index & 1) == 0 ? (value & 240) : (value & 15));

					var output = recipe.Output.ResolvedItemstack.Clone();
					output.StackSize = amount;
					Api.World.SpawnItemEntity(output, blockSel.FullPosition);

					MarkDirty(true);

					slot.Itemstack.Collectible.DamageItem(world, byPlayer.Entity, slot);
				}
			}
			else if(slot?.Itemstack?.Collectible is ILiquidSource source)
			{
				if(source.AllowHeldLiquidTransfer)
				{
					var contentStackToMove = source.GetContent(slot.Itemstack);
					if(contentStackToMove != null)
					{
						var props = BlockLiquidContainerBase.GetContainableProps(contentStackToMove);
						if(props != null)
						{
							var stack = contentStackToMove.Clone();
							stack.StackSize = Math.Min(contentStackToMove.StackSize, (int)((byPlayer.WorldData.EntityControls.ShiftKey ? source.CapacityLitres : source.TransferSizeLitres) * props.ItemsPerLitre));
							if(stack.StackSize > 0 && TryAccept(Api.World.BlockAccessor, Pos, BlockFacing.UP, ref stack))
							{
								if(stack.StackSize > 0)
								{
									source.TryTakeContent(slot.Itemstack, stack.StackSize);
									DoLiquidMovedEffects(byPlayer, stack, props);
									slot.MarkDirty();
								}
							}
						}
					}
				}
			}
		}

		public WorldInteraction[] GetInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
		{
			int index = (blockSel.Position.X - (Pos.X - ((size.X - 2) >> 1))) + (blockSel.Position.Z - (Pos.Z - ((size.Y - 2) >> 1))) * (size.X - 2);
			if(index >= 0 && index < ((size.X - 2) * (size.Y - 2)))
			{
				int value = layersPacked[index >> 1];
				int amount = (index & 1) == 0 ? (value & 15) : (value >> 4);
				if(amount > 0) return GetPickInteractionHelp();
			}
			return Array.Empty<WorldInteraction>();
		}

		public int? GetColorWithoutTint(BlockPos partPos)
		{
			var capi = (ICoreClientAPI)Api;
			if(recipe != null && recipe.OutputTexture?.Baked != null)
			{
				int index = (partPos.X - (Pos.X - ((size.X - 2) >> 1))) + (partPos.Z - (Pos.Z - ((size.Y - 2) >> 1))) * (size.X - 2);
				if(index >= 0 && index < ((size.X - 2) * (size.Y - 2)))
				{
					int value = layersPacked[index >> 1];
					int amount = (index & 1) == 0 ? (value & 15) : (value >> 4);
					if(amount > 0)
					{
						return capi.BlockTextureAtlas.GetAverageColor(recipe.OutputTexture.Baked.TextureSubId);
					}
				}
			}
			return GetPartBlockAt(partPos)?.GetColorWithoutTint(capi, partPos);
		}

		public bool TryAccept(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ref ItemStack liquid)
		{
			if(Api.Side != EnumAppSide.Server) return false;
			if(currentLiquidStack == null)
			{
				if(mod.TryGetRecipe(liquid, out var evaporationRecipe) &&
					(recipe == null || recipe == evaporationRecipe || recipe.Output.ResolvedItemstack.Equals(
						Api.World, evaporationRecipe.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
				{
					if(recipe == null)
					{
						Buffer.SetByte(layersPacked, 0, 0);
						layerProgress = 0;
						prevCalendarHour = Api.World.Calendar.TotalHours;
						evaporationArea = (size.X - 2) * (size.Y - 2);
					}
					recipe = evaporationRecipe;
					CalculateLiquidCapacity(0);

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

		private void DoLiquidMovedEffects(IPlayer player, ItemStack contentStack, WaterTightContainableProps props)
		{
			float litresMoved = (float)contentStack.StackSize / props.ItemsPerLitre;
			(player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
			Api.World.PlaySoundAt(props.PourSound, player.Entity, player, true, 16f, GameMath.Clamp(litresMoved / 5f, 0.35f, 1f));
			Api.World.SpawnCubeParticles(player.Entity.Pos.AheadCopy(0.25).XYZ.Add(0.0, player.Entity.SelectionBox.Y2 / 2f, 0.0), contentStack, 0.75f, (int)litresMoved * 2, 0.45f);
		}

		private WorldInteraction[] GetPickInteractionHelp()
		{
			if(pickInteractionHelp == null)
			{
				pickInteractionHelp = new WorldInteraction[] {
					new WorldInteraction() {
						ActionLangCode = "fieldsofsalt:pond-result",
						MouseButton = EnumMouseButton.Right,
						Itemstacks = Array.ConvertAll(Api.World.SearchItems(new AssetLocation("hoe-*")), i => new ItemStack(i))
					}
				};
			}
			return pickInteractionHelp;
		}

		private void PickLiquid(ItemStack liquid)
		{
			if((liquid.StackSize + currentLiquidStack.StackSize) > liquidCapacity)
			{
				liquid.StackSize = Math.Max(liquidCapacity - currentLiquidStack.StackSize, 0);
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
				var timeProgress = recipe.GetProgress(Api.World.Calendar.TotalHours - prevCalendarHour, temperature);

				double unitsInLayer = recipe.Input.StackSize * evaporationArea;
				int prevStepEvaporatedTotal = (int)(layerProgress * unitsInLayer);
				int evaporatedUnits = (int)((layerProgress + timeProgress) * unitsInLayer) - prevStepEvaporatedTotal;
				if(evaporatedUnits > 0)
				{
					evaporatedUnits = Math.Min(evaporatedUnits, currentLiquidStack.StackSize);
					currentLiquidStack.StackSize -= evaporatedUnits;
					if(currentLiquidStack.StackSize == 0) currentLiquidStack = null;

					layerProgress += (double)evaporatedUnits / unitsInLayer;
					if(layerProgress >= 1)
					{
						int addLayers = (int)layerProgress;
						layerProgress -= addLayers;
						int addOutputUnitsAmount = addLayers * recipe.Output.StackSize * evaporationArea;

						int count = ((size.X - 2) * (size.Y - 2)) >> 1;
						int usedLayers = 0;
						for(int i = 0; i < count; i++)
						{
							uint value = layersPacked[i];
							uint newValue = 0;

							int size = (int)(value & 15);
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
								layersPacked[count] = (byte)GameMath.Clamp(size, 0, 15);
								usedLayers += size;
							}
						}
						CalculateLiquidCapacity(usedLayers);
						markDirtyNext = true;
					}
					else if(!markDirtyNext && Api is ICoreServerAPI sapi)
					{
						sapi.Network.BroadcastBlockEntityPacket<int>(Pos, 2001, currentLiquidStack?.StackSize ?? 0);
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

		public override void OnReceivedServerPacket(int packetid, byte[] data)
		{
			base.OnReceivedServerPacket(packetid, data);
			if(packetid == 2001 && data.Length == 4)
			{
				if(currentLiquidStack != null)
				{
					int amount = SerializerUtil.Deserialize<int>(data);
					if(amount <= 0) currentLiquidStack = null;
					else currentLiquidStack.StackSize = amount;
					MarkDirty(true);
				}
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

				int size = (int)(value & 15);
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

		private void RegisterMultiblock()
		{
			if(size == null) return;
			if(multiblockRegistered) return;
			multiblockRegistered = true;

			var fromPos = new BlockPos(Pos.X - (size.X >> 1), Pos.Y, Pos.Z - (size.Y >> 1));
			var toPos = fromPos.AddCopy(size.X - 1, 0, size.Y - 1);

			var accessor = Api.World.BlockAccessor;
			bool RegisterBlock(BlockPos tmpPos)
			{
				mod.AddReferenceToMainBlock(tmpPos, Pos);
				accessor.MarkBlockDirty(tmpPos);
				return false;
			}

			connectors.Clear();
			bool RegisterBorderBlock(BlockPos tmpPos, BlockFacing face)
			{
				if(accessor.GetBlock(tmpPos) is ILiquidSinkConnector connector && connector.CanConnect(accessor, tmpPos, face))
				{
					connectors.Add(new ConnectorInfo(tmpPos.Copy(), face));
					var channelPos = tmpPos.AddCopy(face);
					if(accessor.GetBlock(channelPos) is ILiquidChannel channel)
					{
						channel.AddSink(accessor, channelPos, face.Opposite, this);
					}
				}
				mod.AddReferenceToMainBlock(tmpPos, Pos);
				accessor.MarkBlockDirty(tmpPos);
				return false;
			}
			IterateStructureBlocks(fromPos, toPos, RegisterBorderBlock, RegisterBlock);
		}

		private void UnregisterMultiblock()
		{
			if(size == null) return;
			if(!multiblockRegistered) return;
			multiblockRegistered = false;

			var fromPos = new BlockPos(Pos.X - (size.X >> 1), Pos.Y, Pos.Z - (size.Y >> 1));
			var toPos = fromPos.AddCopy(size.X - 1, 0, size.Y - 1);

			bool UnregisterBlock(BlockPos tmpPos)
			{
				mod.RemoveReferenceToMainBlock(tmpPos, Pos);
				return false;
			}
			bool UnregisterBorderBlock(BlockPos tmpPos, BlockFacing face)
			{
				mod.RemoveReferenceToMainBlock(tmpPos, Pos);
				return false;
			}
			IterateStructureBlocks(fromPos, toPos, UnregisterBorderBlock, UnregisterBlock);

			var channelPos = new BlockPos();
			var accessor = Api.World.BlockAccessor;
			foreach(var connector in connectors)
			{
				channelPos.Set(connector.pos);
				channelPos.Add(connector.face);
				if(accessor.GetBlock(channelPos) is ILiquidChannel channel)
				{
					channel.RemoveSink(accessor, channelPos, connector.face.Opposite, this);
				}
			}
		}

		/// <summary>
		/// Creates a structure in the specified area.
		/// The structure must be validated beforehand.
		/// </summary>
		public static void CreateStructure(IWorldAccessor world, BlockPos fromPos, BlockPos toPos, ItemStack mainBlock, ItemStack surrogateBlock)
		{
			var size = new Vec2i(toPos.X - fromPos.X + 1, toPos.Z - fromPos.Z + 1);

			var blockId2index = new Dictionary<int, int>();
			var grid = new ushort[size.X * size.Y];

			grid[0] = ushort.MaxValue;
			grid[size.X - 1] = ushort.MaxValue;
			grid[size.X * (size.Y - 1)] = ushort.MaxValue;
			grid[size.X * size.Y - 1] = ushort.MaxValue;

			var blockAccessor = world.GetBlockAccessorBulkUpdate(true, false);
			bool CheckBlock(BlockPos pos, int gridIndex)
			{
				var block = blockAccessor.GetBlock(pos);
				if(block is IMultiblockPartBlock)
				{
					grid[gridIndex] = ushort.MaxValue;
				}
				else
				{
					if(!blockId2index.TryGetValue(block.Id, out var blockIndex))
					{
						blockIndex = blockId2index.Count;
						blockId2index[block.Id] = blockIndex;
					}
					grid[gridIndex] = (ushort)blockIndex;
					blockAccessor.SetBlock(surrogateBlock.Id, pos, surrogateBlock);
				}
				return false;
			}
			IterateStructureBlocks(fromPos, toPos, CheckBlock, CheckBlock);

			var stack = mainBlock.Clone();
			stack.Attributes.SetInt(SIZE_X_ATTR, size.X);
			stack.Attributes.SetInt(SIZE_Y_ATTR, size.Y);
			stack.Attributes.WritePackedUShortArray(GRID_ATTR, grid, (ushort)(blockId2index.Count - 1), size.X * size.Y);

			var blockIds = new int[blockId2index.Count];
			foreach(var pair in blockId2index) blockIds[pair.Value] = pair.Key;
			stack.Attributes[BLOCK_IDS_ATTR] = new IntArrayAttribute(blockIds);

			blockAccessor.SetBlock(mainBlock.Id, fromPos.AddCopy(size.X >> 1, 0, size.Y >> 1), stack);

			blockAccessor.Commit();
		}

		public static bool IterateStructureBlocks(BlockPos fromPos, BlockPos toPos, System.Func<BlockPos, BlockFacing, bool> borderCallback, System.Func<BlockPos, bool> bodyCallback)
		{
			int fromX = fromPos.X + 1;
			int toX = toPos.X - 1;
			int fromZ = fromPos.Z + 1;
			int toZ = toPos.Z - 1;
			int x, z;

			var tmpPos = fromPos.Copy();
			if(borderCallback != null)
			{
				for(x = fromX; x <= toX; x++)
				{
					tmpPos.X = x;
					tmpPos.Y = fromPos.Y;
					tmpPos.Z = fromPos.Z;
					if(borderCallback(tmpPos, BlockFacing.NORTH)) return true;
					tmpPos.Z = toPos.Z;
					if(borderCallback(tmpPos, BlockFacing.SOUTH)) return true;
				}
				for(z = fromZ; z <= toZ; z++)
				{
					tmpPos.Z = z;
					tmpPos.Y = fromPos.Y;
					tmpPos.X = fromPos.X;
					if(borderCallback(tmpPos, BlockFacing.WEST)) return true;
					tmpPos.X = toPos.X;
					if(borderCallback(tmpPos, BlockFacing.EAST)) return true;
				}
			}
			if(bodyCallback != null)
			{
				tmpPos.Y = fromPos.Y;
				for(x = fromX; x <= toX; x++)
				{
					tmpPos.X = x;
					for(z = fromZ; z <= toZ; z++)
					{
						tmpPos.Z = z;
						if(bodyCallback(tmpPos)) return true;
					}
				}
			}
			return false;
		}

		public delegate bool StructureBlockPosDelegate(BlockPos pos, int gridIndex);
		public static bool IterateStructureBlocks(BlockPos fromPos, BlockPos toPos, StructureBlockPosDelegate borderCallback, StructureBlockPosDelegate bodyCallback)
		{
			int sizeX = toPos.X - fromPos.X + 1;
			int sizeZ = toPos.Z - fromPos.Z + 1;
			int lastRow = sizeX * (sizeZ - 1);
			int lastX = sizeX - 1;

			int fromX = fromPos.X + 1;
			int toX = toPos.X - 1;
			int fromZ = fromPos.Z + 1;
			int toZ = toPos.Z - 1;
			int x, z, gx, gz;

			var tmpPos = fromPos.Copy();
			if(borderCallback != null)
			{
				for(x = fromX, gx = 1; x <= toX; x++, gx++)
				{
					tmpPos.X = x;
					tmpPos.Y = fromPos.Y;
					tmpPos.Z = fromPos.Z;
					if(borderCallback(tmpPos, gx)) return true;
					tmpPos.Z = toPos.Z;
					if(borderCallback(tmpPos, gx + lastRow)) return true;
				}
				for(z = fromZ, gz = sizeX; z <= toZ; z++, gz += sizeX)
				{
					tmpPos.Z = z;
					tmpPos.Y = fromPos.Y;
					tmpPos.X = fromPos.X;
					if(borderCallback(tmpPos, gz)) return true;
					tmpPos.X = toPos.X;
					if(borderCallback(tmpPos, gz + lastX)) return true;
				}
			}
			if(bodyCallback != null)
			{
				tmpPos.Y = fromPos.Y;
				for(z = fromZ, gz = sizeX; z <= toZ; z++, gz += sizeX)
				{
					tmpPos.Z = z;
					for(x = fromX, gx = 1; x <= toX; x++, gx++)
					{
						tmpPos.X = x;
						if(bodyCallback(tmpPos, gz + gx)) return true;
					}
				}
			}
			return false;
		}

		private readonly struct ConnectorInfo
		{
			public readonly BlockPos pos;
			public readonly BlockFacing face;

			public ConnectorInfo(BlockPos pos, BlockFacing face)
			{
				this.pos = pos;
				this.face = face;
			}
		}
	}
}