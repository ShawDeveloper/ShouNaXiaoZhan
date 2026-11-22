using System.Collections.Generic;
using UnityEngine;

namespace StorageStation
{
    public enum PrimitiveShape
    {
        Square,
        Circle,
        Ring,
        Triangle,
        HalfLeft,
        HalfRight
    }

    public static class Geometry
    {
        private static readonly Dictionary<PrimitiveShape, Sprite> sprites = new Dictionary<PrimitiveShape, Sprite>();

        public static SpriteRenderer Create(
            Transform parent,
            string name,
            PrimitiveShape shape,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder = 0)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSprite(shape);
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        public static Sprite GetSprite(PrimitiveShape shape)
        {
            if (sprites.TryGetValue(shape, out var cached)) return cached;

            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = shape + "Primitive";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[resolution * resolution];
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var uv = new Vector2((x + 0.5f) / resolution, (y + 0.5f) / resolution);
                    var p = uv - Vector2.one * 0.5f;
                    var visible = shape == PrimitiveShape.Square;
                    if (shape == PrimitiveShape.Circle || shape == PrimitiveShape.Ring ||
                        shape == PrimitiveShape.HalfLeft || shape == PrimitiveShape.HalfRight)
                    {
                        var radius = p.sqrMagnitude;
                        visible = radius <= 0.25f;
                        if (shape == PrimitiveShape.Ring) visible = visible && radius >= 0.12f;
                        if (shape == PrimitiveShape.HalfLeft) visible = visible && p.x <= 0f;
                        if (shape == PrimitiveShape.HalfRight) visible = visible && p.x >= 0f;
                    }
                    else if (shape == PrimitiveShape.Triangle)
                    {
                        visible = uv.y <= 1f - Mathf.Abs(p.x) * 2f && uv.y >= 0f;
                    }
                    pixels[y * resolution + x] = visible ? Color.white : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution);
            sprite.name = shape + "Sprite";
            sprites[shape] = sprite;
            return sprite;
        }

        public static Color Darken(Color color, float amount = 0.45f)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, color.a);
        }
    }
}
