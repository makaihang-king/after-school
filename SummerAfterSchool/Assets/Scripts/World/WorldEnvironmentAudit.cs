using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Childhood
{
    /// <summary>独立实景验收；不创建 SaveRepository、不接触玩家局，仅显式命令行启动。</summary>
    public sealed class WorldEnvironmentAudit : MonoBehaviour
    {
        [Serializable] sealed class SceneResult
        {
            public string location, geometryHash, rendererHash, screenshot;
            public int variant, renderers, surfaces, boundaries, ownedResources;
            public bool deterministic, oldResourcesReleased;
        }
        [Serializable] sealed class AtmosphereResult
        {
            public string location, period, weather, soundSignature;
            public float groundFriction, ordinaryFactor, manholeFactor, sunIntensity, fogDensity;
            public bool rain, sheltered, wet, lamps, passed;
        }
        [Serializable] sealed class AuditReport
        {
            public bool passed, standalone, saveUntouched;
            public int sceneCases, atmosphereCases;
            public List<string> errors=new List<string>();
            public List<SceneResult> scenes=new List<SceneResult>();
            public List<AtmosphereResult> atmospheres=new List<AtmosphereResult>();
            public List<string> legacyGeometry=new List<string>();
        }

        readonly AuditReport report=new AuditReport();
        readonly Dictionary<string,string> saveBefore=new Dictionary<string,string>();
        SceneWorld world;
        SummerAudio audioFx;
        string output;
        bool completed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(System.Environment.GetCommandLineArgs().Contains("--world-environment-test"))
                new GameObject("QA · 世界环境独立验收").AddComponent<WorldEnvironmentAudit>();
        }

        void Awake()
        {
            output=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"Logs","QA","WorldV04"));
            var args=System.Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--qa-output") output=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(output);
            SnapshotSaves(saveBefore);
            Application.logMessageReceived+=OnLog;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
        }

        IEnumerator Start()
        {
            // 驱动嵌套迭代器并记录异常，不能把中断的矩阵写成通过。
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                object current=null;bool moved=false;
                try {moved=stack.Peek().MoveNext();if(moved) current=stack.Peek().Current;}
                catch(Exception e){report.errors.Add(e.ToString());Complete();yield break;}
                if(!moved){stack.Pop();continue;}
                if(current is IEnumerator child && !(current is CustomYieldInstruction)) stack.Push(child);
                else yield return current;
            }
            Complete();
        }

        IEnumerator Run()
        {
            yield return null;
            report.standalone=FindFirstObjectByType<SummerGame>()==null;
            if(!report.standalone){report.errors.Add("SummerGame 已启动，独立世界验收拒绝运行。");yield break;}
            world=new GameObject("QA · 世界").AddComponent<SceneWorld>();
            audioFx=new GameObject("QA · 世界声景").AddComponent<SummerAudio>();
            foreach(var location in GameCatalog.Locations)
            {
                var rendererHashes=new HashSet<string>();string geometry=null;
                for(int variant=0;variant<3;variant++)
                {
                    var context=Context(location.id,variant,DayPeriod.Afternoon,WeatherKind.Sunny);
                    world.BuildWorld(context);world.SetCameraMode(WorldCameraMode.Hub,true);
                    var scene=new SceneResult {location=location.id,variant=variant,rendererHash=RendererHash(),geometryHash=GeometryHash(),
                        renderers=world.ActiveScenery.GetComponentsInChildren<MeshRenderer>().Length,
                        surfaces=world.Terrain.SurfaceColliders.Count(),boundaries=world.Terrain.BoundaryColliders.Count(),ownedResources=world.OwnedResourceCount};
                    rendererHashes.Add(scene.rendererHash);
                    if(geometry==null) geometry=scene.geometryHash;
                    Check(geometry==scene.geometryHash,location.id+" 跨访问变体改变比赛几何");
                    Check(scene.surfaces>=3&&scene.boundaries==4,location.id+" 缺少实体地形或完整围边");
                    var oldRoot=world.ActiveScenery;var oldResources=world.OwnedResourceSnapshot();
                    world.Rebuild(context);world.SetCameraMode(WorldCameraMode.Hub,true);
                    scene.deterministic=scene.rendererHash==RendererHash()&&scene.geometryHash==GeometryHash();
                    Check(scene.deterministic,location.id+" 同环境重建结果不一致");
                    yield return null;
                    scene.oldResourcesReleased=oldRoot==null&&oldResources.All(resource=>resource==null);
                    Check(scene.oldResourcesReleased,location.id+" 重建后旧对象或材质/贴图/网格未释放");
                    scene.screenshot=location.id+"-variant-"+variant+".png";
                    yield return new WaitForEndOfFrame();
                    Capture(scene.screenshot);
                    if(variant==0) File.Copy(Path.Combine(output,scene.screenshot),Path.Combine(output,location.id+"-overview.png"),true);
                    report.scenes.Add(scene);report.sceneCases++;
                }
                Check(rendererHashes.Count==3,location.id+" 三个访问变体真实渲染几何没有区别");
                world.BuildWorld(Context(location.id,0,DayPeriod.Afternoon,WeatherKind.Sunny));
                float dry=world.FloorCollider.sharedMaterial.dynamicFriction;
                var manhole=world.Terrain.SurfaceColliders.FirstOrDefault(c=>c.sharedMaterial!=null&&c.sharedMaterial.name.Contains("井盖"));
                float iron=manhole==null?0:manhole.sharedMaterial.dynamicFriction;
                foreach(DayPeriod period in Enum.GetValues(typeof(DayPeriod))) foreach(WeatherKind weather in Enum.GetValues(typeof(WeatherKind)))
                {
                    int before=report.errors.Count;
                    world.ApplyAtmosphere(Context(location.id,0,period,weather));
                    world.ApplyAtmosphere(Context(location.id,0,period,weather)); // 重复应用必须幂等。
                    float expected=weather==WeatherKind.Rainy&&!world.IsSheltered?.9f:1;
                    float factor=world.FloorCollider.sharedMaterial.dynamicFriction/dry;
                    float metal=manhole==null?1:manhole.sharedMaterial.dynamicFriction/iron;
                    Check(Mathf.Abs(factor-expected)<.0001f,location.id+" 雨天普通摩擦或恢复错误");
                    Check(manhole==null||Mathf.Abs(metal-(expected<1?.85f:1))<.0001f,location.id+" 井盖雨天摩擦错误");
                    Check(world.RainActive==(weather==WeatherKind.Rainy),location.id+" 雨粒子状态错误");
                    Check(world.SurfaceWet==(expected<1),location.id+" 遮蔽状态错误");
                    Check(GeometryHash()==geometry,location.id+" 天气或时段移动比赛几何");
                    var sun=RenderSettings.sun;
                    bool lamps=world.ActiveScenery.GetComponentsInChildren<Light>().Any(light=>light!=sun&&light.intensity>.01f);
                    Check(period!=DayPeriod.Evening||lamps,location.id+" 傍晚没有打开灯具");
                    report.atmospheres.Add(new AtmosphereResult {location=location.id,period=period.ToString(),weather=weather.ToString(),
                        soundSignature=audioFx.EnvironmentSoundSignature,groundFriction=world.SurfaceFriction,ordinaryFactor=factor,manholeFactor=metal,
                        sunIntensity=sun.intensity,fogDensity=RenderSettings.fogDensity,rain=world.RainActive,sheltered=world.IsSheltered,wet=world.SurfaceWet,
                        lamps=lamps,passed=before==report.errors.Count});
                    report.atmosphereCases++;
                    yield return null;
                }
            }
            foreach(string id in new[]{"home","school_gate","alley","old_tree"})
            {
                world.BuildWorld(id);string original=GeometryHash();
                world.BuildWorld(new EnvironmentContext{locationId=id,geometryVersion=1});
                Check(original==GeometryHash(),id+" version1入口不等价");
                report.legacyGeometry.Add(id+":"+original);
                Check(world.SurfaceHeight(new Vector3(0,0,-1.3f))<.001f,id+" 旧发射区出现实体唇边");
                yield return null;
            }
            Destroy(world.gameObject);Destroy(audioFx.gameObject);yield return null;
        }

        static EnvironmentContext Context(string id,int variant,DayPeriod period,WeatherKind weather)
        {
            return new EnvironmentContext {locationId=id,visitVariant=variant,day=2,period=period,weather=weather,weatherSeed=197+variant,
                geometryVersion=2,frictionMultiplier=weather==WeatherKind.Rainy&&id!="shop"&&id!="corridor"?.9f:1};
        }
        void Check(bool condition,string error){if(!condition) report.errors.Add(error);}
        string RendererHash()
        {
            var lines=new List<string>();
            foreach(var renderer in world.ActiveScenery.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh=renderer.GetComponent<MeshFilter>();
                var text=renderer.GetComponent<TextMesh>();
                Color color=text!=null?text.color:renderer.sharedMaterial!=null&&renderer.sharedMaterial.HasProperty("_Color")?renderer.sharedMaterial.color:Color.white;
                // 不纳入对象名或访问标签，只看真实变换/网格/颜色。
                lines.Add(Matrix(renderer.transform.localToWorldMatrix)+"|"+(mesh==null||mesh.sharedMesh==null?0:mesh.sharedMesh.vertexCount)+"|"+
                    (mesh==null||mesh.sharedMesh==null?"":Vector(mesh.sharedMesh.bounds.size))+"|"+color);
            }
            lines.Sort(StringComparer.Ordinal);return Hash(string.Join("\n",lines));
        }
        string GeometryHash()
        {
            var lines=new List<string>();
            foreach(var collider in world.Terrain.SurfaceColliders.Concat(world.Terrain.BoundaryColliders))
            {
                string line=collider.GetType().Name+"|"+Matrix(collider.transform.localToWorldMatrix)+"|"+Vector(collider.bounds.center)+"|"+Vector(collider.bounds.size);
                if(collider is MeshCollider mesh && mesh.sharedMesh.isReadable) foreach(var point in mesh.sharedMesh.vertices) line+="|"+Vector(point);
                lines.Add(line);
            }
            lines.Sort(StringComparer.Ordinal);return Hash(string.Join("\n",lines));
        }
        static string Matrix(Matrix4x4 value){var parts=new string[16];for(int i=0;i<16;i++) parts[i]=value[i].ToString("F5",CultureInfo.InvariantCulture);return string.Join(",",parts);}
        static string Vector(Vector3 value){return value.x.ToString("F5",CultureInfo.InvariantCulture)+","+value.y.ToString("F5",CultureInfo.InvariantCulture)+","+value.z.ToString("F5",CultureInfo.InvariantCulture);}
        static string Hash(string text){using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
        void Capture(string file)
        {
            var camera=world.MainCamera;var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var target=new RenderTexture(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());}
            finally {camera.targetTexture=previousTarget;RenderTexture.active=previousActive;target.Release();Destroy(target);Destroy(image);}
        }
        static void SnapshotSaves(Dictionary<string,string> into)
        {
            if(!Directory.Exists(Application.persistentDataPath)) return;
            foreach(string file in Directory.GetFiles(Application.persistentDataPath,"*.json")) into[file]=Hash(File.ReadAllText(file));
        }
        void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) report.errors.Add(message+"\n"+trace);}
        void Complete()
        {
            if(completed) return;completed=true;
            var after=new Dictionary<string,string>();SnapshotSaves(after);
            report.saveUntouched=saveBefore.Count==after.Count&&saveBefore.All(pair=>after.TryGetValue(pair.Key,out string value)&&value==pair.Value);
            Check(report.saveUntouched,"玩家JSON存档发生变化");
            Check(report.sceneCases==24&&report.atmosphereCases==96,"世界验收矩阵未完整执行");
            report.passed=report.standalone&&report.errors.Count==0;
            File.WriteAllText(Path.Combine(output,"world-report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            Debug.Log("WORLD_ENVIRONMENT_AUDIT "+(report.passed?"PASS":"FAIL")+" "+output);
            Application.logMessageReceived-=OnLog;
            Application.Quit(report.passed?0:1);
        }
    }
}
