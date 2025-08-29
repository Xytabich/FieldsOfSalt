using FieldsOfSalt.Multiblock;
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
		private int liquidBlock = 0;

		private int fillLevel = 0;

		private BlockFacing face;
		private BlockSource blockSource;

		private MultiblockManager manager;
		private ConcurrentDictionary<Xyz, SinkInfo> sinks = new ConcurrentDictionary<Xyz, SinkInfo>();

		private MeshData tmpMesh = null;
		private ItemStack liquidStack = null;
		private WaterTightContainableProps liquidProps = null;
		private LiquidGraphicProps liquidGraphicProps = null;
		private bool isLoaded = false;

		public override void Initialize(ICoreAPI api)
		{
			blockSource = (BlockSource)Block;
			face = blockSource.Face;
			base.Initialize(api);
			manager = api.ModLoader.GetModSystem<MultiblockManager>();

			fillLevel = 0;
			if(liquidBlock > 0)
			{
				if(TryGetLiquidStack(Api, Api.World.GetBlock(liquidBlock), out liquidStack, out liquidProps))
				{
					fillLevel = channelLine.Length + 8;
					if(Api.Side == EnumAppSide.Client)
					{
						GraphicUtil.BakeTexture((ICoreClientAPI)Api, liquidProps.Texture, "Liquid source", out var texture);
						liquidGraphicProps = new LiquidGraphicProps(texture, liquidProps.GlowLevel);
					}
				}
				else
				{
					liquidBlock = 0;
				}
			}

			var tmpPos = Pos.Copy();
			var dir = face.Opposite;
			var accessor = Api.World.BlockAccessor;
			var blockList = new List<int>();
			Action<BlockPos, BlockFacing, ILiquidSink> addSinkCallback = AddSink;
			var prevConnectable = (ILiquidConnectable)Block;
			for(int i = 0; i < channelLine.Length; i++)
			{
				if(!prevConnectable.CanConnect(accessor, tmpPos, dir)) break;

				tmpPos.Add(dir);
				var block = accessor.GetBlock(channelLine[i]);
				if(block is ILiquidChannel channel && channel.CanConnect(accessor, tmpPos, face))
				{
					blockList.Add(block.Id);
					prevConnectable = channel;
				}
				else break;
			}
			channelLine = blockList.ToArray();
			RegisterMultiblock(0, channelLine.Length);

			isLoaded = true;
			tmpPos.SetAll(Pos);
			for(int i = 0; i < channelLine.Length; i++)
			{
				tmpPos.Add(dir);
				((ILiquidChannel)Api.World.GetBlock(channelLine[i])).GetConnectedSinks(accessor, tmpPos, addSinkCallback);
			}

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
					if(block is ILiquidChannel channel && channel.CanConnect(accessor, tmpPos, face) && !manager.GetReferenceToMainBlock(tmpPos))
					{
						expandedLine.Add(block.Id);
						prevConnectable = channel;
					}
					else break;
				}
				if(channelLine.Length < expandedLine.Count)
				{
					channelLine = expandedLine.ToArray();

					RegisterMultiblock(index, expandedLine.Count);
					tmpPos.SetAll(Pos);
					for(int i = index; i < expandedLine.Count; i++)
					{
						tmpPos.Add(dir);
						((ILiquidChannel)Api.World.GetBlock(expandedLine[i])).GetConnectedSinks(accessor, tmpPos, addSinkCallback);
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
				var accessor = Api.World.BlockAccessor;
				var lastBlock = accessor.GetBlock(channelLine[count - 1]);

				Action<BlockPos, BlockFacing, ILiquidSink> delSinkCallback = RemoveSink;
				var tmpPos = Pos.Copy();
				var dir = face.Opposite;
				tmpPos.Add(dir, index);
				for(int i = index; i < count; i++)
				{
					tmpPos.Add(dir);
					(accessor.GetBlock(channelLine[i]) as ILiquidChannel)?.GetConnectedSinks(accessor, tmpPos, delSinkCallback);
				}
				UnregisterMultiblock(index, count, false);
				Array.Resize(ref channelLine, index);
				MarkDirty(true);

				if(index + 1 < count) TryMoveChannelsToOpposite(lastBlock, count);
			}
		}

		public void AddSink(BlockPos blockPos, BlockFacing connectedFace, ILiquidSink sink)
		{
			if(!isLoaded) return;

			sinks[new Xyz(blockPos.X, blockPos.InternalY, blockPos.Z)] = new SinkInfo(connectedFace, sink);
			if(Api != null && Api.Side == EnumAppSide.Client)
			{
				int level = fillLevel - Math.Abs(face.IsAxisWE ? (blockPos.X - Pos.X) : (blockPos.Z - Pos.Z));
				if(level <= 0 || liquidBlock <= 0)
				{
					sink.SetLiquidLevel(Api.World.BlockAccessor, blockPos, connectedFace, 0, null);
				}
				else
				{
					sink.SetLiquidLevel(Api.World.BlockAccessor, blockPos, connectedFace, Math.Min(level, 7), liquidProps);
				}
				MarkDirty(true);
			}
		}

		public void RemoveSink(BlockPos blockPos, BlockFacing connectedFace, ILiquidSink sink)
		{
			sinks.TryRemove(new Xyz(blockPos.X, blockPos.InternalY, blockPos.Z), out _);
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
				if(block is ILiquidChannel channel && channel.CanConnect(accessor, tmpPos, face) && !manager.GetReferenceToMainBlock(tmpPos))
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
				blockSource.GetConnectedSinks(accessor, Pos, addSinkCallback);
			}
			UpdateLiquidBlock(Api.World.BlockAccessor);

			tmpPos.SetAll(Pos);
			for(int i = 0; i < channelLine.Length; i++)
			{
				tmpPos.Add(dir);
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
			int nextLiquidBlock = tree.GetInt("liquidBlock", 0);
			var newLine = (tree["channelLine"] as IntArrayAttribute)?.value ?? Array.Empty<int>();
			int prevCount = channelLine.Length;
			channelLine = newLine;
			if(Api == null)
			{
				liquidBlock = nextLiquidBlock;
			}
			else
			{
				if(liquidBlock != nextLiquidBlock)
				{
					liquidBlock = nextLiquidBlock;
					if(liquidBlock > 0)
					{
						if(TryGetLiquidStack(Api, Api.World.GetBlock(liquidBlock), out liquidStack, out liquidProps))
						{
							if(Api.Side == EnumAppSide.Client)
							{
								GraphicUtil.BakeTexture((ICoreClientAPI)Api, liquidProps.Texture, "Liquid source", out var texture);
								liquidGraphicProps = new LiquidGraphicProps(texture, liquidProps.GlowLevel);
							}
						}
						else
						{
							liquidBlock = 0;
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
			tree.SetInt("liquidBlock", liquidBlock);
		}

		public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			//Cache in case of multithreading
			var liquidProps = this.liquidGraphicProps;
			var fillLevel = this.fillLevel;
			var channelLine = this.channelLine;
			if(liquidProps != null && fillLevel > 0)
			{
				if(tmpMesh == null) tmpMesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();
				var accessor = Api.World.GetLockFreeBlockAccessor();
				Span<int> fillLevels = stackalloc int[6];
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
				blockSource.GenLiquidMesh(accessor, Pos, tmpMesh, fillLevels);
				if(tmpMesh.VerticesCount > 0)
				{
					tmpMesh.SetTexPos(liquidProps.texture);
					tmpMesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.Transparent);
					tmpMesh.Flags.Fill(liquidProps.glowLevel);
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
							tmpMesh.Translate(offset);
							tmpMesh.SetTexPos(liquidProps.texture);
							tmpMesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.Transparent);
							tmpMesh.Flags.Fill(liquidProps.glowLevel);
							mesher.AddMeshData(tmpMesh);
						}
					}
				}
			}
			return base.OnTesselation(mesher, tessThreadTesselator);
		}

		public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
		{
			base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
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
			if(liquidBlock != 0)
			{
				if(oldBlockIdMapping.TryGetValue(liquidBlock, out var code))
				{
					var block = worldForNewMappings.GetBlock(code);
					if(block == null)
					{
						liquidBlock = 0;
					}
					else
					{
						liquidBlock = block.Id;
					}
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
			if(liquidBlock != 0)
			{
				blockIdMapping[liquidBlock] = Api.World.GetBlock(liquidBlock).Code;
			}
		}

		public void UpdateLiquidBlock(IBlockAccessor blockAccessor)
		{
			int nextLiquidBlock = 0;
			WaterTightContainableProps nextLiquidProps = null;
			ItemStack nextLiquidStack = null;
			var block = blockAccessor.GetBlock(Pos.AddCopy(face));
			if(block.IsLiquid() && block.LiquidLevel >= 7)
			{
				if(TryGetLiquidStack(Api, block, out var fs, out var fp))
				{
					nextLiquidBlock = block.Id;
					nextLiquidProps = fp;
					nextLiquidStack = fs;
				}
			}
			if(nextLiquidBlock != liquidBlock)
			{
				liquidBlock = nextLiquidBlock;
				liquidProps = nextLiquidProps;
				liquidStack = nextLiquidStack;
				if(Api != null)
				{
					if(liquidProps != null && Api.Side == EnumAppSide.Client)
					{
						GraphicUtil.BakeTexture((ICoreClientAPI)Api, liquidProps.Texture, "Liquid source", out var texture);
						liquidGraphicProps = new LiquidGraphicProps(texture, liquidProps.GlowLevel);
					}
					ResetLiquidLayer(blockAccessor);
					MarkDirty(true);
				}
			}
		}

		private void OnTick(float dt)
		{
			if(liquidBlock > 0)
			{
				if(fillLevel < channelLine.Length + 8)
				{
					fillLevel++;
					if(Api.Side == EnumAppSide.Client) MarkDirty(true);
				}
				var accessor = Api.World.BlockAccessor;
				var tmpPos = new BlockPos(0);
				int stackSize = (int)Math.Ceiling(liquidProps.ItemsPerLitre * blockSource.LitresPerTickGeneration);
				foreach(var pair in sinks)
				{
					var pos = pair.Key;
					int offset = Math.Abs(face.IsAxisWE ? (pos.X - Pos.X) : (pos.Z - Pos.Z));
					if(offset <= fillLevel)
					{
						tmpPos.SetInternal(pos.X, pos.Y, pos.Z);
						var stack = liquidStack;
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
						liquidGraphicProps = null;
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
				var mainBlock = new BlockPos(0);
				if(accessor.GetBlock(blockPos) is ILiquidChannel channel &&
					channel.CanConnect(accessor, blockPos, face) && manager.GetReferenceToMainBlock(blockPos, mainBlock))
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
			if(Api.Side == EnumAppSide.Client && liquidBlock <= 0)
			{
				var tmpPos = new BlockPos(0);
				foreach(var pair in sinks)
				{
					tmpPos.SetInternal(pair.Key.X, pair.Key.Y, pair.Key.Z);
					pair.Value.sink.SetLiquidLevel(blockAccessor, tmpPos, pair.Value.connectedFace, 0, null);
				}
			}
		}

		private void RegisterMultiblock(int offset, int size)
		{
			if(size == 0) return;

			if(offset == 0) manager.AddReferenceToMainBlock(Pos, Pos);

			var tmpPos = Pos.Copy();
			var dir = face.Opposite;
			tmpPos.Add(dir, offset);
			for(int i = offset; i < size; i++)
			{
				tmpPos.Add(dir);
				manager.AddReferenceToMainBlock(tmpPos, Pos);
			}

			if(Api.Side == EnumAppSide.Client)
			{
				var accessor = Api.World.BlockAccessor;
				tmpPos.SetAll(Pos);
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

			if((offset == 0) & unregisterSelf) manager.RemoveReferenceToMainBlock(Pos, Pos);

			var tmpPos = Pos.Copy();
			var dir = face.Opposite;
			tmpPos.Add(dir, offset);
			for(int i = offset; i < size; i++)
			{
				tmpPos.Add(dir);
				manager.RemoveReferenceToMainBlock(tmpPos, Pos);
			}
		}

		private static bool TryGetLiquidStack(ICoreAPI api, Block liquidBlock, out ItemStack liquidStack, out WaterTightContainableProps liquidProps)
		{
			try
			{
				var props = liquidBlock.Attributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
				if(props != null && props.WhenFilled?.Stack != null)
				{
					if(props.WhenFilled.Stack.Resolve(api.World, "fieldsofsalt:source"))
					{
						liquidStack = props.WhenFilled.Stack.ResolvedItemstack;
						liquidProps = BlockLiquidContainerBase.GetContainableProps(liquidStack);
						if(liquidProps != null)
						{
							return true;
						}
					}
				}
			}
			catch { }
			liquidStack = null;
			liquidProps = null;
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

		private class LiquidGraphicProps
		{
			public TextureAtlasPosition texture;
			public int glowLevel;

			public LiquidGraphicProps(TextureAtlasPosition texture, int glowLevel)
			{
				this.texture = texture;
				this.glowLevel = glowLevel;
			}
		}
	}
}