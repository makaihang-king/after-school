"""用已经部署的 ACE-Step 离线模型生成本游戏配乐，不下载任何模型。

每首曲目的规格（BPM、配器、种子和循环处理）会同步写入
``Tools/LocalAI/BGM/generation_manifest.json``，这样重新生成或回退时都能追溯。
"""
from pathlib import Path
import os
import sys
import json
import time
import traceback
import argparse

sys.stdout.reconfigure(encoding="utf-8",errors="replace")
sys.stderr.reconfigure(encoding="utf-8",errors="replace")

ROOT=Path(__file__).resolve().parents[1]
MODEL_ROOT=Path(r"D:\AI\ACE-Step-Models")
SERVICE_ROOT=Path(r"D:\AI\ACE-Step-1.5")
DELIVERY=ROOT/"Tools/LocalAI/BGM"
DELIVERY.mkdir(parents=True,exist_ok=True)
MANIFEST=DELIVERY/"generation_manifest.json"
os.environ["ACESTEP_CHECKPOINTS_DIR"]=str(MODEL_ROOT)
os.environ["HF_HUB_OFFLINE"]="1"
os.environ["TRANSFORMERS_OFFLINE"]="1"
os.environ["HF_DATASETS_OFFLINE"]="1"
os.environ["ACESTEP_COMPILE_MODEL"]="false"
os.environ["ACESTEP_DISABLE_TQDM"]="1"
sys.path.insert(0,str(SERVICE_ROOT))

TRACKS={
    "summer_loop": {
        "label": "街区探索",
        "bpm": 80,
        "seed": 2026100401,
        "caption": ("Slow instrumental nostalgic summer afternoon exploration music, 80 BPM. Warm diatonic "
            "harmonica carries a simple singable lead motif, supported by gentle fingerpicked acoustic guitar, "
            "soft felt piano chords, mellow upright bass and very light brushed percussion. Add occasional wooden "
            "marimba accents, never busy or electronic. A sunny Chinese childhood neighborhood, paper toys, cicadas, "
            "friends playing after school; relaxed, cozy, handcrafted and playful, no vocals, no singing, no speech."),
        "rms": 0.12,
        "peak": 0.78,
    },
    "summer_match": {
        "label": "圆片比赛",
        "bpm": 88,
        "seed": 2026100402,
        "caption": ("Slow-to-moderate instrumental childhood paper pog game soundtrack, 88 BPM. Keep a warm "
            "diatonic harmonica hook as the lead, with relaxed plucked acoustic guitar, soft felt piano, upright "
            "bass, wooden marimba or xylophone answers and a clear but gentle brushed drum groove. Friendly small "
            "competitive lift without tension, no heavy drums, no synths, no cinematic build, no vocals, no singing, "
            "no speech; sunny after-school courtyard and handmade paper toys."),
        "rms": 0.115,
        "peak": 0.76,
    },
    "summer_collection": {
        "label": "铁盒收藏",
        "bpm": 74,
        "seed": 2026100403,
        "caption": ("Quiet instrumental childhood collection music, 74 BPM. A tender restrained diatonic harmonica "
            "phrase appears occasionally while soft felt piano and nylon-string guitar carry the melody; sparse "
            "wooden marimba notes, delicate upright bass and almost no percussion. Opening a small tin box of paper "
            "toys at a wooden desk in late-summer golden light, peaceful affectionate and intimate, no vocals, no "
            "singing, no speech, no electronic instruments."),
        "rms": 0.105,
        "peak": 0.72,
    },
}


def status(data):
    (DELIVERY/"generation_status.json").write_text(json.dumps(data,ensure_ascii=False,indent=2,default=str),encoding="utf-8")


