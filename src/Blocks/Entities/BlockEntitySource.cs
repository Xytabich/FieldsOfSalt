using FieldsOfSalt.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common.Database;
using Vintagestory.GameContent;

namespace FieldsOfSalt.Blocks.Entities
{
	public class BlockEntitySource : BlockEntity
	{
		private const int MAX_PATH_LENGTH = 128;

		private int[] channelLine = Array.Empty<int>();
		private int fluidBlock = 0;

		private int fillLevel = 0;

		private BlockFacing face;

		private FieldsOfSaltMod mod;
		private ConcurrentDictionary<Xyz, SinkInfo> sinks = new ConcurrentDictionary<Xyz, SinkInfo>();

		private MeshData tmpMesh = null;
		private ItemStack fluidStack = null;
		private WaterTightContainableProps fluidProps = null;
		private FluidGraphicProps fluidGraphicProps = null;

		public override void Initialize(ICoreAPI api)
		{
			face = ((BlockSource)Block).Face;
			base.Initialize(api);
			mod = api.ModLoader.GetModSystem<FieldsOfSaltMod>();

			fillLevel = 0;
			if(fluidBlock > 0)
			{
				if(TryGetFluidStack(Api, Api.World.GetBlock(fluidBlock), out fluidStack, out fluidProps))
				{
					fillLevel = channelLine.Length + 8;
					if(Api.Side == EnumAppSide.Client)
					{
						GraphicUtil.BakeTexture((ICoreClientAPI)Api, fluidProps.Texture, "Liquid source", out var texture);
						fluidGraphicProps = new FluidGraphicProps(texture, fluidProps.GlowLevel);
					}
				}
				else
				{
					fluidBlock = 0;
				}
			}
			RegisterMultiblock(0, channelLine.Length);

			RegisterGameTickListener(OnTick, 100);
		}

		public void AddChannel(BlockPos blockPos)
		{
			int index = Math.Abs(face.IsAxisWE ? (blockPos.X - Pos.X) : (blockPos.Z - Pos.Z)) - 1;
			if(index == channelLine.Length)
			{
				var tmpPos = Pos.Copy();
				var dir = face.Opposite;
				var accessor = Api.World.BlockAccessor;
				Action<BlockPos, BlockFacing, ILiquidSink> addSinkCallback = AddSink;
				var prevConnectable = index == 0 ? (ILiquidConnectable)Block : (ILiquidConnectable)Api.World.GetBlock(channelLine[index - 1]);

				var expandedLine = new List<int>(channelLine);
				tmpPos.Add(dir, index);
				for(int i = index; i < MAX_PATH_LENGTH; i++)
				{
					if(!prevConnectable.CanConnect(accessor, tmpPos, dir)) break;

					tmpPos.Add(dir);
					var block = accessor.GetBlock(tmpPos);
					if(block is ILiquidChannel channel && channel.CanConnect(accessor, tmpPos, face) && !mod.GetReferenceToMainBlock(tmpPos))
					{
						expandedLine.Add(block.Id);
						prevConnectable = channel;
					}
					else break;
				}
				if(index < expandedLine.Count)
				{
					this.channelLine = expandedLine.ToArray();

					RegisterMultiblock(index, channelLine.Length);
					for(int i = index; i < channelLine.Length; i++)
					{
						((ILiquidChannel)Api.World.GetBlock(channelLine[i])).GetConnectedSinks(accessor, tmpPos, addSinkCallback);
					}
					MarkDirty(true);
				}
			}
		}

		public void RemoveChannel(BlockPos blockPos)
		{
			int index = Math.Abs(face.IsAxisWE ? (blockPos.X - Pos.X) : (blockPos.Z - Pos.Z)) - 1;
			if(index >= 0 && index < channelLine.Length)
			{
				int count = channelLine.Length;
				var lastBlock = Api.World.GetBlock(channelLine[channelLine.Length - 1]);

				UnregisterMultiblock(index, channelLine.Length, false);
				Array.Resize(ref channelLine, index);
				MarkDirty(true);

				if(index + 1 < count) TryMoveChannelsToOpposite(lastBlock, count);
			}
		}

		public void AddSink(BlockPos blockPos, BlockFacing connectedFace, ILiquidSink sink)
		{
			sinks[new Xyz(blockPos.X, blockPos.Y, blockPos.Z)] = new SinkInfo(connectedFace, sink);
			if(Api != null && Api.Side == EnumAppSide.Client)
			{
				int level = fillLevel - Math.Abs(face.IsAxisWE ? (blockPos.X - Pos.X) : (blockPos.Z - Pos.Z));
				if(level <= 0 || fluidBlock <= 0)
				{
					sink.SetLiquidLevel(Api.World.BlockAccessor, blockPos, connectedFace, 0, null);
				}
				else
				{
					sink.SetLiquidLevel(Api.World.BlockAccessor, blockPos, connectedFace, Math.Min(level, 7), fluidProps);
				}
				MarkDirty(true);
			}
		}

