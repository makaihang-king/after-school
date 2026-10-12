using System;
using System.Collections.Generic;
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
        public ArenaTerrain Arena { get; set; }
        public int BoundaryRecoveries { get; private set; }
        public int PenetrationRecoveries { get; private set; }
        public IReadOnlyList<Collider> PhysicsColliders => physicsColliders;
        public float HalfHeight => .0325f * Thickness;
        public Vector3 SnapshotPosition => Claimed ? claimedPosition : Body.position;
        public Quaternion SnapshotRotation => Claimed ? claimedRotation : Body.rotation;
        public float Radius => 0.31f;
        public bool FaceDown => Vector3.Dot(transform.up, Vector3.up) < -0.84f;
        public bool Quiet => Body.IsSleeping() || (Body.linearVelocity.sqrMagnitude < 0.008f && Body.angularVelocity.sqrMagnitude < 0.04f);
        public event Action<DiscBody, float> Hit;
        public Collider LastHitCollider { get; private set; }
        public Vector3 LastHitNormal { get; private set; }
        private Material rimMaterial;
        private Material faceMaterial, backMaterial;
        private MeshRenderer artRenderer;
        private readonly List<Collider> physicsColliders = new List<Collider>();
        private PhysicsMaterial paperPhysics;
        private Vector3 claimedPosition;
        private Quaternion claimedRotation = Quaternion.identity;

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
            var pm = new PhysicsMaterial("纸片摩擦")
            {
                dynamicFriction = friction * 0.32f,
                staticFriction = friction * 0.42f,
                bounciness = 0.13f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            disc.paperPhysics = pm;
            // Sweep CCD 支持基本碰撞体。八条内接矩形的并集覆盖圆面，边缘误差小于 6 mm，
            // 不再依赖薄凸网格的离散碰撞，也不放大圆片可用于得分的真实半径。
            const int strips = 8;
            float halfAngle = Mathf.PI / (strips * 2);
            for (int i = 0; i < strips; i++)
            {
                var shape = new GameObject("纸片连续碰撞 " + i).transform;
                shape.SetParent(root.transform, false);
                shape.localRotation = Quaternion.Euler(0, i * 180f / strips, 0);
                var box = shape.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(.62f * Mathf.Cos(halfAngle), height, .62f * Mathf.Sin(halfAngle));
                box.contactOffset = .0025f;
                box.sharedMaterial = pm;
                disc.physicsColliders.Add(box);
            }
            disc.Body = root.AddComponent<Rigidbody>();
            disc.Body.mass = 0.08f * weight;
            disc.Body.linearDamping = 0.16f;
            disc.Body.angularDamping = 0.65f;
            disc.Body.maxAngularVelocity = 45;
            disc.Body.maxDepenetrationVelocity = 12;
            disc.Body.interpolation = RigidbodyInterpolation.Interpolate;
            disc.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            disc.Body.solverIterations = 24;
            disc.Body.solverVelocityIterations = 16;
            disc.Body.sleepThreshold = 0.02f;
            disc.Body.centerOfMass = new Vector3(edgeLift * 0.015f, 0, 0);
            // 相互重叠的基本形状不应把纸圆片的惯性计算成八根长条。
            float radialInertia = disc.Body.mass * (3f * disc.Radius * disc.Radius + height * height) / 12f;
            disc.Body.inertiaTensorRotation = Quaternion.identity;
            disc.Body.inertiaTensor = new Vector3(radialInertia, .5f * disc.Body.mass * disc.Radius * disc.Radius, radialInertia);
            return disc;
        }

        public static DiscBody FromCollider(Collider collider)
        {
            return collider == null ? null : collider.attachedRigidbody != null
                ? collider.attachedRigidbody.GetComponent<DiscBody>() : collider.GetComponentInParent<DiscBody>();
        }

        void FixedUpdate()
        {
            if (Body != null && !Body.isKinematic)
            {
                // Sweep CCD 不扫旋转；快速翻边时用包含角运动的预测接触。
                var mode = Body.angularVelocity.sqrMagnitude > 64f ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
                if (Body.collisionDetectionMode != mode) Body.collisionDetectionMode = mode;
            }
            ConstrainToArena(Arena);
        }
        void LateUpdate() { ConstrainToArena(Arena); }

        /// <summary>仅兜底腾空飞出实物围边或深穿地面的异常；保留朝向，得分仍由停稳结果决定。</summary>
        public void ConstrainToArena(ArenaTerrain terrain)
        {
            if (Body == null || Body.isKinematic || Claimed || terrain == null || !terrain.IsTerrainEnabled) return;
            Vector3 position = Body.position;
            Vector3 velocity = Body.linearVelocity;
            HorizontalExtents(Body.rotation, out float extentX, out float extentZ);
            Vector3 corrected = terrain.ClampInside(position, extentX + .002f, extentZ + .002f);
            if (Mathf.Abs(corrected.x - position.x) > .006f || Mathf.Abs(corrected.z - position.z) > .006f)
            {
                if (corrected.x != position.x && Mathf.Sign(velocity.x) == Mathf.Sign(position.x - corrected.x)) velocity.x *= -.35f;
                if (corrected.z != position.z && Mathf.Sign(velocity.z) == Mathf.Sign(position.z - corrected.z)) velocity.z *= -.35f;
                position = corrected;
                BoundaryRecoveries++;
            }
            else if (Time.inFixedTimeStep)
            {
                // 高空掠过低路沿时，在下个物理步跨线前反弹，避免出现一帧飞出画面。
                Vector3 projected = position + velocity * Time.fixedDeltaTime;
                Vector3 angular = Body.angularVelocity;
                Quaternion nextRotation = angular.sqrMagnitude < .001f ? Body.rotation :
                    Quaternion.AngleAxis(angular.magnitude * Time.fixedDeltaTime * Mathf.Rad2Deg, angular.normalized) * Body.rotation;
                HorizontalExtents(nextRotation, out float nextX, out float nextZ);
                Vector3 bounded = terrain.ClampInside(projected, Mathf.Max(extentX, nextX) + .004f, Mathf.Max(extentZ, nextZ) + .004f);
                float up = Mathf.Abs(Vector3.Dot(Body.rotation * Vector3.up, Vector3.up));
                float verticalExtent = Radius * Mathf.Sqrt(Mathf.Max(0, 1f - up * up)) + HalfHeight * up;
                float bottom = Mathf.Min(position.y, projected.y) - verticalExtent;
                bool reflected = false;
                if (bottom > .36f && Mathf.Abs(bounded.x - projected.x) > .002f && Mathf.Sign(velocity.x) == Mathf.Sign(projected.x - bounded.x))
                { velocity.x *= -.35f; reflected = true; }
                if (bottom > (projected.z < position.z ? .22f : .36f) && Mathf.Abs(bounded.z - projected.z) > .002f && Mathf.Sign(velocity.z) == Mathf.Sign(projected.z - bounded.z))
                { velocity.z *= -.35f; reflected = true; }
                if (reflected) { Body.linearVelocity = velocity; BoundaryRecoveries++; }
            }
            if (position.y < -.035f)
            {
                float upright = Mathf.Abs(Vector3.Dot(Body.rotation * Vector3.up, Vector3.up));
                float extent = Radius * Mathf.Sqrt(Mathf.Max(0, 1f - upright * upright)) + HalfHeight * upright;
                position.y = terrain.SurfaceHeight(position) + extent + .006f;
                if (velocity.y < 0) velocity.y *= -.13f;
                PenetrationRecoveries++;
            }
            if (position != Body.position)
            {
                Body.position = position;
                Body.linearVelocity = velocity;
                Body.WakeUp();
            }
        }

        void HorizontalExtents(Quaternion bodyRotation, out float extentX, out float extentZ)
        {
            extentX = extentZ = 0;
            foreach (BoxCollider collider in physicsColliders)
            {
                Quaternion rotation = bodyRotation * collider.transform.localRotation;
                Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
                Vector3 half = collider.size * .5f;
                extentX = Mathf.Max(extentX, Mathf.Abs(right.x) * half.x + Mathf.Abs(up.x) * half.y + Mathf.Abs(forward.x) * half.z);
                extentZ = Mathf.Max(extentZ, Mathf.Abs(right.z) * half.x + Mathf.Abs(up.z) * half.y + Mathf.Abs(forward.z) * half.z);
            }
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
            LastHitCollider = collision.collider;
            LastHitNormal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up;
            // 声音由对局统一节流。地面接触不应吞掉紧接着的目标接触与斜擦推力。
            if (speed > .5f) Hit?.Invoke(this, speed);
            ResolveSurfacePenetration(collision.collider);
            ConstrainToArena(Arena);
        }
        void OnCollisionStay(Collision collision) { ResolveSurfacePenetration(collision.collider); ConstrainToArena(Arena); }

        void ResolveSurfacePenetration(Collider surface)
        {
            if (Arena == null || !Arena.IsTerrainEnabled || Claimed || Body.isKinematic || surface.attachedRigidbody != null) return;
            float deepest = .012f; Vector3 correction = Vector3.zero;
            foreach (var shape in physicsColliders)
            {
                if (Physics.ComputePenetration(shape, Body.position + Body.rotation * shape.transform.localPosition,
                    Body.rotation * shape.transform.localRotation, surface, surface.transform.position, surface.transform.rotation,
                    out Vector3 normal, out float depth) && depth > deepest)
                { deepest = depth; correction = normal * (depth + .001f); }
            }
            if (correction == Vector3.zero) return;
            Body.position += correction;
            Vector3 direction = correction.normalized, velocity = Body.linearVelocity;
            float inward = Vector3.Dot(velocity, direction);
            if (inward < 0) Body.linearVelocity = velocity - direction * inward;
            PenetrationRecoveries++;
        }

        public void MarkClaimed()
        {
            // 隐藏 Rigidbody 后，Unity 可能读回零姿态；动画移动也不能修改稳定比赛快照。
            if (!Claimed) { claimedPosition = Body.position; claimedRotation = Body.rotation; }
            Claimed = true;
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; Body.isKinematic = true; }
            foreach (var collider in physicsColliders) collider.enabled = false;
            rimMaterial.color = new Color(1, 0.8f, 0.2f);
        }

        public void RestorePose(Vector3 position, Quaternion rotation, bool claimed)
        {
            var interpolation = Body.interpolation;
            Body.interpolation = RigidbodyInterpolation.None;
            foreach (var collider in physicsColliders) collider.enabled = false;
            gameObject.SetActive(true); transform.localScale = Vector3.one;
            Claimed = claimed; Body.isKinematic = claimed;
            if (claimed) { claimedPosition = position; claimedRotation = rotation; }
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position; Body.rotation = rotation;
            foreach (var collider in physicsColliders) collider.enabled = !claimed;
            if (!claimed)
            {
                Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            Body.interpolation = interpolation;
            if (claimed) MarkClaimed(); else Body.Sleep();
            gameObject.SetActive(!claimed);
        }

        private void OnDestroy()
        {
            if (artRenderer != null) foreach (var material in artRenderer.sharedMaterials) if (material != null) Destroy(material);
            var filter = GetComponent<MeshFilter>(); if (filter != null) Destroy(filter.sharedMesh);
            if (paperPhysics != null) Destroy(paperPhysics);
        }
    }
}
