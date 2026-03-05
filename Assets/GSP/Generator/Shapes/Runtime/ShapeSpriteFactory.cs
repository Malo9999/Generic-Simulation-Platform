using UnityEngine;

public static class ShapeSpriteFactory
{
    public static Texture2D CreateTexture(int size, Color32[] pixels)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = $"shape_tex_{size}"
        };

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    public static Sprite CreateSprite(Texture2D texture, int ppu)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(1, ppu));
    }
}