		public void RemoveSink(BlockPos blockPos, BlockFacing connectedFace, ILiquidSink sink)
		{
			sinks.TryRemove(new Xyz(blockPos.X, blockPos.Y, blockPos.Z), out _);
			if(Api.Side == EnumAppSide.Client)
			{
				MarkDirty(true);
			}
		}

		public override void OnBlockPlaced(ItemStack byItemStack = null)
		{
			base.OnBlockPlaced(byItemStack);

			var tmpPos = Pos.Copy();
			var dir = face.Opposite;
			var accessor = Api.World.BlockAccessor;
			var blockList = new List<int>();
			Action<BlockPos, BlockFacing, ILiquidSink> addSinkCallback = AddSink;
			var prevConnectable = (ILiquidConnectable)Block;
			for(int i = 0; i < MAX_PATH_LENGTH; i++)
			{
				if(!prevConnectable.CanConnect(accessor, tmpPos, dir)) break;

				tmpPos.Add(dir);
				var block = accessor.GetBlock(tmpPos);
				if(block is ILiquidChannel channel && channel.CanConnect(accessor, tmpPos, face) && !mod.GetReferenceToMainBlock(tmpPos))
				{
					blockList.Add(block.Id);
					prevConnectable = channel;
				}
				else break;
			}
			channelLine = blockList.ToArray();

			RegisterMultiblock(0, channelLine.Length);
			if(channelLine.Length == 0)
			{
				((BlockSource)Block).GetConnectedSinks(accessor, Pos, addSinkCallback);
			}
			UpdateLiquidBlock(Api.World.BlockAccessor);

			for(int i = 0; i < channelLine.Length; i++)
			{
				((ILiquidChannel)Api.World.GetBlock(channelLine[i])).GetConnectedSinks(accessor, tmpPos, addSinkCallback);
			}
			MarkDirty(true);
		}

		public override void OnBlockRemoved()
		{
			UnregisterMultiblock(0, channelLine.Length, true);
			if(channelLine.Length > 0) TryMoveChannelsToOpposite(Api.World.GetBlock(channelLine[channelLine.Length - 1]), channelLine.Length);
			base.OnBlockRemoved();
		}

