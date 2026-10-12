using System;
using UnityEngine;

namespace Childhood
{
    internal sealed class WorldPalette
    {
        public string LocationName;
        public bool Courtyard;
        public bool Alley;
        public bool OldTree;
        public float Friction;
        public Material Ground, Paving, Brick, Plaster, Cream, Roof, DarkRoof, Wood, Iron, Rubber, Glass;
        public Material Mint, Red, Blue, Yellow, White, Chalk, Terracotta, Soil, Trunk, Leaf, LeafLight, LeafDark, DryLeaf, Grass;
        public Material Lamp, Paper, Cable, Stone, TileRed;
        public Material Canopy, CanopyLight, CanopyDark;

        public static WorldPalette Create(string id, Func<UnityEngine.Object, UnityEngine.Object> own)
        {
            var p = new WorldPalette();
            string value = id.ToLowerInvariant();
            p.Courtyard = value.Contains("court") || value.Contains("home") || value.Contains("yard") || value.Contains("院");
            p.Alley = value.Contains("alley") || value.Contains("shop") || value.Contains("巷");
            p.OldTree = value.Contains("old_tree") || value.Contains("tree") || value.Contains("槐");
            p.LocationName = p.Courtyard ? "外婆家的小院" : p.OldTree ? "老槐树下" : p.Alley ? "小卖部巷口" : "放学后的校门口";
            p.Friction = p.Courtyard ? 0.68f : p.Alley ? 0.44f : p.OldTree ? 0.63f : 0.58f;
            p.Ground = Material("细粒沥青", new Color(0.43f, 0.47f, 0.46f), own, 0f);
            p.Ground.mainTexture = (Texture2D)own(Texture("road", 256, new Color(0.69f, 0.72f, 0.71f)));
            p.Ground.mainTextureScale = new Vector2(12f, 12f);
            p.Paving = Material("蓝灰石砖", new Color(0.65f, 0.7f, 0.70f), own);
            p.Paving.mainTexture = (Texture2D)own(Texture("paving", 256, new Color(0.72f, 0.76f, 0.76f)));
            p.Paving.mainTextureScale = new Vector2(12f, 9f);
            p.Brick = Material("年代感红砖", new Color(0.85f, 0.73f, 0.65f), own);
            p.Brick.mainTexture = (Texture2D)own(Texture("brick", 256, new Color(0.67f, 0.39f, 0.28f)));
            p.Brick.mainTextureScale = new Vector2(2.6f, 1.5f);
            p.Plaster = Material("泛旧的奶油墙", new Color(0.91f, 0.87f, 0.70f), own);
            p.Plaster.mainTexture = (Texture2D)own(Texture("plaster", 128, Color.white));
            p.Plaster.mainTextureScale = new Vector2(3f, 3f);
            p.Cream = Material("象牙白", new Color(0.85f, 0.82f, 0.66f), own);
            p.Roof = Material("褪色瓦顶", new Color(0.56f, 0.29f, 0.20f), own);
            p.DarkRoof = Material("屋檐阴影", new Color(0.25f, 0.30f, 0.32f), own);
            p.Wood = Material("老木头", new Color(0.49f, 0.29f, 0.12f), own);
            p.Wood.mainTexture = (Texture2D)own(Texture("wood", 128, new Color(0.93f, 0.84f, 0.66f)));
            p.Iron = Material("深蓝铁器", new Color(0.16f, 0.23f, 0.27f), own, 0.65f, 0.28f);
            p.Rubber = Material("轮胎", new Color(0.055f, 0.071f, 0.07f), own);
            p.Glass = Material("旧窗玻璃", new Color(0.30f, 0.50f, 0.54f), own, 0.4f, 0.65f);
            p.Mint = Material("薄荷绿油漆", new Color(0.35f, 0.64f, 0.56f), own, 0.15f);
            p.Red = Material("朱红", new Color(0.81f, 0.25f, 0.16f), own);
            p.Blue = Material("湖蓝", new Color(0.25f, 0.50f, 0.60f), own);
            p.Yellow = Material("暖橘黄", new Color(0.95f, 0.69f, 0.26f), own);
            p.White = Material("白色", new Color(0.94f, 0.94f, 0.83f), own);
            p.Chalk = Material("孩子们画的粉笔线", new Color(0.95f, 0.93f, 0.78f), own);
            p.Chalk.SetColor("_EmissionColor", new Color(0.08f, 0.07f, 0.045f));
            p.Chalk.EnableKeyword("_EMISSION");
            p.Terracotta = Material("陶土花盆", new Color(0.62f, 0.32f, 0.20f), own);
            p.Soil = Material("花土", new Color(0.22f, 0.20f, 0.13f), own);
            p.Trunk = Material("槐树树皮", new Color(0.34f, 0.28f, 0.18f), own);
            p.Trunk.mainTexture = (Texture2D)own(Texture("wood", 128, new Color(0.87f, 0.84f, 0.70f)));
            p.Leaf = Material("槐叶翠绿", new Color(0.33f, 0.48f, 0.24f), own);
            p.LeafLight = Material("受光叶子", new Color(0.49f, 0.62f, 0.30f), own);
            p.LeafDark = Material("深绿油漆与叶色", new Color(0.23f, 0.36f, 0.22f), own);
            p.Canopy = FoliageMaterial("槐叶翠绿", new Color(0.33f, 0.48f, 0.24f), own);
            p.CanopyLight = FoliageMaterial("受光叶子", new Color(0.49f, 0.62f, 0.30f), own);
            p.CanopyDark = FoliageMaterial("叶间阴影", new Color(0.23f, 0.36f, 0.22f), own);
            p.DryLeaf = Material("落叶", new Color(0.72f, 0.53f, 0.22f), own);
            p.Grass = Material("墙角的小草", new Color(0.36f, 0.49f, 0.24f), own);
            p.Lamp = Material("暖光灯泡", new Color(1f, 0.88f, 0.57f), own);
            p.Lamp.SetColor("_EmissionColor", new Color(1.25f, 0.76f, 0.26f));
            p.Lamp.EnableKeyword("_EMISSION");
            p.Paper = Material("旧纸张", new Color(0.93f, 0.84f, 0.62f), own);
            p.Cable = Material("电线", new Color(0.13f, 0.16f, 0.17f), own);
            p.Stone = Material("旧水泥", new Color(0.59f, 0.62f, 0.56f), own);
            p.TileRed = Material("红色地砖", new Color(0.64f, 0.40f, 0.31f), own);
            return p;
        }

