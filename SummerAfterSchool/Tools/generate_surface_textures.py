"""生成无缝原创微缩街区材质；不使用外部贴图。"""
from pathlib import Path
import math
import numpy as np
from PIL import Image,ImageDraw,ImageFilter

ROOT=Path(__file__).resolve().parents[1]
OUTPUT=ROOT/"Assets/Resources/Art/Surfaces"
OUTPUT.mkdir(parents=True,exist_ok=True)
N=512
rng=np.random.default_rng(19980617)
yy,xx=np.mgrid[:N,:N]


def save(name,arr):
    Image.fromarray(np.clip(arr,0,255).astype(np.uint8)).save(OUTPUT/(name+".png"))


grain=rng.normal(0,1.8,(N,N,1))
slow=(np.sin(xx*math.tau/N*3+np.cos(yy*math.tau/N*2))+np.cos(yy*math.tau/N*5))*2
plaster=np.zeros((N,N,3))+np.array([228,216,192])
plaster+=slow[:,:,None]+grain
save("plaster",plaster)

wood=np.zeros((N,N,3))+np.array([162,112,71])
curve=yy*math.tau/N+np.sin(xx*math.tau/N)*.15+np.sin(xx*math.tau/N*3)*.018
g=np.sin(curve*21)+np.sin(curve*41)*.42+np.sin(curve*9)*.43
wood+=g[:,:,None]*7+grain
wood[(yy%128)<3]-=28
for cx,cy in [(133,54),(372,347)]:
    dx=np.minimum(np.abs(xx-cx),N-np.abs(xx-cx));dy=np.minimum(np.abs(yy-cy),N-np.abs(yy-cy))
    r=np.sqrt((dx/2)**2+(dy*2)**2)
    rings=np.sin(r*.32)*np.exp(-r/21)
    wood+=rings[:,:,None]*18
save("wood",wood)

brick=np.zeros((N,N,3))+np.array([185,157,125])
row=yy//64
bx=(xx+(row%2)*64)%128
by=yy%64
mortar=(bx<7)|(by<7)
bricks=np.zeros_like(brick)+np.array([164,87,60])
indices=(xx+(row%2)*64)//128
variation=(np.sin(indices*11+row*29)+np.cos(indices*13-row*3))*7
bricks+=variation[:,:,None]+grain*2
edges=np.minimum.reduce([bx,128-bx,by,64-by])
bricks+=(np.clip(12-edges,0,6)*1.6)[:,:,None]
brick[~mortar]=bricks[~mortar]
brick+=grain*.6
save("brick",brick)

stone=np.zeros((N,N,3))+np.array([159,156,140])
noise=(np.sin(xx*math.tau/N*7+np.cos(yy*math.tau/N*5))*3+np.cos(yy*math.tau/N*17+np.sin(xx*math.tau/N*9))*2)
stone+=noise[:,:,None]+grain*1.2
sx=xx%256;sy=yy%256
joint=(sx<4)|(sy<4)
stone[joint]=np.array([105,110,99])+grain[joint]
save("stone",stone)

cloth=np.zeros((N,N,3))+np.array([193,168,132])
cloth+=((xx%4==0)*-10+(yy%4==0)*7)[:,:,None]+grain*.7
save("cloth",cloth)
print("Generated seamless brick, wood, plaster, stone and canvas textures.")
