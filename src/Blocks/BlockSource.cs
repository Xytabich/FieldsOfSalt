﻿using FieldsOfSalt.Blocks.Entities;
using FieldsOfSalt.Utils;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public class BlockSource : Block, ILiquidChannel
	{
		public BlockFacing Face;
		public float LitresPerTickGeneration;

		private Cuboidf fillArea;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);
			Face = BlockFacing.FromCode(Variant["side"]);
			LitresPerTickGeneration = Attributes?["genLitres"].AsFloat(0.01f) ?? 0.01f;

			fillArea = new Cuboidf();
			float[] bb = Attributes?["fillArea"].AsArray<float>();
			if(bb != null && bb.Length == 6)
			{
				fillArea.Set(bb[0], bb[1], bb[2], bb[3], bb[4], bb[5]);
			}
		}

		public bool CanConnect(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face)
		{
			return Face.Index == face.Opposite.Index;
		}

		public unsafe void GenLiquidMesh(IBlockAccessor blockAccessor, BlockPos pos, MeshData outMesh, ReadOnlySpan<int> levels)
		{
			GraphicUtil.AddLiquidMesh(outMesh, fillArea, 0);

			float* heights = stackalloc float[4];
			GraphicUtil.GetLiquidHeightsHorizontal(levels, fillArea, heights);
			GraphicUtil.MultiplyLiquidHeightsHorizontal(outMesh, heights);
		}

		public void AddSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ILiquidSink sink)
		{
			(blockAccessor.GetBlockEntity(pos) as BlockEntitySource)?.AddSink(pos.AddCopy(face), face.Opposite, sink);
		}

		public void RemoveSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ILiquidSink sink)
		{
			(blockAccessor.GetBlockEntity(pos) as BlockEntitySource)?.RemoveSink(pos.AddCopy(face), face.Opposite, sink);
		}

		public void GetConnectedSinks(IBlockAccessor blockAccessor, BlockPos pos, Action<BlockPos, BlockFacing, ILiquidSink> actionCallback)
		{
			var tmpPos = pos.Copy();
			tmpPos.SetAll(pos);
			tmpPos.Add(Face.Opposite);
			if(blockAccessor.GetBlock(tmpPos) is ILiquidSinkConnector conn)
			{
				if(conn.CanConnect(blockAccessor, tmpPos, Face))
				{
					var sink = conn.GetLiquidSink(blockAccessor, tmpPos, Face);
					if(sink != null) actionCallback(tmpPos, Face, sink);
				}
			}
		}

		public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
		{
			base.OnNeighbourBlockChange(world, pos, neibpos);
			if(neibpos.FacingFrom(pos).Index == Face.Index)
			{
				(world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySource)?.UpdateLiquidBlock(world.BlockAccessor);
			}
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