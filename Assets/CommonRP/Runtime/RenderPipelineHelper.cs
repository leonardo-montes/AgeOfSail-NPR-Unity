using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public static class RenderPipelineHelper
{
    public static readonly int[] TempRTsId = new int[]
    {
        Shader.PropertyToID("_TempRT0"),
        Shader.PropertyToID("_TempRT1"),
        Shader.PropertyToID("_TempRT2"),
        Shader.PropertyToID("_TempRT3"),
        Shader.PropertyToID("_TempRT4"),
        Shader.PropertyToID("_TempRT5"),
        Shader.PropertyToID("_TempRT6"),
        Shader.PropertyToID("_TempRT7"),
        Shader.PropertyToID("_TempRT8"),
        Shader.PropertyToID("_TempRT9"),
        Shader.PropertyToID("_TempRT10"),
        Shader.PropertyToID("_TempRT11"),
        Shader.PropertyToID("_TempRT12"),
        Shader.PropertyToID("_TempRT13"),
        Shader.PropertyToID("_TempRT14"),
        Shader.PropertyToID("_TempRT15"),
    };

    public static readonly int[] SourceIds = new int[]
    {
        Shader.PropertyToID("_Source0"),
        Shader.PropertyToID("_Source1"),
        Shader.PropertyToID("_Source2"),
        Shader.PropertyToID("_Source3"),
        Shader.PropertyToID("_Source4"),
        Shader.PropertyToID("_Source5"),
        Shader.PropertyToID("_Source6"),
        Shader.PropertyToID("_Source7"),
        Shader.PropertyToID("_Source8"),
        Shader.PropertyToID("_Source9"),
        Shader.PropertyToID("_Source10"),
        Shader.PropertyToID("_Source11"),
        Shader.PropertyToID("_Source12"),
        Shader.PropertyToID("_Source13"),
        Shader.PropertyToID("_Source14"),
        Shader.PropertyToID("_Source15")
    };

    /// <summary>
    /// Draw a fullscreen triangle using a specific image-processing shader pass.
    /// </summary>
    /// <param name="buffer">The command buffer to fill.</param>
    /// <param name="from">The texture to sample from.</param>
    /// <param name="to">The texture to fill.</param>
    /// <param name="pass">The image-processing shader pass to use.</param>
    public static void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, int pass, Material material)
    {
        buffer.SetGlobalTexture(SourceIds[0], from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
    }

    /// <summary>
    /// Draw a fullscreen triangle using a specific image-processing shader pass.
    /// </summary>
    /// <param name="buffer">The command buffer to fill.</param>
    /// <param name="from">The texture to sample from.</param>
    /// <param name="to">The texture to fill.</param>
    /// <param name="pass">The image-processing shader pass to use.</param>
    public static void Draw(CommandBuffer buffer, RenderTargetIdentifier[] from, RenderTargetIdentifier[] to, int pass, Material material)
    {
        for (int i = 0; i < from.Length; ++i)
            buffer.SetGlobalTexture(SourceIds[i], from[i]);
        buffer.SetRenderTarget(to, BuiltinRenderTextureType.None);
        buffer.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Check if we need to render a specific buffer.
    /// </summary>
    /// <returns>Returns true if we need to render a specific buffer.</returns>
    public static bool IsDebugRender (Camera camera, string section)
    {
        return camera.cameraType == CameraType.SceneView && SceneView.currentDrawingSceneView.cameraMode.drawMode == DrawCameraMode.UserDefined &&
                SceneView.currentDrawingSceneView.cameraMode.section == section;
    }
#endif

    /// <summary>
    /// Specify an array of Texture resources to read from during the pass.
    /// </summary>
    /// <param name="input">The Texture resource to read from during the pass.</param>
    /// <returns>An updated resource handle to the input resource.</returns>
    public static TextureHandle[] ReadTextures(this RenderGraphBuilder builder, in TextureHandle[] textures)
    {
        TextureHandle[] newHandles = new TextureHandle[textures.Length];
        for (int i = 0; i < textures.Length; ++i)
        {
            newHandles[i] = builder.ReadTexture(textures[i]);
        }
        return newHandles;
    }


    /// <summary>
    /// Specify an array of Texture resources to write to during the pass.
    /// </summary>
    /// <param name="input">The Texture resource to write to during the pass.</param>
    /// <returns>An updated resource handle to the input resource.</returns>
    public static TextureHandle[] WriteTextures(this RenderGraphBuilder builder, in TextureHandle[] textures)
    {
        TextureHandle[] newHandles = new TextureHandle[textures.Length];
        for (int i = 0; i < textures.Length; ++i)
        {
            newHandles[i] = builder.WriteTexture(textures[i]);
        }
        return newHandles;
    }

    /// <summary>
    /// Specify an array of Texture resources to read and write to during the pass.
    /// </summary>
    /// <param name="input">The Texture resource to read and write to during the pass.</param>
    /// <returns>An updated resource handle to the input resource.</returns>
    public static TextureHandle[] ReadWriteTextures(this RenderGraphBuilder builder, in TextureHandle[] textures)
    {
        TextureHandle[] newHandles = new TextureHandle[textures.Length];
        for (int i = 0; i < textures.Length; ++i)
        {
            newHandles[i] = builder.ReadWriteTexture(textures[i]);
        }
        return newHandles;
    }
}
