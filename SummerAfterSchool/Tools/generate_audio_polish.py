"""原创音频细化：程序合成地点环境、物理细碰、动作变体与柔和反馈；保留 ACE 主配乐。"""
from pathlib import Path
import hashlib
import json
import math
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/"Assets/Resources/Audio"
OUT.mkdir(parents=True,exist_ok=True)
SR=44100
RNG=np.random.default_rng(2026100302)
MANIFEST=[]


def stereo(data,pan=0):
    if data.ndim==2:return data
    return data[:,None]*np.array([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)])[None,:]


def save(name,data,peak=.65,loop=False,source="original procedural synthesis"):
    data=np.asarray(data,dtype=np.float64)
    data=stereo(data)
    data-=np.mean(data,axis=0)[None,:]
    if loop:
        # 数毫秒的连续边界桥接，不插入静音。
        n=min(160,len(data)//20)
        correction=data[0]-data[-1]
        data[-n:]+=np.linspace(0,1,n)[:,None]*correction
    else:
        a=min(round(.0018*SR),len(data)//10);b=min(round(.018*SR),len(data)//8)
        data[:a]*=np.linspace(0,1,a)[:,None];data[-b:]*=np.linspace(1,0,b)[:,None]
    maximum=float(np.max(np.abs(data)))
    if maximum>0:data*=peak/maximum
    pcm=np.rint(np.clip(data,-.99,.99)*32767).astype("<i2")
    target=OUT/(name+".wav")
    with wave.open(str(target),"wb") as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(pcm.tobytes())
    MANIFEST.append({"file":target.name,"source":source,"sample_rate":SR,"duration":len(pcm)/SR,
        "loop":loop,"peak":float(np.max(np.abs(pcm.astype(np.int32))))/32768,
        "rms":float(np.sqrt(np.mean((pcm.astype(np.float64)/32768)**2))),
        "boundary_jump":float(np.max(np.abs(pcm[0].astype(np.int32)-pcm[-1].astype(np.int32))))/32768,
        "sha256":hashlib.sha256(target.read_bytes()).hexdigest()})


def damped(freq,duration,decay=8,weight=1,brightness=.18):
    t=np.arange(round(duration*SR))/SR
    attack=1-np.exp(-t*650)
    env=attack*np.exp(-t*decay)
    return weight*env*(np.sin(math.tau*freq*t)+brightness*np.sin(math.tau*freq*2.03*t)*np.exp(-t*8)
        +brightness*.28*np.sin(math.tau*freq*4.21*t)*np.exp(-t*15))


def low_noise(duration,cutoff=2800):
    n=round(duration*SR)
    freqs=np.fft.rfftfreq(n,1/SR)
    spectrum=(RNG.normal(size=len(freqs))+1j*RNG.normal(size=len(freqs)))
    spectrum*=np.exp(-(freqs/cutoff)**4)
    data=np.fft.irfft(spectrum,n=n)
    return data/(np.std(data)+1e-9)


def add(target,signal,when,pan=0,gain=1):
    signal=stereo(signal,pan)*gain
    ids=(np.arange(len(signal))+round(when*SR))%len(target)
    target[ids]+=signal


def impact(kind,variant):
    duration=.30 if kind in ["paper","rubber"] else .48
    t=np.arange(round(duration*SR))/SR
    noise=low_noise(duration,2100 if kind=="paper" else 3000)
    factor=1+(variant-1.5)*.04
    if kind=="paper":
        signal=noise*np.exp(-t*48)*.19+damped(153*factor,duration,25,.55,.04)+damped(316*factor,duration,40,.17,.03)
    elif kind=="metal":
        signal=noise*np.exp(-t*62)*.07
        for f,w,d in [(502,.62,19),(1118,.23,26),(1667,.12,34),(2189,.035,48)]:signal+=damped(f*factor,duration,d,w,.02)
    elif kind=="glass":
        signal=noise*np.exp(-t*78)*.035
        for f,w,d in [(784,.59,15),(1269,.28,22),(1891,.13,28),(2564,.025,43)]:signal+=damped(f*factor,duration,d,w,.01)
    elif kind=="wood":
        signal=noise*np.exp(-t*48)*.12+damped(243*factor,duration,25,.6,.07)+damped(527*factor,duration,38,.17,.04)
    else:
        signal=noise*np.exp(-t*55)*.055+damped(106*factor,duration,28,.7,.05)+damped(219*factor,duration,32,.1,.02)
    return signal


def create_feedback():
    for kind in ["paper","metal","glass","wood","rubber"]:
        for v in range(4):save(f"impact_{kind}_{v:02}",impact(kind,v),.67 if kind in ["paper","wood","rubber"] else .55)
    for v in range(4):
        factor=1+(v-1.5)*.035
        for name,duration,low,noise_gain,decay in [("slap",.36,126,.26,29),("smash",.49,76,.34,21),("skim",.34,288,.12,34)]:
            t=np.arange(round(duration*SR))/SR
            signal=low_noise(duration,1900 if name!="skim" else 2500)*np.exp(-t*decay)*noise_gain
            signal+=damped(low*factor,duration,decay*.65,.6,.05)
            if name=="skim":signal*=np.sin(np.minimum(t/.26,1)*math.pi/2);signal+=damped(510*factor,duration,28,.12,.02)
            save(f"{name}_{v:02}",signal,.83 if name=="smash" else .69 if name=="slap" else .49)
        ui=damped((740+v*23)*factor,.16,27,.7,.02)+damped(1110+v*15,.16,33,.15,.02)
        save(f"ui_{v:02}",ui,.31)
        flip=np.zeros((round(.48*SR),2))
        add(flip,impact("paper",v),0,gain=.20)
        add(flip,damped(660*factor,.28,15,.45,.06),.055,pan=-.10)
        add(flip,damped(990*factor,.24,17,.25,.04),.11,pan=.10)
        save(f"flip_{v:02}",flip,.51)
        collected=damped(523.25*factor,.52,9,.60,.12)+damped(783.99*factor,.52,11,.20,.05)
        save(f"collect_{v:02}",collected,.55)
    rare=np.zeros((round(1.35*SR),2))
    for i,f in enumerate([523.25,659.25,783.99,1046.5]):add(rare,damped(f,.90,5.8,.23,.05),i*.075,pan=(i-1.5)*.1)
    save("rare_collect",rare,.50)
    for streak in range(2,7):
        combo=np.zeros((round(.85*SR),2))
        shift=2**((streak-2)*2/12)
        for i,f in enumerate([523.25,659.25,783.99]):add(combo,damped(f*shift,.5,9,.3,.05),i*.06,pan=(i-1)*.12)
        save(f"combo_{streak:02}",combo,.49)
    for name,notes in [("win",[60,64,67,72]),("lose",[64,62,60])]:
        for v in range(4):
            signal=np.zeros((round(1.45*SR),2))
            for i,note in enumerate(notes):
                freq=440*2**((note-69)/12)*(1+(v-1.5)*.0015)
                add(signal,damped(freq,1.0,5.6+v*.25,.26,.07),i*(.12+v*.004),pan=(i-1.5)*(.06+v*.018))
                add(signal,damped(freq/2,1.0,6.9+v*.22,.07,.02),i*(.12+v*.004))
            save(f"{name}_{v:02}",signal,.50 if name=="win" else .34)
    duration=2
    t=np.arange(round(duration*SR))/SR
    charge=(np.sin(math.tau*196*t)*.55+np.sin(math.tau*294*t)*.27+np.sin(math.tau*392*t)*.08)
    charge*=.78+.10*np.cos(math.tau*2*t)+.025*np.sin(math.tau*7*t)
    save("charge_loop",charge,.32,True)


def periodic_noise(duration,low,high):
    n=round(SR*duration);f=np.fft.rfftfreq(n,1/SR)
    spectrum=(RNG.normal(size=len(f))+1j*RNG.normal(size=len(f)))
    filt=np.exp(-(f/high)**4)*(1-np.exp(-(f/max(low,1))**4));spectrum*=filt
    result=np.fft.irfft(spectrum,n=n)
    return result/(np.std(result)+1e-9)


def birdsong(duration=.44,frequency=2300):
    t=np.arange(round(duration*SR))/SR
    env=np.sin(math.pi*t/duration)**2
    phase=math.tau*(frequency*t+620*t*t-220*t*t*t)
    return (np.sin(phase)+.17*np.sin(phase*2))*env*(.8+.2*np.sin(math.tau*19*t))


def create_environment():
    duration=28;length=round(duration*SR);t=np.arange(length)/SR
    wind1=periodic_noise(duration,70,480);wind2=periodic_noise(duration,100,600)
    modulation=.7+.15*np.sin(math.tau*3*t/duration)+.1*np.cos(math.tau*5*t/duration)
    wind=np.stack([wind1,wind2],axis=1)*modulation[:,None]
    save("ambience_wind",wind,.22,True)
    for location in ["courtyard","tree","alley","school"]:
        bed=np.zeros((length,2))
        if location in ["courtyard","tree"]:
            leaf=periodic_noise(duration,700,2450)*(.45+.22*np.sin(math.tau*4*t/duration))
            bed+=stereo(leaf,.15)*(.012 if location=="courtyard" else .024)
            starts=[2.1,8.6,16.2,23.4] if location=="courtyard" else [5.3,19.7]
            for i,start in enumerate(starts):
                add(bed,birdsong(.38+i*.015,2160+i*143),start,pan=(-.6 if i%2 else .65),gain=.12 if location=="courtyard" else .07)
                add(bed,birdsong(.25,2490+i*80),start+.44,pan=(-.6 if i%2 else .65),gain=.075 if location=="courtyard" else .04)
        if location=="tree":
            buzz=periodic_noise(duration,3400,5300)
            chorus=(.32+.18*np.sin(math.tau*5*t/duration))*(.72+.28*np.sin(math.tau*17*t))
            bed+=stereo(buzz*chorus,-.22)*.012
            # 偶尔的落叶与布料摆动比整段恒定蝉声更有空间感。
            for start in [3.8,12.4,20.3]:
                d=1.2;bt=np.arange(round(d*SR))/SR
                add(bed,low_noise(d,1500)*np.sin(math.pi*bt/d)**2,start,pan=.65,gain=.012)
        if location in ["alley","school"]:
            street=periodic_noise(duration,85,350)
            bed+=stereo(street,-.15)*.012
            for start,direction in [(2.4,1),(13.2,-1),(21.4,1)]:
                for step in range(6):
                    foot=impact("wood",step%4)
                    add(bed,foot,start+step*.47,pan=direction*(-.5+step*.19),gain=.021 if location=="alley" else .017)
            for start in [6.4,18.3]:
                bell=damped(932,.85,6,.24,.02)+damped(1423,.85,9,.07,.01)
                add(bed,bell,start,pan=-.55,gain=.14 if location=="alley" else .08)
            if location=="school":
                for start in [9.2,25.1]:
                    # 远处脚步和操场轻球声，避免合成人声。
                    add(bed,impact("rubber",0),start,pan=.7,gain=.10)
                    add(bed,impact("rubber",1),start+.48,pan=.5,gain=.05)
                add(bed,birdsong(.39,2440),11.7,pan=.5,gain=.07)
        # 背景层具有轻底噪，可循环而不会在边界突然从静音醒来。
        bed+=wind*.007
        save("ambience_"+location,bed,.25 if location=="courtyard" else .21,True)


def main():
    original=OUT/"summer_loop.wav"
    before=hashlib.sha256(original.read_bytes()).hexdigest() if original.exists() else None
    create_feedback();create_environment()
    after=hashlib.sha256(original.read_bytes()).hexdigest() if original.exists() else None
    assert before==after,"原本 ACE 主配乐被意外改变"
    report={"seed":2026100302,"source":"original procedural synthesis; not AI generated", "preserved_summer_loop_sha256":after,
        "files":MANIFEST,"notes":"four variants per impact/gesture; quiet outdoor layers; seamless generated loop boundary; no recorded voices"}
    (ROOT/"Tools/audio_polish_manifest.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print(f"Generated {len(MANIFEST)} original audio assets; ACE summer_loop preserved.")


if __name__=="__main__":main()
