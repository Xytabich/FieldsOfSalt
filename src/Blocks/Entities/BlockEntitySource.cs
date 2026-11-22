using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FieldsOfSalt.Multiblock;
using FieldsOfSalt.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable enable
namespace FieldsOfSalt.Blocks.Entities
{
	public class BlockEntitySource : BlockEntity
	{
		private LiquidNetwork? network = null;

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			var block = (BlockSource)Block;
			if(api.Side == EnumAppSide.Server)
			{
				network = new LiquidDistribution(api.World, block, Pos, block.Face.Opposite, block.MaxDistribution);
			}
			else
			{
				network = new LiquidGraphics(api.World, block, Pos, block.Face.Opposite, block.MaxDistribution);
			}
		}

		public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
		{
			(network as LiquidGraphics)?.Tesselate(mesher);
			return base.OnTesselation(mesher, tessThreadTesselator);
		}

		public void OnSourceBlockChanged()
		{
			network?.MarkSourceDirty();
		}

		public void OnChannelConfigurationChanged()
		{
			network?.MarkChannelsDirty();
		}

		public override void OnBlockRemoved()
		{
			base.OnBlockRemoved();
			network?.Dispose();
			network = null;
		}

		public override void OnBlockUnloaded()
		{
			base.OnBlockUnloaded();
			network?.Dispose();
			network = null;
		}

		private class LiquidGraphics : LiquidNetwork
		{
			private int liquidPathLength = 0;
			private WaterTightContainableProps? liquidProps = null;
			private LiquidGraphicProps? liquidGraphicProps = null;

			private bool isTicking = false;
			private bool isImmediateUpdateTriggered = false;
			private long tickerId;

			public LiquidGraphics(IWorldAccessor world, IChannelLiquidSource blockSource, BlockPos pos, BlockFacing channelsDirection, int maxChannels)
				: base(world, blockSource, pos, channelsDirection, maxChannels)
			{
				StartTicking();
			}

			public override bool MarkChannelsDirty()
			{
				if(base.MarkChannelsDirty())
				{
					StartTicking();
					ImmediateUpdate();
					return true;
				}
				return false;
			}

			public override bool MarkSourceDirty()
			{
				if(base.MarkSourceDirty())
				{
					StartTicking();
					return true;
				}
				return false;
			}

			public void Tesselate(ITerrainMeshPool mesher)
			{
				var liquidProps = liquidGraphicProps;
				if(liquidProps == null) return;
				int level = liquidPathLength + 1;

				var tmpPos = pos.Copy();
				var channels = new[] { (ILiquidChannel)blockSource }.Concat(this.channels.Select(c => (ILiquidChannel)c));
				var accessor = world.GetBlockAccessor(false, false, false);
				var tmpMesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();
				var cwChannelFace = channelsDirection.GetCW();
				var ccwChannelFace = channelsDirection.GetCCW();
				var inChannelFace = channelsDirection.Opposite;
				var outChannelFace = channelsDirection;
				Span<int> fillLevels = stackalloc int[6];
				var offset = new Vec3f();
				foreach(var channel in channels)
				{
					if(level <= 0) break;

					fillLevels[inChannelFace.Index] = Math.Min(level, 7);
					level--;
					fillLevels[cwChannelFace.Index] = Math.Min(level, 7);
					fillLevels[ccwChannelFace.Index] = Math.Min(level, 7);
					fillLevels[outChannelFace.Index] = Math.Min(level, 7);

					tmpMesh.Clear();
					channel.GenLiquidMesh(accessor, tmpPos.Copy(), tmpMesh, fillLevels);
					if(tmpMesh.VerticesCount > 0)
					{
						tmpMesh.Translate(offset);
						tmpMesh.SetTexPos(liquidProps.texture);
						tmpMesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.Transparent);
						tmpMesh.Flags.Fill(liquidProps.glowLevel);
						mesher.AddMeshData(tmpMesh);
					}

					tmpPos.Add(outChannelFace);
					offset.Add(outChannelFace.Normalf.X, outChannelFace.Normalf.Y, outChannelFace.Normalf.Z);
				}
			}

			public override void Dispose()
			{
				base.Dispose();
				StopTicking();
			}

			protected override void Tick()
			{
				if(UpdateNetwork()) MarkMeshDirty();
				if(liquidProps == null)
				{
					if(liquidPathLength > 0)
					{
						liquidPathLength--;
					}
					else
					{
						liquidGraphicProps = null;
						StopTicking();
					}
					MarkMeshDirty();
				}
				else
				{
					if(liquidPathLength < Math.Min(maxChannels, channels.Count + 7))
					{
						liquidPathLength++;
					}
					else
					{
						StopTicking();
					}
					MarkMeshDirty();
				}
			}

