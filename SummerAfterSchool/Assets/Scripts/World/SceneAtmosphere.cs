using UnityEngine;
using UnityEngine.Rendering;

namespace Childhood
{
    public sealed partial class SceneWorld
    {
        public EnvironmentContext Environment { get; private set; }
        public EnvironmentContext CurrentEnvironment { get { return Environment; } }
        public Transform ActiveScenery { get { return scenery; } }
        public int OwnedResourceCount { get { return ownedResources.Count; } }
        public UnityEngine.Object[] OwnedResourceSnapshot() { return ownedResources.ToArray(); }
        public bool IsSheltered { get { return LocationId == "shop" || LocationId == "corridor"; } }
        public bool SurfaceWet { get { return Environment != null && Environment.weather == WeatherKind.Rainy && !IsSheltered; } }
        public bool RainActive { get { return rain != null && rain.isPlaying; } }
        public string EnvironmentSignature { get { return Environment == null ? LocationId : LocationId + ":" + Environment.visitVariant + ":" + Environment.geometryVersion + ":" + Environment.day + ":" + Environment.period + ":" + Environment.weather + ":" + Environment.weatherSeed; } }

        ParticleSystem rain, motes;
        Vector3 introStart = new Vector3(9.2f,7.1f,-10.9f), introEnd = new Vector3(-.7f,8.1f,-11.8f);
        AmbientMode previousAmbientMode;
        Color previousSkyColor, previousEquatorColor, previousGroundColor, previousFogColor;
        float previousAmbientIntensity, previousFogDensity;
        bool previousFog;
        FogMode previousFogMode;
        Light previousSun;
        float previousShadowDistance;
        ShadowQuality previousShadowQuality;
        ShadowResolution previousShadowResolution;
        int previousShadowCascades;

        static EnvironmentContext CopyEnvironment(EnvironmentContext value)
        {
            if(value == null) return new EnvironmentContext();
            return new EnvironmentContext
            {
                locationId=string.IsNullOrEmpty(value.locationId)?"home":value.locationId,
                visitVariant=Mathf.Clamp(value.visitVariant,0,2), day=value.day, period=value.period,
                weather=value.weather, weatherSeed=value.weatherSeed, geometryVersion=value.geometryVersion,
                frictionMultiplier=value.frictionMultiplier
            };
        }

        void CaptureAtmosphere()
        {
            previousAmbientMode=RenderSettings.ambientMode; previousSkyColor=RenderSettings.ambientSkyColor;
            previousEquatorColor=RenderSettings.ambientEquatorColor;previousGroundColor=RenderSettings.ambientGroundColor;
            previousAmbientIntensity=RenderSettings.ambientIntensity;previousFog=RenderSettings.fog;
            previousFogMode=RenderSettings.fogMode;previousFogColor=RenderSettings.fogColor;previousFogDensity=RenderSettings.fogDensity;
            previousSun=RenderSettings.sun;previousShadowDistance=QualitySettings.shadowDistance;
            previousShadowQuality=QualitySettings.shadows;previousShadowResolution=QualitySettings.shadowResolution;previousShadowCascades=QualitySettings.shadowCascades;
        }

        void RestoreAtmosphere()
        {
            RenderSettings.ambientMode=previousAmbientMode;RenderSettings.ambientSkyColor=previousSkyColor;
            RenderSettings.ambientEquatorColor=previousEquatorColor;RenderSettings.ambientGroundColor=previousGroundColor;
            RenderSettings.ambientIntensity=previousAmbientIntensity;RenderSettings.fog=previousFog;
            RenderSettings.fogMode=previousFogMode;RenderSettings.fogColor=previousFogColor;RenderSettings.fogDensity=previousFogDensity;
            RenderSettings.sun=previousSun;QualitySettings.shadowDistance=previousShadowDistance;
            QualitySettings.shadows=previousShadowQuality;QualitySettings.shadowResolution=previousShadowResolution;QualitySettings.shadowCascades=previousShadowCascades;
        }

