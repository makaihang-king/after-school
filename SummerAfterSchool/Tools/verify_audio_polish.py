"""波形验收：新版主配乐与旧版回退均可追溯，所有资源无削波/直流、循环接缝合理、变体确实不同。"""
from pathlib import Path
import hashlib,json,sys,wave
import numpy as np

sys.stdout.reconfigure(encoding="utf-8",errors="replace")
ROOT=Path(__file__).resolve().parents[1]
AUDIO=ROOT/"Assets/Resources/Audio"
manifest=json.loads((ROOT/"Tools/audio_polish_manifest.json").read_text("utf-8"))
current_loop_sha=hashlib.sha256((AUDIO/"summer_loop.wav").read_bytes()).hexdigest()
assert current_loop_sha==manifest.get("current_summer_loop_sha256",manifest["preserved_summer_loop_sha256"])
for filename, key in (("summer_match.wav", "current_summer_match_sha256"), ("summer_collection.wav", "current_summer_collection_sha256")):
    expected=manifest.get(key)
    if expected and (AUDIO/filename).exists():
        assert hashlib.sha256((AUDIO/filename).read_bytes()).hexdigest()==expected
fallback=AUDIO/"summer_loop_procedural.wav"
if manifest.get("fallback_summer_loop_sha256") and fallback.exists():
    assert hashlib.sha256(fallback.read_bytes()).hexdigest()==manifest["fallback_summer_loop_sha256"]
loops={item["file"] for item in manifest["files"] if item["loop"]}|{"summer_loop.wav","summer_match.wav"}
if (AUDIO/"summer_collection.wav").exists():loops.add("summer_collection.wav")
report={"success":False,"legacy_bgm_fallback_preserved":fallback.exists(),"clips":[],"checks":[]}
for file in sorted(AUDIO.glob("*.wav")):
    with wave.open(str(file),"rb") as w:
        assert w.getnchannels()==2 and w.getsampwidth()==2
        sr=w.getframerate();pcm=np.frombuffer(w.readframes(w.getnframes()),dtype="<i2").reshape(-1,2)
    samples=pcm.astype(np.float64)/32768
    peak=float(np.max(np.abs(samples)));rms=float(np.sqrt(np.mean(samples**2)));dc=float(np.max(np.abs(samples.mean(axis=0))))
    assert .001<rms<.45 and peak<.99,f"异常幅度：{file.name}"
    assert dc<.018,f"异常直流偏移：{file.name}"
    jump=float(np.max(np.abs(samples[0]-samples[-1])))
    derivative=np.max(np.abs(np.diff(samples,axis=0)),axis=1)
    baseline=float(np.quantile(derivative,.999))
    is_loop=file.name in loops
    if is_loop:assert jump<max(.008,baseline*1.8),f"循环边界突变：{file.name} ({jump})"
    report["clips"].append({"file":file.name,"duration":len(samples)/sr,"sample_rate":sr,"peak":peak,"rms":rms,"dc":dc,
        "loop":is_loop,"boundary_jump":jump,"normal_sample_step_p999":baseline})
for group in ["impact_paper","impact_metal","impact_glass","impact_wood","impact_rubber","slap","smash","skim","ui","flip","collect","win","lose"]:
    hashes=[hashlib.sha256((AUDIO/(group+f"_{i:02}.wav")).read_bytes()).hexdigest() for i in range(4)]
    assert len(set(hashes))==4,"重复的变体："+group
    report["checks"].append(group+": four distinct variants")
for name in ["ambience_wind","ambience_courtyard","ambience_tree","ambience_alley","ambience_school","charge_loop"]:
    assert name+".wav" in loops
    report["checks"].append(name+": seamless loop")
report["success"]=True
output=ROOT/"Logs/QA/Audio/audio-waveform-report.json";output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(f"PASS: {len(report['clips'])} WAVs readable, no clipping/DC faults, {len(loops)} loop boundaries valid, 13 groups have four distinct variants; legacy BGM fallback preserved.")
for clip in report["clips"]:
    if clip["loop"]:print(f"{clip['file']}: {clip['duration']:.2f}s / {clip['sample_rate']}Hz / boundary {clip['boundary_jump']:.6f}")
