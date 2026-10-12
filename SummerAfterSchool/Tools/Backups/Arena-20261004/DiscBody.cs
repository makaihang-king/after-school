using System;
using UnityEngine;

namespace Childhood
{
    public sealed class DiscBody : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public int ArtIndex { get; private set; }
        public bool IsTarget { get; set; }
        public bool Claimed { get; set; }
        public float Weight { get; private set; }
        public float Thickness { get; private set; }
        public float EdgeLift { get; private set; }
        public float Wear { get; private set; }
        public bool Treasured { get; private set; }
        public float Radius => 0.31f;
        public bool FaceDown => Vector3.Dot(transform.up, Vector3.up) < -0.84f;
        public bool Quiet => Body.IsSleeping() || (Body.linearVelocity.sqrMagnitude < 0.008f && Body.angularVelocity.sqrMagnitude < 0.04f);
        public event Action<DiscBody, float> Hit;
        public Collider LastHitCollider { get; private set; }
        private float lastSound;
        private Material rimMaterial;
        private Material faceMaterial, backMaterial;
        private MeshRenderer artRenderer;

        public static DiscBody Spawn(Transform parent, int artIndex, Vector3 position,
            float weight = 1, float thickness = 1, float friction = 1, float edgeLift = 0.15f)
        {
            var root = new GameObject("圆片_" + artIndex);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var disc = root.AddComponent<DiscBody>();
            disc.ArtIndex = artIndex; disc.Weight = weight; disc.Thickness = thickness; disc.EdgeLift = edgeLift;
            float height = 0.065f * thickness;
            var mesh = CylinderMesh(0.31f, height, 48);
            var filter = root.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            disc.rimMaterial = MaterialFor(new Color(0.75f, 0.62f, 0.43f), true);
            var back = MaterialFor(new Color(0.83f, 0.76f, 0.57f));
            var backTexture = Resources.Load<Texture2D>("Art/Pogs/pog_back");
            if (backTexture != null) { back.mainTexture = backTexture; back.color = Color.white; }
            var texture = Resources.Load<Texture2D>("Art/Pogs/pog_" + artIndex.ToString("00"));
            var face = MaterialFor(Color.white);
            if (texture != null) face.mainTexture = texture;
            disc.faceMaterial = face; disc.backMaterial = back;
            renderer.sharedMaterials = new[] { disc.rimMaterial, face, back };
            disc.artRenderer = renderer;
            disc.ConfigureAppearance(0f);
            var collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = true;
            var pm = new PhysicsMaterial("纸片摩擦")
            {
                dynamicFriction = friction * 0.32f,
                staticFriction = friction * 0.42f,
                bounciness = 0.13f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            collider.sharedMaterial = pm;
            disc.Body = root.AddComponent<Rigidbody>();
            disc.Body.mass = 0.08f * weight;
            disc.Body.linearDamping = 0.16f;
            disc.Body.angularDamping = 0.65f;
            disc.Body.maxAngularVelocity = 45;
            disc.Body.maxDepenetrationVelocity = 4;
            disc.Body.interpolation = RigidbodyInterpolation.Interpolate;
            disc.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            disc.Body.solverIterations = 18;
            disc.Body.solverVelocityIterations = 12;
            disc.Body.sleepThreshold = 0.02f;
            disc.Body.centerOfMass = new Vector3(edgeLift * 0.015f, 0, 0);
            return disc;
        }

        /// <summary>只改变印刷纸面、纸层边缘和珍藏包边；不影响凸体、质量或摩擦。</summary>
        public void ConfigureAppearance(float wear, bool treasured = false)
        {
            Wear = Mathf.Clamp01(wear);
            Treasured = treasured;
            ConfigurePaperMaterial(rimMaterial);
            ConfigurePaperMaterial(faceMaterial);
            ConfigurePaperMaterial(backMaterial);
        }

        void ConfigurePaperMaterial(Material material)
        {
            if (material == null || !material.HasProperty("_Wear")) return;
            material.SetFloat("_Wear", Wear);
            material.SetFloat("_Treasured", Treasured ? 1f : 0f);
            material.SetFloat("_Seed", ArtIndex * 1.731f + .37f);
        }

        private static Material MaterialFor(Color color, bool edge = false)
        {
            var shader = Resources.Load<Shader>("Shaders/PaperDisc");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader); mat.color = color;
            mat.name = edge ? "可见纸纤维的层压片边" : "有年代感的圆片印刷";
            mat.SetFloat("_Glossiness", .16f); mat.SetFloat("_Metallic", 0);
            if (mat.HasProperty("_Edge")) mat.SetFloat("_Edge", edge ? 1f : 0f);
            return mat;
        }