        /// <summary>统一使用已保存的环境，不在 World 内重新抽天气或访问变体。</summary>
        public void ApplyAtmosphere(EnvironmentContext context)
        {
            if(context == null || !IsBuilt) return;
            if(context.locationId != LocationId || context.visitVariant != Environment.visitVariant || context.geometryVersion != Environment.geometryVersion)
            {
                BuildWorld(context);
                return;
            }
            Environment=CopyEnvironment(context);
            bool evening=Environment.period==DayPeriod.Evening;
            bool morning=Environment.period==DayPeriod.Morning;
            bool noon=Environment.period==DayPeriod.Noon;
            bool rainy=Environment.weather==WeatherKind.Rainy;
            bool cloudy=Environment.weather==WeatherKind.Cloudy;
            Color sky=evening?new Color(.78f,.53f,.49f):morning?new Color(.64f,.77f,.87f):new Color(.59f,.77f,.96f);
            Color haze=evening?new Color(.70f,.55f,.47f):new Color(.73f,.80f,.82f);
            Color sun=evening?new Color(1,.64f,.38f):morning?new Color(1,.86f,.65f):noon?new Color(1,.96f,.85f):new Color(1,.88f,.73f);
            if(cloudy){sky=Color.Lerp(sky,new Color(.63f,.66f,.69f),.76f);haze=Color.Lerp(haze,new Color(.67f,.71f,.72f),.67f);sun=Color.Lerp(sun,new Color(.90f,.93f,.95f),.6f);}
            if(rainy){sky=Color.Lerp(sky,new Color(.42f,.49f,.56f),.88f);haze=new Color(.57f,.64f,.68f);sun=new Color(.78f,.84f,.90f);}
            RenderSettings.ambientSkyColor=Color.Lerp(sky,Color.white,.27f);
            RenderSettings.ambientEquatorColor=Color.Lerp(haze,new Color(.52f,.56f,.55f),.38f);
            RenderSettings.ambientGroundColor=evening?new Color(.36f,.32f,.29f):new Color(.34f,.37f,.35f);
            RenderSettings.ambientIntensity=rainy?.84f:cloudy?.96f:evening?.83f:1.08f;
            RenderSettings.fogColor=haze;RenderSettings.fogDensity=rainy?.023f:cloudy?.018f:evening?.019f:.012f;
            if(sunlight!=null)
            {
                sunlight.color=sun;sunlight.intensity=rainy?.68f:cloudy?.92f:evening?.95f:noon?1.46f:1.30f;
                sunlight.shadowStrength=rainy?.22f:cloudy?.43f:.74f;
                sunlight.transform.rotation=Quaternion.Euler(evening?14:morning?24:noon?73:38,morning?-76:evening?68:-34,0);
            }
            var skybox=RenderSettings.skybox;
            if(skybox!=null)
            {
                skybox.SetColor("_SkyTint",sky);skybox.SetColor("_GroundColor",haze);
                skybox.SetFloat("_AtmosphereThickness",rainy?1.65f:cloudy?1.35f:evening?1.15f:.9f);
                skybox.SetFloat("_Exposure",rainy?.83f:cloudy?1.02f:evening?1.01f:1.18f);
                skybox.SetFloat("_SunSize",rainy?.015f:cloudy?.025f:.04f);
            }
            if(MainCamera!=null) MainCamera.backgroundColor=sky;
            if(palette!=null)
            {
                palette.Ground.SetFloat("_Glossiness",SurfaceWet?.23f:.06f);
                palette.Paving.SetFloat("_Glossiness",SurfaceWet?.21f:.06f);
                palette.Stone.SetFloat("_Glossiness",SurfaceWet?.18f:.06f);
                palette.Iron.SetFloat("_Glossiness",SurfaceWet?.46f:.28f);
                palette.Lamp.SetColor("_EmissionColor",evening?new Color(1.6f,.87f,.31f):IsSheltered&&(rainy||cloudy)?new Color(.7f,.45f,.18f):new Color(.015f,.012f,.008f));
            }
            if(scenery!=null) foreach(var lamp in scenery.GetComponentsInChildren<Light>(true))
            {
                if(lamp==sunlight) continue;
                lamp.intensity=evening?1.15f:IsSheltered&&(rainy||cloudy)?.55f:0;
            }
            if(rain!=null)
            {
                uint seed=unchecked((uint)Environment.weatherSeed);if(seed==0) seed=1;
                if(rain.randomSeed!=seed)
                {
                    if(rain.isPlaying) rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    rain.randomSeed=seed;
                }
                if(rainy) { if(!rain.isPlaying) rain.Play(); }
                else rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if(motes!=null)
            {
                var emission=motes.emission;emission.rateOverTime=rainy?0:cloudy?1.3f:IsSheltered?2:5;
                if(rainy) motes.Clear();
            }
            if(Terrain!=null) Terrain.ApplyWeather(Environment);
            SurfaceFriction=FloorCollider!=null&&FloorCollider.sharedMaterial!=null?FloorCollider.sharedMaterial.dynamicFriction:palette.Friction;
            var audio=FindFirstObjectByType<SummerAudio>();if(audio!=null) audio.SetEnvironment(Environment);
        }

        void SetupWeatherParticles()
        {
            Shader shader=Shader.Find("Particles/Standard Unlit")??Shader.Find("Sprites/Default");
            var material=Own(new Material(shader));material.name="细长透明雨丝";
            var texture=Own(new Texture2D(4,32,TextureFormat.RGBA32,false));
            var pixels=new Color[128];for(int y=0;y<32;y++) for(int x=0;x<4;x++) pixels[y*4+x]=new Color(.76f,.85f,.91f,(x==1||x==2)?Mathf.Sin(y*Mathf.PI/31)*.46f:0);
            texture.SetPixels(pixels);texture.Apply();material.mainTexture=texture;
            if(material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode",2);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite",0);material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
            }
            rain=CreateParticles(IsSheltered?"窗檐外的雨":"街区阵雨",material,new Color(.79f,.86f,.93f,.6f),.035f,.062f,8,11);
            rain.transform.rotation=Quaternion.Euler(90,0,0);
            rain.transform.position=IsSheltered?new Vector3(9.2f,8,3):new Vector3(0,8,3);
            var main=rain.main;main.loop=true;main.startLifetime=1.0f;main.maxParticles=900;main.gravityModifier=0;
            main.startRotation=0;main.startSize3D=true;main.startSizeX=.045f;main.startSizeY=.36f;main.startSizeZ=.045f;
            rain.useAutoRandomSeed=false;
            var shape=rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=IsSheltered?new Vector3(5.4f,16,.02f):new Vector3(23,22,.02f);
            var emission=rain.emission;emission.enabled=true;emission.rateOverTime=IsSheltered?125:420;
            var renderer=rain.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.3f;renderer.velocityScale=.015f;
        }

