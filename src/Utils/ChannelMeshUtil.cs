using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace FieldsOfSalt.Utils
{
	public static class ChannelMeshUtil
	{
		[ThreadStatic]
		private static Vec2f cuboidUVOffset = null;
		[ThreadStatic]
		private static Vec2f cuboidUVSize = null;
		[ThreadStatic]
		private static Vec3f cuboidCenter = null;
		[ThreadStatic]
		private static Vec3f cuboidSize = null;

		private static readonly int[] vertexFlags = new int[4];

		public static void AddLiquidMesh(MeshData outMesh, Cuboidf fillArea)
		{
			if(cuboidUVOffset == null) cuboidUVOffset = new Vec2f();
			if(cuboidUVSize == null) cuboidUVSize = new Vec2f();
			if(cuboidCenter == null) cuboidCenter = new Vec3f();
			if(cuboidSize == null) cuboidSize = new Vec3f();

			cuboidUVOffset.X = fillArea.X1;
			cuboidUVSize.X = fillArea.Width;
			cuboidUVOffset.Y = fillArea.Z1;
			cuboidUVSize.Y = fillArea.Length;
			cuboidCenter.X = fillArea.X1 * 0.5f + fillArea.X2 * 0.5f;
			cuboidCenter.Y = fillArea.Y1 * 0.5f + fillArea.Y2 * 0.5f;
			cuboidCenter.Z = fillArea.Z1 * 0.5f + fillArea.Z2 * 0.5f;
			cuboidSize.X = fillArea.Width;
			cuboidSize.Z = fillArea.Length;
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.UP, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, -1, ModelCubeUtilExt.EnumShadeMode.Off, vertexFlags);
		}

		public static unsafe void MultiplyLiquidHeightsHorizontal(MeshData outMesh, float* heights)
		{
			int compCount = outMesh.VerticesCount * 3;
			for(int i = 0; i < compCount; i += 3)
			{
				outMesh.xyz[i + 1] = Math.Max(GameMath.Lerp(heights[BlockFacing.indexWEST], heights[BlockFacing.indexEAST], outMesh.xyz[i]),
					GameMath.Lerp(heights[BlockFacing.indexNORTH], heights[BlockFacing.indexSOUTH], outMesh.xyz[i + 2]));
			}
		}

		public static unsafe void GetLiquidHeightsHorizontal(int[] levels, Cuboidf[] fillAreas, float* outHeights)
		{
			const float m = 1f / 7f;
			for(int i = 0; i < 4; i++)
			{
				var cuboid = fillAreas[i];
				outHeights[i] = cuboid.Y1 + levels[i] * m * cuboid.Height;
			}
		}

		public static unsafe void GetLiquidHeightsHorizontal(int[] levels, Cuboidf fillArea, float* outHeights)
		{
			const float m = 1f / 7f;
			for(int i = 0; i < 4; i++)
			{
				outHeights[i] = fillArea.Y1 + levels[i] * m * fillArea.Height;
			}
		}
	}
}