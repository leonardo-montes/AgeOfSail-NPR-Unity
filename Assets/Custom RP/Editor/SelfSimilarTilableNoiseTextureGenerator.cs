using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering;
using System.IO;

public class SelfSimilarTilableNoiseTextureGenerator : MonoBehaviour
{
	[MenuItem("Assets/Generate Self-Similar Tilable Noise Texture (RG) to '.PNG'")]
    static void GenerateNoiseTextureRG()
    {
        int width = 1024;
        int height = 1024;

        float scale = 20f;
        int octaves = 6;
        float persistence = 0.5f;
        float lacunarity = 2f; 

        Texture2D texture = new Texture2D(width, height, GraphicsFormat.R8G8_UNorm, TextureCreationFlags.None);

        float2 gOffset = new float2(UnityEngine.Random.value * 999.9f, UnityEngine.Random.value * 999.9f);
        float2 minMaxR = new float2(Mathf.Infinity, Mathf.NegativeInfinity);
        float2 minMaxG = new float2(Mathf.Infinity, Mathf.NegativeInfinity);
        float2[,] colors = new float2[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;

                float r = GeneratePerlinNoise(xCoord, yCoord, octaves, persistence, lacunarity);
                float g = GeneratePerlinNoise(xCoord + gOffset.x, yCoord + gOffset.y, octaves, persistence, lacunarity);

                minMaxR.x = Mathf.Min(minMaxR.x, r);
                minMaxR.y = Mathf.Max(minMaxR.y, r);

                minMaxG.x = Mathf.Min(minMaxG.x, g);
                minMaxG.y = Mathf.Max(minMaxG.y, g);

                colors[x, y] = new float2(r, g);
            }
        }
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                colors[x, y].x = (colors[x, y].x - minMaxR.x) / (minMaxR.y - minMaxR.x);
                colors[x, y].y = (colors[x, y].y - minMaxG.x) / (minMaxG.y - minMaxG.x);
                Color color = new Color(colors[x, y].x, colors[x, y].y, 0.0f, 1.0f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();

        string path = Path.Combine(Application.dataPath, string.Format("{0}.png", GUID.Generate().ToString()));
        File.WriteAllBytes(path, texture.EncodeToPNG ());
        AssetDatabase.Refresh();
        AssetDatabase.LoadAssetAtPath("Assets/" + path, typeof(Texture2D));
        AssetDatabase.Refresh();
    }

    static float GeneratePerlinNoise(float x, float y, int octaves, float persistence, float lacunarity)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;

        for (int i = 0; i < octaves; i++)
        {
            float xCoord = x * frequency;
            float yCoord = y * frequency;

            float perlinValue = Mathf.PerlinNoise(xCoord, yCoord);
            total += perlinValue * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total;
    }

}
