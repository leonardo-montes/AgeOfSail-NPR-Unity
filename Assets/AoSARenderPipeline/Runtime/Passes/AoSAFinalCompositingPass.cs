using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	/// <summary>
	/// Image-processing pass compositing the final render pipeline with the bloom textures and warping the image using the
	/// result from the 'warp pass'. Also applies saturation and an overlay color.
	/// 
	/// Also used for debug rendering.
	/// </summary>
	public class FinalCompositingPass
	{
		private static readonly ProfilingSampler m_sampler = new("FinalCompositingPass");

		private static int WarpBloomId = Shader.PropertyToID("_WarpBloom");
		private static int OverlayId = Shader.PropertyToID("_Overlay");
		private static int SaturationId = Shader.PropertyToID("_Saturation");

#if UNITY_EDITOR
		private static int DebugCountId = Shader.PropertyToID("_DebugCount");
#endif

		private Camera m_camera;
		private AoSARenderPipelineSettings m_settings;
		
		private TextureHandle m_litColorBuffer, m_warpBuffer;
		private TextureHandle[] m_bloomBuffers;
		
#if UNITY_EDITOR
		private TextureHandle m_debugBuffer;
		private TextureHandle[] m_debugBuffers;
#endif

		private Color m_overlay;
		private float m_saturation;

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
		/// Composite the textures into the final target.
		/// </summary>
		private void Render(CommandBuffer buffer)
		{
			buffer.SetGlobalFloat(WarpBloomId, m_settings.warpBloom ? 1.0f : 0.0f);
			buffer.SetGlobalColor(OverlayId, m_overlay);
			buffer.SetGlobalFloat(SaturationId, m_saturation);
			
			buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[0], m_litColorBuffer);
			buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[1], m_warpBuffer);
			for (int i = 0; i < m_bloomBuffers.Length; ++i)
				buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[2 + i], m_bloomBuffers[i]);

			buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.None);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)Pass.FinalCompositingPass, MeshTopology.Triangles, 3);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Copy debug buffers to the final target.
		/// </summary>
		private void RenderDebug (CommandBuffer buffer)
		{
			Pass pass;
			string name = UnityEditor.SceneView.currentDrawingSceneView.cameraMode.name;
			if (name == AdditionalCameraModes.Warp.ToString() ||
				name == AdditionalCameraModes.LitColor.ToString() ||
				name == AdditionalCameraModes.ShadowedColor.ToString())
			{
				pass = Pass.Copy;
				buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[0], m_debugBuffer);
			}
			else
			{
				pass = Pass.DebugMultiple;
				buffer.SetGlobalInt(DebugCountId, m_debugBuffers.Length);

				for (int i = 0; i < m_debugBuffers.Length; ++i)
					buffer.SetGlobalTexture(RenderPipelineHelper.SourceIds[i], m_debugBuffers[i]);
			}

			buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.None);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}
#endif

		public static void Record(RenderGraph renderGraph, Camera camera, AoSARenderPipelineSettings settings, Color overlay, float saturation, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out FinalCompositingPass pass, m_sampler);
			
			pass.m_settings = settings;
			pass.m_camera = camera;
			pass.m_overlay = overlay;
			pass.m_saturation = saturation;
			
			ReadTextures(camera, builder, pass, textures);
			
			builder.SetRenderFunc<FinalCompositingPass>((pass, context) => pass.Render(context));
		}

		/// <summary>
		/// Read the appropriate textures depending on debugging needs.
		/// </summary>
		private static void ReadTextures (Camera camera, RenderGraphBuilder builder, FinalCompositingPass pass, in CameraRendererTextures textures)
		{
#if UNITY_EDITOR
			if (RenderPipelineHelper.IsDebugRender(camera, AdditionalDrawModes.Section))
			{
				string name = UnityEditor.SceneView.currentDrawingSceneView.cameraMode.name;
				if (name == AdditionalCameraModes.Warp.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.warpColor);
				else if (name == AdditionalCameraModes.LitColor.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.litColorBuffer);
				else if (name == AdditionalCameraModes.ShadowedColor.ToString())
					pass.m_debugBuffer = builder.ReadTexture(textures.shadowedColorBuffer);
				else if (name == AdditionalCameraModes.Shadow.ToString())
					pass.m_debugBuffers = builder.ReadTextures(textures.shadowBuffers);
				else if (name == AdditionalCameraModes.SoftBlur.ToString())
					pass.m_debugBuffers = builder.ReadTextures(textures.softBlurBuffers);
				else if (name == AdditionalCameraModes.HeavyBlur.ToString())
					pass.m_debugBuffers = builder.ReadTextures(textures.heavyBlurBuffers);
			}
			else
			{
				pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
				pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
				pass.m_bloomBuffers = builder.ReadTextures(textures.bloomBuffers);
			}
#else
			pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
			pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
			pass.m_bloomBuffers = builder.ReadTextures(textures.bloomBuffers);
#endif
		}
	}
}