		public override void OnBlockUnloaded()
		{
			UnregisterMultiblock(0, channelLine.Length, true);
			base.OnBlockUnloaded();
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
		{
			base.FromTreeAttributes(tree, worldAccessForResolve);
			int nextFluidBlock = tree.GetInt("fluidBlock", 0);
			var newLine = (tree["channelLine"] as IntArrayAttribute)?.value ?? Array.Empty<int>();
			int prevCount = channelLine.Length;
			channelLine = newLine;
			if(Api == null)
			{
				fluidBlock = nextFluidBlock;
			}
			else
			{
				if(fluidBlock != nextFluidBlock)
				{
					fluidBlock = nextFluidBlock;
					if(fluidBlock > 0)
					{
						if(TryGetFluidStack(Api, Api.World.GetBlock(fluidBlock), out fluidStack, out fluidProps))
						{
							if(Api.Side == EnumAppSide.Client)
							{
								GraphicUtil.BakeTexture((ICoreClientAPI)Api, fluidProps.Texture, "Liquid source", out var texture);
								fluidGraphicProps = new FluidGraphicProps(texture, fluidProps.GlowLevel);
							}
						}
						else
						{
							fluidBlock = 0;
						}
					}

					ResetLiquidLayer(Api.World.BlockAccessor);
				}
				if(prevCount > channelLine.Length) UnregisterMultiblock(channelLine.Length, prevCount, false);
				else if(prevCount < channelLine.Length) RegisterMultiblock(prevCount, channelLine.Length);
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			base.ToTreeAttributes(tree);
			tree["channelLine"] = new IntArrayAttribute(channelLine);
			tree.SetInt("fluidBlock", fluidBlock);
		}

		public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			//Cache in case of multithreading
			var fluidProps = this.fluidGraphicProps;
			var fillLevel = this.fillLevel;
			var channelLine = this.channelLine;
			if(fluidProps != null && fillLevel > 0)
			{
				if(tmpMesh == null) tmpMesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();
				var accessor = Api.World.GetLockFreeBlockAccessor();
				var fillLevels = new int[6];//TODO: Span & stackalloc
				var side = face.IsAxisNS ? BlockFacing.EAST : BlockFacing.NORTH;

				fillLevels[face.Index] = Math.Min(fillLevel, 7);
				int level = fillLevel - 1;

				if(channelLine.Length == 0 && sinks.Count == 0)
				{
					level = 0;
				}

				fillLevels[face.Opposite.Index] = Math.Min(level, 7);
				fillLevels[side.Index] = Math.Min(level, 7);
				fillLevels[side.Opposite.Index] = Math.Min(level, 7);

				tmpMesh.Clear();
				((BlockSource)Block).GenLiquidMesh(accessor, Pos, tmpMesh, fillLevels);
				if(tmpMesh.VerticesCount > 0)
				{
					//for(int j = 0; j < tmpMesh.VerticesCount; j++)//TODO: color doesn't work?
					//{
					//	tmpMesh.Rgba[j * 4] = 127;
					//}
					tmpMesh.SetTexPos(fluidProps.texture);
					tmpMesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.Transparent);
					tmpMesh.Flags.Fill(fluidProps.glowLevel);
					mesher.AddMeshData(tmpMesh);
				}

				if(channelLine.Length > 0)
				{
					var tmpPos = Pos.Copy();
					var dir = face.Opposite;
					var offset = new Vec3f();
					for(int i = 0; i < channelLine.Length; i++)
					{
						if(level <= 0) break;

						tmpPos.Add(dir);
						offset.Add(dir.Normalf.X, dir.Normalf.Y, dir.Normalf.Z);

						fillLevels[face.Index] = Math.Min(level, 7);
						level--;

						fillLevels[face.Opposite.Index] = Math.Min(level, 7);
						fillLevels[side.Index] = Math.Min(level, 7);
						fillLevels[side.Opposite.Index] = Math.Min(level, 7);

						tmpMesh.Clear();
						(Api.World.GetBlock(channelLine[i]) as ILiquidChannel)?.GenLiquidMesh(accessor, tmpPos, tmpMesh, fillLevels);
						if(tmpMesh.VerticesCount > 0)
						{
							//for(int j = 0; j < tmpMesh.VerticesCount; j++)//TODO: color doesn't work?
							//{
							//	tmpMesh.Rgba[j * 4] = 127;
							//}
							tmpMesh.Translate(offset);
							tmpMesh.SetTexPos(fluidProps.texture);
							tmpMesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.Transparent);
							tmpMesh.Flags.Fill(fluidProps.glowLevel);
							mesher.AddMeshData(tmpMesh);
						}
					}
				}
			}
			return base.OnTesselation(mesher, tessThreadTesselator);
		}

		public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
		{
			base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
			for(int i = 0; i < channelLine.Length; i++)
			{
				if(oldBlockIdMapping.TryGetValue(channelLine[i], out var code))
				{
					var block = worldForNewMappings.GetBlock(code);
					if(block == null)
					{
						Array.Resize(ref channelLine, i);
						break;
					}
					channelLine[i] = block.Id;
				}
			}
		}

		public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
		{
			base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
			for(int i = 0; i < channelLine.Length; i++)
			{
				blockIdMapping[channelLine[i]] = Api.World.GetBlock(channelLine[i]).Code;
			}
		}

		public void UpdateLiquidBlock(IBlockAccessor blockAccessor)
		{
			int nextFluidBlock = 0;
			WaterTightContainableProps nextFluidProps = null;
			ItemStack nextFluidStack = null;
			var block = blockAccessor.GetBlock(Pos.AddCopy(face));
			if(block.IsLiquid() && block.LiquidLevel >= 7)
			{
				if(TryGetFluidStack(Api, block, out var fs, out var fp))
				{
					nextFluidBlock = block.Id;
					nextFluidProps = fp;
					nextFluidStack = fs;
				}
			}
			if(nextFluidBlock != fluidBlock)
			{
				fluidBlock = nextFluidBlock;
				fluidProps = nextFluidProps;
				fluidStack = nextFluidStack;
				if(Api != null)
				{
					if(fluidProps != null && Api.Side == EnumAppSide.Client)
					{
						GraphicUtil.BakeTexture((ICoreClientAPI)Api, fluidProps.Texture, "Liquid source", out var texture);
						fluidGraphicProps = new FluidGraphicProps(texture, fluidProps.GlowLevel);
					}
					ResetLiquidLayer(blockAccessor);
					MarkDirty(true);
				}
			}
		}