        void ApplyCameraEnvironment(WorldCameraMode mode)
        {
            introStart=new Vector3(9.2f,7.1f,-10.9f);introEnd=new Vector3(-.7f,8.1f,-11.8f);
            if(Environment==null||Environment.geometryVersion<2||mode==WorldCameraMode.Match||mode==WorldCameraMode.Collection) return;
            Vector3 view;
            switch(LocationId)
            {
                case "home": view=new Vector3(-2.2f,7.2f,-10.5f);break;
                case "alley":view=new Vector3(1.8f,7.3f,-10.8f);break;
                case "old_tree":view=new Vector3(-1.8f,7.7f,-10.8f);break;
                case "square":view=new Vector3(2.2f,8.8f,-12.3f);break;
                case "shop":view=new Vector3(-1.4f,7.5f,-10.2f);break;
                case "corridor":view=new Vector3(-1.3f,6.9f,-9.4f);break;
                case "playground":view=new Vector3(1.2f,9.0f,-12.4f);break;
                default:view=new Vector3(0,8.2f,-11.8f);break;
            }
            float variant=Environment.visitVariant-1;
            view+=new Vector3(variant*1.35f,variant*.25f,variant*.45f);
            desiredTarget=new Vector3(variant*.1f,.72f,LocationId=="corridor"?1.55f:2.05f);
            desiredFov=LocationId=="corridor"?46:LocationId=="square"||LocationId=="playground"?45:44;
            introEnd=view;introStart=view+new Vector3(5.7f, -.6f,1.1f);
            desiredPosition=mode==WorldCameraMode.Intro?introStart:view;
        }
    }
}