def update_manifest(track, spec, latest=None):
    """保留每首曲目的生成规格和最近一次运行结果，不保存大型波形数据。"""
    try:
        manifest=json.loads(MANIFEST.read_text("utf-8")) if MANIFEST.exists() else {}
    except (OSError, json.JSONDecodeError):
        manifest={}
    manifest.setdefault("schema_version", 1)
    manifest["updated_at"]=time.strftime("%Y-%m-%dT%H:%M:%S%z")
    manifest["model"]={"service":"ACE-Step 1.5","checkpoint":"acestep-v15-turbo","lm":"acestep-5Hz-lm-0.6B",
                       "offline":True,"inference_steps":8,"runtime":sys.executable}
    tracks=manifest.setdefault("tracks", {})
    # 每次运行都登记全部曲目的目标规格，即使某一首因本地依赖缺失而尚未生成。
    for track_name, track_spec in TRACKS.items():
        tracks.setdefault(track_name, {}).update({"label":track_spec["label"],"bpm":track_spec["bpm"],
            "seed":track_spec["seed"],"caption":track_spec["caption"],"duration_seconds":48,
            "loop_crossfade_seconds":1.6,"target_rms":track_spec["rms"],"peak_ceiling":track_spec["peak"],
            "instrumental":True,"negative_prompt":"vocals, singing, speech, distorted recording, harsh synthesizer, aggressive drums, dramatic cinematic music"})
    entry=tracks.setdefault(track, {})
    entry.update({"label":spec["label"],"bpm":spec["bpm"],"seed":spec["seed"],"caption":spec["caption"],
                  "duration_seconds":48,"loop_crossfade_seconds":1.6,"target_rms":spec["rms"],
                  "peak_ceiling":spec["peak"],"instrumental":True,
                  "negative_prompt":"vocals, singing, speech, distorted recording, harsh synthesizer, aggressive drums, dramatic cinematic music"})
    if latest:
        entry["latest"]={k:latest[k] for k in ("success","phase","source_file","exported_file","sample_rate",
            "exported_duration","started_at","finished_at","error") if k in latest}
    MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2,default=str),encoding="utf-8")


def write_prompt_specs():
    """把可读的提示词副本放在每首曲目的交付目录，便于美术和音频 QA 复核。"""
    for track_name, spec in TRACKS.items():
        track_dir=DELIVERY if track_name == "summer_loop" else DELIVERY/track_name
        track_dir.mkdir(parents=True,exist_ok=True)
        payload={"track":track_name,"label":spec["label"],"bpm":spec["bpm"],"seed":spec["seed"],
            "duration_seconds":48,"caption":spec["caption"],"instrumental":True,
            "instruments":["diatonic harmonica","fingerpicked acoustic guitar","felt piano","upright bass",
                           "wooden marimba or xylophone","light brushed percussion"],
            "loop_crossfade_seconds":1.6,"target_rms":spec["rms"],"peak_ceiling":spec["peak"],
            "negative_prompt":"vocals, singing, speech, distorted recording, harsh synthesizer, aggressive drums, dramatic cinematic music",
            "generation_mode":"local ACE-Step 1.5 turbo + 0.6B LM; offline only"}
        (track_dir/"prompt_spec.json").write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding="utf-8")