        static Material FoliageMaterial(string name, Color color, Func<UnityEngine.Object, UnityEngine.Object> own)
        {
            Shader shader = Resources.Load<Shader>("Shaders/FoliageWind");
            if (shader == null) return Material(name, color, own);
            var material = (Material)own(new Material(shader));
            material.name = name + " · 微风叶影";
            material.color = color;
            material.SetFloat("_WindStrength", .044f);
            material.SetFloat("_Glossiness", .08f);
            material.enableInstancing = true;
            return material;
        }

        static Material Material(string name, Color color, Func<UnityEngine.Object, UnityEngine.Object> own, float metal = 0f, float smooth = 0.06f)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Diffuse");
            var material = (Material)own(new Material(shader));
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Glossiness", smooth);
            material.enableInstancing = true;
            return material;
        }

        static float Noise(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 65535) / 65535f;
            }
        }

        static Texture2D Texture(string style, int size, Color baseColor)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            texture.name = "手工纹理 · " + style;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grit = Noise(x, y, 37);
                    float brightness = 0.9f + grit * 0.17f;
                    Color color = baseColor;
                    if (style == "brick")
                    {
                        int row = y / 32;
                        int bx = (x + (row % 2) * 32) % 64;
                        int by = y % 32;
                        float variation = Noise((x + (row % 2) * 32) / 64, row, 9);
                        brightness *= 0.80f + variation * 0.3f;
                        if (bx < 3 || by < 3) { color = new Color(0.68f, 0.63f, 0.52f); brightness = 0.93f + grit * 0.08f; }
                        else if (bx < 5 || by < 5) brightness *= 0.84f;
                    }
                    else if (style == "paving")
                    {
                        int row = y / 64;
                        int bx = (x + (row % 2) * 32) % 64;
                        int by = y % 64;
                        brightness *= 0.91f + Noise((x + (row % 2) * 32) / 64, row, 3) * 0.16f;
                        if (bx < 2 || by < 2) brightness *= 0.58f;
                        else if (bx < 4 || by < 4) brightness *= 1.05f;
                    }
                    else if (style == "road")
                    {
                        brightness = 0.88f + grit * 0.25f;
                        if (grit > 0.987f) brightness = 1.28f;
                        else if (grit < 0.025f) brightness = 0.7f;
                    }
                    else if (style == "wood")
                    {
                        float grain = Mathf.Sin(x * 0.46f + Mathf.Sin(y * 0.043f) * 1.7f + Noise(x / 12, y / 56, 13));
                        brightness = 0.84f + grit * 0.14f + grain * 0.12f;
                    }
                    else brightness = 0.94f + grit * 0.08f + Mathf.Sin(x * 0.037f) * Mathf.Sin(y * 0.054f) * 0.04f;
                    pixels[y * size + x] = color * brightness;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        public static Texture2D SoftParticleTexture()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "柔和光尘";
            Color[] pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, a);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2D PaperChipTexture()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "不规则细纸屑";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float u = (x - 15.5f) / 15.5f, v = (y - 15.5f) / 15.5f;
                float edge = Mathf.Max(Mathf.Abs(u) * 1.8f, Mathf.Abs(v) * 1.06f);
                float alpha = Mathf.Clamp01((1f - edge - Noise(x / 3,y / 3,47) * .14f) * 12f);
                float fibre = .93f + Noise(x,y,21) * .07f;
                pixels[y * 32 + x] = new Color(fibre,fibre,fibre,alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false,true);
            return texture;
        }
    }
}
