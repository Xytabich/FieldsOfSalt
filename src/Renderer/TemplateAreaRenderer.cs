using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace FieldsOfSalt.Renderer
{
	public class TemplateAreaRenderer : IRenderer
	{
		public double RenderOrder => 0.89;
		public int RenderRange => 1;

		private readonly ICoreClientAPI capi;

		private Vec3d areaCenter = null;
		private Vec3f areaSize = null;

		private MeshRef meshRef = null;
		private MeshData meshData = null;

		public TemplateAreaRenderer(ICoreClientAPI capi)
		{
			this.capi = capi;
			meshData = new MeshData(24, 36, withNormals: false, withUv: false, withRgba: true, withFlags: false);
			for(int i = 0; i < 6; i++)
			{
				ModelCubeUtilExt.AddFace(meshData, BlockFacing.ALLFACES[i], Vec3f.Zero, Vec3f.One, -1);
			}

			capi.Event.RegisterRenderer(this, EnumRenderStage.OIT, "fieldsofsalt:templatearea");
		}

		public unsafe void RenderFrameNext(Vec3d center, Vec3f size, int color)
		{
			this.areaCenter = center;
			this.areaSize = size;

			fixed(byte* ptr = meshData.Rgba)
			{
				for(int i = 0; i < meshData.VerticesCount; i++)
				{
					((int*)ptr)[i] = color;
				}
			}
		}

		void IRenderer.OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			if(areaCenter == null) return;

			var rapi = capi.Render;
			if(meshRef == null)
			{
				meshRef = rapi.UploadMesh(meshData);
				meshData.xyz = null;//Only color will be needed next
			}
			else
			{
				rapi.UpdateMesh(meshRef, meshData);
			}

			rapi.GlPushMatrix();
			rapi.GlLoadMatrix(rapi.CameraMatrixOrigin);

			var playerPos = capi.World.Player.Entity.CameraPos;
			rapi.GlTranslate((float)(areaCenter.X - playerPos.X), (float)(areaCenter.Y - playerPos.Y), (float)(areaCenter.Z - playerPos.Z));
			rapi.GlScale(areaSize.X, areaSize.Y, areaSize.Z);

			var prog = ShaderPrograms.Blockhighlights;
			prog.Use();
			prog.ModelViewMatrix = rapi.CurrentModelviewMatrix;
			prog.ProjectionMatrix = rapi.CurrentProjectionMatrix;
			rapi.RenderMesh(meshRef);
			prog.Stop();

			rapi.GlPopMatrix();

			areaCenter = null;
		}

		public void Dispose()
		{
			capi.Event.UnregisterRenderer(this, EnumRenderStage.OIT);
			meshRef?.Dispose();
			meshRef = null;
		}
	}
}