        private static Mesh CylinderMesh(float radius, float height, int segments)
        {
            // 分离侧面与正反面顶点，保证圆片印花不受边缘 UV 影响。
            var vertices = new Vector3[segments * 6 + 2];
            var uv = new Vector2[vertices.Length];
            var side = new int[segments * 6]; var top = new int[segments * 3]; var bottom = new int[segments * 3];
            int topCenter = segments * 6, bottomCenter = topCenter + 1;
            vertices[topCenter] = Vector3.up * height * 0.5f; vertices[bottomCenter] = Vector3.down * height * 0.5f;
            uv[topCenter] = uv[bottomCenter] = Vector2.one * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                Vector3 q = new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
                int n = i * 6;
                vertices[n] = p + Vector3.down * height * 0.5f; vertices[n + 1] = p + Vector3.up * height * 0.5f;
                vertices[n + 2] = q + Vector3.down * height * 0.5f; vertices[n + 3] = q + Vector3.up * height * 0.5f;
                vertices[n + 4] = p + Vector3.up * height * 0.5f; vertices[n + 5] = p + Vector3.down * height * 0.5f;
                uv[n] = new Vector2((float)i / segments, 0); uv[n + 1] = new Vector2((float)i / segments, 1);
                uv[n + 2] = new Vector2((float)(i + 1) / segments, 0); uv[n + 3] = new Vector2((float)(i + 1) / segments, 1);
                uv[n + 4] = new Vector2(p.x / radius / 2 + 0.5f, p.z / radius / 2 + 0.5f);
                uv[n + 5] = uv[n + 4];
                int s = i * 6; side[s] = n; side[s + 1] = n + 1; side[s + 2] = n + 2;
                side[s + 3] = n + 2; side[s + 4] = n + 1; side[s + 5] = n + 3;
                top[i * 3] = topCenter; top[i * 3 + 1] = ((i + 1) % segments) * 6 + 4; top[i * 3 + 2] = n + 4;
                bottom[i * 3] = bottomCenter; bottom[i * 3 + 1] = n + 5; bottom[i * 3 + 2] = ((i + 1) % segments) * 6 + 5;
            }
            var mesh = new Mesh { name = "圆片48边_正反面分离" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.subMeshCount = 3;
            mesh.SetTriangles(side, 0); mesh.SetTriangles(top, 1); mesh.SetTriangles(bottom, 2);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed > 0.5f && Time.unscaledTime - lastSound > 0.12f)
            { lastSound = Time.unscaledTime; LastHitCollider = collision.collider; Hit?.Invoke(this, speed); }
        }

        public void MarkClaimed()
        {
            Claimed = true; Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; Body.isKinematic = true;
            rimMaterial.color = new Color(1, 0.8f, 0.2f);
        }

        private void OnDestroy()
        {
            if (artRenderer != null) foreach (var material in artRenderer.sharedMaterials) if (material != null) Destroy(material);
            var filter = GetComponent<MeshFilter>(); if (filter != null) Destroy(filter.sharedMesh);
            var collider = GetComponent<Collider>(); if (collider != null && collider.sharedMaterial != null) Destroy(collider.sharedMaterial);
        }
    }
}
