using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    public enum WorldCameraMode { Intro, Hub, Match, Collection }

    /// <summary>放学后的小镇布景与摄像机。比赛坐标始终保持在世界原点。</summary>
    public sealed partial class SceneWorld : MonoBehaviour
    {
        public Collider FloorCollider { get; private set; }
        public ArenaTerrain Terrain { get; private set; }
        public float SurfaceFriction { get; private set; } = 0.58f;
        public Camera MainCamera { get; private set; }
        public string LocationId { get; private set; } = "school";
        public bool IsBuilt { get; private set; }
        public bool ArenaBoundaryVisible { get; private set; } = true;
        public WorldCameraMode CameraMode { get; private set; } = WorldCameraMode.Hub;
        public bool IsTopDownMatchView { get; private set; }
        public float MatchAzimuth { get; private set; }
        public float MatchElevation { get; private set; } = 40f;
        public float MatchDistance { get; private set; } = 12f;
        public float EffectiveMatchDistance
        {
            get
            {
                EvaluateMatchFrame(MatchAzimuth, EffectiveMatchElevation, MatchDistance, matchTarget, out _, out float distance, out _);
                return distance;
            }
        }

        Transform scenery;
        GameObject arenaBoundary;
        GameObject arenaCaption;
        WorldPalette palette;
        readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        Vector3 desiredPosition;
        Vector3 desiredTarget;
        Vector3 smoothPosition;
        Vector3 smoothTarget;
        float desiredFov = 43f;
        float shakeTime;
        float shakeDuration;
        float shakeStrength;
        float introTime;
        ParticleSystem impactDust;
        ParticleSystem impactChips;
        Font signFont;
        Light sunlight;
        Color previousAmbient;
        Material previousSkybox;
        bool capturedEnvironment;
        static readonly Vector3 matchTarget = new Vector3(0f, .2f, .55f);
        const float matchTargetOffset = .8f;
        // 包含围场路沿外侧。整个场地留在底部操作栏上方，出手片另留完整半径余量。
        static readonly Vector3[] matchFrameCorners =
        {
            new Vector3(ArenaTerrain.MinX - .26f, -.01f, ArenaTerrain.MinZ - .26f),
            new Vector3(ArenaTerrain.MinX - .26f, -.01f, ArenaTerrain.MaxZ + .26f),
            new Vector3(ArenaTerrain.MaxX + .26f, -.01f, ArenaTerrain.MinZ - .26f),
            new Vector3(ArenaTerrain.MaxX + .26f, -.01f, ArenaTerrain.MaxZ + .26f),
            new Vector3(ArenaTerrain.MinX - .26f, .5f, ArenaTerrain.MinZ - .26f),
            new Vector3(ArenaTerrain.MinX - .26f, .5f, ArenaTerrain.MaxZ + .26f),
            new Vector3(ArenaTerrain.MaxX + .26f, .5f, ArenaTerrain.MinZ - .26f),
            new Vector3(ArenaTerrain.MaxX + .26f, .5f, ArenaTerrain.MaxZ + .26f)
        };
        Vector3 smoothMatchCenter = matchTarget;
        float smoothMatchAzimuth;
        float smoothMatchElevation = 40f;
        float smoothMatchDistance = 12f;
        bool matchInteractionLocked;
        readonly List<MatchDecoration> matchDecorations = new List<MatchDecoration>();
        readonly List<MatchDecorationGroup> matchDecorationGroups = new List<MatchDecorationGroup>();
        Vector3 lastDecorationCameraPosition;
        Quaternion lastDecorationCameraRotation;
        Rect lastProtectedMatchRect;
        float lastProtectedMatchDepth, lastDecorationCameraFov, lastDecorationTime;
        PogMatch sightingMatch;
        bool decorationViewValid;

        sealed class MatchDecoration
        {
            public MeshRenderer Renderer;
            public bool WasEnabled;
            public bool CanOcclude;
            public UnityEngine.Rendering.ShadowCastingMode ShadowMode;
        }

        sealed class MatchDecorationGroup
        {
            public readonly List<MatchDecoration> Members = new List<MatchDecoration>();
        }

        struct MatchProjection
        {
            public Vector3 Position, Forward, Right, Up;
            public float Tangent, Aspect, Near;
        }

        public void BuildWorld(string locationId)
        {
            // 旧 QA 与 schema1 的布局入口保留精确旧几何。
            BuildWorld(new EnvironmentContext { locationId = string.IsNullOrEmpty(locationId) ? "school_gate" : locationId, geometryVersion = 1 });
        }

        public void BuildWorld(EnvironmentContext context)
        {
            Environment = CopyEnvironment(context);
            LocationId = Environment.locationId;
            ClearScenery();
            if (!capturedEnvironment)
            {
                previousAmbient = RenderSettings.ambientLight;
                previousSkybox = RenderSettings.skybox;
                CaptureAtmosphere();
                capturedEnvironment = true;
            }
            if (signFont == null) signFont = FindChineseFont();
            palette = WorldPalette.Create(Environment, Own);
            SurfaceFriction = palette.Friction;
            var root = new GameObject("放学后 · " + palette.LocationName);
            root.transform.SetParent(transform, false);
            scenery = root.transform;
            var builder = new NeighborhoodBuilder(scenery, palette, signFont, Own, Environment);
            FloorCollider = builder.Build();
            Terrain = scenery.GetComponent<ArenaTerrain>();
            CacheMatchDecorations();
            var boundaryTransform = scenery.Find("圆片比赛的手画粉笔圈");
            arenaBoundary = boundaryTransform == null ? null : boundaryTransform.gameObject;
            var captionTransform = scenery.Find("地上的粉笔字");
            arenaCaption = captionTransform == null ? null : captionTransform.gameObject;
            SetArenaBoundaryVisible(ArenaBoundaryVisible);
            SetupAtmosphere();
            SetupCamera();
            SetupParticles();
            SetupWeatherParticles();
            IsBuilt = true;
            ApplyAtmosphere(Environment);
            SetCameraMode(CameraMode, true);
        }

        public void Rebuild(string locationId) { BuildWorld(locationId); }
        public void Rebuild(EnvironmentContext context) { BuildWorld(context); }
        public void Rebuild() { BuildWorld(Environment); }

        public float SurfaceHeight(Vector3 position) { return Terrain == null ? 0f : Terrain.SurfaceHeight(position); }
        public Vector3 SurfaceNormal(Vector3 position) { return Terrain == null ? Vector3.up : Terrain.SurfaceNormal(position); }
        public Vector3 SurfacePoint(Vector3 position, float clearance = .02f)
        {
            position.y = SurfaceHeight(position) + clearance;
            return position;
        }
        public void SetPogTerrainEnabled(bool enabled) { if (Terrain != null) Terrain.SetEnabled(enabled); }

        /// <summary>切换街区装饰粉笔圈；具体玩法绘制自己的真实得分边界时可隐藏它。</summary>
        public void SetArenaBoundaryVisible(bool visible)
        {
            ArenaBoundaryVisible = visible;
            if (arenaBoundary != null) arenaBoundary.SetActive(visible);
            UpdateArenaCaption();
        }

        void UpdateArenaCaption()
        {
            if (arenaCaption != null) arenaCaption.SetActive(ArenaBoundaryVisible &&
                (CameraMode == WorldCameraMode.Hub || CameraMode == WorldCameraMode.Intro));
        }

        T Own<T>(T resource) where T : UnityEngine.Object
        {
            if (resource != null) ownedResources.Add(resource);
            return resource;
        }

        UnityEngine.Object Own(UnityEngine.Object resource)
        {
            if (resource != null) ownedResources.Add(resource);
            return resource;
        }

        static Font FindChineseFont()
        {
            Font bundled = Resources.Load<Font>("Fonts/Chinese");
            if (bundled != null) return bundled;
            string[] preferred = { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Noto Sans CJK SC", "PingFang SC", "Arial" };
            foreach (var family in preferred)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(family, 64);
                    if (font != null) return font;
                }
                catch (Exception) { }
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void SetupAtmosphere()
        {
            bool mobile = Application.isMobilePlatform;
            QualitySettings.shadowDistance = mobile ? 20f : 48f;
            QualitySettings.shadows = mobile ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = mobile ? ShadowResolution.Low : ShadowResolution.High;
            QualitySettings.shadowCascades = mobile ? 1 : 2;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.59f, 0.61f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.34f, 0.32f);
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.74f, 0.80f, 0.82f);
            RenderSettings.fogDensity = 0.014f;

            var lightObject = new GameObject("傍晚 · 暖阳");
            lightObject.transform.SetParent(scenery, false);
            sunlight = lightObject.AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.color = new Color(1f, 0.88f, 0.73f);
            sunlight.intensity = 1.30f;
            sunlight.shadows = mobile ? LightShadows.Hard : LightShadows.Soft;
            sunlight.shadowStrength = 0.74f;
            sunlight.shadowBias = 0.035f;
            sunlight.shadowNormalBias = 0.28f;
            lightObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
            RenderSettings.sun = sunlight;

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = Own(new Material(skyShader));
                sky.name = "傍晚的天空";
                sky.SetColor("_SkyTint", new Color(0.64f, 0.77f, 0.95f));
                sky.SetColor("_GroundColor", new Color(0.73f, 0.76f, 0.72f));
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetFloat("_Exposure", 1.18f);
                RenderSettings.skybox = sky;
            }
        }

        void SetupCamera()
        {
            if (MainCamera == null) MainCamera = Camera.main;
            if (MainCamera == null)
            {
                var cameraObject = new GameObject("放学后 · 主镜头");
                cameraObject.transform.SetParent(transform, false);
                MainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            MainCamera.allowHDR = !Application.isMobilePlatform;
            MainCamera.allowMSAA = !Application.isMobilePlatform;
            MainCamera.nearClipPlane = 0.15f;
            MainCamera.farClipPlane = 110f;
            MainCamera.clearFlags = CameraClearFlags.Skybox;
            MainCamera.backgroundColor = new Color(0.73f, 0.82f, 0.88f);
            if (!Application.isMobilePlatform && MainCamera.GetComponent<SummerImageFinish>() == null)
                MainCamera.gameObject.AddComponent<SummerImageFinish>();
            if (MainCamera.GetComponent<AudioListener>() == null && FindFirstObjectByType<AudioListener>() == null)
                MainCamera.gameObject.AddComponent<AudioListener>();
        }

        public void SetCameraMode(WorldCameraMode mode, bool immediate = false)
        {
            WorldCameraMode previousMode = CameraMode;
            CameraMode = mode;
            UpdateArenaCaption();
            introTime = 0f;
            switch (mode)
            {
                case WorldCameraMode.Intro:
                    desiredPosition = new Vector3(9.2f, 7.1f, -10.9f);
                    desiredTarget = new Vector3(0.2f, 1.2f, 2.0f);
                    desiredFov = 44f;
                    break;
                case WorldCameraMode.Match:
                    UpdateDesiredMatchCamera();
                    if (previousMode != WorldCameraMode.Match && !immediate && MainCamera != null)
                        CaptureSmoothMatchOrbit(MainCamera.transform.position - smoothTarget);
                    break;
                case WorldCameraMode.Collection:
                    desiredPosition = new Vector3(-0.8f, 6.6f, -8.2f);
                    desiredTarget = new Vector3(0f, 0.15f, 0.3f);
                    desiredFov = 40f;
                    break;
                default:
                    desiredPosition = new Vector3(0f, 8.2f, -11.8f);
                    desiredTarget = new Vector3(0f, 0.8f, 2.0f);
                    desiredFov = 44f;
                    break;
            }
            ApplyCameraEnvironment(mode);
            if (mode != WorldCameraMode.Match) RestoreMatchDecorations();
            else decorationViewValid = false;
            if (mode == WorldCameraMode.Match && matchInteractionLocked) immediate = false;
            if (immediate && MainCamera != null)
            {
                if (mode == WorldCameraMode.Match)
                {
                    smoothMatchAzimuth = MatchAzimuth;
                    smoothMatchElevation = EffectiveMatchElevation;
                    smoothMatchDistance = EffectiveMatchDistance;
                    smoothMatchCenter = matchTarget;
                }
                smoothPosition = desiredPosition;
                smoothTarget = desiredTarget;
                MainCamera.transform.position = smoothPosition;
                MainCamera.transform.rotation = CameraRotation(smoothTarget - smoothPosition);
                MainCamera.fieldOfView = desiredFov;
                if (mode == WorldCameraMode.Match) UpdateMatchDecorations(smoothPosition);
            }
        }

        /// <summary>站位与镜头共用一个方位角；0°在场地前方，正角绕世界 Y 轴旋转。</summary>
        public void SetMatchOrbit(float azimuth, float elevation, float distance, bool immediate = false)
        {
            if (!float.IsNaN(azimuth) && !float.IsInfinity(azimuth)) MatchAzimuth = Mathf.Repeat(azimuth, 360f);
            if (!float.IsNaN(elevation) && !float.IsInfinity(elevation)) MatchElevation = Mathf.Clamp(elevation, 32f, 72f);
            if (!float.IsNaN(distance) && !float.IsInfinity(distance)) MatchDistance = Mathf.Clamp(distance, 10f, 15f);
            if (CameraMode == WorldCameraMode.Match) SetCameraMode(WorldCameraMode.Match, immediate);
        }

        public void ResetMatchOrbit(bool immediate = false)
        {
            IsTopDownMatchView = false;
            SetMatchOrbit(0f, 40f, 12f, immediate);
        }

        /// <summary>蓄力起点确定后冻结镜头，直到出手或取消，保持屏幕拖拽映射稳定。</summary>
        public void SetMatchInteractionLocked(bool locked)
        {
            matchInteractionLocked = locked;
        }

        float EffectiveMatchElevation { get { return IsTopDownMatchView ? 90f : MatchElevation; } }

        void UpdateDesiredMatchCamera()
        {
            EvaluateMatchFrame(MatchAzimuth, EffectiveMatchElevation, MatchDistance, matchTarget,
                out desiredTarget, out float distance, out desiredFov);
            desiredPosition = desiredTarget + MatchOrbitOffset(MatchAzimuth, EffectiveMatchElevation, distance);
        }

        void EvaluateMatchFrame(float azimuth, float elevation, float requestedDistance, Vector3 center,
            out Vector3 target, out float distance, out float fov)
        {
            Quaternion turn = Quaternion.Euler(0f, azimuth, 0f);
            Vector3 side = turn * Vector3.back;
            Vector3 right = turn * Vector3.right;
            float radians = elevation * Mathf.Deg2Rad;
            Vector3 forward = -side * Mathf.Cos(radians) - Vector3.up * Mathf.Sin(radians);
            Vector3 up = -side * Mathf.Sin(radians) + Vector3.up * Mathf.Cos(radians);
            target = center + side * matchTargetOffset;
            float aspect = MainCamera == null ? 16f / 9f : Mathf.Max(.5f, MainCamera.aspect);
            float baseFov = IsTopDownMatchView ? 48f : 44f;
            float tangent = Mathf.Tan(baseFov * .5f * Mathf.Deg2Rad);
            // 所有片角都先由 RestHeight 落到地形上；最近的地面投影点保守覆盖 .31m 片半径。
            Vector3 launchSide = Quaternion.Euler(0f, MatchAzimuth, 0f) * Vector3.back;
            Vector3 launchEdge = new Vector3(0f, -.01f, .55f) + launchSide * 2.8f + side * .31f;
            // 俯视时抬起的片缘离镜头更近；覆盖地形高程与所有倾角的上缘高度。
            Vector3 raisedLaunchEdge = launchEdge + Vector3.up * 1.01f;
            float minimum = RequiredMatchDistance(launchEdge - target, forward, up, right, tangent, aspect, .305f);
            minimum = Mathf.Max(minimum, RequiredMatchDistance(raisedLaunchEdge - target, forward, up, right, tangent, aspect, .305f));
            foreach (Vector3 corner in matchFrameCorners)
                minimum = Mathf.Max(minimum, RequiredMatchDistance(corner - target, forward, up, right, tangent, aspect, .24f));
            distance = Mathf.Clamp(Mathf.Max(requestedDistance, minimum), 10f, 15f);
            // 对角站位同时看到矩形近角时，先退到安全距离，再适度扩大镜头视野。
            tangent = Mathf.Max(tangent, RequiredMatchTangent(launchEdge - target, forward, up, right, distance, aspect, .305f));
            tangent = Mathf.Max(tangent, RequiredMatchTangent(raisedLaunchEdge - target, forward, up, right, distance, aspect, .305f));
            foreach (Vector3 corner in matchFrameCorners)
                tangent = Mathf.Max(tangent, RequiredMatchTangent(corner - target, forward, up, right, distance, aspect, .24f));
            fov = Mathf.Atan(tangent) * 2f * Mathf.Rad2Deg;
        }

        static float RequiredMatchDistance(Vector3 offset, Vector3 forward, Vector3 up, Vector3 right,
            float tangent, float aspect, float bottom)
        {
            float vertical = Vector3.Dot(offset, up);
            float verticalRoom = vertical < 0f ? .5f - bottom : .45f;
            float verticalDistance = Mathf.Abs(vertical) / (2f * verticalRoom * tangent);
            float horizontalDistance = Mathf.Abs(Vector3.Dot(offset, right)) / (.94f * aspect * tangent);
            return Mathf.Max(verticalDistance, horizontalDistance) - Vector3.Dot(offset, forward);
        }

        static float RequiredMatchTangent(Vector3 offset, Vector3 forward, Vector3 up, Vector3 right,
            float distance, float aspect, float bottom)
        {
            float depth = Mathf.Max(.1f, distance + Vector3.Dot(offset, forward));
            float vertical = Vector3.Dot(offset, up);
            float verticalRoom = vertical < 0f ? .5f - bottom : .45f;
            return Mathf.Max(Mathf.Abs(vertical) / (2f * verticalRoom * depth),
                Mathf.Abs(Vector3.Dot(offset, right)) / (.94f * aspect * depth));
        }

        static Vector3 MatchOrbitOffset(float azimuth, float elevation, float distance)
        {
            float radians = elevation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(0f, Mathf.Sin(radians) * distance, -Mathf.Cos(radians) * distance);
            return Quaternion.Euler(0f, azimuth, 0f) * offset;
        }

        void CaptureSmoothMatchOrbit(Vector3 offset)
        {
            float planar = new Vector2(offset.x, offset.z).magnitude;
            smoothMatchDistance = Mathf.Max(.1f, offset.magnitude);
            smoothMatchElevation = Mathf.Atan2(offset.y, planar) * Mathf.Rad2Deg;
            smoothMatchAzimuth = planar > .001f ? Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg : MatchAzimuth;
            smoothMatchCenter = smoothTarget - Quaternion.Euler(0f, smoothMatchAzimuth, 0f) * Vector3.back * matchTargetOffset;
        }

        Quaternion CameraRotation(Vector3 forward)
        {
            if (CameraMode != WorldCameraMode.Match) return Quaternion.LookRotation(forward);
            // 在正上方也显式保留方位：屏幕上边始终指向当前站位的对侧。
            Vector3 screenUp = Quaternion.Euler(0f, smoothMatchAzimuth, 0f) * Vector3.forward;
            return Quaternion.LookRotation(forward, screenUp);
        }

        void CacheMatchDecorations()
        {
            matchDecorations.Clear();
            matchDecorationGroups.Clear();
            if (scenery == null) return;
            Transform arenaGeometry = scenery.Find("打片 · 实体地形与路沿边界");
            var groups = new Dictionary<Transform, MatchDecorationGroup>();
            var cables = new Dictionary<string, MatchDecorationGroup>();
            foreach (MeshRenderer renderer in scenery.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null || !renderer.enabled ||
                    (FloorCollider != null && renderer.transform == FloorCollider.transform) ||
                    (arenaGeometry != null && renderer.transform.IsChildOf(arenaGeometry))) continue;
                bool pedestrian = false;
                for (Transform parent = renderer.transform; parent != null && parent != scenery; parent = parent.parent)
                    if (parent.name.StartsWith("路人", StringComparison.Ordinal)) { pedestrian = true; break; }
                if (pedestrian) continue;
                Transform groupRoot = renderer.transform;
                while (groupRoot.parent != null && groupRoot.parent != scenery) groupRoot = groupRoot.parent;
                MatchDecorationGroup group;
                // Cable 的细段直接挂在 scenery 下；同名的一组线保持完整剖面，避免悬空断线。
                bool cable = groupRoot == renderer.transform &&
                    (groupRoot.name.Contains("电线") || groupRoot.name.Contains("晾衣绳"));
                if (cable)
                {
                    if (!cables.TryGetValue(groupRoot.name, out group))
                    { group = new MatchDecorationGroup(); cables.Add(groupRoot.name, group); matchDecorationGroups.Add(group); }
                }
                else if (!groups.TryGetValue(groupRoot, out group))
                { group = new MatchDecorationGroup(); groups.Add(groupRoot, group); matchDecorationGroups.Add(group); }
                var decoration = new MatchDecoration
                {
                    Renderer = renderer, WasEnabled = renderer.enabled, ShadowMode = renderer.shadowCastingMode,
                    CanOcclude = renderer.bounds.max.y > .5f && renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                };
                group.Members.Add(decoration); matchDecorations.Add(decoration);
            }
            matchDecorationGroups.RemoveAll(group => !group.Members.Exists(member => member.CanOcclude));
            sightingMatch = GetComponent<PogMatch>();
            decorationViewValid = false;
        }

        void UpdateMatchDecorations(Vector3 cameraPosition)
        {
            if (MainCamera == null) return;
            var projection = new MatchProjection
            {
                Position = cameraPosition, Forward = MainCamera.transform.forward,
                Right = MainCamera.transform.right, Up = MainCamera.transform.up,
                Tangent = Mathf.Tan(MainCamera.fieldOfView * .5f * Mathf.Deg2Rad),
                Aspect = Mathf.Max(.5f, MainCamera.aspect), Near = MainCamera.nearClipPlane
            };
            Rect protectedRect = new Rect();
            float protectedDepth = 0f;
            bool hasProtectedRect = false;
            foreach (Vector3 corner in matchFrameCorners)
                IncludeMatchProjection(corner, projection, ref protectedRect, ref protectedDepth, ref hasProtectedRect);
            if (sightingMatch == null) sightingMatch = GetComponent<PogMatch>();
            if (sightingMatch != null)
            {
                IncludeMatchDisc(sightingMatch.Striker, projection, ref protectedRect, ref protectedDepth, ref hasProtectedRect);
                foreach (DiscBody target in sightingMatch.Targets)
                    IncludeMatchDisc(target, projection, ref protectedRect, ref protectedDepth, ref hasProtectedRect);
            }
            if (!hasProtectedRect) return;
            protectedRect = Rect.MinMaxRect(protectedRect.xMin - .02f, protectedRect.yMin - .02f,
                protectedRect.xMax + .02f, protectedRect.yMax + .02f);
            bool stableView = decorationViewValid && (cameraPosition - lastDecorationCameraPosition).sqrMagnitude < .0004f &&
                Quaternion.Angle(MainCamera.transform.rotation, lastDecorationCameraRotation) < .1f &&
                Mathf.Abs(MainCamera.fieldOfView - lastDecorationCameraFov) < .05f &&
                Mathf.Abs(protectedRect.xMin - lastProtectedMatchRect.xMin) < .002f &&
                Mathf.Abs(protectedRect.xMax - lastProtectedMatchRect.xMax) < .002f &&
                Mathf.Abs(protectedRect.yMin - lastProtectedMatchRect.yMin) < .002f &&
                Mathf.Abs(protectedRect.yMax - lastProtectedMatchRect.yMax) < .002f &&
                Mathf.Abs(protectedDepth - lastProtectedMatchDepth) < .015f;
            // 静止镜头仍定期检查风动叶团和真实片姿态，不能因相机不动永久漏掉遮挡。
            if (stableView && Time.unscaledTime - lastDecorationTime < .08f) return;
            lastDecorationCameraPosition = cameraPosition;
            lastDecorationCameraRotation = MainCamera.transform.rotation;
            lastDecorationCameraFov = MainCamera.fieldOfView;
            lastProtectedMatchRect = protectedRect;
            lastProtectedMatchDepth = protectedDepth;
            lastDecorationTime = Time.unscaledTime;
            decorationViewValid = true;
            foreach (MatchDecorationGroup group in matchDecorationGroups)
            {
                bool obscuresArena = false;
                foreach (MatchDecoration decoration in group.Members)
                {
                    MeshRenderer renderer = decoration.Renderer;
                    if (renderer == null || !decoration.CanOcclude) continue;
                    Bounds bounds = renderer.bounds;
                    bounds.Expand(.10f);
                    if (bounds.Contains(cameraPosition) || (ProjectMatchBounds(bounds, projection, out Rect screenBounds, out float nearDepth) &&
                        nearDepth < protectedDepth - .03f && protectedRect.Overlaps(screenBounds)))
                    { obscuresArena = true; break; }
                }
                foreach (MatchDecoration decoration in group.Members)
                {
                    if (decoration.Renderer == null) continue;
                    // 保留原有投影。原本不投影的组件不额外开启阴影，移动分支也保持原预算。
                    bool keepShadow = decoration.ShadowMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                    decoration.Renderer.enabled = decoration.WasEnabled && (!obscuresArena || keepShadow);
                    decoration.Renderer.shadowCastingMode = obscuresArena && keepShadow
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : decoration.ShadowMode;
                }
            }
        }

        static void IncludeMatchDisc(DiscBody disc, MatchProjection projection, ref Rect rect, ref float depth, ref bool hasRect)
        {
            if (disc == null || disc.Claimed || !disc.gameObject.activeInHierarchy) return;
            float extent = disc.Radius + disc.HalfHeight + .025f;
            Bounds bounds = new Bounds(disc.transform.position, Vector3.one * extent * 2f);
            for (int i = 0; i < 8; i++)
                IncludeMatchProjection(BoundsCorner(bounds, i), projection, ref rect, ref depth, ref hasRect);
        }

        static void IncludeMatchProjection(Vector3 point, MatchProjection projection, ref Rect rect, ref float depth, ref bool hasRect)
        {
            Vector3 offset = point - projection.Position;
            float z = Vector3.Dot(offset, projection.Forward);
            if (z <= projection.Near) return;
            float x = .5f + Vector3.Dot(offset, projection.Right) / (2f * z * projection.Tangent * projection.Aspect);
            float y = .5f + Vector3.Dot(offset, projection.Up) / (2f * z * projection.Tangent);
            rect = hasRect ? Rect.MinMaxRect(Mathf.Min(rect.xMin, x), Mathf.Min(rect.yMin, y),
                Mathf.Max(rect.xMax, x), Mathf.Max(rect.yMax, y)) : Rect.MinMaxRect(x, y, x, y);
            depth = Mathf.Max(depth, z); hasRect = true;
        }

        static Vector3 BoundsCorner(Bounds bounds, int index)
        {
            Vector3 extent = bounds.extents;
            return bounds.center + new Vector3((index & 1) == 0 ? -extent.x : extent.x,
                (index & 2) == 0 ? -extent.y : extent.y, (index & 4) == 0 ? -extent.z : extent.z);
        }

        static bool ProjectMatchBounds(Bounds bounds, MatchProjection projection, out Rect rect, out float nearDepth)
        {
            rect = new Rect(); nearDepth = float.PositiveInfinity;
            float farDepth = float.NegativeInfinity, ignoredDepth = 0f;
            bool hasRect = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = BoundsCorner(bounds, i);
                float z = Vector3.Dot(corner - projection.Position, projection.Forward);
                nearDepth = Mathf.Min(nearDepth, z); farDepth = Mathf.Max(farDepth, z);
                IncludeMatchProjection(corner, projection, ref rect, ref ignoredDepth, ref hasRect);
            }
            if (farDepth <= projection.Near) return false;
            // 跨过相机近裁面的墙面按覆盖视野处理，避免身处楼体时透视翻转漏判。
            if (nearDepth <= projection.Near) rect = Rect.MinMaxRect(-10f, -10f, 10f, 10f);
            return hasRect || nearDepth <= projection.Near;
        }

        void RestoreMatchDecorations()
        {
            foreach (MatchDecoration decoration in matchDecorations)
                if (decoration.Renderer != null)
                {
                    decoration.Renderer.enabled = decoration.WasEnabled;
                    decoration.Renderer.shadowCastingMode = decoration.ShadowMode;
                }
            decorationViewValid = false;
        }

        public void SetTopDownMatchView(bool enabled, bool immediate = false)
        {
            IsTopDownMatchView = enabled;
            if (CameraMode == WorldCameraMode.Match) SetCameraMode(WorldCameraMode.Match, immediate);
        }

        public void ToggleTopDownMatchView()
        {
            SetTopDownMatchView(!IsTopDownMatchView);
        }

        public void Shake(float strength = 0.12f, float duration = 0.2f)
        {
            shakeStrength = Mathf.Max(shakeStrength, Mathf.Clamp(strength, 0f, 0.65f));
            shakeTime = shakeDuration = Mathf.Max(duration, 0.01f);
        }

        /// <summary>一次落片或撞击的扬尘、细小纸屑和镜头反馈。</summary>
        public void Punch(Vector3 worldPoint, float strength = 1f)
        {
            strength = Mathf.Clamp(strength, 0.15f, 3f);
            worldPoint = SurfacePoint(worldPoint, .025f);
            if (impactDust != null)
            {
                impactDust.transform.position = worldPoint;
                impactDust.Emit(Mathf.RoundToInt(7f + strength * 8f));
            }
            if (impactChips != null)
            {
                impactChips.transform.position = worldPoint;
                impactChips.Emit(Mathf.RoundToInt(3f + strength * 4f));
            }
            Shake(0.04f + strength * 0.04f, 0.14f + strength * 0.03f);
        }

        void LateUpdate()
        {
            if (!IsBuilt || MainCamera == null) return;
            if (CameraMode == WorldCameraMode.Match && Time.timeScale <= 0f) return;
            if (CameraMode == WorldCameraMode.Match && matchInteractionLocked)
            { UpdateMatchDecorations(MainCamera.transform.position); return; }
            if (CameraMode == WorldCameraMode.Intro)
            {
                introTime += Time.unscaledDeltaTime;
                float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(introTime / 12f));
                desiredPosition = Vector3.Lerp(introStart, introEnd, travel);
            }
            float smoothing = 1f - Mathf.Exp(-3.5f * Time.unscaledDeltaTime);
            float currentMatchFov = 0f;
            if (CameraMode == WorldCameraMode.Match)
            {
                // 插值球面参数，180°换边也只绕场地，不从场地中央穿过去。
                smoothMatchAzimuth = Mathf.LerpAngle(smoothMatchAzimuth, MatchAzimuth, smoothing);
                smoothMatchElevation = Mathf.Lerp(smoothMatchElevation, EffectiveMatchElevation, smoothing);
                smoothMatchDistance = Mathf.Lerp(smoothMatchDistance, EffectiveMatchDistance, smoothing);
                smoothMatchCenter = Vector3.Lerp(smoothMatchCenter, matchTarget, smoothing);
                EvaluateMatchFrame(smoothMatchAzimuth, smoothMatchElevation, smoothMatchDistance, smoothMatchCenter,
                    out smoothTarget, out float distance, out currentMatchFov);
                smoothPosition = smoothTarget + MatchOrbitOffset(smoothMatchAzimuth, smoothMatchElevation, distance);
            }
            else
            {
                smoothTarget = Vector3.Lerp(smoothTarget, desiredTarget, smoothing);
                smoothPosition = Vector3.Lerp(smoothPosition, desiredPosition, smoothing);
            }
            Vector3 wobble = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float envelope = Mathf.Clamp01(shakeTime / shakeDuration);
                float t = Time.unscaledTime * 53f;
                wobble = new Vector3(Mathf.Sin(t * 1.31f), Mathf.Cos(t * 1.73f) * .7f, Mathf.Sin(t * 2.07f) * 0.15f) * shakeStrength * envelope * envelope;
            }
            else shakeStrength = 0f;
            MainCamera.transform.position = smoothPosition + wobble;
            MainCamera.transform.rotation = CameraRotation(smoothTarget - smoothPosition);
            MainCamera.fieldOfView = Mathf.Max(currentMatchFov, Mathf.Lerp(MainCamera.fieldOfView, desiredFov, smoothing));
            if (CameraMode == WorldCameraMode.Match) UpdateMatchDecorations(smoothPosition + wobble);
        }

        void SetupParticles()
        {
            Shader particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null) particleShader = Shader.Find("Sprites/Default");
            var material = Own(new Material(particleShader));
            material.name = "阳光中漂浮的灰尘";
            material.mainTexture = Own(WorldPalette.SoftParticleTexture());
            material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 2f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
            }
            impactDust = CreateParticles("落片扬尘", material, new Color(0.91f, 0.83f, 0.70f, 0.35f), 0.07f, 0.23f, 0.35f, 0.65f);
            var dustMain = impactDust.main;
            dustMain.gravityModifier = 0.08f;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(.35f,.7f);
            var size = impactDust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.65f)));
            var chipMaterial = Own(new Material(material));
            chipMaterial.name = "细碎纸纤维";
            chipMaterial.mainTexture = Own(WorldPalette.PaperChipTexture());
            impactChips = CreateParticles("落片纸屑", chipMaterial, new Color(.95f, .89f, .73f, .9f), 0.022f, 0.048f, .85f, 1.25f);
            var chipsMain = impactChips.main;
            chipsMain.gravityModifier = 0.75f;
            chipsMain.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var spin = impactChips.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f,6f);
            var chipsShape = impactChips.shape;
            chipsShape.angle = 66f;

            motes = CreateParticles("树荫下的光尘", material, new Color(1f, 0.9f, 0.67f, 0.3f), 0.012f, 0.027f, 0.025f, 0.08f);
            motes.transform.position = new Vector3(0f, 2.4f, 3f);
            var main = motes.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.maxParticles = 80;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 5f, 9f);
            var emission = motes.emission;
            emission.enabled = true;
            emission.rateOverTime = 5f;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.3f;
            motes.Play();
        }

        ParticleSystem CreateParticles(string name, Material material, Color color, float minSize, float maxSize, float minSpeed, float maxSpeed)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(scenery, false);
            obj.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var particles = obj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startColor = color;
            main.maxParticles = 160;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 79f;
            shape.radius = 0.13f;
            var emission = particles.emission;
            emission.enabled = false;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return particles;
        }

        void ClearScenery()
        {
            RestoreMatchDecorations();
            matchDecorations.Clear();
            matchDecorationGroups.Clear(); sightingMatch = null;
            matchInteractionLocked = false;
            rain = null;
            motes = null;
            FloorCollider = null;
            Terrain = null;
            arenaBoundary = null;
            arenaCaption = null;
            IsBuilt = false;
            if (scenery != null)
            {
                scenery.gameObject.SetActive(false);
                Destroy(scenery.gameObject);
            }
            for (int i = 0; i < ownedResources.Count; i++) if (ownedResources[i] != null) Destroy(ownedResources[i]);
            ownedResources.Clear();
        }

        void OnDestroy()
        {
            ClearScenery();
            if (capturedEnvironment)
            {
                RenderSettings.skybox = previousSkybox;
                RenderSettings.ambientLight = previousAmbient;
                RestoreAtmosphere();
            }
        }
    }
}
