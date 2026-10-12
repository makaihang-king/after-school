using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    /// <summary>纯程序构建的微缩街区；装饰物不会拦截游戏的地面射线。</summary>
    internal sealed class NeighborhoodBuilder
    {
        readonly Transform root;
        readonly WorldPalette p;
        readonly Font font;
        readonly Func<UnityEngine.Object, UnityEngine.Object> own;
        readonly System.Random random = new System.Random(1998);
        Material[] flowerColors;
        Material signMaterial;
        Mesh leafClusterMesh;
        ArenaTerrain arenaTerrain;
        Transform arenaGeometry;
        PhysicsMaterial arenaPhysics;

        public NeighborhoodBuilder(Transform root, WorldPalette palette, Font font, Func<UnityEngine.Object, UnityEngine.Object> own)
        {
            this.root = root;
            p = palette;
            this.font = font;
            this.own = own;
            flowerColors = new[] { p.White, p.Red, p.Yellow };
            Shader signShader = Resources.Load<Shader>("Shaders/WorldSignText");
            if (signShader != null && font != null)
            {
                signMaterial = (Material)own(new Material(signShader));
                signMaterial.name = "墙牌文字 · 正常深度遮挡";
                root.gameObject.AddComponent<WorldSignFontAtlas>().Configure(font, signMaterial);
            }
        }

        public Collider Build()
        {
            var floor = Box("整片街道 · 可交互地面", new Vector3(0f, -0.16f, 3f), new Vector3(27f, 0.32f, 29f), p.Ground);
            var collider = floor.AddComponent<BoxCollider>();
            var physics = new PhysicsMaterial("街道摩擦");
            physics.dynamicFriction = p.Friction;
            physics.staticFriction = p.Friction + 0.08f;
            physics.frictionCombine = PhysicsMaterialCombine.Average;
            collider.sharedMaterial = (PhysicsMaterial)own(physics);
            collider.contactOffset = .002f;
            Sidewalks();
            if (p.Courtyard) Courtyard();
            else if (p.OldTree) Garden();
            else if (p.Alley) AlleyCorner();
            else School();
            Shop();
            AlleyHouses();
            Tree(new Vector3(-5.45f, 0.08f, 2.05f), p.OldTree ? 1.18f : 1f);
            Tree(new Vector3(8.9f, 0.08f, 8.3f), 0.83f);
            StreetFurniture();
            BuildArenaTerrain(collider);
            ChalkArena();
            GroundDetails();
            StreetLife();
            OverheadWires();
            DistantTown();
            return collider;
        }

        void BuildArenaTerrain(Collider floor)
        {
            var geometry = Group("打片 · 实体地形与路沿边界", Vector3.zero);
            arenaGeometry = geometry;
            arenaTerrain = root.gameObject.AddComponent<ArenaTerrain>();
            arenaTerrain.Initialize(floor, geometry);
            arenaTerrain.Description = p.OldTree ? "花纹石砖、树根缓坡和后沿三级台阶" : p.Alley ? "高低红砖、斜砖、铸铁井盖和左侧台阶" :
                p.Courtyard ? "旧石砖、花纹砖、小缓坡和院边台阶" : "凸起井盖、水泥砖和右侧马路牙子台阶";
            arenaPhysics = floor.sharedMaterial;
            ArenaBorders(geometry);

            // 每处都有足够宽的落片面；发射带保持平坦，小凸纹仅作为贴在砖面上的图案。
            if (p.OldTree)
            {
                // 铺地仅比整片地面高 .5 毫米，碰撞沿用基础 Floor，避免铺砖前缘挡住斜擦。
                Box("树下花纹砖铺地 · 齐平", new Vector3(0f, -.0395f, 1.125f), new Vector3(6.9f, .08f, 4.35f), p.Paving, geometry);
                PatternedSlab("树根旁的花纹方砖", new Vector3(-.83f, 0f, 1.2f), 1.06f, .94f, .068f, p.Paving, geometry);
                PatternedSlab("青灰花纹方砖", new Vector3(.83f, 0f, .25f), 1.10f, 1.02f, .042f, p.Paving, geometry);
                ArenaSlab("被树根顶起的斜砖", new Vector3(-1.86f, 0f, .2f), 1.08f, 1.06f, .074f, .029f, -.019f, .055f, p.Stone, geometry);
                ArenaSlab("树根隆起的宽缓砖坡", new Vector3(1.13f, 0f, 1.74f), 1.30f, 1.12f, .12f, -.032f, .017f, .14f, p.Paving, geometry);
                ArenaSteps(geometry, false, false, 2.75f);
                ArenaManhole(new Vector3(-2.52f, 0f, 2.02f), .51f, .044f, geometry);
            }
            else if (p.Alley)
            {
                Box("胡同红砖铺地 · 齐平", new Vector3(0f, -.0395f, 1.1f), new Vector3(6.9f, .08f, 4.3f), p.Brick, geometry);
                ArenaSlab("磨亮的旧砖平台", new Vector3(-.83f, 0f, 1.2f), 1.06f, .98f, .052f, 0f, 0f, .035f, p.TileRed, geometry);
                ArenaSlab("翘起一层的红砖", new Vector3(.83f, 0f, .25f), 1.10f, 1.02f, .085f, 0f, 0f, .035f, p.TileRed, geometry);
                ArenaSlab("胡同起伏砖 · 左坡", new Vector3(-1.94f, 0f, .18f), 1.10f, 1.06f, .072f, .026f, -.023f, .065f, p.Brick, geometry);
                ArenaSlab("胡同起伏砖 · 后坡", new Vector3(1.45f, 0f, 1.56f), 1.16f, 1.12f, .11f, -.036f, .022f, .07f, p.TileRed, geometry);
                ArenaSteps(geometry, true, true, -2.83f);
                ArenaManhole(new Vector3(2.56f, 0f, 2.06f), .55f, .056f, geometry);
            }
            else if (p.Courtyard)
            {
                Box("小院旧石砖铺地 · 齐平", new Vector3(0f, -.0395f, 1.12f), new Vector3(6.2f, .08f, 4.3f), p.Paving, geometry);
                PatternedSlab("小院花纹砖", new Vector3(-.83f, 0f, 1.2f), 1.08f, 1.04f, .054f, p.Cream, geometry);
                ArenaSlab("院里高一层的石砖", new Vector3(.83f, 0f, .25f), 1.10f, 1.02f, .047f, 0f, 0f, .04f, p.Stone, geometry);
                ArenaSlab("小院晒暖的起伏砖", new Vector3(.85f, 0f, 1.58f), 1.2f, 1.08f, .102f, .04f, -.016f, .09f, p.Paving, geometry);
                ArenaSteps(geometry, false, false, 2.81f);
                ArenaManhole(new Vector3(-2.27f, 0f, .44f), .51f, .047f, geometry);
            }
            else
            {
                ArenaManhole(new Vector3(-.83f, 0f, 1.2f), .68f, .053f, geometry);
                ArenaSlab("校门口的方形水泥砖", new Vector3(.83f, 0f, .25f), 1.10f, 1.02f, .045f, 0f, 0f, .035f, p.Stone, geometry);
                ArenaSlab("路边有起伏的旧石砖", new Vector3(1.91f, 0f, 1.38f), 1.14f, 1.10f, .091f, -.026f, .026f, .075f, p.Paving, geometry);
                ArenaSteps(geometry, true, false, 2.82f);
            }
            // 初始化时立即同步，粉笔线可直接采样刚创建的实体高程。
            Physics.SyncTransforms();
        }

        GameObject ArenaBlock(string name, Vector3 center, Vector3 size, Material material, Transform parent, bool boundary = false)
        {
            var block = Box(name, center, size, material, parent);
            var collider = block.AddComponent<BoxCollider>();
            collider.sharedMaterial = arenaPhysics;
            collider.contactOffset = .002f;
            if (boundary) arenaTerrain.RegisterBoundary(collider);
            else arenaTerrain.RegisterSurface(collider);
            return block;
        }

        void ArenaBorders(Transform parent)
        {
            const float width = .26f;
            float depth = ArenaTerrain.MaxZ - ArenaTerrain.MinZ + width * 2f;
            float centerZ = (ArenaTerrain.MinZ + ArenaTerrain.MaxZ) * .5f;
            // 侧、后方实心路沿高 .36；前方较低，避免近处路沿挡住出手视线。
            ArenaBlock("左侧实体围场路沿", new Vector3(ArenaTerrain.MinX - width * .5f, .14f, centerZ), new Vector3(width, .44f, depth), p.Stone, parent, true);
            ArenaBlock("右侧实体围场路沿", new Vector3(ArenaTerrain.MaxX + width * .5f, .14f, centerZ), new Vector3(width, .44f, depth), p.Stone, parent, true);
            ArenaBlock("后方实体围场路沿", new Vector3(0f, .14f, ArenaTerrain.MaxZ + width * .5f), new Vector3(7.3f, .44f, width), p.Stone, parent, true);
            ArenaBlock("前方低路沿 · 发射区边界", new Vector3(0f, .07f, ArenaTerrain.MinZ - width * .5f), new Vector3(7.3f, .30f, width), p.Stone, parent, true);
            for (int i = 0; i < 11; i++)
            {
                float x = -3.35f + i * .67f;
                Box("前路沿刷白分段", new Vector3(x, .221f, ArenaTerrain.MinZ - width * .5f), new Vector3(.49f, .002f, .21f), i % 2 == 0 ? p.Cream : p.Stone, parent);
                Box("后路沿刷白分段", new Vector3(x, .361f, ArenaTerrain.MaxZ + width * .5f), new Vector3(.49f, .002f, .21f), i % 2 == 0 ? p.Cream : p.Stone, parent);
            }
            for (int i = 0; i < 10; i++)
            {
                float z = -2.9f + i * .66f;
                foreach (int side in new[] { -1, 1 })
                    Box("侧路沿刷白分段", new Vector3(side * 3.78f, .361f, z), new Vector3(.21f, .002f, .48f), i % 2 == 0 ? p.Cream : p.Stone, parent);
            }
        }

        void ArenaSteps(Transform parent, bool side, bool left, float offset)
        {
            // 每一级都用与可见石块一致的 BoxCollider，粉笔圈外留有约 .5 米缓冲带。
            float direction = left ? -1f : 1f;
            for (int i = 0; i < 3; i++)
            {
                float height = .07f + i * .07f;
                float across = offset + direction * (i - 1) * .29f;
                float acrossSize = .30f;
                if (i == 2)
                {
                    float nearEdge = across - direction * .15f;
                    float farEdge = side ? (left ? ArenaTerrain.MinX : ArenaTerrain.MaxX) : ArenaTerrain.MaxZ;
                    across = (nearEdge + farEdge) * .5f;
                    acrossSize = Mathf.Abs(farEdge - nearEdge);
                }
                Vector3 size = side ? new Vector3(acrossSize, height + .06f, 3.54f) : new Vector3(3.64f, height + .06f, acrossSize);
                Vector3 position = side ? new Vector3(across, (height - .06f) * .5f, .75f) : new Vector3(0f, (height - .06f) * .5f, across);
                ArenaBlock("可打片的马路牙子台阶 · 第 " + (i + 1) + " 级", position, size, i % 2 == 0 ? p.Stone : p.Paving, parent);
                for (int seam = 0; seam < 6; seam++)
                {
                    float along = -1.50f + seam * .58f;
                    Vector3 a = side ? new Vector3(across - .13f, height + .0008f, .75f + along) : new Vector3(along, height + .0008f, across - .13f);
                    Vector3 b = side ? a + Vector3.right * .26f : a + Vector3.forward * .26f;
                    ArenaPaintLine("台阶石缝", a, b, .007f, p.DarkRoof, parent);
                }
            }
        }

        GameObject ArenaSlab(string name, Vector3 center, float width, float depth, float height,
            float slopeX, float slopeZ, float bevel, Material material, Transform parent)
        {
            float x = width * .5f, z = depth * .5f;
            var corners = new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) };
            var vertices = new Vector3[12];
            var uv = new Vector2[12];
            var triangles = new List<int>(60);
            for (int i = 0; i < 4; i++)
            {
                float tx = corners[i].x * (x - bevel), tz = corners[i].y * (z - bevel);
                float ox = corners[i].x * x, oz = corners[i].y * z;
                vertices[i] = new Vector3(tx, height + tx * slopeX + tz * slopeZ, tz);
                vertices[i + 4] = new Vector3(ox, Mathf.Max(.004f, height - .02f + ox * slopeX + oz * slopeZ), oz);
                vertices[i + 8] = new Vector3(ox, -.06f, oz);
                uv[i] = new Vector2(tx / width + .5f, tz / depth + .5f);
                uv[i + 4] = uv[i + 8] = new Vector2(ox / width + .5f, oz / depth + .5f);
                int j = (i + 1) % 4;
                triangles.AddRange(new[] { i, i + 4, j + 4, i, j + 4, j, i + 4, i + 8, j + 8, i + 4, j + 8, j + 4 });
            }
            triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3, 8, 10, 9, 8, 11, 10 });
            var mesh = new Mesh { name = name + " · 顶面与碰撞共用" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var slab = MeshObject(name, mesh, material, parent);
            slab.transform.localPosition = center;
            var collider = slab.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = arenaPhysics;
            collider.contactOffset = .002f;
            arenaTerrain.RegisterSurface(collider);
            return slab;
        }

        void PatternedSlab(string name, Vector3 center, float width, float depth, float height, Material material, Transform parent)
        {
            var slab = ArenaSlab(name, center, width, depth, height, 0f, 0f, .035f, material, parent);
            Transform tile = slab.transform;
            // 菱形花瓣纹样嵌在平整砖顶；图案没有突出的碰撞体。
            ArenaPaintCircle("花纹砖内圈", new Vector3(0f, height + .0008f, 0f), .115f, .008f, p.Cream, tile);
            for (int petal = 0; petal < 4; petal++)
            {
                float a = petal * Mathf.PI * .5f;
                Vector3 forward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 lateral = new Vector3(-forward.z, 0f, forward.x);
                Vector3 origin = new Vector3(0f, height + .0008f, 0f);
                Vector3[] points = { origin + forward * .13f, origin + forward * .245f + lateral * .075f,
                    origin + forward * .36f, origin + forward * .245f - lateral * .075f };
                for (int i = 0; i < 4; i++) ArenaPaintLine("花纹砖菱形花瓣", points[i], points[(i + 1) % 4], .011f, p.DarkRoof, tile);
            }
            float bx = width * .5f - .10f, bz = depth * .5f - .10f;
            Vector3[] border = { new Vector3(-bx,height+.0008f,-bz),new Vector3(-bx,height+.0008f,bz),
                new Vector3(bx,height+.0008f,bz),new Vector3(bx,height+.0008f,-bz) };
            for (int i = 0; i < 4; i++) ArenaPaintLine("花纹砖双线边框", border[i], border[(i + 1) % 4], .008f, p.Cream, tile);
        }

        void ArenaManhole(Vector3 center, float radius, float height, Transform parent)
        {
            var ring = Cylinder("可碰撞的井盖铸铁外圈", center + Vector3.up * (height - .014f) * .5f, radius + .055f, height - .014f, p.DarkRoof, parent);
            var cap = Cylinder("可碰撞的凸起井盖", center + Vector3.up * height * .5f, radius, height, p.Iron, parent);
            var ironGrip = new PhysicsMaterial("井盖铸铁摩擦") { dynamicFriction = .32f, staticFriction = .41f,
                bounciness = .04f, frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            own(ironGrip);
            foreach (GameObject obj in new[] { ring, cap })
            {
                var collider = obj.AddComponent<MeshCollider>();
                collider.sharedMesh = obj.GetComponent<MeshFilter>().sharedMesh;
                collider.sharedMaterial = ironGrip;
                collider.contactOffset = .002f;
                arenaTerrain.RegisterSurface(collider);
            }
            Vector3 top = center + Vector3.up * (height + .0008f);
            ArenaPaintCircle("井盖嵌入式外圈", top, radius * .86f, .011f, p.Stone, parent);
            for (int i = -3; i <= 3; i++)
            {
                float offset = i * radius * .21f;
                float half = Mathf.Sqrt(radius * radius * .65f - offset * offset);
                ArenaPaintLine("井盖交叉铸铁纹样", top + new Vector3(-half, 0f, offset), top + new Vector3(half, 0f, offset), .009f, p.Stone, parent);
                ArenaPaintLine("井盖交叉铸铁纹样", top + new Vector3(offset, 0f, -half), top + new Vector3(offset, 0f, half), .009f, p.Stone, parent);
            }
        }

        void ArenaPaintLine(string name, Vector3 a, Vector3 b, float width, Material material, Transform parent)
        {
            Vector3 offset = Vector3.Cross((b - a).normalized, Vector3.up) * width * .5f;
            var mesh = new Mesh { name = "嵌入表面的线条" };
            mesh.vertices = new[] { a - offset, a + offset, b - offset, b + offset };
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateNormals();
            var obj = MeshObject(name, mesh, material, parent);
            obj.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void ArenaPaintCircle(string name, Vector3 center, float radius, float width, Material material, Transform parent)
        {
            const int segments = 48;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = center + direction * (radius - width * .5f);
                vertices[i * 2 + 1] = center + direction * (radius + width * .5f);
                int next = ((i + 1) % segments) * 2, t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = next; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = next; triangles[t + 5] = next + 1;
            }
            var mesh = new Mesh { name = "嵌入表面的环纹" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
            var obj = MeshObject(name, mesh, material, parent);
            obj.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Garden()
        {
            LowWall(new Vector3(-1.2f, 0.07f, 5.9f), 12.3f);
            Text("槐树旁的墙绘", "老 槐 树 下 · 放 学 就 来", new Vector3(-0.4f, 1.10f, 5.682f), 0.34f, new Color(0.98f, 0.89f, 0.67f));
            Bench(new Vector3(-0.8f, 0.08f, 4.62f), 0f);
            FlowerPot(new Vector3(1.3f, 0.08f, 4.86f), 0.92f);
            Tree(new Vector3(-1.7f, 0.08f, 8.25f), 1.19f);
            Tree(new Vector3(2.2f, 0.08f, 9.3f), 0.98f);
            Box("花坛", new Vector3(-0.4f, 0.23f, 7.17f), new Vector3(11.3f, 0.46f, 1.5f), p.Brick);
            Box("花坛土", new Vector3(-0.4f, 0.47f, 7.17f), new Vector3(11.08f, 0.035f, 1.28f), p.Soil);
            for (int i = 0; i < 20; i++)
                Ball("花坛绿植", new Vector3(-5.4f + i * 0.53f, 0.69f + Range(-0.1f, 0.1f), 7.17f + Range(-0.38f, 0.38f)), new Vector3(0.75f, 0.46f, 0.62f), i % 2 == 0 ? p.Canopy : p.CanopyLight).isStatic=false;
        }

        void AlleyCorner()
        {
            var wall = Group("通往小卖部的巷口", new Vector3(-1.3f, 0.07f, 7.5f));
            Box("巷口砖墙", new Vector3(0f, 1.05f, 0f), new Vector3(12.4f, 2.1f, 0.48f), p.Brick, wall);
            Box("巷口墙帽", new Vector3(0f, 2.15f, 0f), new Vector3(12.55f, 0.15f, 0.59f), p.Stone, wall);
            Box("旧墙广告底板", new Vector3(0.1f, 1.26f, -0.265f), new Vector3(4.8f, 1.12f, 0.024f), p.Cream, wall);
            Text("旧墙广告", "冰镇汽水 · 五毛雪糕\n阳光小卖部 →", new Vector3(0.1f, 1.26f, -0.287f), 0.34f, new Color(0.66f, 0.25f, 0.17f), wall);
            Tree(new Vector3(-2.3f, 0.07f, 9.1f), 0.88f);
            Tree(new Vector3(1.4f, 0.07f, 10.2f), 0.92f);
            Crate(new Vector3(2.2f, 0.29f, 6.05f), root, true);
            Crate(new Vector3(2.22f, 0.7f, 6.05f), root, false);
        }

        float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }

        Transform Group(string name, Vector3 position, float rotation = 0f, Transform parent = null)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent == null ? root : parent, false);
            group.localPosition = position;
            group.localRotation = Quaternion.Euler(0f, rotation, 0f);
            return group;
        }

        GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent == null ? root : parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            obj.isStatic = true;
            return obj;
        }

        GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            return Primitive(PrimitiveType.Cube, name, position, scale, material, parent);
        }

        GameObject Ball(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            return Primitive(PrimitiveType.Sphere, name, position, scale, material, parent);
        }

        GameObject Cylinder(string name, Vector3 position, float radius, float height, Material material, Transform parent = null)
        {
            return Primitive(PrimitiveType.Cylinder, name, position, new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, parent);
        }

        GameObject Rod(string name, Vector3 start, Vector3 end, float radius, Material material, Transform parent = null)
        {
            var rod = Cylinder(name, (start + end) * 0.5f, radius, Vector3.Distance(start, end), material, parent);
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
            return rod;
        }

        GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent = null, bool ownMesh = true)
        {
            if (ownMesh) own(mesh);
            var obj = new GameObject(name);
            obj.transform.SetParent(parent == null ? root : parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            obj.isStatic = true;
            return obj;
        }

        TextMesh Text(string name, string text, Vector3 position, float size, Color color, Transform parent = null, Quaternion? rotation = null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent == null ? root : parent, false);
            obj.transform.localPosition = position;
            obj.transform.localRotation = rotation ?? Quaternion.identity;
            var mesh = obj.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 64;
            if (font != null && font.dynamic) font.RequestCharactersInTexture(text,64,FontStyle.Normal);
            mesh.characterSize = size * 0.16f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.text = text;
            mesh.richText = false;
            obj.GetComponent<MeshRenderer>().sharedMaterial = signMaterial == null ? font.material : signMaterial;
            obj.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            obj.isStatic = true;
            return mesh;
        }

        void Sidewalks()
        {
            Box("左侧石砖人行道", new Vector3(-7.8f, 0.01f, 2f), new Vector3(7.5f, 0.12f, 19f), p.Paving);
            Box("右侧石砖人行道", new Vector3(7.9f, 0.01f, 2f), new Vector3(7.5f, 0.12f, 19f), p.Paving);
            Box("学校门前石砖", new Vector3(-0.3f, 0.01f, 5.4f), new Vector3(8.4f, 0.12f, 2.4f), p.Paving);
            for (int i = 0; i < 22; i++)
            {
                float z = -7.2f + i * 0.81f;
                Box("左路缘石", new Vector3(-4.01f, 0.078f, z), new Vector3(0.18f, 0.18f, 0.79f), i % 6 == 0 ? p.Cream : p.Stone);
                Box("右路缘石", new Vector3(4.08f, 0.078f, z), new Vector3(0.18f, 0.18f, 0.79f), i % 6 == 0 ? p.Cream : p.Stone);
            }
            // 两道排水沟让道路边缘有明确的体积和层次。
            Box("左排水沟", new Vector3(-3.86f, 0.005f, 1f), new Vector3(0.09f, 0.012f, 16f), p.DarkRoof);
            Box("右排水沟", new Vector3(3.93f, 0.005f, 1f), new Vector3(0.09f, 0.012f, 16f), p.DarkRoof);
            for (int s = 0; s < 3; s++)
            {
                var drain = Group("铸铁雨水篦子", new Vector3(s == 1 ? 3.75f : -3.72f, 0.021f, -3f + s * 4.1f));
                Box("凹槽", Vector3.zero, new Vector3(0.39f, 0.03f, 0.6f), p.DarkRoof, drain);
                for (int i = 0; i < 6; i++) Box("篦条", new Vector3(0f, 0.025f, -0.25f + i * 0.1f), new Vector3(0.37f, 0.015f, 0.035f), p.Iron, drain);
            }
        }

        void School()
        {
            Transform building = Group("向阳小学 · 老教学楼", new Vector3(-1.0f, 0.07f, 8.9f));
            Box("三层奶油色楼体", new Vector3(0f, 2.9f, 0f), new Vector3(12.6f, 5.8f, 4.5f), p.Plaster, building);
            Box("砖墙裙", new Vector3(0f, 0.44f, -2.29f), new Vector3(12.67f, 0.88f, 0.14f), p.Brick, building);
            for (int floor = 0; floor < 3; floor++)
            {
                float y = 1.15f + floor * 1.65f;
                Box("水泥腰线", new Vector3(0f, y - 0.43f, -2.30f), new Vector3(12.75f, 0.11f, 0.17f), p.Cream, building);
                for (int col = 0; col < 9; col++) Window(new Vector3(-5.4f + col * 1.36f, y + 0.35f, -2.31f), 0.93f, 1.0f, building, col % 4 == 1);
            }
            Box("校名底板", new Vector3(0f, 5.47f, -2.42f), new Vector3(3.7f, 0.67f, 0.14f), p.Cream, building);
            Text("校名", "向 阳 小 学", new Vector3(0f, 5.47f, -2.51f), 0.47f, new Color(0.65f, 0.21f, 0.12f), building);
            Roof(building, 12.95f, 4.85f, 5.84f, 0.96f);
            for (int i = -5; i < 6; i++)
                Rod("屋顶瓦垄", new Vector3(i * 1.14f, 5.91f, -2.46f), new Vector3(i * 1.14f, 6.83f, 0f), 0.024f, p.Roof, building);

            GatePillar(new Vector3(-3.45f, 0.07f, 4.8f), "向阳小学");
            GatePillar(new Vector3(0.92f, 0.07f, 4.8f), "好好学习");
            LowWall(new Vector3(-6.2f, 0.07f, 5.05f), 4.85f);
            LowWall(new Vector3(2.6f, 0.07f, 5.05f), 2.8f);
            IronGate(new Vector3(-2.36f, 0.07f, 4.91f), 1.52f, -19f);
            IronGate(new Vector3(-0.22f, 0.07f, 4.91f), 1.52f, 14f);
            var board = Group("校门宣传栏", new Vector3(-4.83f, 0.07f, 4.83f));
            Box("木边框", new Vector3(0f, 1.45f, 0f), new Vector3(1.76f, 1.25f, 0.11f), p.Wood, board);
            Box("绿板", new Vector3(0f, 1.45f, -0.063f), new Vector3(1.57f, 1.08f, 0.02f), p.LeafDark, board);
            Text("黑板报", "快乐放学\n文明游戏", new Vector3(0f, 1.5f, -0.08f), 0.22f, new Color(0.95f, 0.90f, 0.69f), board);
            for (int i = 0; i < 2; i++)
                Rod("宣传栏支脚", new Vector3(-0.65f + i * 1.3f, 0f, 0f), new Vector3(-0.65f + i * 1.3f, 0.9f, 0f), 0.035f, p.Iron, board);

            var flag = Group("学校旗杆", new Vector3(-4.3f, 0.07f, 7.0f));
            Cylinder("旗杆基座", new Vector3(0f, 0.07f, 0f), 0.32f, 0.14f, p.Stone, flag);
            Cylinder("旗杆", new Vector3(0f, 3.6f, 0f), 0.032f, 7.2f, p.Iron, flag);
            Ball("旗杆顶", new Vector3(0f, 7.24f, 0f), Vector3.one * 0.095f, p.Yellow, flag);
            var cloth = Box("红旗", new Vector3(0.60f, 6.57f, 0f), new Vector3(1.2f, 0.65f, 0.015f), p.Red, flag);
            cloth.isStatic = false;
            cloth.AddComponent<WorldWindSway>().Configure(2f, 0.8f, new Vector3(0f, 1f, 0f));
            var star = Text("旗上的星星", "★", new Vector3(-0.25f, 0.20f, -0.85f), 0.19f, new Color(1f, 0.87f, 0.35f), cloth.transform);
            // 红旗有缩放，文字用真实尺寸抵消父级缩放，随旗一起轻摆。
            star.transform.localScale = new Vector3(1f / 1.2f, 1f / .65f, 1f / .015f);
            star.gameObject.isStatic = false;
        }

        void Courtyard()
        {
            var house = Group("槐树大院 · 旧平房", new Vector3(-1f, 0.07f, 7.3f));
            Box("平房墙体", new Vector3(0f, 1.65f, 0f), new Vector3(11.8f, 3.3f, 4.5f), p.Plaster, house);
            Box("墙裙", new Vector3(0f, 0.48f, -2.3f), new Vector3(11.87f, 0.96f, 0.13f), p.Brick, house);
            Roof(house, 12.2f, 4.9f, 3.34f, 1.15f);
            for (int i = -2; i <= 2; i++) Window(new Vector3(i * 2.3f, 1.85f, -2.34f), 1.3f, 1.32f, house, true);
            Box("木门", new Vector3(0f, 1.10f, -2.4f), new Vector3(1.25f, 2.2f, 0.18f), p.Wood, house);
            Box("门框", new Vector3(0f, 2.24f, -2.46f), new Vector3(1.52f, 0.18f, 0.2f), p.DarkRoof, house);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("春联", new Vector3(side * 0.82f, 1.17f, -2.5f), new Vector3(0.19f, 1.98f, 0.016f), p.Red, house);
                Text("春联文字", side < 0 ? "平\n安\n如\n意" : "快\n乐\n童\n年", new Vector3(side * 0.82f, 1.2f, -2.518f), 0.15f, new Color(1f, 0.87f, 0.36f), house);
            }
            Box("院子门牌", new Vector3(1.1f, 2.38f, -2.43f), new Vector3(0.66f, 0.40f, 0.04f), p.Blue, house);
            Text("门牌", "槐荫里\n12 号", new Vector3(1.1f, 2.38f, -2.46f), 0.12f, Color.white, house);
            Box("外婆家的木牌", new Vector3(0f, 2.83f, -2.36f), new Vector3(2.12f, 0.42f, 0.07f), p.Wood, house);
            Text("外婆家木牌文字", "外 婆 家 的 小 院", new Vector3(0f, 2.83f, -2.407f), 0.23f, new Color(0.98f, 0.91f, 0.73f), house);
            LowWall(new Vector3(-5.7f, 0.07f, 4.7f), 3.9f);
            LowWall(new Vector3(2.7f, 0.07f, 4.7f), 2.6f);
            Text("院子涂鸦", "放学见！", new Vector3(2.6f, 0.87f, 4.43f), 0.26f, new Color(0.97f, 0.87f, 0.67f));
        }

        void GatePillar(Vector3 position, string inscription)
        {
            var pillar = Group("校门砖柱", position);
            Box("柱脚", new Vector3(0f, 0.12f, 0f), new Vector3(0.76f, 0.24f, 0.78f), p.Stone, pillar);
            Box("红砖柱", new Vector3(0f, 1.2f, 0f), new Vector3(0.56f, 2.4f, 0.58f), p.Brick, pillar);
            Box("柱帽", new Vector3(0f, 2.46f, 0f), new Vector3(0.75f, 0.18f, 0.78f), p.Cream, pillar);
            Ball("柱顶球", new Vector3(0f, 2.65f, 0f), new Vector3(0.27f, 0.29f, 0.27f), p.Cream, pillar);
            Box("校牌", new Vector3(0f, 1.45f, -0.313f), new Vector3(0.33f, 1.66f, 0.047f), p.Cream, pillar);
            string vertical = string.Join("\n", Array.ConvertAll(inscription.ToCharArray(), c => c.ToString()));
            Text("竖排校牌", vertical, new Vector3(0f, 1.46f, -0.346f), 0.27f, new Color(0.25f, 0.28f, 0.24f), pillar);
        }

        void LowWall(Vector3 position, float width)
        {
            var wall = Group("老红砖围墙", position);
            Box("红砖墙", new Vector3(0f, 0.63f, 0f), new Vector3(width, 1.26f, 0.40f), p.Brick, wall);
            Box("墙帽", new Vector3(0f, 1.31f, 0f), new Vector3(width + 0.09f, 0.12f, 0.48f), p.Stone, wall);
            for (int i = 0; i < Mathf.RoundToInt(width / 0.26f); i++)
            {
                float x = -width * 0.5f + 0.13f + i * 0.26f;
                Rod("围墙铁栏", new Vector3(x, 1.36f, 0f), new Vector3(x, 1.9f, 0f), 0.016f, p.Iron, wall);
                Ball("栏杆尖", new Vector3(x, 1.94f, 0f), new Vector3(0.056f, 0.10f, 0.056f), p.Iron, wall);
            }
            Rod("栏杆横梁", new Vector3(-width * 0.5f, 1.69f, 0f), new Vector3(width * 0.5f, 1.69f, 0f), 0.025f, p.Iron, wall);
            for (int i = 0; i < 4; i++) GrassTuft(new Vector3(-width * 0.48f + i * width * 0.28f, 0.015f, -0.25f), wall);
        }

        void IronGate(Vector3 position, float width, float angle)
        {
            var gate = Group("半开的蓝铁校门", position, angle);
            Box("门下部钢板", new Vector3(0f, 0.32f, 0f), new Vector3(width, 0.52f, 0.07f), p.Iron, gate);
            for (int row = 0; row < 3; row++)
                Rod("铁门横梁", new Vector3(-width * 0.5f, 0.62f + row * 0.58f, 0f), new Vector3(width * 0.5f, 0.62f + row * 0.58f, 0f), 0.028f, p.Iron, gate);
            for (int i = 0; i < 8; i++)
            {
                float x = -width * 0.45f + i * width * 0.90f / 7f;
                Rod("铁门竖条", new Vector3(x, 0.57f, 0f), new Vector3(x, 1.88f, 0f), 0.018f, p.Iron, gate);
                Ball("铁门矛头", new Vector3(x, 1.94f, 0f), new Vector3(0.061f, 0.15f, 0.061f), p.Iron, gate);
            }
            Box("门把手", new Vector3(width * 0.37f, 0.91f, -0.08f), new Vector3(0.05f, 0.21f, 0.08f), p.Yellow, gate);
        }

        void Window(Vector3 position, float width, float height, Transform parent, bool open)
        {
            var window = Group("带窗台的旧窗", position, 0f, parent);
            Box("窗框外沿", Vector3.zero, new Vector3(width + 0.13f, height + 0.14f, 0.13f), p.Cream, window);
            Box("玻璃", new Vector3(0f, 0f, -0.078f), new Vector3(width, height, 0.025f), p.Glass, window);
            Box("中间竖窗棂", new Vector3(0f, 0f, -0.10f), new Vector3(0.048f, height + 0.035f, 0.036f), p.Iron, window);
            Box("中间横窗棂", new Vector3(0f, 0.05f, -0.10f), new Vector3(width, 0.044f, 0.036f), p.Iron, window);
            Box("厚窗台", new Vector3(0f, -height * 0.5f - 0.09f, -0.06f), new Vector3(width + 0.28f, 0.11f, 0.27f), p.Stone, window);
            var reflection = Box("玻璃映出的天光", new Vector3(-width * 0.21f, height * 0.12f, -0.096f), new Vector3(width * 0.32f, height * 0.61f, 0.008f), p.Blue, window);
            if (open)
            {
                var shutter = Group("半开木窗", new Vector3(width * 0.51f, 0f, -0.12f), -38f, window);
                Box("木窗扇", new Vector3(width * 0.24f, 0f, 0f), new Vector3(width * 0.46f, height, 0.055f), p.Mint, shutter);
                for (int i = 0; i < 5; i++) Box("百叶条", new Vector3(width * 0.24f, -height * 0.36f + i * height * 0.17f, -0.032f), new Vector3(width * 0.35f, 0.025f, 0.01f), p.LeafDark, shutter);
            }
        }

        void Roof(Transform parent, float width, float depth, float bottom, float rise)
        {
            Mesh mesh = new Mesh();
            float w = width * 0.5f, d = depth * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-w,bottom,-d), new Vector3(w,bottom,-d), new Vector3(w,bottom+rise,0f), new Vector3(-w,bottom+rise,0f),
                new Vector3(-w,bottom+rise,0f), new Vector3(w,bottom+rise,0f), new Vector3(w,bottom,d), new Vector3(-w,bottom,d),
                new Vector3(-w,bottom,-d), new Vector3(-w,bottom+rise,0f), new Vector3(-w,bottom,d),
                new Vector3(w,bottom,-d), new Vector3(w,bottom,d), new Vector3(w,bottom+rise,0f)
            };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 7, 6, 4, 6, 5, 8, 10, 9, 11, 13, 12 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector2.zero, Vector2.one, Vector2.right, Vector2.zero, Vector2.right, Vector2.one };
            mesh.RecalculateNormals();
            MeshObject("坡屋顶", mesh, p.Roof, parent);
            Box("前屋檐", new Vector3(0f, bottom - 0.04f, -d), new Vector3(width + 0.09f, 0.16f, 0.18f), p.DarkRoof, parent);
            Rod("屋脊", new Vector3(-w, bottom + rise + 0.02f, 0f), new Vector3(w, bottom + rise + 0.02f, 0f), 0.08f, p.Roof, parent);
        }

        void Shop()
        {
            var shop = Group("阳光小卖部", new Vector3(6.15f, 0.07f, 4.1f), -5f);
            Box("小卖部楼体", new Vector3(0f, 1.73f, 0.3f), new Vector3(4.25f, 3.46f, 4f), p.Plaster, shop);
            Box("砖墙裙", new Vector3(0f, 0.47f, -1.74f), new Vector3(4.3f, 0.94f, 0.14f), p.Brick, shop);
            Box("门内暗处", new Vector3(-0.54f, 1.15f, -1.835f), new Vector3(1.24f, 2.3f, 0.09f), p.DarkRoof, shop);
            Box("右玻璃窗", new Vector3(0.88f, 1.54f, -1.841f), new Vector3(1.38f, 1.50f, 0.06f), p.Glass, shop);
            Box("左玻璃窗", new Vector3(-1.63f, 1.55f, -1.841f), new Vector3(0.48f, 1.50f, 0.06f), p.Glass, shop);
            for (int i = 0; i < 4; i++)
                Box("门窗竖框", new Vector3(-1.99f + i * 1.05f, 1.53f, -1.90f), new Vector3(0.074f, 2.43f, 0.065f), p.Mint, shop);
            Box("窗横框", new Vector3(0f, 1.48f, -1.905f), new Vector3(4.04f, 0.056f, 0.075f), p.Mint, shop);
            var door = Group("半开的木门", new Vector3(-1.13f, 0f, -1.98f), -31f, shop);
            Box("绿门", new Vector3(0.49f, 1.15f, 0f), new Vector3(0.97f, 2.3f, 0.08f), p.Mint, door);
            Box("门玻璃", new Vector3(0.49f, 1.61f, -0.048f), new Vector3(0.77f, 0.88f, 0.022f), p.Glass, door);
            Box("门把", new Vector3(0.79f, 1.0f, -0.09f), new Vector3(0.055f, 0.24f, 0.04f), p.Yellow, door);
            Box("招牌框", new Vector3(0f, 2.88f, -1.86f), new Vector3(4.43f, 0.77f, 0.2f), p.Wood, shop);
            Box("小卖部牌匾", new Vector3(0f, 2.88f, -1.975f), new Vector3(4.21f, 0.62f, 0.03f), p.Cream, shop);
            Text("店名", "阳 光 小 卖 部", new Vector3(0f, 2.88f, -2.002f), 0.46f, new Color(0.67f, 0.22f, 0.12f), shop);
            Roof(shop, 4.6f, 4.3f, 3.5f, 0.65f);
            Awning(shop);
            Freezer(new Vector3(0.90f, 0f, -2.49f), shop);
            Crate(new Vector3(1.72f, 0.2f, -2.14f), shop, true);
            Crate(new Vector3(1.76f, 0.60f, -2.08f), shop, false);
            var price = Group("雪糕价目牌", new Vector3(-1.9f, 0f, -2.72f), -4f, shop);
            Box("小黑板", new Vector3(0f, 0.70f, 0f), new Vector3(0.57f, 0.75f, 0.07f), p.Wood, price);
            Box("小黑板面", new Vector3(0f, 0.70f, -0.045f), new Vector3(0.47f, 0.65f, 0.014f), p.LeafDark, price);
            Text("价目表", "雪糕\n五毛一根\n汽水 1 元", new Vector3(0f, 0.72f, -0.059f), 0.12f, new Color(0.96f, 0.91f, 0.74f), price);
            Rod("黑板左脚", new Vector3(-0.19f, 0f, 0.2f), new Vector3(-0.19f, 0.6f, 0f), 0.023f, p.Wood, price);
            Rod("黑板右脚", new Vector3(0.19f, 0f, 0.2f), new Vector3(0.19f, 0.6f, 0f), 0.023f, p.Wood, price);
            Box("墙上价格海报", new Vector3(1.52f, 2.0f, -1.926f), new Vector3(0.37f, 0.40f, 0.008f), p.Paper, shop);
            Text("海报", "冰\n汽水", new Vector3(1.52f, 2.0f, -1.939f), 0.15f, new Color(0.8f, 0.19f, 0.12f), shop);
            FlowerPot(new Vector3(2.0f, 0f, -2.65f), 0.7f, shop);
        }

        void Awning(Transform shop)
        {
            var awning = Group("红白条纹遮阳棚", new Vector3(0f, 2.42f, -1.93f), 0f, shop);
            for (int i = 0; i < 14; i++)
            {
                float x = -2.04f + i * 0.312f;
                var stripe = Box("遮阳棚条纹", new Vector3(x, -0.1f, -0.62f), new Vector3(0.306f, 0.035f, 1.3f), i % 2 == 0 ? p.Red : p.Cream, awning);
                stripe.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
                Box("垂下的棚边", new Vector3(x, -0.25f, -1.23f), new Vector3(0.305f, 0.2f, 0.029f), i % 2 == 0 ? p.Red : p.Cream, awning);
            }
            Rod("棚边金属梁", new Vector3(-2.18f, -0.15f, -1.25f), new Vector3(2.18f, -0.15f, -1.25f), 0.02f, p.Iron, awning);
            for (int side = -1; side <= 1; side += 2)
                Rod("斜撑", new Vector3(side * 2.0f, -0.10f, -1.25f), new Vector3(side * 2.0f, -0.65f, 0f), 0.022f, p.Iron, awning);
        }

        void Freezer(Vector3 position, Transform parent)
        {
            var freezer = Group("老式卧式雪糕冰柜", position, 0f, parent);
            Box("冰柜主体", new Vector3(0f, 0.46f, 0f), new Vector3(1.50f, 0.77f, 0.78f), p.Cream, freezer);
            Box("红色柜腰", new Vector3(0f, 0.38f, -0.401f), new Vector3(1.46f, 0.36f, 0.027f), p.Red, freezer);
            Text("冰柜字样", "冰 凉 一 夏", new Vector3(0f, 0.40f, -0.425f), 0.22f, new Color(1f, 0.96f, 0.75f), freezer);
            Box("顶框", new Vector3(0f, 0.88f, 0f), new Vector3(1.58f, 0.11f, 0.86f), p.White, freezer);
            for (int i = 0; i < 2; i++)
            {
                Box("滑动玻璃盖", new Vector3(-0.37f + i * 0.74f, 0.946f, 0f), new Vector3(0.69f, 0.025f, 0.7f), p.Glass, freezer);
                Box("冰柜把手", new Vector3(-0.1f + i * 0.22f, 0.977f, -0.04f), new Vector3(0.06f, 0.035f, 0.2f), p.Cream, freezer);
            }
            for (int i = 0; i < 4; i++) Cylinder("冰柜脚", new Vector3((i % 2 == 0 ? -1f : 1f) * 0.58f, 0.075f, (i < 2 ? -1f : 1f) * 0.27f), 0.05f, 0.12f, p.Iron, freezer);
            for (int i = 0; i < 5; i++) Box("冰柜散热孔", new Vector3(0.751f, 0.22f + i * 0.056f, 0f), new Vector3(0.018f, 0.015f, 0.45f), p.Iron, freezer);
        }

        void Crate(Vector3 position, Transform parent, bool filled)
        {
            var crate = Group("汽水箱", position, 0f, parent);
            Box("箱底", new Vector3(0f, -0.16f, 0f), new Vector3(0.65f, 0.035f, 0.48f), p.Mint, crate);
            for (int i = 0; i < 3; i++)
            {
                float y = -0.10f + i * 0.105f;
                Box("汽水箱前格", new Vector3(0f, y, -0.25f), new Vector3(0.65f, 0.055f, 0.035f), p.Mint, crate);
                Box("汽水箱后格", new Vector3(0f, y, 0.25f), new Vector3(0.65f, 0.055f, 0.035f), p.Mint, crate);
                Box("汽水箱侧格", new Vector3(-0.31f, y, 0f), new Vector3(0.035f, 0.055f, 0.48f), p.Mint, crate);
                Box("汽水箱侧格", new Vector3(0.31f, y, 0f), new Vector3(0.035f, 0.055f, 0.48f), p.Mint, crate);
            }
            if (filled)
                for (int i = 0; i < 6; i++) Bottle(new Vector3(-0.19f + i % 3 * 0.19f, -0.13f, i < 3 ? -0.11f : 0.11f), crate);
        }

        void Bottle(Vector3 position, Transform parent)
        {
            Cylinder("绿色玻璃汽水瓶", position + new Vector3(0f, 0.12f, 0f), 0.041f, 0.24f, p.LeafDark, parent);
            Cylinder("汽水瓶颈", position + new Vector3(0f, 0.285f, 0f), 0.022f, 0.11f, p.LeafDark, parent);
            Cylinder("瓶盖", position + new Vector3(0f, 0.342f, 0f), 0.025f, 0.014f, p.Red, parent);
            Cylinder("汽水标签", position + new Vector3(0f, 0.13f, 0f), 0.042f, 0.069f, p.Paper, parent);
        }

        void AlleyHouses()
        {
            var house = Group("左侧巷子的居民楼", new Vector3(-9.7f, 0.07f, 2.3f), 14f);
            Box("居民楼墙", new Vector3(0f, 2.3f, 0f), new Vector3(4.9f, 4.6f, 6.8f), p.Plaster, house);
            Box("居民楼底部砖", new Vector3(0f, 0.64f, -3.45f), new Vector3(4.98f, 1.28f, 0.12f), p.Brick, house);
            Roof(house, 5.3f, 7.1f, 4.68f, 0.70f);
            for (int floor = 0; floor < 2; floor++)
                for (int i = 0; i < 3; i++) Window(new Vector3(-1.56f + i * 1.58f, 1.45f + floor * 2.0f, -3.46f), 0.98f, 1.15f, house, i == 2);
            Box("小区门", new Vector3(0f, 1.0f, -3.51f), new Vector3(0.93f, 2f, 0.09f), p.Blue, house);
            Box("门牌", new Vector3(0.89f, 2.1f, -3.53f), new Vector3(0.50f, 0.29f, 0.025f), p.Blue, house);
            Text("居民楼门牌", "槐荫里 8 号", new Vector3(0.89f, 2.1f, -3.554f), 0.093f, Color.white, house);
            // 侧墙位于街道边，留出足够宽的比赛视野。
            var sideWindow = Group("巷子侧窗", new Vector3(2.51f, 2.7f, -1.1f), -90f, house);
            Window(Vector3.zero, 1.15f, 1.18f, sideWindow, false);
            Rod("居民楼排水管", new Vector3(2.57f, 0.1f, -3.19f), new Vector3(2.57f, 4.6f, -3.19f), 0.044f, p.Iron, house);
            Box("空调室外机", new Vector3(2.74f, 3.2f, -0.2f), new Vector3(0.53f, 0.57f, 0.94f), p.Cream, house);
            for (int i = 0; i < 6; i++) Box("空调百叶", new Vector3(3.01f, 2.98f + i * 0.078f, -0.2f), new Vector3(0.016f, 0.017f, 0.78f), p.Iron, house);
            Clothesline();
            FlowerPot(new Vector3(-8.7f, 0.08f, -1.2f), 0.8f);
            FlowerPot(new Vector3(-8.0f, 0.08f, -1.0f), 0.58f);
        }

        void Clothesline()
        {
            var line = Group("巷口的晾衣绳", new Vector3(-6.6f, 0.07f, 5.15f), -7f);
            Rod("晾衣绳左杆", new Vector3(-1.5f, 0f, 0f), new Vector3(-1.5f, 3.15f, 0f), 0.036f, p.Iron, line);
            Rod("晾衣绳右杆", new Vector3(1.5f, 0f, 0f), new Vector3(1.5f, 3.15f, 0f), 0.036f, p.Iron, line);
            Cable("略微下垂的晾衣绳", new Vector3(-1.5f, 3.1f, 0f), new Vector3(1.5f, 3.1f, 0f), 0.13f, 0.008f, line);
            Material[] colors = { p.Cream, p.Blue, p.Red, p.Mint };
            for (int i = 0; i < 4; i++)
            {
                float x = -0.96f + i * 0.64f;
                var garment = Group("晾晒的衣服", new Vector3(x, 2.90f, 0f), Range(-8f, 8f), line);
                var cloth = Box("棉布衣服", new Vector3(0f, -0.34f, 0f), new Vector3(0.42f, 0.61f, 0.018f), colors[i], garment);
                if (i != 0)
                {
                    var sleeve1 = Box("衣袖", new Vector3(-0.28f, -0.18f, 0f), new Vector3(0.19f, 0.21f, 0.016f), colors[i], garment);
                    sleeve1.transform.localRotation = Quaternion.Euler(0f, 0f, -26f);
                    var sleeve2 = Box("衣袖", new Vector3(0.28f, -0.18f, 0f), new Vector3(0.19f, 0.21f, 0.016f), colors[i], garment);
                    sleeve2.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);
                }
                Box("木衣夹", new Vector3(-0.13f, -0.01f, -0.018f), new Vector3(0.028f, 0.07f, 0.031f), p.Wood, garment);
                Box("木衣夹", new Vector3(0.13f, -0.01f, -0.018f), new Vector3(0.028f, 0.07f, 0.031f), p.Wood, garment);
                foreach (Transform child in garment) child.gameObject.isStatic = false;
                garment.gameObject.AddComponent<WorldWindSway>().Configure(2.0f + i * 0.4f, 0.9f, Vector3.right);
            }
        }

        void Tree(Vector3 position, float scale)
        {
            var tree = Group("有年头的老槐树", position, Range(-15f, 15f));
            tree.localScale = Vector3.one * scale;
            // 树池和露出地面的根，让树牢牢长在地上。
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                var brick = Box("树池砖", new Vector3(Mathf.Cos(angle) * 0.86f, 0.065f, Mathf.Sin(angle) * 0.86f), new Vector3(0.35f, 0.13f, 0.20f), p.Brick, tree);
                brick.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }
            Cylinder("树池土", new Vector3(0f, 0.018f, 0f), 0.79f, 0.036f, p.Soil, tree);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                Rod("露出地面的根", new Vector3(Mathf.Cos(angle) * 0.07f, 0.24f, Mathf.Sin(angle) * 0.07f), new Vector3(Mathf.Cos(angle) * 0.72f, 0.03f, Mathf.Sin(angle) * 0.72f), 0.065f, p.Trunk, tree);
            }
            Rod("主树干", new Vector3(0f, 0f, 0f), new Vector3(0.16f, 2.65f, 0.14f), 0.22f, p.Trunk, tree);
            Rod("主树干上部", new Vector3(0.16f, 2.2f, 0.14f), new Vector3(-0.14f, 3.62f, 0f), 0.15f, p.Trunk, tree);
            Material[] leaves = { p.Canopy, p.CanopyLight, p.CanopyDark };
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                Vector3 begin = new Vector3(0.06f, 2.1f + (i % 3) * 0.27f, 0f);
                Vector3 tip = new Vector3(Mathf.Cos(angle) * Range(1.05f, 1.85f), Range(3.5f, 4.8f), Mathf.Sin(angle) * Range(1.15f, 1.9f));
                Rod("槐树枝", begin, tip, 0.08f - i * 0.004f, p.Trunk, tree);
                for (int n = 0; n < 4; n++)
                {
                    Vector3 leafPoint = tip + new Vector3(Range(-0.58f, 0.58f), Range(-0.2f, 0.62f), Range(-0.53f, 0.53f));
                    if (leafClusterMesh == null) leafClusterMesh = (Mesh)own(CreateLeafClusterMesh());
                    var cluster = MeshObject("层叠起伏的槐叶簇", leafClusterMesh, leaves[(i + n) % 3], tree, false);
                    cluster.transform.localPosition = leafPoint;
                    cluster.transform.localScale = new Vector3(Range(1.0f, 1.60f), Range(0.78f, 1.24f), Range(1.0f, 1.55f));
                    cluster.transform.localRotation = Quaternion.Euler(Range(-12,12), Range(0,360), Range(-9,9));
                    cluster.gameObject.isStatic = false;
                    if (n == 0)
                        Rod("细树枝", tip - Vector3.up * 0.2f, leafPoint + Vector3.up * 0.05f, 0.027f, p.Trunk, tree);
                }
            }
            for (int i = 0; i < 12; i++)
                GrassTuft(new Vector3(Mathf.Cos(i * 2.4f) * 0.67f, 0.03f, Mathf.Sin(i * 2.4f) * 0.67f), tree);
            // 树皮的竖向瘢痕能在近景看到，不靠平滑圆柱冒充树干。
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Rod("树皮沟纹", new Vector3(Mathf.Cos(a) * 0.216f, 0.30f, Mathf.Sin(a) * 0.216f), new Vector3(Mathf.Cos(a) * 0.2f + 0.13f, 1.86f, Mathf.Sin(a) * 0.2f + 0.09f), 0.012f, p.Wood, tree);
            }
        }

        void StreetLife()
        {
            Vector3[] flowers = { new Vector3(5.05f,1.12f,-2.68f), new Vector3(-4.85f,.92f,-3.58f) };
            for (int i=0;i<flowers.Length;i++)
            {
                var butterfly = Group("花盆旁偶尔停留的小蝴蝶",flowers[i]);
                var left = Group("左翅根",Vector3.zero,0,butterfly);
                var right = Group("右翅根",Vector3.zero,0,butterfly);
                Material wings = i == 0 ? p.Yellow : p.Cream;
                for (int side=-1;side<=1;side+=2)
                {
                    var hinge = side < 0 ? left : right;
                    Ball("上翅",new Vector3(side*.057f,0,.027f),new Vector3(.12f,.014f,.13f),wings,hinge).isStatic=false;
                    Ball("下翅",new Vector3(side*.043f,0,-.046f),new Vector3(.087f,.014f,.084f),wings,hinge).isStatic=false;
                    Ball("蝶翼细斑",new Vector3(side*.069f,-.008f,.038f),new Vector3(.018f,.004f,.026f),p.DarkRoof,hinge).isStatic=false;
                }
                Rod("细小的蝶身",new Vector3(0,0,-.074f),new Vector3(0,0,.066f),.008f,p.DarkRoof,butterfly).isStatic=false;
                butterfly.gameObject.AddComponent<WorldButterfly>().Configure(left,right,i*2.7f);
            }
            // 落叶只在树池外缘滑落，不经过可操作的比赛中心。
            for (int i=0;i<3;i++)
            {
                var leaf = MeshObject("树荫里缓缓落下的槐叶",LeafMesh(),i==0?p.Leaf:p.DryLeaf);
                leaf.transform.localPosition = new Vector3(-5.32f-i*.22f,2.65f,2.05f+i*.28f);
                leaf.transform.localScale = Vector3.one * (.62f+i*.09f);
                leaf.isStatic = false;
                leaf.AddComponent<WorldDriftingLeaf>().Configure(9.6f+i*1.4f,i*.29f);
            }
        }

        static Mesh CreateLeafClusterMesh()
        {
            const int rings = 12, slices = 20;
            var vertices = new Vector3[(rings + 1) * (slices + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * slices * 6];
            for (int ring = 0; ring <= rings; ring++)
            {
                float latitude = ring * Mathf.PI / rings;
                for (int slice = 0; slice <= slices; slice++)
                {
                    float longitude = slice * Mathf.PI * 2f / slices;
                    var direction = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    float ridges = Mathf.Sin(longitude * 7f + latitude * 5f) * Mathf.Sin(latitude) * .034f;
                    float lobes = Mathf.Cos(longitude * 4f - latitude * 6f) * .030f;
                    int index = ring * (slices + 1) + slice;
                    vertices[index] = direction * (.47f + ridges + lobes);
                    uv[index] = new Vector2((float)slice / slices, (float)ring / rings);
                    if (ring == rings || slice == slices) continue;
                    int t = (ring * slices + slice) * 6;
                    triangles[t] = index; triangles[t + 1] = index + 1; triangles[t + 2] = index + slices + 1;
                    triangles[t + 3] = index + 1; triangles[t + 4] = index + slices + 2; triangles[t + 5] = index + slices + 1;
                }
            }
            var mesh = new Mesh { name = "槐树叶簇 · 轻微不规则轮廓" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        void StreetFurniture()
        {
            Bench(new Vector3(-5.34f, 0.08f, -0.35f), -5f);
            Stool(new Vector3(4.72f, 0.08f, -0.10f), 0.88f);
            Bicycle(new Vector3(-5.45f, 0.08f, -2.38f), -13f, p.Blue);
            Bicycle(new Vector3(8.04f, 0.08f, 0.28f), -25f, p.Red);
            StreetLamp(new Vector3(4.6f, 0.08f, 3.4f));
            StreetLamp(new Vector3(-4.55f, 0.08f, 6.25f));
            FlowerPot(new Vector3(-4.83f, 0.08f, -3.62f), 0.74f);
            FlowerPot(new Vector3(5.03f, 0.08f, -2.64f), 1f);
            FlowerPot(new Vector3(5.64f, 0.08f, -2.29f), 0.70f);
            var bag = Group("放在长椅边的书包", new Vector3(-4.86f, 0.55f, -0.49f), 14f);
            var body = Box("布书包", new Vector3(0f, 0.19f, 0f), new Vector3(0.38f, 0.45f, 0.18f), p.Mint, bag);
            body.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Box("书包前袋", new Vector3(0f, 0.08f, -0.11f), new Vector3(0.28f, 0.18f, 0.055f), p.Blue, bag);
            Torus("书包提手", new Vector3(0f, 0.45f, 0f), 0.074f, 0.013f, p.Rubber, bag);
            Box("书包徽章", new Vector3(0f, 0.26f, -0.113f), new Vector3(0.09f, 0.07f, 0.014f), p.Paper, bag);
            var bucket = Group("墙边搪瓷桶", new Vector3(-4.57f, 0.08f, 3.41f));
            Cylinder("桶身", new Vector3(0f, 0.20f, 0f), 0.19f, 0.4f, p.Cream, bucket);
            Cylinder("桶内水", new Vector3(0f, 0.39f, 0f), 0.175f, 0.01f, p.Glass, bucket);
            Torus("桶沿", new Vector3(0f, 0.4f, 0f), 0.188f, 0.018f, p.Iron, bucket, Quaternion.Euler(90f, 0f, 0f));
            Cylinder("桶底", new Vector3(0f, 0.03f, 0f), 0.194f, 0.038f, p.Iron, bucket);
            Rod("扫帚柄", new Vector3(-4.25f, 0.12f, 3.20f), new Vector3(-4.50f, 1.45f, 3.50f), 0.018f, p.Wood);
            for (int i = 0; i < 12; i++)
                Rod("扫帚草束", new Vector3(-4.25f, 0.30f, 3.20f), new Vector3(-4.44f + i * 0.035f, 0.08f, 3.18f), 0.017f, p.DryLeaf);
            // 红白球藏在路边，留给玩家一点熟悉的童年小发现。
            Ball("路边的皮球", new Vector3(4.49f, 0.26f, -1.33f), Vector3.one * 0.37f, p.Red);
            Ball("皮球白色花瓣", new Vector3(4.42f, 0.37f, -1.45f), new Vector3(0.19f, 0.13f, 0.1f), p.White);
        }

        void Bench(Vector3 position, float angle)
        {
            var bench = Group("树下的老木长椅", position, angle);
            for (int i = 0; i < 4; i++) Box("椅面木条", new Vector3(0f, 0.48f, -0.25f + i * 0.17f), new Vector3(2.1f, 0.076f, 0.135f), p.Wood, bench);
            for (int i = 0; i < 3; i++)
            {
                var plank = Box("椅背木条", new Vector3(0f, 0.73f + i * 0.15f, 0.35f + i * 0.025f), new Vector3(2.1f, 0.10f, 0.061f), p.Wood, bench);
                plank.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.81f;
                Rod("长椅前脚", new Vector3(x, 0f, -0.22f), new Vector3(x, 0.48f, -0.17f), 0.043f, p.Iron, bench);
                Rod("长椅后脚和靠背架", new Vector3(x, 0f, 0.23f), new Vector3(x, 1.05f, 0.43f), 0.043f, p.Iron, bench);
                Rod("椅面架", new Vector3(x, 0.43f, -0.3f), new Vector3(x, 0.43f, 0.35f), 0.039f, p.Iron, bench);
                Rod("扶手前撑", new Vector3(x, 0.5f, -0.16f), new Vector3(x, 0.72f, -0.16f), 0.027f, p.Iron, bench);
                Rod("长椅扶手", new Vector3(x, 0.72f, -0.22f), new Vector3(x, 0.75f, 0.36f), 0.035f, p.Iron, bench);
            }
        }

        void Stool(Vector3 position, float scale)
        {
            var stool = Group("小板凳", position, -9f);
            stool.localScale = Vector3.one * scale;
            Box("板凳面", new Vector3(0f, 0.4f, 0f), new Vector3(0.68f, 0.095f, 0.37f), p.Wood, stool);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.26f : 0.26f;
                float z = i < 2 ? -0.11f : 0.11f;
                Rod("板凳脚", new Vector3(x * 1.13f, 0f, z * 1.4f), new Vector3(x, 0.4f, z), 0.035f, p.Wood, stool);
            }
            Rod("板凳横档", new Vector3(-0.25f, 0.15f, 0f), new Vector3(0.25f, 0.15f, 0f), 0.027f, p.Wood, stool);
        }

        void Bicycle(Vector3 position, float angle, Material paint)
        {
            var bicycle = Group("停在路边的二八自行车", position, angle);
            bicycle.localRotation *= Quaternion.Euler(0f, 0f, -8f);
            Vector3 rear = new Vector3(-0.65f, 0.40f, 0f), front = new Vector3(0.74f, 0.40f, 0f);
            BicycleWheel(rear, bicycle);
            BicycleWheel(front, bicycle);
            Vector3 crank = new Vector3(-0.07f, 0.44f, 0f);
            Vector3 saddleTube = new Vector3(-0.32f, 0.99f, 0f);
            Vector3 headTop = new Vector3(0.45f, 1.02f, 0f);
            Vector3 headBottom = new Vector3(0.54f, 0.83f, 0f);
            Rod("自行车下管", crank, headBottom, 0.033f, paint, bicycle);
            Rod("自行车上管", saddleTube, headTop, 0.03f, paint, bicycle);
            Rod("自行车座管", crank, saddleTube, 0.032f, paint, bicycle);
            Rod("自行车后上叉", rear, saddleTube, 0.024f, paint, bicycle);
            Rod("自行车后下叉", rear, crank, 0.024f, paint, bicycle);
            Rod("自行车头管", headBottom, headTop, 0.036f, paint, bicycle);
            for (int side = -1; side <= 1; side += 2)
            {
                Rod("前叉", headBottom + new Vector3(0f, 0f, side * 0.045f), front + new Vector3(0f, 0f, side * 0.045f), 0.021f, p.Iron, bicycle);
                Rod("后货架支架", rear + new Vector3(0f, 0f, side * 0.1f), new Vector3(-0.68f, 0.92f, side * 0.15f), 0.018f, p.Iron, bicycle);
            }
            Box("自行车后座", new Vector3(-0.69f, 0.94f, 0f), new Vector3(0.55f, 0.035f, 0.30f), p.Iron, bicycle);
            for (int i = 0; i < 4; i++) Box("后货架间隙", new Vector3(-0.88f + i * 0.13f, 0.965f, 0f), new Vector3(0.043f, 0.016f, 0.29f), p.DarkRoof, bicycle);
            Rod("鞍座杆", saddleTube, saddleTube + Vector3.up * 0.13f, 0.028f, p.Iron, bicycle);
            Ball("黑色自行车鞍座", saddleTube + new Vector3(-0.04f, 0.15f, 0f), new Vector3(0.28f, 0.071f, 0.21f), p.Rubber, bicycle);
            Rod("车把立管", headTop, new Vector3(0.36f, 1.24f, 0f), 0.027f, p.Iron, bicycle);
            Rod("车把横管", new Vector3(0.36f, 1.24f, -0.24f), new Vector3(0.36f, 1.24f, 0.24f), 0.024f, p.Iron, bicycle);
            Rod("左车把胶套", new Vector3(0.35f, 1.24f, -0.24f), new Vector3(0.19f, 1.23f, -0.26f), 0.032f, p.Rubber, bicycle);
            Rod("右车把胶套", new Vector3(0.35f, 1.24f, 0.24f), new Vector3(0.19f, 1.23f, 0.26f), 0.032f, p.Rubber, bicycle);
            Ball("小车铃", new Vector3(0.35f, 1.275f, -0.12f), Vector3.one * 0.077f, p.Cream, bicycle);
            Torus("链轮", crank + new Vector3(0f, 0f, -0.065f), 0.13f, 0.019f, p.Iron, bicycle);
            Rod("链条上边", crank + new Vector3(0f, 0.13f, -0.066f), rear + new Vector3(0f, 0.06f, -0.066f), 0.008f, p.Iron, bicycle);
            Rod("链条下边", crank + new Vector3(0f, -0.13f, -0.066f), rear + new Vector3(0f, -0.06f, -0.066f), 0.008f, p.Iron, bicycle);
            Rod("曲柄", crank + new Vector3(0f, 0f, -0.11f), crank + new Vector3(0.15f, -0.08f, -0.11f), 0.013f, p.Iron, bicycle);
            Box("脚踏", crank + new Vector3(0.15f, -0.08f, -0.17f), new Vector3(0.13f, 0.03f, 0.15f), p.Rubber, bicycle);
            Rod("自行车支脚", new Vector3(-0.1f, 0.43f, 0f), new Vector3(-0.25f, 0.02f, 0.24f), 0.017f, p.Iron, bicycle);
            // 前篮子由细铁丝组成，剪影比实心盒子轻巧。
            Vector3 basket = new Vector3(0.67f, 1.10f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float z = -0.18f + i * 0.09f;
                Rod("篮子底", basket + new Vector3(-0.18f, -0.1f, z), basket + new Vector3(0.18f, -0.1f, z), 0.009f, p.Iron, bicycle);
                Rod("篮子竖丝", basket + new Vector3(0.18f, -0.1f, z), basket + new Vector3(0.23f, 0.18f, z), 0.009f, p.Iron, bicycle);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Rod("篮口", basket + new Vector3(-0.18f, 0.18f, side * 0.2f), basket + new Vector3(0.23f, 0.18f, side * 0.2f), 0.014f, p.Iron, bicycle);
                Rod("篮框", basket + new Vector3(-0.18f, -0.1f, side * 0.18f), basket + new Vector3(-0.18f, 0.18f, side * 0.2f), 0.01f, p.Iron, bicycle);
            }
            Rod("篮口前沿", basket + new Vector3(0.23f, 0.18f, -0.2f), basket + new Vector3(0.23f, 0.18f, 0.2f), 0.014f, p.Iron, bicycle);
        }

        void BicycleWheel(Vector3 center, Transform parent)
        {
            Torus("黑色车胎", center, 0.366f, 0.033f, p.Rubber, parent);
            Torus("金属车圈", center, 0.326f, 0.012f, p.Iron, parent);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Rod("车轮辐条", center, center + new Vector3(Mathf.Cos(a) * 0.327f, Mathf.Sin(a) * 0.327f, 0f), 0.0045f, p.Cream, parent);
            }
            Ball("车轴", center, Vector3.one * 0.078f, p.Iron, parent);
        }

        GameObject Torus(string name, Vector3 center, float radius, float tube, Material material, Transform parent = null, Quaternion? rotation = null)
        {
            const int segments = 28, sides = 6;
            var vertices = new Vector3[(segments + 1) * (sides + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * sides * 6];
            int t = 0;
            for (int i = 0; i <= segments; i++) for (int j = 0; j <= sides; j++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = j * Mathf.PI * 2f / sides;
                int index = i * (sides + 1) + j;
                float r = radius + Mathf.Cos(b) * tube;
                vertices[index] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(b) * tube);
                uv[index] = new Vector2((float)i / segments, (float)j / sides);
                if (i < segments && j < sides)
                {
                    int next = index + sides + 1;
                    triangles[t++] = index; triangles[t++] = next; triangles[t++] = next + 1;
                    triangles[t++] = index; triangles[t++] = next + 1; triangles[t++] = index + 1;
                }
            }
            var mesh = new Mesh();
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv = uv; mesh.RecalculateNormals();
            var obj = MeshObject(name, mesh, material, parent);
            obj.transform.localPosition = center;
            obj.transform.localRotation = rotation ?? Quaternion.identity;
            return obj;
        }

        void StreetLamp(Vector3 position)
        {
            var lamp = Group("老式路灯", position);
            Cylinder("路灯底座", new Vector3(0f, 0.1f, 0f), 0.21f, 0.2f, p.Iron, lamp);
            Cylinder("路灯下部", new Vector3(0f, 0.65f, 0f), 0.093f, 1.1f, p.Iron, lamp);
            Cylinder("路灯杆", new Vector3(0f, 2.25f, 0f), 0.051f, 4.0f, p.Iron, lamp);
            Rod("路灯弯臂", new Vector3(0f, 4.1f, 0f), new Vector3(-0.29f, 4.38f, 0f), 0.045f, p.Iron, lamp);
            Rod("路灯横臂", new Vector3(-0.29f, 4.38f, 0f), new Vector3(-0.94f, 4.38f, 0f), 0.045f, p.Iron, lamp);
            Rod("灯罩吊杆", new Vector3(-0.89f, 4.38f, 0f), new Vector3(-0.89f, 4.17f, 0f), 0.033f, p.Iron, lamp);
            Ball("路灯灯罩", new Vector3(-0.89f, 4.10f, 0f), new Vector3(0.55f, 0.17f, 0.37f), p.Iron, lamp);
            Ball("路灯暖光", new Vector3(-0.89f, 4.05f, 0f), new Vector3(0.43f, 0.05f, 0.28f), p.Lamp, lamp);
            // 路牌只保留临街的一块；后方路灯不再重复悬挂难读的小字。
            if (position.x > 0f)
            {
                Box("路灯上的门牌", new Vector3(0f, 2.45f, -0.074f), new Vector3(0.48f, 0.27f, 0.026f), p.Blue, lamp);
                Text("街道牌", "槐荫里", new Vector3(0f, 2.45f, -0.095f), 0.11f, new Color(.95f,.94f,.82f), lamp);
            }
            var lightObject = new GameObject("路灯微光");
            lightObject.transform.SetParent(lamp, false);
            lightObject.transform.localPosition = new Vector3(-0.89f, 3.95f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1f, 0.73f, 0.38f); light.intensity = 0.35f; light.range = 3f;
            light.shadows = LightShadows.None;
        }

        void FlowerPot(Vector3 position, float scale, Transform parent = null)
        {
            var pot = Group("盛开的陶土花盆", position, Range(0f, 360f), parent);
            pot.localScale = Vector3.one * scale;
            Cylinder("陶花盆", new Vector3(0f, 0.21f, 0f), 0.23f, 0.42f, p.Terracotta, pot);
            Cylinder("花盆脚", new Vector3(0f, 0.028f, 0f), 0.17f, 0.055f, p.Terracotta, pot);
            Torus("卷边盆沿", new Vector3(0f, 0.42f, 0f), 0.222f, 0.037f, p.Terracotta, pot, Quaternion.Euler(90f, 0f, 0f));
            Cylinder("花盆土", new Vector3(0f, 0.435f, 0f), 0.206f, 0.019f, p.Soil, pot);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Vector3 flower = new Vector3(Mathf.Cos(a) * Range(0.09f, 0.22f), Range(0.65f, 0.92f), Mathf.Sin(a) * Range(0.09f, 0.22f));
                Rod("花茎", new Vector3(0f, 0.43f, 0f), flower, 0.009f, p.Grass, pot);
                Ball("花叶", new Vector3(flower.x * 0.65f, 0.59f, flower.z * 0.65f), new Vector3(0.12f, 0.05f, 0.08f), p.Leaf, pot);
                for (int petal = 0; petal < 5; petal++)
                {
                    float b = petal * Mathf.PI * 2f / 5f;
                    Ball("花瓣", flower + new Vector3(Mathf.Cos(b) * 0.044f, 0f, Mathf.Sin(b) * 0.044f), new Vector3(0.069f, 0.022f, 0.055f), flowerColors[i % 3], pot);
                }
                Ball("花心", flower + new Vector3(0f, 0.012f, 0f), Vector3.one * 0.034f, p.Yellow, pot);
            }
        }

        void GrassTuft(Vector3 position, Transform parent = null)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 tip = position + new Vector3(Range(-0.09f, 0.09f), Range(0.09f, 0.23f), Range(-0.06f, 0.06f));
                Rod("墙边草叶", position, tip, 0.007f, p.Grass, parent);
            }
        }

        void ChalkArena()
        {
            // 粉笔圈有轻微断笔与粗细变化，保留完整的中心操作空间。
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int segments = 200;
            for (int i = 0; i < segments; i++)
            {
                if (i == 12 || i == 77 || i == 144 || i == 145) continue;
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                float radius = 1.85f + Mathf.Sin(a * 13f) * 0.006f;
                float nextRadius = 1.85f + Mathf.Sin(b * 13f) * 0.006f;
                float width = Range(0.015f, 0.031f);
                int v = vertices.Count;
                vertices.Add(ArenaChalkPoint(a, radius - width));
                vertices.Add(ArenaChalkPoint(a, radius + width));
                vertices.Add(ArenaChalkPoint(b, nextRadius - width));
                vertices.Add(ArenaChalkPoint(b, nextRadius + width));
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v + 1); triangles.Add(v + 2); triangles.Add(v + 3);
            }
            var ring = new Mesh();
            ring.SetVertices(vertices); ring.SetTriangles(triangles, 0); ring.RecalculateNormals();
            MeshObject("圆片比赛的手画粉笔圈", ring, p.Chalk);
            Text("地上的粉笔字", "今 日 擂 台", new Vector3(0f, 0.018f, -2.92f), 0.34f, new Color(0.91f, 0.88f, 0.70f), null, Quaternion.Euler(90f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 3.05f;
                float height = arenaTerrain.SurfaceHeight(root.TransformPoint(new Vector3(x, 0f, -.75f))) - root.position.y + .007f;
                ArenaPaintLine("粉笔站位标记", new Vector3(x - .19f, height, -.75f), new Vector3(x + .19f, height, -.75f), .024f, p.Chalk, arenaGeometry);
                ArenaPaintLine("粉笔站位标记", new Vector3(x, height, -.94f), new Vector3(x, height, -.56f), .024f, p.Chalk, arenaGeometry);
            }
            var chalk = Rod("留在地上的短粉笔", new Vector3(3.28f, 0.029f, -2.40f), new Vector3(3.46f, 0.029f, -2.31f), 0.024f, p.Chalk);
            // 跳房子位于人行道上，暗示这是一座集成童年游戏的街区。
            var hopscotch = Group("另一边的跳房子涂鸦", new Vector3(-6.15f, 0.08f, -4.7f), 4f);
            for (int i = 0; i < 5; i++)
            {
                float z = i * 0.44f;
                int columns = i == 2 || i == 4 ? 2 : 1;
                for (int c = 0; c < columns; c++)
                {
                    float x = columns == 1 ? 0f : -0.24f + c * 0.48f;
                    ChalkBox(new Vector3(x, 0f, z), 0.45f, 0.42f, hopscotch);
                    Text("跳房子数字", (i + c + 1).ToString(), new Vector3(x, 0.009f, z), 0.18f, new Color(0.96f, 0.91f, 0.77f), hopscotch, Quaternion.Euler(90f, 0f, 0f));
                }
            }
        }

        Vector3 ArenaChalkPoint(float angle, float radius)
        {
            var point = new Vector3(Mathf.Cos(angle) * radius, 0f, .55f + Mathf.Sin(angle) * radius);
            point.y = arenaTerrain.SurfaceHeight(root.TransformPoint(point)) - root.position.y + .012f;
            return point;
        }

        void ChalkBox(Vector3 center, float width, float depth, Transform parent)
        {
            float x = width * 0.5f, z = depth * 0.5f;
            Rod("跳房子粉笔线", center + new Vector3(-x, 0.007f, -z), center + new Vector3(x, 0.007f, -z), 0.008f, p.Chalk, parent);
            Rod("跳房子粉笔线", center + new Vector3(-x, 0.007f, z), center + new Vector3(x, 0.007f, z), 0.008f, p.Chalk, parent);
            Rod("跳房子粉笔线", center + new Vector3(-x, 0.007f, -z), center + new Vector3(-x, 0.007f, z), 0.008f, p.Chalk, parent);
            Rod("跳房子粉笔线", center + new Vector3(x, 0.007f, -z), center + new Vector3(x, 0.007f, z), 0.008f, p.Chalk, parent);
        }

        void GroundDetails()
        {
            for (int i = 0; i < 70; i++)
            {
                float x = Range(-9f, 10f), z = Range(-5.6f, 6f);
                if (Mathf.Abs(x) < 3.15f && Mathf.Abs(z) < 3.15f) continue;
                float y = Mathf.Abs(x) > 4.2f || z > 4.25f ? 0.081f : 0.015f;
                var leaf = MeshObject("风吹落的槐叶", LeafMesh(), i % 5 == 0 ? p.Leaf : p.DryLeaf);
                leaf.transform.localPosition = new Vector3(x, y, z);
                leaf.transform.localRotation = Quaternion.Euler(Range(-7f, 7f), Range(0f, 360f), 0f);
                leaf.transform.localScale = Vector3.one * Range(0.7f, 1.4f);
                if (i % 11 == 0) GrassTuft(new Vector3(x, y, z));
            }
            // 克制的细裂缝只放在路缘，不干扰玩家看清圆片。
            for (int i = 0; i < 7; i++)
            {
                Vector3 current = new Vector3(i % 2 == 0 ? -3.42f : 3.43f, 0.011f, -3.5f + i * 1.22f);
                for (int n = 0; n < 4; n++)
                {
                    Vector3 next = current + new Vector3(Range(-0.08f, 0.10f), 0f, Range(0.11f, 0.21f));
                    Rod("细微路面裂纹", current, next, 0.004f, p.DarkRoof);
                    current = next;
                }
            }
            var airplane = Group("落在路边的纸飞机", new Vector3(4.48f, 0.081f, -1.84f), 29f);
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(0f,0f,0.24f), new Vector3(-0.18f,0f,-0.17f), new Vector3(0f,0.06f,-0.06f), new Vector3(0.18f,0f,-0.17f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
            mesh.RecalculateNormals();
            MeshObject("折纸飞机", mesh, p.Paper, airplane);
            // 对战区的井盖由 BuildArenaTerrain 生成，与实体碰撞共用可见几何。
        }

        Mesh LeafMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(0f,0f,0.12f), new Vector3(-0.048f,0f,0.025f), new Vector3(0f,0.009f,0f), new Vector3(0.048f,0f,0.025f), new Vector3(0f,0f,-0.105f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 1, 2, 4, 2, 3, 4 };
            mesh.RecalculateNormals();
            return mesh;
        }

        void Cable(string name, Vector3 start, Vector3 end, float sag, float radius, Transform parent = null)
        {
            Vector3 last = start;
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f;
                Vector3 next = Vector3.Lerp(start, end, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * sag);
                Rod(name, last, next, radius, p.Cable, parent);
                last = next;
            }
        }

        void OverheadWires()
        {
            var pole = Group("街角木电线杆", new Vector3(9.72f, 0.07f, 2.78f));
            Cylinder("木电线杆", new Vector3(0f, 3.2f, 0f), 0.12f, 6.4f, p.Wood, pole);
            Box("电线横担", new Vector3(0f, 5.9f, 0f), new Vector3(1.57f, 0.12f, 0.10f), p.Iron, pole);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.55f + i * 0.55f;
                Cylinder("陶瓷绝缘子", new Vector3(x, 6.01f, 0f), 0.065f, 0.17f, p.Cream, pole);
                Cable("跨过巷子的电线", new Vector3(9.72f + x, 6.13f, 2.78f), new Vector3(-10f + x, 5.52f, 7.0f), 0.74f, 0.014f);
            }
            Box("电表箱", new Vector3(0f, 2.25f, -0.11f), new Vector3(0.39f, 0.63f, 0.14f), p.Stone, pole);
            Box("电表视窗", new Vector3(0f, 2.30f, -0.188f), new Vector3(0.17f, 0.19f, 0.012f), p.Glass, pole);
        }

        void DistantTown()
        {
            // 远处轮廓与空气透视填满背景，近景仍然是有亲切尺度的街角。
            for (int i = 0; i < 8; i++)
            {
                float x = -16f + i * 4.8f;
                float height = Range(4.5f, 8.0f);
                var distant = Group("远处的低层楼房", new Vector3(x, 0f, 19f + (i % 3) * 2f));
                Box("远景楼体", new Vector3(0f, height * 0.5f, 0f), new Vector3(4.2f, height, 5.1f), i % 2 == 0 ? p.Cream : p.Stone, distant);
                Box("屋顶边缘", new Vector3(0f, height + 0.06f, 0f), new Vector3(4.35f, 0.16f, 5.22f), p.DarkRoof, distant);
                for (int row = 0; row < Mathf.FloorToInt(height / 1.3f); row++) for (int col = 0; col < 3; col++)
                    Box("远处窗户", new Vector3(-1.17f + col * 1.17f, 0.87f + row * 1.3f, -2.57f), new Vector3(0.67f, 0.77f, 0.025f), p.Glass, distant);
                if (i % 3 == 0)
                {
                    Cylinder("屋顶水箱", new Vector3(0.9f, height + 0.65f, 0.6f), 0.58f, 1.0f, p.Iron, distant);
                    Rod("屋顶天线", new Vector3(-1f, height, 0f), new Vector3(-1f, height + 1.7f, 0f), 0.023f, p.Iron, distant);
                    for (int n = 0; n < 3; n++) Rod("天线横杆", new Vector3(-1.45f, height + 1.1f + n * 0.24f, 0f), new Vector3(-0.55f, height + 1.1f + n * 0.24f, 0f), 0.017f, p.Iron, distant);
                }
            }
        }
    }

    /// <summary>仅用于布景的轻微随风摇摆，不改变物理游戏。</summary>
    internal sealed class WorldWindSway : MonoBehaviour
    {
        Quaternion rest;
        float amplitude;
        float speed;
        float phase;
        Vector3 axis;
        public void Configure(float degrees, float frequency, Vector3 localAxis)
        {
            rest = transform.localRotation;
            amplitude = degrees;
            speed = frequency;
            axis = localAxis.normalized;
            phase = transform.position.x * 1.7f + transform.position.z;
        }
        void Update()
        {
            float sway = Mathf.Sin(Time.time * speed + phase) * amplitude + Mathf.Sin(Time.time * speed * 1.8f + phase) * amplitude * 0.22f;
            transform.localRotation = rest * Quaternion.AngleAxis(sway, axis);
        }
    }
}
