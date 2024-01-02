using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Scene geometry rendering pass using the 'Final shadow pass' result to lerp between lit and shadowed textures.
	/// </summary>
	public class ColorPass
	{
		private static readonly ProfilingSampler Sampler = new("Color Pass (Opaque Geometry)");

		private static readonly ShaderTagId[] ShaderTagIDs = { new("ColorPass") };

		private static readonly int FinalShadowBufferId = Shader.PropertyToID("_FinalShadowBuffer");


		private RendererListHandle m_list;
		private TextureHandle m_finalShadowBuffer, m_colorAttachment, m_depthAttachment;
		private Camera m_camera;

		void Render(RenderGraphContext context)
		{
			// Set the render target.
			context.cmd.SetRenderTarget(m_colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, m_depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			
			// Clear only color.
			context.cmd.ClearRenderTarget(false, true, m_camera.backgroundColor.linear);

			// Add the 'Final shadow pass' result to the shaders.
			context.cmd.SetGlobalTexture(FinalShadowBufferId, m_finalShadowBuffer);

			// Render the scene geometry using the renderer list.
			context.cmd.DrawRendererList(m_list);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out ColorPass pass, Sampler);

			// Generate a renderer list for rendering the opaque scene geometry. 
			pass.m_list = builder.UseRendererList(renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIDs, cullingResults, camera)
				{
					sortingCriteria = SortingCriteria.CommonOpaque,
					rendererConfiguration = PerObjectData.None,
					renderQueueRange = RenderQueueRange.opaque
				}));

			pass.m_camera = camera;
			
			pass.m_finalShadowBuffer = builder.ReadTexture(textures.finalShadowBuffer);
			pass.m_colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
			pass.m_depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);

			builder.SetRenderFunc<ColorPass>((pass, context) => pass.Render(context));
		}
	}
}