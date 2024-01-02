using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Scene geometry rendering pass for getting the lighting information.
	/// </summary>
	public class ShadowPass
	{
		private static readonly ProfilingSampler Sampler = new("Shadow Pass (Opaque Geometry)");

		private static readonly ShaderTagId[] ShaderTagIDs = { new("ShadowPass") };
		private static readonly Color ClearColor = new Color(1.0f, 0.0f, 0.0f, 0.0f);
		private static readonly Color ClearColorNoLight = new Color(0.0f, 0.0f, 0.0f, 0.0f);


		private RendererListHandle m_list;
		private TextureHandle m_colorAttachment, m_depthAttachment;
		private bool m_hasLight;

		private void Render(RenderGraphContext context)
		{
			// Set the render target and clear it
			context.cmd.SetRenderTarget(m_colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, m_depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			context.cmd.ClearRenderTarget(true, true, m_hasLight ? ClearColor : ClearColorNoLight);

			// Render the renderer list
			context.cmd.DrawRendererList(m_list);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		public static RendererListHandle Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, in CameraRendererTextures textures,
			in ShadowTextures shadowTextures, bool hasLight)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out ShadowPass pass, Sampler);

			// Create a renderer list from the scene geometry
			RendererListHandle listHandle = renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIDs, cullingResults, camera)
				{
					sortingCriteria = SortingCriteria.CommonOpaque,
					rendererConfiguration = PerObjectData.None,
					renderQueueRange = RenderQueueRange.opaque
				});
			pass.m_list = builder.UseRendererList(listHandle);
			pass.m_hasLight = hasLight;

			pass.m_colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
			pass.m_depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);
			builder.ReadTexture(shadowTextures.directionalAtlas);

			builder.SetRenderFunc<ShadowPass>((pass, context) => pass.Render(context));
			return listHandle;
		}
	}
}