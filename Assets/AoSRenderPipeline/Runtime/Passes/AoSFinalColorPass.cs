using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Image-processing pass compositing the result from the 'Final shadow pass' to apply bloom and warp the image using the
	/// result from the 'warp pass'.
	/// 
	/// Also used for debug rendering.
	/// </summary>
	public class FinalColorPass
	{
		private static readonly ProfilingSampler m_sampler = new("Final Color Pass");

		private static GlobalKeyword WarpBloomId = GlobalKeyword.Create("_WARP_BLOOM");

		private TextureHandle m_colorAttachment, m_warpBuffer, m_finalShadowBuffer;
		
#if UNITY_EDITOR
		private TextureHandle m_debugBuffer;
#endif

		private AoSRenderPipelineSettings m_settings;
		private Camera m_camera;

		/// <summary>
		/// Render the final target with or without debugging.
		/// </summary>
		private void Render(RenderGraphContext context)
		{
#if UNITY_EDITOR
			if (RenderPipelineHelper.IsDebugRender(m_camera, AdditionalDrawModes.Section))
				RenderDebug(context.cmd);
			else
				Render(context.cmd);
#else
			Render(context.cmd);
#endif

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		/// <summary>
		/// Composite the three textures into the final target.
		/// </summary>
		private void Render(CommandBuffer buffer)
		{
			// Set if we need to warp the bloom or not.
			if (m_settings.warpBloom)
				buffer.EnableKeyword(WarpBloomId);
			else
				buffer.DisableKeyword(WarpBloomId);

			// Render the images with the 3 textures.
			buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[1], m_warpBuffer);
			buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[2], m_finalShadowBuffer);
			RenderPipelineHelper.Draw(buffer, m_colorAttachment, BuiltinRenderTextureType.CameraTarget, (int)Pass.FinalColorPass, m_settings.Material);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Copy a debug buffer directly to the final target.
		/// </summary>
		private void RenderDebug (CommandBuffer buffer)
		{
			RenderPipelineHelper.Draw(buffer, m_debugBuffer, BuiltinRenderTextureType.CameraTarget, (int)Pass.Copy, m_settings.Material);
		}
#endif

		public static void Record(RenderGraph renderGraph, Camera camera, AoSRenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out FinalColorPass pass, m_sampler);

			pass.m_settings = settings;
			pass.m_camera = camera;

			ReadTextures(camera, builder, pass, textures);

			builder.SetRenderFunc<FinalColorPass>((pass, context) => pass.Render(context));
		}

		/// <summary>
		/// Read the appropriate textures depending on debugging needs.
		/// </summary>
		private static void ReadTextures (Camera camera, RenderGraphBuilder builder, FinalColorPass pass, in CameraRendererTextures textures)
		{
#if UNITY_EDITOR
			if (RenderPipelineHelper.IsDebugRender(camera, AdditionalDrawModes.Section))
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