			private void ImmediateUpdate()
			{
				if(isImmediateUpdateTriggered) return;
				isImmediateUpdateTriggered = true;

				void TriggerImmediateUpdate()
				{
					isImmediateUpdateTriggered = false;
					if(UpdateNetwork()) MarkMeshDirty();
				}

				if(Environment.CurrentManagedThreadId == RuntimeEnv.MainThreadId)
				{
					TriggerImmediateUpdate();
				}
				else
				{
					world.Api.Event.EnqueueMainThreadTask(TriggerImmediateUpdate, "fieldsofsalt:besource-update-network");
				}
			}

			private void StartTicking()
			{
				if(isTicking) return;
				isTicking = true;
				tickerId = world.RegisterGameTickListener(_ => Tick(), 100, 100);
			}

			private void MarkMeshDirty()
			{
				world.BlockAccessor.MarkBlockDirty(pos);
			}

			private void StopTicking()
			{
				if(isTicking)
				{
					isTicking = false;
					world.UnregisterGameTickListener(tickerId);
				}
			}

			protected override void RebuildNetwork()
			{
				base.RebuildNetwork();
				liquidPathLength = Math.Min(liquidPathLength, channels.Count + 7);
			}

			protected override void UpdateLiquid(WaterTightContainableProps? liquidProps, ItemStack? liquidStack)
			{
				if(liquidProps != this.liquidProps)
				{
					this.liquidProps = liquidProps;
					if(liquidProps != null)
					{
						GraphicUtil.BakeTexture((ICoreClientAPI)world.Api, liquidProps.Texture, "Liquid source", out var texture);
						liquidGraphicProps = new LiquidGraphicProps(texture, liquidProps.GlowLevel);
					}
				}
			}
		}

		private sealed class LiquidGraphicProps
		{
			public readonly TextureAtlasPosition texture;
			public readonly int glowLevel;

			public LiquidGraphicProps(TextureAtlasPosition texture, int glowLevel)
			{
				this.texture = texture;
				this.glowLevel = glowLevel;
			}
		}

		private class LiquidDistribution : LiquidNetwork
		{
			private readonly long tickerId;

			private int liquidPathLength = 0;
			private WaterTightContainableProps? liquidProps = null;
			private ItemStack? liquidStack = null;

			public LiquidDistribution(IWorldAccessor world, IChannelLiquidSource blockSource, BlockPos pos, BlockFacing channelsDirection, int maxChannels)
				: base(world, blockSource, pos, channelsDirection, maxChannels)
			{
				tickerId = world.RegisterGameTickListener(_ => Tick(), 1000, 10);
			}

			public override void Dispose()
			{
				base.Dispose();
				world.UnregisterGameTickListener(tickerId);
			}

			protected override void Tick()
			{
				base.Tick();
				if(liquidPathLength < channels.Count)
				{
					liquidPathLength++;
				}
				if(liquidPathLength != 0 && liquidStack != null)
				{
					DistributeLiquid();
				}
			}

			protected override void RebuildNetwork()
			{
				base.RebuildNetwork();
				liquidPathLength = Math.Min(liquidPathLength, channels.Count);
			}

			protected override void UpdateLiquid(WaterTightContainableProps? liquidProps, ItemStack? liquidStack)
			{
				this.liquidProps = liquidProps;
				this.liquidStack = liquidStack;
			}

			private void DistributeLiquid()
			{
				var blockAccessor = world.GetBlockAccessor(false, false, false);
				int stackSize = (int)Math.Ceiling(liquidProps!.ItemsPerLitre * blockSource.LitresPerTickGeneration);
				foreach(var (sinkPos, sinkFace, sink) in sinks)
				{
					int offset = Math.Abs(channelsDirection.IsAxisWE ? (sinkPos.X - pos.X) : (sinkPos.Z - pos.Z));
					if(offset <= liquidPathLength)
					{
						var stack = liquidStack!.Clone();
						stack.StackSize = stackSize;
						sink.TryAccept(blockAccessor, sinkPos, sinkFace, ref stack);
					}
				}
			}
		}

		private abstract class LiquidNetwork : IDisposable
		{
			protected readonly IWorldAccessor world;
			protected readonly IChannelLiquidSource blockSource;
			protected readonly BlockPos pos;
			protected readonly BlockFacing channelsDirection;
			protected readonly int maxChannels;

			private bool isChannelsDirty = true;
			protected readonly List<(BlockPos, BlockFacing, ILiquidSink)> sinks = new();
			protected readonly ConcurrentStack<Block> channels = new();

			private readonly MultiblockManager manager;

			private bool isSourceDirty = true;

			protected LiquidNetwork(IWorldAccessor world, IChannelLiquidSource blockSource, BlockPos pos, BlockFacing channelsDirection, int maxChannels)
			{
				this.world = world;
				this.blockSource = blockSource;
				this.pos = pos;
				this.channelsDirection = channelsDirection;
				this.maxChannels = maxChannels;
				manager = world.Api.ModLoader.GetModSystem<MultiblockManager>();
			}

			public virtual bool MarkChannelsDirty()
			{
				if(isChannelsDirty) return false;

				isChannelsDirty = true;
				world.BlockAccessor.MarkBlockDirty(pos);
				ClearNetwork();
				return true;
			}

			public virtual bool MarkSourceDirty()
			{
				if(isSourceDirty) return false;

				isSourceDirty = true;
				return true;
			}

			public virtual void Dispose()
			{
				ClearNetwork();
			}

			protected virtual void Tick()
			{
				UpdateNetwork();
			}

			protected virtual bool UpdateNetwork()
			{
				bool isUpdated = false;
				if(isSourceDirty)
				{
					isSourceDirty = false;
					UpdateSource();
					isUpdated = true;
				}
				if(isChannelsDirty)
				{
					isChannelsDirty = false;
					RebuildNetwork();
					isUpdated = true;
				}
				return isUpdated;
			}

			protected virtual void RebuildNetwork()
			{
				var blockAccessor = world.GetBlockAccessor(false, false, false);
				var cwChannelFace = channelsDirection.GetCW();
				var ccwChannelFace = channelsDirection.GetCCW();
				var inChannelFace = channelsDirection.Opposite;

				var tmpPos = pos.Copy();
				for(int i = 0; i < maxChannels; i++)
				{
					tmpPos.Add(channelsDirection);
					var block = blockAccessor.GetBlock(tmpPos);
					if(block is not ILiquidChannel channel) break;
					if(!channel.CanConnect(blockAccessor, tmpPos, inChannelFace)) break;
					channels.Push(block);

					channel.GetConnectedSinks(blockAccessor, tmpPos, (pos, face, sink) => sinks.Add((pos, face, sink)));
				}

				RegisterMultiblock(channels.Count);
			}

			private void UpdateSource()
			{
				WaterTightContainableProps? liquidProps = null;
				ItemStack? liquidStack = null;
				var block = world.GetBlockAccessor(false, false, false).GetBlock(pos.AddCopy(channelsDirection.Opposite));
				if(block.IsLiquid() && block.LiquidLevel >= 7)
				{
					var props = block.Attributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
					if(props != null && props.WhenFilled?.Stack != null)
					{
						if(props.WhenFilled.Stack.Resolve(world, "fieldsofsalt:source"))
						{
							liquidStack = props.WhenFilled.Stack.ResolvedItemstack;
							liquidProps = BlockLiquidContainerBase.GetContainableProps(liquidStack);
							if(liquidProps == null)
							{
								liquidStack = null;
							}
						}
					}
				}
				UpdateLiquid(liquidProps, liquidStack);
			}

			protected abstract void UpdateLiquid(WaterTightContainableProps? liquidProps, ItemStack? liquidStack);

			protected virtual void ClearNetwork()
			{
				UnregisterMultiblock(channels.Count);
				sinks.Clear();
				channels.Clear();
			}

			private void RegisterMultiblock(int channels)
			{
				manager.AddReferenceToMainBlock(pos, pos);

				var tmpPos = pos.Copy();
				for(int i = 0; i < channels; i++)
				{
					tmpPos.Add(channelsDirection);
					manager.AddReferenceToMainBlock(tmpPos, pos);
				}

				if(world.Side == EnumAppSide.Client)
				{
					var accessor = world.BlockAccessor;
					tmpPos.SetAll(pos);
					for(int i = 0; i < channels; i++)
					{
						tmpPos.Add(channelsDirection);
						accessor.MarkBlockDirty(tmpPos);// Trigger chunks retesselation to update connected channels
					}
				}
			}

			private void UnregisterMultiblock(int channels)
			{
				manager.RemoveReferenceToMainBlock(pos, pos);

				var tmpPos = pos.Copy();
				for(int i = 0; i < channels; i++)
				{
					tmpPos.Add(channelsDirection);
					manager.RemoveReferenceToMainBlock(tmpPos, pos);
				}
			}
		}
	}
}