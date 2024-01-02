using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace AoSA.RenderPipeline
{
	/// <summary>
	/// Scene geometry rendering pass that fills the warp buffer.
	/// </summary>
	public class WarpPass
	{
		private static readonly ProfilingSampler Sampler = new("Warp Pass (Opaque Geometry)");

		private static readonly ShaderTagId[] ShaderTagIDs = { new("WarpPass") };

		private static readonly int WarpWidthId = Shader.PropertyToID("_WarpWidth");
		private static readonly int WarpTextureId = Shader.PropertyToID("_WarpTexture");
		private static readonly int WarpTextureScaleId = Shader.PropertyToID("_WarpTextureScale");
		private static readonly int WarpGlobalDistanceFadeId = Shader.PropertyToID("_WarpGlobalDistanceFade");
		private static readonly int LineBoilTimeId = Shader.PropertyToID("_LineBoilTime");

		public static readonly Color ClearColor = new Color(0.5f, 0.5f, 0.0f, 1.0f);

		private RendererListHandle m_list;
		private TextureHandle m_warpColor, m_warpDepth;
		private AoSARenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			// Set and clear the render target
			context.cmd.SetRenderTarget(m_warpColor, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, m_warpDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			context.cmd.ClearRenderTarget(true, true, ClearColor);

			// Set shader properties
			float t = (float)(Time.realtimeSinceStartupAsDouble % 3600.0);
			Vector4 lineBoilTime = new Vector4(t,							// x = realtime
											Mathf.Floor(t * 24) / 24,	// y = 24 fps
											Mathf.Floor(t * 12) / 12,	// z = 12 fps (animate on twos)
											Mathf.Floor(t * 8) / 8);		// w = 8  fps (animate on threes)
			context.cmd.SetGlobalVector(LineBoilTimeId, lineBoilTime);

			context.cmd.SetGlobalTexture(WarpTextureId, m_settings.warpTexture);
			context.cmd.SetGlobalFloat(WarpTextureScaleId, m_settings.warpGlobalScale);
			context.cmd.SetGlobalFloat(WarpGlobalDistanceFadeId, m_settings.warpGlobalDistanceFade);
			context.cmd.SetGlobalFloat(WarpWidthId, m_settings.warpWidth);

			// Render the renderer list
			context.cmd.DrawRendererList(m_list);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, AoSARenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out WarpPass pass, Sampler);

			// Create a renderer list from the scene opaque geometry.
			pass.m_list = builder.UseRendererList(renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIDs, cullingResults, camera)
				{
					sortingCriteria = SortingCriteria.CommonOpaque,
					rendererConfiguration = PerObjectData.None,
					renderQueueRange = RenderQueueRange.opaque,
				}));

			pass.m_settings = settings;

			pass.m_warpColor = builder.ReadWriteTexture(textures.warpColor);
			pass.m_warpDepth = builder.ReadWriteTexture(textures.warpDepth);

			builder.SetRenderFunc<WarpPass>((pass, context) => pass.Render(context));
		}
	}
}