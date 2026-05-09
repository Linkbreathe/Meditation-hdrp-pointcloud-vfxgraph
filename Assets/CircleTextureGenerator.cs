#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class CircleTextureGenerator
{
    [MenuItem("Tools/Generate Circle Texture")]
    public static void Generate()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                // 边缘软化一个像素
                float alpha = Mathf.Clamp01(radius - dist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        string path = "Assets/Textures/CircleMask.png";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        // 自动设置为 Alpha 透明
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        Debug.Log("Circle texture saved to: " + path);
    }
}
#endif