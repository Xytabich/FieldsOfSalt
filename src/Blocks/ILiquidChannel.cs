using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public interface ILiquidChannel : ILiquidConnectable
	{
		/// <summary>
		/// Connects sink to the network
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Channel position</param>
		/// <param name="face">Channel face</param>
		/// <param name="sink">Sink instance</param>
		void AddSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ILiquidSink sink);

		/// <summary>
		/// Disconnects sink from a network
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Channel position</param>
		/// <param name="face">Channel face</param>
		/// <param name="sink">Sink instance</param>
		void RemoveSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ILiquidSink sink);

		/// <summary>
		/// Calls a callback for each sink connected to the channel
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Channel position</param>
		/// <param name="addSinkCallback">Callback (BlockPos sinkPosition, BlockFacing sinkFace, ILiquidSink sinkInstance)</param>
		void GetConnectedSinks(IBlockAccessor blockAccessor, BlockPos pos, Action<BlockPos, BlockFacing, ILiquidSink> addSinkCallback);

		/// <summary>
		/// Generates liquid mesh according to levels.
		/// UV coordinates must be in the range 0-1 (where 0 is the start point of the cube and 1 is the end point)
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Channel position</param>
		/// <param name="outMesh">Destination mesh</param>
		/// <param name="levels">Liquid levels per face, 6 total</param>
		void GenLiquidMesh(IBlockAccessor blockAccessor, BlockPos pos, MeshData outMesh, ReadOnlySpan<int> levels);
	}
}