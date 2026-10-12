"""资源交付检查：文件完整、中文名称一致、PNG/WAV 可读。"""
from pathlib import Path
import re,json,wave,sys
import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8",errors="replace")
root=Path(__file__).resolve().parents[1]
art=root/"Assets/Resources/Art/Pogs"
files=sorted(art.glob("pog_[0-9][0-9].png"))
assert len(files)==42
catalog=(root/"Assets/Scripts/Core/GameCatalog.cs").read_text("utf-8")
names=re.search(r"var names = new\[\] \{ (.*?) \};",catalog).group(1)
labels=re.findall(r'"([^"]+)"',names)
manifest=json.loads((root/"Tools/procedural_asset_manifest.json").read_text("utf-8"))
extra=re.search(r"var extraNames = new\[\] \{ (.*?) \};",catalog).group(1)
labels += re.findall(r'"([^"]+)"',extra)
assert labels==[p["title"] for p in manifest["pogs"]]
for f in files:
    with Image.open(f) as im:
        assert im.size==(768,768) and im.mode=="RGBA"
        assert im.getpixel((0,0))[3]==0
    with Image.open(f) as im:
        im.verify()
print("42 张圆片、尺寸、透明外沿与中文目录名称一致，PNG 可读。")
for f in sorted((root/"Assets/Resources/Audio").glob("*.wav")):
    with wave.open(str(f)) as w:
        assert w.getsampwidth()==2 and w.getnchannels()==2
        data=np.frombuffer(w.readframes(w.getnframes()),dtype="<i2")
        assert len(data)>1000 and np.max(np.abs(data.astype(np.int32)))<32767
        print(f"{f.name}: {w.getframerate()} Hz, stereo, {w.getnframes()/w.getframerate():.2f} s, 无削波")
