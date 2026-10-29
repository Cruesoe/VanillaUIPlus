using System;
using System.Collections.Generic;
using UnityEngine;

namespace VanillaUIPlus;

// Zooms bottom-right play-setting icons whose visible glyph fills less than TargetFill of the texture.
public static class IconFit
{
    private const float TargetFill = 0.9f;
    private const byte AlphaThreshold = 40;
    private static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);
    private static readonly Dictionary<Texture2D, Rect> UvByTex = new Dictionary<Texture2D, Rect>();

    public static void Draw(Rect rect, Texture2D tex)
    {
        Rect uv = Uv(tex);
        if (uv == FullUv)
        {
            GUI.DrawTexture(rect, tex);
        }
        else
        {
            GUI.DrawTextureWithTexCoords(rect, tex, uv);
        }
    }

    public static Rect Uv(Texture2D tex)
    {
        if (!UvByTex.TryGetValue(tex, out Rect uv))
        {
            uv = Measure(tex);
            UvByTex[tex] = uv;
        }

        return uv;
    }

    // Unreadable textures (GetPixels32 throws) and textures already at TargetFill draw unchanged.
    private static Rect Measure(Texture2D tex)
    {
        Color32[] pixels;
        try
        {
            pixels = tex.GetPixels32();
        }
        catch (Exception)
        {
            return FullUv;
        }

        int width = tex.width, height = tex.height;
        if (pixels.Length < width * height)
        {
            return FullUv;
        }

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a > AlphaThreshold)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        if (maxX < 0)
        {
            return FullUv;
        }

        float fill = Mathf.Max((maxX - minX + 1) / (float)width, (maxY - minY + 1) / (float)height);
        if (fill >= TargetFill)
        {
            return FullUv;
        }

        // The UV window keeps the aspect ratio and is centred on the glyph, kept inside the texture.
        float size = fill / TargetFill;
        float centerX = (minX + maxX + 1) / 2f / width;
        float centerY = (minY + maxY + 1) / 2f / height;
        return new Rect(
            Mathf.Clamp(centerX - size / 2f, 0f, 1f - size),
            Mathf.Clamp(centerY - size / 2f, 0f, 1f - size),
            size,
            size);
    }
}
