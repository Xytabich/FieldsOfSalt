using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace FieldsOfSalt.Utils
{
	public static class GraphicUtil
	{
		[ThreadStatic]
		private static Vec2f cuboidUVOffset = null;
		[ThreadStatic]
		private static Vec2f cuboidUVSize = null;
		[ThreadStatic]
		private static Vec3f cuboidCenter = null;
		[ThreadStatic]
		private static Vec3f cuboidSize = null;
		[ThreadStatic]
		private static int[] vertexFlags = null;

		public static void AddLiquidMesh(MeshData outMesh, Cuboidf fillArea, int flags)
		{
			EnsureTmpVariables();

			cuboidUVOffset.X = fillArea.X1;
			cuboidUVSize.X = fillArea.Width;
			cuboidUVOffset.Y = fillArea.Z1;
			cuboidUVSize.Y = fillArea.Length;
			cuboidCenter.X = (fillArea.X1 + fillArea.X2) * 0.5f;
			cuboidCenter.Y = (fillArea.Y1 + fillArea.Y2) * 0.5f;
			cuboidCenter.Z = (fillArea.Z1 + fillArea.Z2) * 0.5f;
			cuboidSize.X = fillArea.Width;
			cuboidSize.Y = 0;
			cuboidSize.Z = fillArea.Length;

			vertexFlags.Fill(flags);
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.UP, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, 0, -1, ModelCubeUtilExt.EnumShadeMode.Off, vertexFlags);
		}

		public static void AddLiquidBlockMesh(MeshData outMesh, Vec3f offset, TextureAtlasPosition texPos, int color, short renderPass, int flags)
		{
			EnsureTmpVariables();

			cuboidUVOffset.X = texPos.x1;
			cuboidUVSize.X = texPos.x2 - texPos.x1;
			cuboidUVOffset.Y = texPos.y1;
			cuboidUVSize.Y = texPos.y2 - texPos.y1;
			cuboidCenter.X = offset.X;
			cuboidCenter.Y = offset.Y;
			cuboidCenter.Z = offset.Z;
			cuboidSize.X = 1;
			cuboidSize.Y = 0;
			cuboidSize.Z = 1;

			vertexFlags.Fill(flags);
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.UP, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, texPos.atlasTextureId, color, ModelCubeUtilExt.EnumShadeMode.Off, vertexFlags, renderPass: renderPass);
		}

		public static void AddContentMesh(MeshData outMesh, Vec3f offset, float height, TextureAtlasPosition texPos, int color, int flags)
		{
			EnsureTmpVariables();

			cuboidUVOffset.X = texPos.x1;
			cuboidUVSize.X = texPos.x2 - texPos.x1;
			cuboidUVOffset.Y = texPos.y1;
			cuboidUVSize.Y = texPos.y2 - texPos.y1;
			cuboidCenter.X = offset.X;
			cuboidCenter.Y = offset.Y + height * 0.5f;
			cuboidCenter.Z = offset.Z;
			cuboidSize.X = 1;
			cuboidSize.Y = height;
			cuboidSize.Z = 1;

			vertexFlags.Fill(flags);
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.UP, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, texPos.atlasTextureId, color, ModelCubeUtilExt.EnumShadeMode.On, vertexFlags);

			cuboidUVSize.Y = (texPos.y2 - texPos.y1) * height;
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.WEST, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, texPos.atlasTextureId, color, ModelCubeUtilExt.EnumShadeMode.On, vertexFlags);
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.EAST, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, texPos.atlasTextureId, color, ModelCubeUtilExt.EnumShadeMode.On, vertexFlags);
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.NORTH, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, texPos.atlasTextureId, color, ModelCubeUtilExt.EnumShadeMode.On, vertexFlags);
			ModelCubeUtilExt.AddFace(outMesh, BlockFacing.SOUTH, cuboidCenter, cuboidSize, cuboidUVOffset, cuboidUVSize, texPos.atlasTextureId, color, ModelCubeUtilExt.EnumShadeMode.On, vertexFlags);
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

		public static unsafe void GetLiquidHeightsHorizontal(ReadOnlySpan<int> levels, Cuboidf[] fillAreas, float* outHeights)
		{
			const float m = 1f / 7f;
			for(int i = 0; i < 4; i++)
			{
				var cuboid = fillAreas[i];
				outHeights[i] = cuboid.Y1 + levels[i] * m * cuboid.Height;
			}
		}

		public static unsafe void GetLiquidHeightsHorizontal(ReadOnlySpan<int> levels, Cuboidf fillArea, float* outHeights)
		{
			const float m = 1f / 7f;
			for(int i = 0; i < 4; i++)
			{
				outHeights[i] = fillArea.Y1 + levels[i] * m * fillArea.Height;
			}
		}

		public static void BakeTexture(ICoreClientAPI capi, CompositeTexture texture, string sourceForLogging, out TextureAtlasPosition texPos)
		{
			var atlas = capi.BlockTextureAtlas;
			texPos = atlas.UnknownTexturePosition;
			if(texture != null)
			{
				texture.Bake(capi.Assets);
				if(capi.BlockTextureAtlas.GetOrInsertTexture(texture.Base, out var textureSubId, out var _, () => {
					IAsset asset = capi.Assets.TryGet(texture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
					if(asset != null)
					{
						return asset.ToBitmap(capi);
					}
					capi.World.Logger.Warning("{0} defined texture {1}, but no such texture found.", sourceForLogging, texture.Base);
					return null;
				}))
				{
					texture.Baked.TextureSubId = textureSubId;

					var texId = texture.Baked?.TextureSubId;
					texPos = texId.HasValue ? atlas.Positions[texId.Value] : atlas.UnknownTexturePosition;
				}
			}
		}

		private static void EnsureTmpVariables()
		{
			if(cuboidUVOffset == null) cuboidUVOffset = new Vec2f();
			if(cuboidUVSize == null) cuboidUVSize = new Vec2f();
			if(cuboidCenter == null) cuboidCenter = new Vec3f();
			if(cuboidSize == null) cuboidSize = new Vec3f();
			if(vertexFlags == null) vertexFlags = new int[4];
		}
	}
}