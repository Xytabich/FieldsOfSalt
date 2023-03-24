using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FieldsOfSalt.Blocks
{
	public interface ILiquidSink
	{
		/// <summary>
		/// Pouring liquid into the sink, returns <see langword="true"/> if the sink can accept liquid.
		/// Information about the amount of liquid is passed through <paramref name="liquid"/>, if the sink cannot accept the entire volume of liquid, it indicates the amount of liquid it received in <see cref="IItemStack.StackSize"/>.
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Sink position</param>
		/// <param name="face">Sink face</param>
		/// <param name="liquid">If it is not possible to get the full amount - assign the stack size to the amount consumed (i.e. do not subtract from the stack size, but indicate the consumption).</param>
		/// <returns></returns>
		bool TryAccept(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, ref ItemStack liquid);

		/// <summary>
		/// Sets the level of liquid received from the channel.
		/// For visual purposes only, called on the client side.
		/// </summary>
		/// <param name="blockAccessor"></param>
		/// <param name="pos">Sink position</param>
		/// <param name="face">Sink face</param>
		/// <param name="level">Value 0-7</param>
		/// <param name="liquidProps">Liquid properties, such as texture. May be <see langword="null"/> if the liquid is over</param>
		void SetLiquidLevel(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing face, int level, WaterTightContainableProps liquidProps);
	}
}