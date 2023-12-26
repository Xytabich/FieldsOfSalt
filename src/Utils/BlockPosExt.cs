using System.Runtime.CompilerServices;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Utils
{
	public static class BlockPosExt
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAll(this BlockPos target, BlockPos source)
		{
			target.X = source.X;
			target.Y = source.Y;
			target.Z = source.Z;
			target.dimension = source.dimension;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetInternal(this BlockPos target, int x, int y, int z)
		{
			target.X = x;
			target.InternalY = y;
			target.Z = z;
		}
	}
}