		private void OnTick(float dt)
		{
			if(fluidBlock > 0)
			{
				if(fillLevel < channelLine.Length + 8)
				{
					fillLevel++;
					if(Api.Side == EnumAppSide.Client) MarkDirty(true);
				}
				var accessor = Api.World.BlockAccessor;
				var tmpPos = new BlockPos();
				int stackSize = (int)Math.Ceiling(fluidProps.ItemsPerLitre * 0.01f);
				foreach(var pair in sinks)
				{
					var pos = pair.Key;
					int offset = Math.Abs(face.IsAxisWE ? (pos.X - Pos.X) : (pos.Z - Pos.Z));
					if(offset <= fillLevel)
					{
						tmpPos.Set(pos.X, pos.Y, pos.Z);
						var stack = fluidStack;
						stack.StackSize = stackSize;
						pair.Value.sink.TryAccept(accessor, tmpPos, pair.Value.connectedFace, ref stack);
					}
				}
			}
			else if(fillLevel > 0)
			{
				fillLevel--;
				if(Api.Side == EnumAppSide.Client)
				{
					if(fillLevel <= 0)
					{
						fluidGraphicProps = null;
					}
					MarkDirty(true);
				}
			}
		}

		private void TryMoveChannelsToOpposite(Block lastChannelBlock, int lastChannelOffset)
		{
			var lastBlockPos = Pos.AddCopy(face.Opposite, lastChannelOffset);
			var accessor = Api.World.BlockAccessor;
			if(lastChannelBlock is ILiquidChannel lastChannel && lastChannel.CanConnect(accessor, lastBlockPos, face.Opposite))
			{
				var blockPos = Pos.AddCopy(face.Opposite, lastChannelOffset + 1);
				var mainBlock = new BlockPos();
				if(accessor.GetBlock(blockPos) is ILiquidChannel channel &&
					channel.CanConnect(accessor, blockPos, face) && mod.GetReferenceToMainBlock(blockPos, mainBlock))
				{
					if(accessor.GetBlockEntity(mainBlock) is BlockEntitySource source)
					{
						source.AddChannel(lastBlockPos);
					}
				}
			}
		}

		private void ResetLiquidLayer(IBlockAccessor blockAccessor)
		{
			if(Api.Side == EnumAppSide.Client && fluidBlock <= 0)
			{
				var tmpPos = new BlockPos();
				foreach(var pair in sinks)
				{
					tmpPos.Set(pair.Key.X, pair.Key.Y, pair.Key.Z);
					pair.Value.sink.SetLiquidLevel(blockAccessor, tmpPos, pair.Value.connectedFace, 0, null);
				}
			}
		}

		private void RegisterMultiblock(int offset, int size)
		{
			if(size == 0) return;

			if(offset == 0) mod.AddReferenceToMainBlock(Pos, Pos);

			var tmpPos = Pos.Copy();
			var dir = face.Opposite;
			tmpPos.Add(dir, offset);
			for(int i = offset; i < size; i++)
			{
				tmpPos.Add(dir);
				mod.AddReferenceToMainBlock(tmpPos, Pos);
			}

			if(Api.Side == EnumAppSide.Client)
			{
				var accessor = Api.World.BlockAccessor;
				tmpPos.Set(Pos);
				tmpPos.Add(dir, offset);
				for(int i = offset; i < size; i++)
				{
					tmpPos.Add(dir);
					accessor.MarkBlockDirty(tmpPos);// Trigger chunks retesselation to draw properly connected channels
				}
			}
		}

		private void UnregisterMultiblock(int offset, int size, bool unregisterSelf)
		{
			if(size == 0) return;

			if((offset == 0) & unregisterSelf) mod.RemoveReferenceToMainBlock(Pos, Pos);

			var tmpPos = Pos.Copy();
			var dir = face.Opposite;
			tmpPos.Add(dir, offset);
			for(int i = offset; i < size; i++)
			{
				tmpPos.Add(dir);
				mod.RemoveReferenceToMainBlock(tmpPos, Pos);
			}
		}

		private static bool TryGetFluidStack(ICoreAPI api, Block fluidBlock, out ItemStack fluidStack, out WaterTightContainableProps fluidProps)
		{
			try
			{
				var props = fluidBlock.Attributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
				if(props != null && props.WhenFilled?.Stack != null)
				{
					if(props.WhenFilled.Stack.Resolve(api.World, "fieldsofsalt:source"))
					{
						fluidStack = props.WhenFilled.Stack.ResolvedItemstack;
						fluidProps = BlockLiquidContainerBase.GetContainableProps(fluidStack);
						if(fluidProps != null)
						{
							return true;
						}
					}
				}
			}
			catch { }
			fluidStack = null;
			fluidProps = null;
			return false;
		}

		private readonly struct SinkInfo
		{
			public readonly BlockFacing connectedFace;
			public readonly ILiquidSink sink;

			public SinkInfo(BlockFacing connectedFace, ILiquidSink sink)
			{
				this.connectedFace = connectedFace;
				this.sink = sink;
			}
		}

		private class FluidGraphicProps
		{
			public TextureAtlasPosition texture;
			public int glowLevel;

			public FluidGraphicProps(TextureAtlasPosition texture, int glowLevel)
			{
				this.texture = texture;
				this.glowLevel = glowLevel;
			}
		}
	}
}