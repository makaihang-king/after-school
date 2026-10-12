using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Childhood.Editor
{
    public static class PhysicsCalibration
    {
        [Serializable] public sealed class Entry
        {
            public string mode;
            public float weight, thickness, impulse, lever, extraTorque, horizontal, minUp = 1f, finalUp, maxHeight, duration;
            public bool flipped, quiet;
            public string contactLog;
        }
        [Serializable] public sealed class Report { public string unity; public List<Entry> entries = new List<Entry>(); }

        static Scene scene;
        static PhysicsScene physics;
        static Transform root;
        static Collider floor;
        static List<DiscBody> live = new List<DiscBody>();

        [MenuItem("放学后/校准翻片物理")]
        public static void Run()
        {
            var report = new Report { unity = Application.unityVersion };
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var oldSimulation = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            physics = scene.GetPhysicsScene();
            var container = new GameObject("Calibration objects"); SceneManager.MoveGameObjectToScene(container, scene); root = container.transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(0,-.25f,0); ground.transform.localScale = new Vector3(40f,.5f,40f);
            floor = ground.GetComponent<Collider>();
            floor.sharedMaterial = new PhysicsMaterial("校准地面") { dynamicFriction = .58f, staticFriction = .66f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            try
            {
                foreach (float weight in new[] { .54167f, 1f, 2.16667f })
                foreach (float impulse in new[] { .025f,.040f,.055f,.070f,.085f,.10f,.115f,.13f,.15f,.18f,.22f,.28f })
                    report.entries.Add(Test("current_lever", weight, weight < .6f ? .5185f : weight > 2f ? .7037f : 1f, impulse, .25f, 0f, impulse * .425f));
                foreach (float impulse in new[] { .035f,.05f,.065f,.08f,.095f,.11f,.13f,.15f,.18f })
                foreach (float lever in new[] { .14f,.20f,.28f })
                    report.entries.Add(Test("lever_scan", 1f, 1f, impulse, lever, 0f, impulse * .22f));
                foreach (float weight in new[] { .54167f, 1f, 2.16667f })
                foreach (float power in new[] { .25f,.40f,.60f,.80f,1f })
                {
                    float impulse = (.045f + power * .045f) * weight;
                    float torque = (.008f + power * .005f) * weight;
                    report.entries.Add(Test("mass_scaled_torque_p" + power, weight, weight < .6f ? .5185f : weight > 2f ? .7037f : 1f,
                        impulse, .16f, torque, impulse * .32f));
                }
                foreach (float power in new[] { .32f,.42f,.58f,.72f,.92f })
                {
                    report.entries.Add(Trajectory(power,false,false));
                    report.entries.Add(Trajectory(power,true,false));
                    report.entries.Add(Trajectory(power,true,true));
                    foreach(float delay in new[]{.035f,.065f,.095f})
                    {
                        report.entries.Add(Trajectory(power,true,false,delay));
                        report.entries.Add(Trajectory(power,true,true,delay));
                    }
                }
                foreach(float damping in new[]{.65f,1.3f,2f})
                {
                    report.entries.Add(Series("old_mass",.17f,.065f,.75f,damping));
                    report.entries.Add(Series("lower_flatter",.125f,.025f,.25f,damping));
                    report.entries.Add(Series("middle_flatter",.135f,.020f,.25f,damping));
                }
            }
            finally
            {
                string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Tools","PhysicsCalibration");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"edge-impulse-report.json"),JsonUtility.ToJson(report,true));
                var csv = new System.Text.StringBuilder("mode,weight,thickness,impulse,lever,extraTorque,horizontal,minUp,finalUp,maxHeight,duration,flipped,quiet\n");
                foreach (var entry in report.entries)
                    csv.AppendLine(string.Join(",",entry.mode,entry.weight,entry.thickness,entry.impulse,entry.lever,entry.extraTorque,entry.horizontal,
                        entry.minUp,entry.finalUp,entry.maxHeight,entry.duration,entry.flipped,entry.quiet));
                File.WriteAllText(Path.Combine(output,"edge-impulse-report.csv"),csv.ToString());
                Debug.Log("PHYSICS_CALIBRATION_COMPLETE entries=" + report.entries.Count);
                if (floor != null && floor.sharedMaterial != null) UnityEngine.Object.DestroyImmediate(floor.sharedMaterial);
                UnityEngine.Object.DestroyImmediate(container);
                Physics.simulationMode = oldSimulation;
            }
        }

        static Entry Test(string mode, float weight, float thickness, float impulse, float lever, float extraTorque, float horizontal)
        {
            // 使用正式 DiscBody 的同一个 48 边凸网格、质量、阻尼与摩擦，不修改得分朝向。
            var disc = DiscBody.Spawn(root, 0, new Vector3(0,.09f,0),weight,thickness,1f,.5f);
            disc.Body.interpolation = RigidbodyInterpolation.None;
            live.Add(disc);
            for (int i = 0; i < 100; i++) physics.Simulate(.01f);
            Vector3 edge = disc.Body.position - Vector3.forward * lever;
            disc.Body.AddForceAtPosition(Vector3.up * impulse + Vector3.forward * horizontal,edge,ForceMode.Impulse);
            if (extraTorque != 0f) disc.Body.AddTorque(Vector3.right * extraTorque,ForceMode.Impulse);
            var entry = new Entry { mode=mode,weight=weight,thickness=thickness,impulse=impulse,lever=lever,extraTorque=extraTorque,horizontal=horizontal,
                maxHeight=disc.Body.position.y };
            float quiet = 0f;
            for (int i = 0; i < 1200; i++)
            {
                physics.Simulate(.01f);
                entry.duration = (i+1)*.01f;
                float up = Vector3.Dot(disc.Body.rotation * Vector3.up,Vector3.up);
                entry.minUp = Mathf.Min(entry.minUp,up);
                entry.maxHeight = Mathf.Max(entry.maxHeight,disc.Body.position.y);
                quiet = disc.Quiet ? quiet + .01f : 0f;
                if (i > 70 && quiet > .4f) { entry.quiet = true; break; }
            }
            entry.finalUp = Vector3.Dot(disc.Body.rotation * Vector3.up,Vector3.up); entry.flipped = entry.finalUp < -.84f;
            // 在编辑态先解绑资源，防止正式 OnDestroy 使用延迟 Destroy 的编辑器限制。
            Cleanup(disc);
            return entry;
        }

        static void Cleanup(DiscBody disc)
        {
            var renderer = disc.GetComponent<MeshRenderer>(); var materials = renderer.sharedMaterials;
            renderer.sharedMaterials = new Material[0];
            var mesh = disc.GetComponent<MeshFilter>().sharedMesh; disc.GetComponent<MeshFilter>().sharedMesh = null;
            PhysicsMaterial physical = null;
            foreach (var collider in disc.GetComponentsInChildren<Collider>())
            {
                if (physical == null) physical = collider.sharedMaterial;
                collider.sharedMaterial = null;
                if (collider is MeshCollider meshCollider) meshCollider.sharedMesh = null;
            }
            foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(physical);
            disc.gameObject.SetActive(false);
            UnityEngine.Object.DestroyImmediate(disc.gameObject);
            live.Remove(disc);
        }

        static Entry Trajectory(float power,bool earlyContact,bool massScale,float delay=0f)
        {
            var all = new List<DiscBody>();
            for(int i=0;i<5;i++)
            {
                var def = GameCatalog.Cards[5+i].Physics;
                var d = DiscBody.Spawn(root,5+i,new Vector3((i%3-1)*.83f,.08f,.25f+i/3*.95f),def.massGrams/4.8f,
                    def.thicknessMeters/.0027f,def.grip/.64f,Mathf.Clamp01(def.flipEase-.5f));
                d.Body.interpolation=RigidbodyInterpolation.None; all.Add(d);
            }
            for(int i=0;i<100;i++) physics.Simulate(.01f);
            var cueDef = GameCatalog.Cards[0].Physics;
            var cue = DiscBody.Spawn(root,0,new Vector3(0,1.25f,-2.25f),cueDef.massGrams/4.8f,cueDef.thicknessMeters/.0027f,cueDef.grip/.64f,.66f);
            cue.Body.interpolation=RigidbodyInterpolation.None;
            Vector3 direction = (all[0].Body.position-new Vector3(0,0,-2.25f)); direction.y=0; direction.Normalize();
            cue.Body.rotation=Quaternion.Euler(-15f,0,0);
            float timeToFloor=(-2f+Mathf.Sqrt(4f+2f*9.81f*1.15f))/9.81f;
            cue.Body.linearVelocity=direction*((1.25f+power*3.3f)/timeToFloor)+Vector3.down*2f;
            cue.Body.angularVelocity=Vector3.right*3f;
            var log = new System.Text.StringBuilder(); bool triggered=false; int count=0;
            float simTime=0f,pulseTime=-1f;Vector3 capturedImpact=Vector3.zero;
            Action<Vector3> pulse=impact=>
            {
                foreach(var target in all)
                {
                    Vector3 offset=target.Body.position-impact; offset.y=0; float distance=offset.magnitude;
                    if(distance>1.3f) {log.Append("outside("+target.ArtIndex+",d="+distance.ToString("F2")+"); "); continue;}
                    float falloff=Mathf.Pow(1f-distance/1.3f,.75f); Vector3 away=distance>.02f?offset.normalized:direction;
                    float j=massScale ? target.Weight*(.17f+power*.065f)*falloff : .20f*(.32f+power*.68f)*falloff;
                    Vector3 force=Vector3.up*j+away*(massScale?.06f:.085f)*(.32f+power*.68f)*falloff;
                    Vector3 edge=target.Body.position-away*(.23f+target.EdgeLift*.045f);
                    target.Body.AddForceAtPosition(force,edge,ForceMode.Impulse);
                    log.Append("pulse("+target.ArtIndex+",d="+distance.ToString("F2")+",J="+j.ToString("F3")+"); ");
                }
            };
            var recorder = cue.gameObject.AddComponent<CalibrationContactRecorder>();
            recorder.Hit = collision =>
            {
                log.Append("hit("+collision.collider.name+",y="+cue.Body.position.y.ToString("F3")+",speed="+collision.relativeVelocity.magnitude.ToString("F2")+"); "); count++;
                if(triggered || cue.Body.position.y >= (earlyContact ? .7f : .4f)) return;
                triggered=true;
                Vector3 impact=cue.Body.position; impact.y=.02f;
                if(delay>0f){capturedImpact=impact;pulseTime=simTime+delay;}else pulse(impact);
            };
            var entry=new Entry{mode="trajectory_"+(earlyContact?"early":"current")+(massScale?"_mass":"")+"_p"+power+"_delay"+delay,impulse=power,lever=earlyContact?.7f:.4f,extraTorque=delay};
            for(int i=0;i<1500;i++)
            {
                physics.Simulate(.01f);
                simTime+=.01f;
                if(pulseTime>=0f && simTime>=pulseTime){pulse(capturedImpact);pulseTime=-1f;}
                foreach(var target in all)
                {
                    float up=Vector3.Dot(target.Body.rotation*Vector3.up,Vector3.up);
                    entry.minUp=Mathf.Min(entry.minUp,up);entry.maxHeight=Mathf.Max(entry.maxHeight,target.Body.position.y);
                    if(i>800){target.Body.linearDamping=2.5f;target.Body.angularDamping=3f;}
                }
            }
            int flipped=0;
            foreach(var target in all)
            {
                float up=Vector3.Dot(target.Body.rotation*Vector3.up,Vector3.up); if(up<-.84f) flipped++;
                log.Append("final("+target.ArtIndex+",up="+up.ToString("F3")+",pos="+target.Body.position.ToString("F2")+"); ");
            }
            entry.finalUp=flipped;entry.flipped=flipped>0;entry.contactLog="contacts="+count+"; "+log;entry.quiet=triggered;
            foreach(var target in all) Cleanup(target); Cleanup(cue); return entry;
        }

        static Entry Series(string formula,float peak,float powerSlope,float exponent,float damping)
        {
            var all=new List<DiscBody>();
            for(int i=0;i<5;i++)
            {
                var def=GameCatalog.Cards[5+i].Physics;
                var d=DiscBody.Spawn(root,5+i,new Vector3((i%3-1)*.83f,.08f,.25f+i/3*.95f),def.massGrams/4.8f,
                    def.thicknessMeters/.0027f,def.grip/.64f,Mathf.Clamp01(def.flipEase-.5f));
                d.Body.interpolation=RigidbodyInterpolation.None;d.Body.angularDamping=damping;all.Add(d);
            }
            for(int i=0;i<100;i++)physics.Simulate(.01f);
            var log=new System.Text.StringBuilder();int score=0;
            var result=new Entry{mode="series_"+formula+"_damping"+damping,impulse=peak,lever=exponent,extraTorque=powerSlope};
            for(int shot=1;shot<=9;shot++)
            {
                DiscBody chosen=null;
                foreach(var target in all)if(!target.Claimed&&(chosen==null||target.Body.position.z<chosen.Body.position.z))chosen=target;
                if(chosen==null||score>=3)break;
                Vector3 direction=chosen.Body.position-new Vector3(0,0,-2.25f);direction.y=0f;
                float power=Mathf.Clamp01((direction.magnitude-1.25f)/3.3f);direction.Normalize();
                var def=GameCatalog.Cards[0].Physics;
                var cue=DiscBody.Spawn(root,0,new Vector3(0,1.25f,-2.25f),def.massGrams/4.8f,def.thicknessMeters/.0027f,def.grip/.64f,.66f);
                cue.Body.interpolation=RigidbodyInterpolation.None;cue.Body.rotation=Quaternion.Euler(-15f,0,0);
                float timeToFloor=(-2f+Mathf.Sqrt(4f+2f*9.81f*1.15f))/9.81f;
                cue.Body.linearVelocity=direction*((1.25f+power*3.3f)/timeToFloor)+Vector3.down*2f;cue.Body.angularVelocity=Vector3.right*3f;
                bool triggered=false;float elapsed=0f,pulseAt=-1f,quiet=0f;Vector3 captured=Vector3.zero;
                var recorder=cue.gameObject.AddComponent<CalibrationContactRecorder>();
                recorder.Hit=collision=>
                {
                    if(triggered||cue.Body.position.y>=.4f||collision.relativeVelocity.magnitude<.5f)return;
                    triggered=true;captured=cue.Body.position;captured.y=.02f;pulseAt=elapsed+.095f;
                    log.Append("shot"+shot+"(aim="+chosen.ArtIndex+",p="+power.ToString("F3")+",hit="+collision.collider.name+",cy="+cue.Body.position.y.ToString("F3")+"); ");
                };
                for(int frame=0;frame<1800;frame++)
                {
                    physics.Simulate(.01f);elapsed+=.01f;
                    if(pulseAt>=0f&&elapsed>=pulseAt)
                    {
                        pulseAt=-1f;
                        foreach(var target in all)
                        {
                            if(target.Claimed)continue;
                            Vector3 offset=target.Body.position-captured;offset.y=0f;float distance=offset.magnitude;if(distance>1.3f)continue;
                            float falloff=Mathf.Pow(1-distance/1.3f,exponent);Vector3 away=distance>.02f?offset.normalized:direction;
                            float lift=target.Weight*(peak+power*powerSlope)*falloff;
                            target.Body.AddForceAtPosition(Vector3.up*lift+away*.06f*(.32f+power*.68f)*falloff,
                                target.Body.position-away*(.23f+target.EdgeLift*.045f),ForceMode.Impulse);
                            log.Append("pulse("+target.ArtIndex+",d="+distance.ToString("F2")+",J="+lift.ToString("F3")+"); ");
                        }
                    }
                    bool settled=elapsed>.7f;
                    foreach(var target in all)
                    {
                        if(target.Claimed)continue;
                        float up=Vector3.Dot(target.Body.rotation*Vector3.up,Vector3.up);result.minUp=Mathf.Min(result.minUp,up);
                        result.maxHeight=Mathf.Max(result.maxHeight,target.Body.position.y);if(!target.Quiet)settled=false;
                        if(elapsed>8f){target.Body.linearDamping=2.5f;target.Body.angularDamping=3f;}
                    }
                    if(!cue.Quiet&&cue.Body.position.magnitude<7f&&elapsed<8f)settled=false;
                    quiet=settled?quiet+.01f:0f;if(quiet>.35f)break;
                }
                foreach(var target in all)
                {
                    if(target.Claimed)continue;float up=Vector3.Dot(target.Body.rotation*Vector3.up,Vector3.up);
                    log.Append("final("+target.ArtIndex+",up="+up.ToString("F3")+"); ");
                    if(up<-.84f){score++;target.Claimed=true;target.Body.isKinematic=true;target.gameObject.SetActive(false);}
                }
                log.Append("score="+score+"; ");result.duration+=elapsed;Cleanup(cue);
                for(int frame=0;frame<120;frame++)physics.Simulate(.01f);
            }
            result.finalUp=score;result.flipped=score>0;result.quiet=score>=3;result.contactLog=log.ToString();
            foreach(var target in all)Cleanup(target);return result;
        }
    }

    [ExecuteAlways]
    internal sealed class CalibrationContactRecorder : MonoBehaviour
    {
        public Action<Collision> Hit;
        void OnCollisionEnter(Collision collision) {Hit?.Invoke(collision);}
    }
}
