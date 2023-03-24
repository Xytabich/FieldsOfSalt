using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public interface ILiquidSinkConnector : ILiquidConnectable
	{
		/// <summary>
		/// Returns the sink instance if possible
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Sink position</param>
		/// <param name="face">Sink face</param>
		/// <returns>Sink instance or <see langword="null"/></returns>
		ILiquidSink GetLiquidSink(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face);
	}
}