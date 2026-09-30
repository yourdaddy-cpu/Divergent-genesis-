using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Items;
using DivergentGenesis.World;

namespace DivergentGenesis.UI
{
    /// <summary>
    /// Draws every inventory icon procedurally at 32x32. Blocks get an isometric
    /// cube, tools get a silhouette. Costs a few KB instead of a texture atlas and
    /// means new items appear the moment you add them to the database.
    /// </summary>
    public static class ItemIconFactory
    {
        private const int Size = 32;
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>(128);

        public static Sprite Get(ItemId id)
        {
            int key = (int)id;
            Sprite s;
            if (Cache.TryGetValue(key, out s) && s != null) return s;

            s = Render(id);
            Cache[key] = s;
            return s;
        }

        public static Sprite GetFromStack(ItemStack stack)
        {
            return stack.IsEmpty ? null : Get(stack.Id);
        }

        private static Sprite Render(ItemId id)
        {
            var def = ItemDef.Get(id);
            var px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            Color32 c = def.Color;
            Color32 dark = Shade(c, 0.62f);
            Color32 light = Shade(c, 1.28f);
            Color32 edge = Shade(c, 0.42f);

            if (def.IsBlock) DrawBlock(px, c, dark, light, edge);
            else if (def.Tool == ToolClass.Sword) DrawSword(px, c, dark, light);
            else if (def.Tool == ToolClass.Pickaxe) DrawPickaxe(px, c, dark, light);
            else if (def.Tool == ToolClass.Axe) DrawAxe(px, c, dark, light);
            else if (def.Tool == ToolClass.Shovel) DrawShovel(px, c, dark, light);
            else if (def.IsFood) DrawFood(px, c, light);
            else DrawNugget(px, c, light, dark);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "Icon_" + id
            };
            tex.SetPixels32(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
            sprite.name = "Icon_" + id;
            return sprite;
        }

        // ------------------------------------------------------------- shapes
        private static void DrawBlock(Color32[] px, Color32 c, Color32 dark, Color32 light, Color32 edge)
        {
            // top face (a rhombus), then two side faces: a cheap isometric cube
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int u = x - 16, v = y - 16;
                // top diamond
                if (Mathf.Abs(u) + Mathf.Abs(v) <= 9) { px[y * Size + x] = light; continue; }
                // left face
                if (u <= 0 && v >= -9 - u && v <= 9 + u) { px[y * Size + x] = c; continue; }
                // right face
                if (u >= 0 && v >= u - 9 && v <= 18 - u) { px[y * Size + x] = dark; continue; }
            }
            for (int i = 0; i < Size; i++)
            {
                px[(2) * Size + i] = edge;
                px[(Size - 3) * Size + i] = edge;
            }
        }

        private static void DrawSword(Color32[] px, Color32 c, Color32 dark, Color32 light)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int d = x + y;
                if (d >= 10 && d <= 24 && x >= 6 && x <= 24) px[y * Size + x] = (d < 16) ? light : c;
                // guard
                if (d >= 8 && d <= 10 && x >= 6 && x <= 16) px[y * Size + x] = dark;
                // handle
                if (d < 8 && x >= 4 && x <= 9) px[y * Size + x] = new Color32(120, 90, 56, 255);
            }
        }

        private static void DrawPickaxe(Color32[] px, Color32 c, Color32 dark, Color32 light)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (x >= 14 && x <= 18 && y <= 26) { px[y * Size + x] = new Color32(124, 92, 56, 255); continue; }
                int dy = y - 10;
                if (dy <= 0 && Mathf.Abs(x - 16) + Mathf.Abs(dy) <= 11) px[y * Size + x] = (dy < -4) ? light : c;
            }
        }

        private static void DrawAxe(Color32[] px, Color32 c, Color32 dark, Color32 light)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (x >= 16 && x <= 20 && y <= 26) { px[y * Size + x] = new Color32(124, 92, 56, 255); continue; }
                if (x >= 18 && x <= 28 && y >= 6 && y <= 18)
                    px[y * Size + x] = (y < 11) ? light : c;
            }
        }

        private static void DrawShovel(Color32[] px, Color32 c, Color32 dark, Color32 light)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (x >= 14 && x <= 17 && y >= 8 && y <= 26) { px[y * Size + x] = new Color32(124, 92, 56, 255); continue; }
                if (y >= 22 && x >= 10 && x <= 21) px[y * Size + x] = (x < 15) ? light : c;
            }
        }

        private static void DrawFood(Color32[] px, Color32 c, Color32 light)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - 16f) / 9f;
                float dy = (y - 15f) / 10f;
                float d = dx * dx + dy * dy;
                if (d <= 1f) px[y * Size + x] = d < 0.55f ? light : c;
            }
            if (px[(26) * Size + 16] == new Color32(0, 0, 0, 0)) px[26 * Size + 16] = Shade(c, 0.5f);
        }

        private static void DrawNugget(Color32[] px, Color32 c, Color32 light, Color32 dark)
        {
            int[][] blobs =
            {
                new[] { 10, 12, 6 }, new[] { 21, 18, 5 }, new[] { 14, 22, 4 }, new[] { 23, 11, 3 }
            };
            for (int b = 0; b < blobs.Length; b++)
            for (int y = -blobs[b][2]; y <= blobs[b][2]; y++)
            for (int x = -blobs[b][2]; x <= blobs[b][2]; x++)
            {
                if (x * x + y * y > blobs[b][2] * blobs[b][2]) continue;
                int px2 = blobs[b][0] + x, py = blobs[b][1] + y;
                if (px2 < 0 || px2 >= Size || py < 0 || py >= Size) continue;
                px[py * Size + px2] = (x + y < 0) ? light : (x + y > 2 ? dark : c);
            }
        }

        private static Color32 Shade(Color32 c, float f)
        {
            return new Color32(
                (byte)Mathf.Clamp(c.r * f, 0f, 255f),
                (byte)Mathf.Clamp(c.g * f, 0f, 255f),
                (byte)Mathf.Clamp(c.b * f, 0f, 255f),
                255);
        }
    }
}
