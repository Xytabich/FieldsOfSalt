using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Blocks
{
	public interface ILiquidConnectable
	{
		/// <summary>
		/// Returns whether it is possible to connect from the side.
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Connectable position</param>
		/// <param name="face">Connectable face</param>
		/// <returns></returns>
		bool CanConnect(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face);
	}
}