def main():
    global DELIVERY
    parser=argparse.ArgumentParser()
    parser.add_argument("--track",choices=["summer_loop","summer_collection","summer_match"],default="summer_loop")
    args=parser.parse_args()
    write_prompt_specs()
    spec=TRACKS[args.track]
    caption=spec["caption"];bpm=spec["bpm"];seed=spec["seed"]
    if args.track!="summer_loop":
        DELIVERY=DELIVERY/args.track;DELIVERY.mkdir(parents=True,exist_ok=True)
    st={"success":False,"started_at":time.time(),"source":"local ACE-Step 1.5 turbo + 0.6B LM", "caption":caption,
        "seed":seed,"duration":48,"bpm":bpm,"key":"C major","track":args.track,"label":spec["label"],
        "phase":"preflight","loop_crossfade_seconds":1.6,"target_rms":spec["rms"],"peak_ceiling":spec["peak"]}
    update_manifest(args.track,spec,st)
    status(st)
    try:
        from acestep.model_downloader import check_main_model_exists, check_model_exists
        assert check_main_model_exists(MODEL_ROOT), "本地主模型不完整；停止，不下载。"
        assert check_model_exists("acestep-v15-turbo",MODEL_ROOT), "本地 turbo 模型不完整；停止，不下载。"
        from acestep.handler import AceStepHandler
        from acestep.llm_inference import LLMHandler
        from acestep.inference import GenerationConfig,GenerationParams,generate_music
        print("Initializing installed ACE-Step models with int8 quantization and CPU offload...",flush=True)
        dit=AceStepHandler();llm=LLMHandler()
        msg,ok=dit.initialize_service(project_root=str(SERVICE_ROOT),config_path="acestep-v15-turbo",device="cuda",
            use_flash_attention=False,compile_model=False,offload_to_cpu=True,offload_dit_to_cpu=True,quantization="int8_weight_only")
        print(msg,flush=True)
        if not ok: raise RuntimeError(msg)
        msg,ok=llm.initialize(checkpoint_dir=str(MODEL_ROOT),lm_model_path="acestep-5Hz-lm-0.6B",backend="pt",device="cuda",offload_to_cpu=True)
        print(msg,flush=True)
        if not ok: raise RuntimeError(msg)
        st["phase"]="generation";status(st);update_manifest(args.track,spec,st)
        params=GenerationParams(task_type="text2music",caption=caption,lyrics="[Instrumental]",instrumental=True,
            bpm=bpm,keyscale="C major",timesignature="4",duration=48,seed=seed,inference_steps=8,vocal_language="unknown",
            thinking=True,use_cot_metas=False,use_cot_caption=False,use_cot_language=False,
            lm_negative_prompt="vocals, singing, speech, distorted recording, harsh synthesizer, aggressive drums, dramatic cinematic music",
            enable_normalization=True,normalization_db=-3.0)
        config=GenerationConfig(batch_size=1,use_random_seed=False,seeds=[seed],audio_format="wav")
        result=generate_music(dit,llm,params,config,save_dir=str(DELIVERY/"source"))
        if not result.success or not result.audios: raise RuntimeError(result.error or result.status_message)
        source=Path(result.audios[0]["path"])
        import numpy as np
        import soundfile as sf
        data,sr=sf.read(source,always_2d=True)
        if data.shape[1]==1: data=np.repeat(data,2,axis=1)
        data=data[:,:2]
        # 前后 1.6 秒等功率叠合，导出衔接平顺的游戏循环。
        n=min(round(sr*1.6),len(data)//8)
        w=np.linspace(0,1,n)[:,None]
        transition=data[-n:]*np.cos(w*np.pi/2)+data[:n]*np.sin(w*np.pi/2)
        loop=np.concatenate([data[n:-n],transition])
        rms=float(np.sqrt(np.mean(loop**2)))
        desired_rms=spec["rms"]
        peak_ceiling=spec["peak"]
        if rms>0: loop*=min(desired_rms/rms,peak_ceiling/max(float(np.max(np.abs(loop))),.01))
        target=ROOT/"Assets/Resources/Audio"/(args.track+".wav")
        original=target.with_name(args.track+"_procedural.wav")
        if target.exists() and not original.exists():
            original.write_bytes(target.read_bytes())
            st["fallback_file"]=str(original)
        sf.write(target,loop,sr,subtype="PCM_16")
        st.update(success=True,phase="complete",source_file=str(source),exported_file=str(target),sample_rate=sr,
            exported_duration=len(loop)/sr,params=params.to_dict(),config=config.to_dict(),status_message=result.status_message,
            loop_processing={"crossfade_seconds":1.6,"normalization_db":-3.0,"target_rms":desired_rms,
                             "peak_ceiling":peak_ceiling,"channels":2,"format":"PCM_16"})
        print(f"Generated and exported AI BGM: {target}",flush=True)
    except Exception as exc:
        st["error"]=str(exc);st["phase"]="failed";traceback.print_exc()
    finally:
        st["finished_at"]=time.time();status(st)
        update_manifest(args.track,spec,st)
    return 0 if st["success"] else 1


if __name__=="__main__": sys.exit(main())
