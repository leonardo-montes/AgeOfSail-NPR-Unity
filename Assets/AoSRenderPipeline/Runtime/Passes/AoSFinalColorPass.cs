using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class FinalColorPass
	{
		private static readonly ProfilingSampler m_sampler = new("Final Color Pass");

		private static int WarpBloomId = Shader.PropertyToID("_WarpBloom");

		private static int Source0Id = Shader.PropertyToID("_Source0");
		private static int Source1Id = Shader.PropertyToID("_Source1");
		private static int Source2Id = Shader.PropertyToID("_Source2");

		private TextureHandle m_colorAttachment, m_warpBuffer, m_finalShadowBuffer;
		
#if UNITY_EDITOR
		private TextureHandle m_debugBuffer;
#endif

		private AoSRenderPipelineSettings m_settings;
		private Camera m_camera;

		private void Render(RenderGraphContext context)
		{
#if UNITY_EDITOR
			if (IsDebugRender(m_camera))
				RenderDebug(context.cmd);
			else
				Render(context.cmd);
#else
			Render(context.cmd);
#endif

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Render(CommandBuffer buffer)
		{
			buffer.SetGlobalFloat(WarpBloomId, m_settings.warpBloom ? 1.0f : 0.0f);

			buffer.SetGlobalTexture(Source1Id, m_warpBuffer);
			buffer.SetGlobalTexture(Source2Id, m_finalShadowBuffer);
			Draw(buffer, m_colorAttachment, BuiltinRenderTextureType.CameraTarget, AoSRenderPipeline.Pass.FinalColorPass);
		}

#if UNITY_EDITOR
		private void RenderDebug (CommandBuffer buffer)
		{
			buffer.SetGlobalTexture(Source0Id, m_debugBuffer);
			buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.None);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)AoSRenderPipeline.Pass.Copy, MeshTopology.Triangles, 3);
		}
#endif

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSRenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(Source0Id, from);
			buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, Camera camera, AoSRenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out FinalColorPass pass, m_sampler);
			pass.m_settings = settings;
			pass.m_camera = camera;
			ReadTextures(camera, builder, pass, textures);
			builder.SetRenderFunc<FinalColorPass>((pass, context) => pass.Render(context));
		}

#if UNITY_EDITOR
		private static bool IsDebugRender (Camera camera)
		{
			return camera.cameraType == CameraType.SceneView && SceneView.currentDrawingSceneView.cameraMode.drawMode == DrawCameraMode.UserDefined &&
					SceneView.currentDrawingSceneView.cameraMode.section == AdditionalDrawModes.Section;
		}
#endif

		private static void ReadTextures (Camera camera, RenderGraphBuilder builder, FinalColorPass pass, in CameraRendererTextures textures)
		{
#if UNITY_EDITOR
			if (IsDebugRender(camera))
			{
				string name = SceneView.currentDrawingSceneView.cameraMode.name;
				if (name == AdditionalCameraModes.Warp.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.warpColor);
				else if (name == AdditionalCameraModes.FinalShadow.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.finalShadowBuffer);
				else if (name == AdditionalCameraModes.SoftBlur.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.softBlurBuffer);
				else if (name == AdditionalCameraModes.HeavyBlur.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.heavyBlurBuffer);
			}
			else
			{
				pass.m_colorAttachment = builder.ReadTexture(textures.colorAttachment);
				pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
				pass.m_finalShadowBuffer = builder.ReadTexture(textures.finalShadowBuffer);
			}
#else
			pass.m_colorAttachment = builder.ReadTexture(textures.colorAttachment);
			pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
			pass.m_finalShadowBuffer = builder.ReadTexture(textures.finalShadowBuffer);
#endif
		}
	}
}