using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    /// <summary>打片区域的实体地形；射线只查询地面，避免瞄准线误读圆片或路沿。</summary>
    public sealed class ArenaTerrain : MonoBehaviour
    {
        public const float MinX = -3.65f;
        public const float MaxX = 3.65f;
        public const float MinZ = -3.15f;
        public const float MaxZ = 3.5f;

        readonly List<Collider> surfaces = new List<Collider>();
        readonly List<Collider> boundaries = new List<Collider>();
        Collider baseFloor;
        Transform terrainRoot;

        public bool IsTerrainEnabled { get; private set; } = true;
        public string Description { get; internal set; }
        public IEnumerable<Collider> SurfaceColliders { get { return surfaces; } }
        public IEnumerable<Collider> BoundaryColliders { get { return boundaries; } }

        internal void Initialize(Collider floor, Transform geometryRoot)
        {
            baseFloor = floor;
            terrainRoot = geometryRoot;
            surfaces.Clear();
            boundaries.Clear();
            surfaces.Add(floor);
        }

        internal void RegisterSurface(Collider collider) { surfaces.Add(collider); }
        internal void RegisterBoundary(Collider collider) { boundaries.Add(collider); }

        /// <summary>弹珠玩法仍使用原来的整片平地；恢复打片时同时恢复实体地形和围边。</summary>
        public void SetEnabled(bool value)
        {
            bool changed = IsTerrainEnabled != value;
            IsTerrainEnabled = value;
            if (terrainRoot != null) terrainRoot.gameObject.SetActive(value);
            if (changed) Physics.SyncTransforms();
        }

        public bool Contains(Vector3 position, float margin = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            margin = Mathf.Clamp(margin, 0f, (MaxZ - MinZ) * .5f - .001f);
            return local.x >= MinX + margin && local.x <= MaxX - margin &&
                local.z >= MinZ + margin && local.z <= MaxZ - margin;
        }

        public Vector3 ClampInside(Vector3 position, float margin = 0f)
        {
            return ClampInside(position, margin, margin);
        }

        public Vector3 ClampInside(Vector3 position, float marginX, float marginZ)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            marginX = Mathf.Clamp(marginX, 0f, (MaxX - MinX) * .5f - .001f);
            marginZ = Mathf.Clamp(marginZ, 0f, (MaxZ - MinZ) * .5f - .001f);
            local.x = Mathf.Clamp(local.x, MinX + marginX, MaxX - marginX);
            local.z = Mathf.Clamp(local.z, MinZ + marginZ, MaxZ - marginZ);
            return transform.TransformPoint(local);
        }

        public float SurfaceHeight(Vector3 position)
        {
            SampleSurface(position, out float height, out Vector3 normal);
            return height;
        }

        public Vector3 SurfaceNormal(Vector3 position)
        {
            SampleSurface(position, out float height, out Vector3 normal);
            return normal;
        }

        /// <summary>返回可见实体顶面高程/法线；与碰撞共用几何，不使用额外的近似高度函数。</summary>
        public void SampleSurface(Vector3 position, out float height, out Vector3 normal)
        {
            height = baseFloor != null ? baseFloor.bounds.max.y : transform.position.y;
            normal = Vector3.up;
            var ray = new Ray(new Vector3(position.x, height + 3f, position.z), Vector3.down);
            float highest = float.NegativeInfinity;
            foreach (Collider surface in surfaces)
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                if (surface != baseFloor && !IsTerrainEnabled) continue;
                if (!surface.Raycast(ray, out RaycastHit hit, 8f) || hit.point.y <= highest) continue;
                highest = hit.point.y;
                height = hit.point.y;
                normal = hit.normal;
            }
        }
    }
}
