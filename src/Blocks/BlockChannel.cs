using FieldsOfSalt.Blocks.Entities;
using FieldsOfSalt.Utils;
using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace FieldsOfSalt.Blocks
{
	public class BlockChannel : Block, ILiquidChannel
	{
		[ThreadStatic]
		private static BlockPos tmpPos = null;
		[ThreadStatic]
		private static BlockPos mainPos = null;

		private EnumAxis axis;

		private FieldsOfSaltMod mod;
		private Cuboidf[] fillAreas = null;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			mod = api.ModLoader.GetModSystem<FieldsOfSaltMod>();
			axis = Variant["side"] == "we" ? EnumAxis.X : EnumAxis.Z;
		}

		public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
		{
			if(Shape?.Alternates != null && Shape.Alternates.Length >= 16)
			{
				if(tmpPos == null) tmpPos = new BlockPos();
				if(BlockChannel.mainPos == null) BlockChannel.mainPos = new BlockPos();
				var mainPos = BlockChannel.mainPos;

				int variantIndex = 0;
				var accessor = api.World.BlockAccessor;
				if(!mod.GetReferenceToMainBlock(pos, mainPos))
				{
					mainPos = null;
				}
				if(axis == EnumAxis.X)
				{
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.WEST, true, mainPos, 1);
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.EAST, true, mainPos, 2);
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.NORTH, false, mainPos, 4);
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.SOUTH, false, mainPos, 8);
				}
				else
				{
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.NORTH, true, mainPos, 1);
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.SOUTH, true, mainPos, 2);
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.WEST, false, mainPos, 4);
					variantIndex |= CanConnect(accessor, pos, chunkExtBlocks, extIndex3d, BlockFacing.EAST, false, mainPos, 8);
				}
				var variantKey = "fieldsofsalt:channel|" + Code.ToShortString() + "-" + variantIndex;
				sourceMesh = ObjectCacheUtil.GetOrCreate(api, variantKey, () => {
					var tesselator = (ShapeTesselator)((ICoreClientAPI)api).Tesselator;
					tesselator.TesselateBlock(this, Shape.Alternates[variantIndex], out var mesh, (TextureSource)tesselator.GetTextureSource(this));//Why is it hidden so deep..?
					return mesh;
				});
			}
			base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);
		}

		public bool CanConnect(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face)
		{
			return axis == EnumAxis.X ? face.IsAxisWE : face.IsAxisNS;
		}

		public unsafe void GenLiquidMesh(IBlockAccessor blockAccessor, BlockPos pos, MeshData outMesh, int[] levels)
		{
			EnsureFillAreas();
			if(tmpPos == null) tmpPos = new BlockPos();
			if(BlockChannel.mainPos == null) BlockChannel.mainPos = new BlockPos();
			var mainPos = BlockChannel.mainPos;

			var accessor = api.World.BlockAccessor;
			if(!mod.GetReferenceToMainBlock(pos, mainPos))
			{
				mainPos = null;
			}
			int connectedSides = 0;
			if(axis == EnumAxis.X)
			{
				connectedSides |= CanConnect(accessor, pos, BlockFacing.NORTH, false, mainPos, 1);
				connectedSides |= CanConnect(accessor, pos, BlockFacing.EAST, true, mainPos, 2);
				connectedSides |= CanConnect(accessor, pos, BlockFacing.SOUTH, false, mainPos, 4);
				connectedSides |= CanConnect(accessor, pos, BlockFacing.WEST, true, mainPos, 8);
			}
			else
			{
				connectedSides |= CanConnect(accessor, pos, BlockFacing.NORTH, true, mainPos, 1);
				connectedSides |= CanConnect(accessor, pos, BlockFacing.EAST, false, mainPos, 2);
				connectedSides |= CanConnect(accessor, pos, BlockFacing.SOUTH, true, mainPos, 4);
				connectedSides |= CanConnect(accessor, pos, BlockFacing.WEST, false, mainPos, 8);
			}
			connectedSides |= 16;//center
			for(int i = 0; i < 5; i++)
			{
				if((connectedSides & 1) != 0)
				{
					GraphicUtil.AddLiquidMesh(outMesh, fillAreas[i], 0);
				}
				connectedSides >>= 1;
			}

			float* heights = stackalloc float[4];
			GraphicUtil.GetLiquidHeightsHorizontal(levels, fillAreas, heights);
			GraphicUtil.MultiplyLiquidHeightsHorizontal(outMesh, heights);
		}

		public void AddSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ILiquidSink sink)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				(blockAccessor.GetBlockEntity(mainPos) as BlockEntitySource)?.AddSink(pos.AddCopy(face), face.Opposite, sink);
			}
		}

		public void RemoveSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ILiquidSink sink)
		{
			var mainPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, mainPos))
			{
				(blockAccessor.GetBlockEntity(mainPos) as BlockEntitySource)?.RemoveSink(pos.AddCopy(face), face.Opposite, sink);
			}
		}

		public void GetConnectedSinks(IBlockAccessor blockAccessor, BlockPos pos, Action<BlockPos, BlockFacing, ILiquidSink> addSinkCallback)
		{
			if(tmpPos == null) tmpPos = new BlockPos();
			if(axis == EnumAxis.X)
			{
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.WEST, addSinkCallback);
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.EAST, addSinkCallback);
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.NORTH, addSinkCallback);
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.SOUTH, addSinkCallback);
			}
			else
			{
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.NORTH, addSinkCallback);
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.SOUTH, addSinkCallback);
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.WEST, addSinkCallback);
				TryAddConnectedSink(blockAccessor, pos, BlockFacing.EAST, addSinkCallback);
			}
		}

		public override bool DisplacesLiquids(IBlockAccessor blockAccess, BlockPos pos)
		{
			return true;
		}

		public override float LiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
		{
			return 1f;
		}

		public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
		{
			base.OnBlockPlaced(world, blockPos, byItemStack);
			var blockAccessor = api.World.BlockAccessor;

			if(tmpPos == null) tmpPos = new BlockPos();
			if(axis == EnumAxis.X)
			{
				TryConnectToMultiblock(blockAccessor, blockPos, BlockFacing.WEST);
				TryConnectToMultiblock(blockAccessor, blockPos, BlockFacing.EAST);
			}
			else
			{
				TryConnectToMultiblock(blockAccessor, blockPos, BlockFacing.NORTH);
				TryConnectToMultiblock(blockAccessor, blockPos, BlockFacing.SOUTH);
			}
		}

		public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
		{
			base.OnBlockRemoved(world, pos);
			if(tmpPos == null) tmpPos = new BlockPos();
			if(mod.GetReferenceToMainBlock(pos, tmpPos))
			{
				if(api.World.BlockAccessor.GetBlockEntity(tmpPos) is BlockEntitySource source)
				{
					source.RemoveChannel(pos);
				}
			}
		}

		private void EnsureFillAreas()
		{
			if(this.fillAreas != null) return;

			var fillAreas = new Cuboidf[5];
			float[][] attr = Attributes?["fillAreas"].AsObject<float[][]>();
			if(attr != null && attr.Length != 5) attr = null;
			for(int i = 0; i < 5; i++)
			{
				var cuboid = new Cuboidf();
				float[] bb = attr == null ? null : attr[i];
				if(bb != null && bb.Length == 6)
				{
					cuboid.Set(bb[0], bb[1], bb[2], bb[3], bb[4], bb[5]);
				}
				fillAreas[i] = cuboid;
			}
			this.fillAreas = fillAreas;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TryConnectToMultiblock(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face)
		{
			tmpPos.Set(pos);
			tmpPos.Add(face);
			if(!mod.GetReferenceToMainBlock(tmpPos, tmpPos))
			{
				tmpPos.Set(pos);
				tmpPos.Add(face);
			}
			if(blockAccessor.GetBlockEntity(tmpPos) is BlockEntitySource source)
			{
				source.AddChannel(pos);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int CanConnect(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, bool checkChannel, BlockPos mainBlockReference, int flag)
		{
			tmpPos.Set(pos);
			tmpPos.Add(face);
			if(blockAccessor.GetBlock(tmpPos) is ILiquidConnectable connectable)
			{
				if(connectable.CanConnect(blockAccessor, tmpPos, face.Opposite))
				{
					if(connectable is ILiquidChannel)
					{
						if(!checkChannel || mod.GetReferenceToMainBlock(tmpPos, tmpPos) ? (tmpPos.Equals(mainBlockReference) == false) : (mainBlockReference != null))
						{
							return 0;
						}
					}
					return flag;
				}
			}
			return 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int CanConnect(IBlockAccessor blockAccessor, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d,
			BlockFacing face, bool checkChannel, BlockPos mainBlockReference, int flag)
		{
			if(chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[face.Index]] is ILiquidConnectable connectable)
			{
				tmpPos.Set(pos);
				tmpPos.Add(face);
				if(connectable.CanConnect(blockAccessor, tmpPos, face.Opposite))
				{
					if(connectable is ILiquidChannel)
					{
						if(!checkChannel || mod.GetReferenceToMainBlock(tmpPos, tmpPos) ? (tmpPos.Equals(mainBlockReference) == false) : (mainBlockReference != null))
						{
							return 0;
						}
					}
					return flag;
				}
			}
			return 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void TryAddConnectedSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, Action<BlockPos, BlockFacing, ILiquidSink> addSinkCallback)
		{
			tmpPos.Set(pos);
			tmpPos.Add(face);
			if(blockAccessor.GetBlock(tmpPos) is ILiquidSinkConnector conn)
			{
				if(conn.CanConnect(blockAccessor, tmpPos, face.Opposite))
				{
					var sink = conn.GetLiquidSink(blockAccessor, tmpPos, face.Opposite);
					if(sink != null) addSinkCallback(tmpPos, face.Opposite, sink);
				}
			}
		}
	}
}