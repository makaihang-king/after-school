using System;
using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    public enum WorldCameraMode { Intro, Hub, Match, Collection }

    /// <summary>放学后的小镇布景与摄像机。比赛坐标始终保持在世界原点。</summary>
    public sealed class SceneWorld : MonoBehaviour
    {
        public Collider FloorCollider { get; private set; }
        public ArenaTerrain Terrain { get; private set; }
        public float SurfaceFriction { get; private set; } = 0.58f;
        public Camera MainCamera { get; private set; }
        public string LocationId { get; private set; } = "school";
        public bool IsBuilt { get; private set; }
        public bool ArenaBoundaryVisible { get; private set; } = true;
        public WorldCameraMode CameraMode { get; private set; } = WorldCameraMode.Hub;

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

        public void BuildWorld(string locationId)
        {
            LocationId = string.IsNullOrEmpty(locationId) ? "school" : locationId;
            ClearScenery();
            if (!capturedEnvironment)
            {
                previousAmbient = RenderSettings.ambientLight;
                previousSkybox = RenderSettings.skybox;
                capturedEnvironment = true;
            }
            if (signFont == null) signFont = FindChineseFont();
            palette = WorldPalette.Create(LocationId, Own);
            SurfaceFriction = palette.Friction;
            var root = new GameObject("放学后 · " + palette.LocationName);
            root.transform.SetParent(transform, false);
            scenery = root.transform;
            var builder = new NeighborhoodBuilder(scenery, palette, signFont, Own);
            FloorCollider = builder.Build();
            Terrain = scenery.GetComponent<ArenaTerrain>();
            var boundaryTransform = scenery.Find("圆片比赛的手画粉笔圈");
            arenaBoundary = boundaryTransform == null ? null : boundaryTransform.gameObject;
            var captionTransform = scenery.Find("地上的粉笔字");
            arenaCaption = captionTransform == null ? null : captionTransform.gameObject;
            SetArenaBoundaryVisible(ArenaBoundaryVisible);
            SetupAtmosphere();
            SetupCamera();
            SetupParticles();
            IsBuilt = true;
            SetCameraMode(CameraMode, true);
        }

        public void Rebuild(string locationId) { BuildWorld(locationId); }
        public void Rebuild() { BuildWorld(LocationId); }

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
            QualitySettings.shadowDistance = 48f;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowCascades = 2;
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
            sunlight.shadows = LightShadows.Soft;
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
                MainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            MainCamera.allowHDR = true;
            MainCamera.allowMSAA = true;
            MainCamera.nearClipPlane = 0.15f;
            MainCamera.farClipPlane = 110f;
            MainCamera.clearFlags = CameraClearFlags.Skybox;
            MainCamera.backgroundColor = new Color(0.73f, 0.82f, 0.88f);
            if (MainCamera.GetComponent<SummerImageFinish>() == null) MainCamera.gameObject.AddComponent<SummerImageFinish>();
            if (MainCamera.GetComponent<AudioListener>() == null && FindFirstObjectByType<AudioListener>() == null)
                MainCamera.gameObject.AddComponent<AudioListener>();
        }

        public void SetCameraMode(WorldCameraMode mode, bool immediate = false)
        {
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
                    desiredPosition = new Vector3(0f, 8.0f, -9.0f);
                    desiredTarget = new Vector3(0f, 0.2f, 1.0f);
                    desiredFov = 42f;
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
            if (immediate && MainCamera != null)
            {
                smoothPosition = desiredPosition;
                smoothTarget = desiredTarget;
                MainCamera.transform.position = smoothPosition;
                MainCamera.transform.rotation = Quaternion.LookRotation(smoothTarget - smoothPosition);
                MainCamera.fieldOfView = desiredFov;
            }
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
            if (CameraMode == WorldCameraMode.Intro)
            {
                introTime += Time.unscaledDeltaTime;
                float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(introTime / 12f));
                desiredPosition = Vector3.Lerp(new Vector3(9.2f, 7.1f, -10.9f), new Vector3(-0.7f, 8.1f, -11.8f), travel);
            }
            float smoothing = 1f - Mathf.Exp(-3.5f * Time.unscaledDeltaTime);
            smoothPosition = Vector3.Lerp(smoothPosition, desiredPosition, smoothing);
            smoothTarget = Vector3.Lerp(smoothTarget, desiredTarget, smoothing);
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
            MainCamera.transform.rotation = Quaternion.LookRotation(smoothTarget - smoothPosition);
            MainCamera.fieldOfView = Mathf.Lerp(MainCamera.fieldOfView, desiredFov, smoothing);
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

            var motes = CreateParticles("树荫下的光尘", material, new Color(1f, 0.9f, 0.67f, 0.3f), 0.012f, 0.027f, 0.025f, 0.08f);
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
            }
        }
    }
}
