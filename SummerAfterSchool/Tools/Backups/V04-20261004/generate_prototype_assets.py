"""可重复生成原创圆片印花与物理反馈音效。所有素材为程序化创作，不是 AI 生成。"""
from pathlib import Path
import argparse
import json
import math
import wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Art/Pogs"
AUDIO = ROOT / "Assets/Resources/Audio"
SIZE = 1024
RNG = np.random.default_rng(19980817)
FONT = "C:/Windows/Fonts/msyh.ttc"
TITLE_FONT = "C:/Windows/Fonts/simkai.ttf"
THEMES = [
    ("汽水小火箭", "rocket", "#b75b3a", "#ef9f50"),
    ("月牙小队长", "moon", "#4b5482", "#8f9fd8"),
    ("西瓜冲浪手", "watermelon", "#44857a", "#e97876"),
    ("纸飞机一号", "plane", "#417eaa", "#75b4d8"),
    ("橘猫午睡", "cat", "#8b5444", "#dcac77"),
    ("云端邮差", "cloud", "#417e95", "#8ac5df"),
    ("星星捕手", "star", "#48586c", "#d6b36a"),
    ("糖果风车", "pinwheel", "#8b536e", "#e5a1cb"),
    ("小熊机车", "bearbike", "#5c7665", "#a2bcb0"),
    ("雨靴探险家", "boots", "#397a88", "#e5bd58"),
    ("蒸汽小火车", "train", "#725047", "#cb7358"),
    ("蓝鲸电台", "whale", "#35586d", "#669dbd"),
    ("萤火夏令营", "firefly", "#3d6254", "#b8d470"),
    ("红砖守卫", "brick", "#9b5340", "#c88167"),
    ("爆米花骑士", "popcorn", "#777551", "#dcae6e"),
    ("雪糕宇航员", "astronaut", "#516077", "#a7cee4"),
    ("彩虹水母", "jellyfish", "#765a87", "#a4b8e4"),
    ("霓虹溜溜球", "yoyo", "#715068", "#db84ca"),
    ("豆豆大力王", "strongbean", "#55756a", "#89bba9"),
    ("弹簧青蛙", "frog", "#456969", "#82b379"),
    ("潜水鸭船长", "duck", "#3f777c", "#ebc379"),
    ("葡萄泡泡龙", "dragon", "#69537d", "#b09bdd"),
    ("胶鞋跳跳侠", "sneaker", "#57666b", "#8aae97"),
    ("山丘巨人", "hillgiant", "#627c65", "#c59d6c"),
    ("铜铃巡游者", "bell", "#85693f", "#cead68"),
    ("铁皮月亮", "tinmoon", "#47586e", "#9baabd"),
    ("齿轮小狮子", "lion", "#776a4b", "#b09d71"),
    ("太阳徽章", "sun", "#b88f46", "#e4bb64"),
    ("银河独角兽", "unicorn", "#695485", "#c5a9e3"),
    ("夏天的金蝉", "cicada", "#725f3a", "#d7bc65"),
]


def polygon_star(cx, cy, r, count=5, inner=.45, angle=-math.pi/2):
    return [(cx+math.cos(angle+i*math.pi/count)*r*(1 if i%2==0 else inner),
             cy+math.sin(angle+i*math.pi/count)*r*(1 if i%2==0 else inner)) for i in range(count*2)]


def draw_emblem(draw, kind, accent):
    ink = "#49392c"
    cream = "#fff0c9"
    def face(x,y,s=1):
        for dx in [-30,30]: draw.ellipse((x+(dx-7)*s,y-7*s,x+(dx+7)*s,y+7*s),fill=ink)
        draw.arc((x-22*s,y+8*s,x+22*s,y+38*s),0,180,fill=ink,width=max(3,int(6*s)))
    def star(x,y,r): draw.polygon(polygon_star(x,y,r),fill=cream,outline=ink,width=5)
    def ellipse(box,fill): draw.ellipse(box,fill=fill,outline=ink,width=8)
    if kind in ["moon","tinmoon"]:
        ellipse((329,323,695,689),cream if kind=="moon" else accent)
        draw.ellipse((455,298,753,596),fill="#4b5482" if kind=="moon" else "#47586e")
        face(444,532,.8)
        star(633,573,63);star(327,329,29);star(723,386,35)
        if kind=="tinmoon":
            for x,y in [(367,453),(414,621),(550,645)]:
                draw.ellipse((x-8,y-8,x+8,y+8),fill=ink)
                draw.line((x-5,y,x+5,y),fill=cream,width=3)
    elif kind=="watermelon":
        draw.arc((290,448,734,744),0,180,fill="#b5ca8b",width=55)
        draw.pieslice((309,456,716,721),0,180,fill=accent,outline=ink,width=8)
        draw.line((308,588,719,588),fill=ink,width=8)
        for x,y in [(386,620),(464,658),(548,658),(631,620)]:
            draw.ellipse((x-5,y-10,x+5,y+10),fill=ink)
        face(512,501,1)
        draw.line((442,562,416,589),fill=ink,width=9);draw.line((582,562,610,589),fill=ink,width=9)
        for x in [295,425,555,685]: draw.arc((x,709,x+130,754),180,355,fill=cream,width=8)
        draw.ellipse((381,444,645,591),outline=ink,width=7)
        draw.arc((337,339,396,439),190,330,fill=cream,width=7)
    elif kind=="plane":
        draw.polygon([(297,446),(752,306),(553,684),(491,542),(384,589)],fill=cream,outline=ink,width=9)
        draw.polygon([(297,446),(491,542),(752,306),(384,589)],fill=accent,outline=ink,width=7)
        draw.line((491,542,752,306),fill=ink,width=7)
        draw.arc((316,547,665,761),35,190,fill=cream,width=8)
        draw.arc((284,588,485,736),155,310,fill=cream,width=5)
        star(317,327,29);star(717,668,26)
    elif kind=="cat":
        draw.ellipse((317,486,719,709),fill=accent,outline=ink,width=9)
        draw.arc((452,490,759,719),190,460,fill="#b97d4e",width=42)
        draw.polygon([(321,491),(317,332),(423,417),(565,404),(675,330),(689,504)],fill=accent,outline=ink,width=9)
        draw.ellipse((319,399,694,646),fill=accent,outline=ink,width=9)
        draw.polygon([(512,405),(480,465),(516,487),(540,452)],fill=ink)
        for x in [427,589]: draw.arc((x-29,486,x+29,535),0,180,fill=ink,width=9)
        draw.polygon([(494,543),(530,543),(512,561)],fill=ink)
        for x,dx in [(417,-70),(607,70)]:
            draw.line((x,558,x+dx,547),fill=ink,width=5);draw.line((x,583,x+dx,591),fill=ink,width=5)
        draw.text((668,328),"Z",font=ImageFont.truetype(FONT,45),fill=cream)
        draw.text((708,291),"z",font=ImageFont.truetype(FONT,31),fill=cream)
    elif kind=="cloud":
        draw.rounded_rectangle((293,397,731,623),radius=109,fill=cream,outline=ink,width=8)
        draw.ellipse((374,316,551,523),fill=cream);draw.ellipse((499,327,661,519),fill=cream)
        face(512,442,1.1)
        draw.rounded_rectangle((409,536,615,674),radius=12,fill=accent,outline=ink,width=8)
        draw.line((412,539,512,605,612,539),fill=ink,width=7)
        star(292,631,26);star(742,390,28)
    elif kind=="star":
        draw.polygon(polygon_star(502,481,183),fill=accent,outline=ink,width=10)
        face(501,471,1)
        draw.line((592,692,692,426),fill=cream,width=15)
        draw.ellipse((619,328,780,481),outline=cream,width=10)
        for x,y,r in [(287,598,36),(359,335,27),(667,664,32)]: star(x,y,r)
    elif kind=="pinwheel":
        draw.line((508,476,507,730),fill=ink,width=15)
        for points,color in [([(512,490),(300,285),(512,298)],accent),([(512,490),(707,297),(700,490)],cream),([(512,490),(721,701),(512,696)],accent),([(512,490),(307,683),(315,490)],cream)]:
            draw.polygon(points,fill=color,outline=ink,width=8)
        ellipse((472,450,552,530),"#e0b25d")
        star(718,579,27)
    elif kind=="bearbike":
        for x in [372,652]: ellipse((x-85,579,x+85,749),cream);ellipse((x-52,612,x+52,716),accent)
        draw.line((371,662,492,563,652,662,526,674,371,662),fill=ink,width=13)
        draw.line((619,586,660,654),fill=ink,width=13)
        draw.line((601,585,656,571),fill=ink,width=13)
        draw.ellipse((368,306,445,383),fill="#bc976c",outline=ink,width=7)
        draw.ellipse((551,306,628,383),fill="#bc976c",outline=ink,width=7)
        ellipse((396,335,599,531),"#bc976c")
        draw.ellipse((447,422,550,512),fill=cream)
        face(497,428,.8)
        draw.rounded_rectangle((439,512,553,612),radius=27,fill=accent,outline=ink,width=7)
        draw.line((531,538,608,581),fill=ink,width=12)
    elif kind=="boots":
        for x,y in [(309,377),(542,421)]:
            draw.rounded_rectangle((x,y,x+138,y+210),radius=14,fill=accent,outline=ink,width=8)
            draw.rounded_rectangle((x-1,y+163,x+214,y+278),radius=31,fill=accent,outline=ink,width=8)
            draw.line((x+2,y+245,x+207,y+245),fill=ink,width=8)
            draw.line((x+17,y+24,x+119,y+24),fill=cream,width=12)
        for x,y in [(304,314),(577,306),(751,528)]:
            draw.polygon([(x,y-24),(x-14,y+7),(x+14,y+7)],fill=cream)
            draw.ellipse((x-14,y-2,x+14,y+22),fill=cream)
    elif kind=="train":
        for x in [383,498,624]: ellipse((x-52,594,x+52,698),cream)
        draw.rounded_rectangle((308,488,659,626),radius=16,fill=accent,outline=ink,width=9)
        draw.rounded_rectangle((314,348,494,518),radius=10,fill=accent,outline=ink,width=9)
        draw.rounded_rectangle((350,387,448,476),radius=6,fill=cream,outline=ink,width=6)
        draw.rectangle((564,410,607,489),fill=cream,outline=ink,width=7)
        draw.polygon([(659,524),(741,634),(653,634)],fill=accent,outline=ink,width=8)
        draw.line((391,651,625,651),fill=ink,width=9)
        for x,y,r in [(584,352,35),(633,303,46),(699,293,32)]: draw.ellipse((x-r,y-r,x+r,y+r),fill=cream)
    elif kind=="whale":
        draw.polygon([(631,540),(720,428),(710,541),(785,558),(684,597)],fill=accent,outline=ink,width=8)
        ellipse((291,453,706,662),accent)
        draw.arc((326,542,628,665),0,180,fill=cream,width=34)
        draw.ellipse((372,506,388,522),fill=ink)
        draw.arc((449,335,550,469),178,305,fill=cream,width=9)
        draw.arc((491,340,620,479),228,360,fill=cream,width=9)
        draw.arc((363,432,730,715),176,355,fill=ink,width=12)
        draw.rounded_rectangle((292,477,328,559),radius=15,fill=cream,outline=ink,width=6)
        draw.rounded_rectangle((680,477,716,559),radius=15,fill=cream,outline=ink,width=6)
        for x,y in [(663,333),(725,385)]: draw.text((x,y),"♪",font=ImageFont.truetype(FONT,60),fill=cream)
    elif kind=="firefly":
        for x,y in [(366,458),(580,429),(516,569)]:
            draw.ellipse((x-55,y-28,x+5,y+35),fill=cream,outline=ink,width=5)
            draw.ellipse((x-5,y-28,x+55,y+35),fill=cream,outline=ink,width=5)
            ellipse((x-27,y-24,x+27,y+68),accent)
            draw.ellipse((x-23,y+34,x+23,y+72),fill="#fae3a0")
            draw.line((x-10,y-22,x-30,y-42),fill=ink,width=5);draw.line((x+10,y-22,x+30,y-42),fill=ink,width=5)
        draw.polygon([(319,727),(503,623),(692,727)],fill=accent,outline=ink,width=8)
        draw.polygon([(447,727),(503,650),(560,727)],fill=ink)
        star(697,349,27);star(435,303,19)
    elif kind=="brick":
        draw.rounded_rectangle((339,362,685,675),radius=25,fill=accent,outline=ink,width=9)
        for y in [432,503,574,645]: draw.line((344,y,680,y),fill=cream,width=6)
        for x,y in [(453,363),(570,433),(455,504),(570,575)]: draw.line((x,y,x,y+69),fill=cream,width=6)
        draw.ellipse((417,456,608,589),fill=cream,outline=ink,width=6);face(512,503,1.1)
        draw.line((334,544,289,586,300,650),fill=ink,width=12)
        draw.line((690,544,733,586,722,650),fill=ink,width=12)
        draw.line((424,677,412,732),fill=ink,width=17);draw.line((601,677,613,732),fill=ink,width=17)
        draw.line((728,475,728,734),fill=cream,width=10)
        star(728,448,38)
    elif kind=="popcorn":
        draw.polygon([(373,464),(651,464),(623,718),(401,718)],fill=accent,outline=ink,width=9)
        for x in [425,484,544,603]: draw.line((x,480,x,706),fill=cream,width=20)
        for x,y,r in [(389,436,61),(459,385,73),(554,397,73),(631,444,58),(508,474,79)]:
            draw.ellipse((x-r,y-r,x+r,y+r),fill=cream,outline=ink,width=5)
        face(510,583,1)
        draw.polygon([(290,420),(316,399),(343,644),(315,642)],fill=cream,outline=ink,width=6)
        ellipse((652,544,764,680),cream)
        star(708,608,36)
    elif kind=="astronaut":
        draw.rounded_rectangle((370,362,654,657),radius=116,fill=cream,outline=ink,width=9)
        draw.rounded_rectangle((401,399,623,514),radius=50,fill=accent,outline=ink,width=7)
        face(510,446,.8)
        draw.line((513,659,513,746),fill="#ba9066",width=35)
        draw.line((415,542,332,609),fill=ink,width=12);draw.line((611,542,694,589),fill=ink,width=12)
        draw.ellipse((480,550,544,614),fill=accent,outline=ink,width=6)
        star(311,350,35);star(722,394,27)
        draw.arc((360,675,656,758),5,170,fill=cream,width=5)
    elif kind=="jellyfish":
        for i,color in enumerate(["#eaa9aa",accent,"#dec987","#8cbba5"]):
            x=389+i*76
            draw.arc((x-35,521,x+35,715),0,180,fill=color,width=18)
            draw.arc((x+1,602,x+79,743),180,360,fill=color,width=18)
        draw.pieslice((329,327,695,686),180,360,fill=accent,outline=ink,width=9)
        draw.line((330,507,694,507),fill=ink,width=8)
        face(512,446,1)
        for x in [362,425,488,551,614]: draw.arc((x,477,x+64,544),0,180,fill=cream,width=9)
        for x,y in [(302,372),(728,634)]: draw.ellipse((x-16,y-16,x+16,y+16),outline=cream,width=6)
    elif kind=="yoyo":
        draw.arc((386,285,710,512),0,235,fill=cream,width=9)
        draw.line((395,390,483,508),fill=cream,width=8)
        ellipse((329,472,650,740),accent)
        ellipse((348,480,617,716),cream)
        ellipse((393,523,571,673),accent)
        star(482,598,52)
        draw.line((680,577,735,563),fill=cream,width=8)
        draw.line((663,680,709,720),fill=cream,width=8)
    elif kind=="strongbean":
        draw.rounded_rectangle((405,369,623,690),radius=108,fill=accent,outline=ink,width=9)
        face(512,467,1.1)
        draw.arc((401,350,584,483),179,340,fill=cream,width=11)
        draw.line((407,551,346,586,327,505,293,498),fill=ink,width=17)
        draw.line((619,551,680,586,699,505,733,498),fill=ink,width=17)
        for x in [294,734]:
            draw.ellipse((x-30,478,x+30,524),fill=accent,outline=ink,width=7)
        draw.line((458,688,435,746),fill=ink,width=15);draw.line((565,688,591,746),fill=ink,width=15)
    elif kind=="frog":
        ellipse((349,365,473,478),accent);ellipse((551,365,675,478),accent)
        ellipse((347,408,677,670),accent)
        for x in [413,611]:
            draw.ellipse((x-25,381,x+25,444),fill=cream,outline=ink,width=5)
            draw.ellipse((x-7,400,x+7,418),fill=ink)
        draw.arc((410,459,614,555),0,180,fill=ink,width=9)
        draw.ellipse((403,539,621,650),fill=cream)
        for y in [660,682,704]: draw.arc((449,y,575,y+38),0,350,fill=ink,width=7)
        draw.arc((308,597,439,714),100,245,fill=ink,width=10);draw.arc((585,597,716,714),300,75,fill=ink,width=10)
    elif kind=="duck":
        draw.ellipse((348,514,692,719),fill=accent,outline=ink,width=9)
        draw.ellipse((457,362,661,562),fill=accent,outline=ink,width=9)
        draw.polygon([(644,433),(755,466),(642,494)],fill="#df8653",outline=ink,width=7)
        draw.ellipse((592,419,611,438),fill=ink)
        draw.polygon([(459,359),(473,318),(599,318),(632,359)],fill=cream,outline=ink,width=7)
        draw.ellipse((522,331,552,351),fill=accent)
        draw.arc((397,546,573,676),0,180,fill=ink,width=7)
        draw.line((383,496,367,446,312,436),fill=cream,width=10)
        for x in [288,416,544,672]: draw.arc((x,701,x+120,745),0,180,fill=cream,width=7)
    elif kind=="dragon":
        draw.polygon([(404,477),(291,355),(310,602),(410,599)],fill=cream,outline=ink,width=7)
        draw.ellipse((375,436,613,710),fill=accent,outline=ink,width=9)
        draw.ellipse((434,559,566,696),fill=cream)
        draw.ellipse((433,349,664,531),fill=accent,outline=ink,width=9)
        draw.polygon([(469,358),(492,296),(518,355)],fill=cream,outline=ink,width=6)
        draw.polygon([(560,359),(586,297),(602,368)],fill=cream,outline=ink,width=6)
        draw.ellipse((593,399,610,416),fill=ink)
        draw.arc((559,438,666,488),0,150,fill=ink,width=6)
        draw.arc((277,551,460,760),25,280,fill=accent,width=38)
        for x,y,r in [(717,419,30),(743,362,22),(693,303,35)]: draw.ellipse((x-r,y-r,x+r,y+r),outline=cream,width=7)
    elif kind=="sneaker":
        draw.polygon([(311,609),(337,424),(428,401),(493,481),(624,529),(726,620),(714,684),(316,685)],fill=accent,outline=ink,width=9)
        draw.rounded_rectangle((304,645,730,709),radius=23,fill=cream,outline=ink,width=8)
        draw.arc((376,387,505,533),0,170,fill=cream,width=20)
        for x,y in [(463,516),(507,535),(551,554)]: draw.line((x-30,y+18,x+35,y-18),fill=cream,width=10)
        star(385,579,46)
        draw.line((626,426,706,381),fill=cream,width=9);draw.line((687,486,765,467),fill=cream,width=9)
    elif kind=="hillgiant":
        draw.polygon([(280,708),(359,479),(463,327),(569,464),(679,526),(745,708)],fill=accent,outline=ink,width=9)
        draw.polygon([(373,466),(463,327),(556,446),(506,420),(463,463),(428,414)],fill=cream)
        face(512,567,1.3)
        draw.line((326,560,264,619,296,672),fill=ink,width=17);draw.line((695,560,756,619,724,672),fill=ink,width=17)
        for x,y in [(311,398),(705,378)]:
            draw.line((x,y,x,y+74),fill=ink,width=9)
            draw.polygon([(x,y-39),(x-35,y+38),(x+35,y+38)],fill="#97b78a",outline=ink,width=6)
    elif kind=="bell":
        draw.ellipse((467,304,557,394),outline=cream,width=15)
        draw.polygon([(363,645),(405,583),(413,443),(455,369),(569,369),(611,443),(619,583),(661,645)],fill=accent,outline=ink,width=10)
        ellipse((358,622,666,689),accent)
        draw.ellipse((478,653,546,721),fill=cream,outline=ink,width=7)
        draw.arc((417,435,607,638),185,263,fill=cream,width=18)
        for x,y,r in [(298,406,28),(727,415,31),(314,674,21)]: star(x,y,r)
    elif kind=="lion":
        draw.polygon(polygon_star(512,502,237,12,.86),fill=accent,outline=ink,width=10)
        draw.ellipse((355,354,669,654),fill="#e4bc77",outline=ink,width=9)
        draw.ellipse((403,520,621,668),fill=cream)
        for x in [447,577]: draw.arc((x-27,451,x+27,499),185,355,fill=ink,width=10)
        draw.polygon([(486,534),(538,534),(512,565)],fill=ink)
        draw.line((512,563,512,589),fill=ink,width=8)
        draw.arc((466,562,514,621),0,145,fill=ink,width=7)
        draw.arc((510,562,558,621),35,180,fill=ink,width=7)
        for x,y in [(308,395),(699,665),(691,369)]:
            draw.polygon(polygon_star(x,y,38,8,.72),fill=cream,outline=ink,width=5)
            draw.ellipse((x-11,y-11,x+11,y+11),fill=accent,outline=ink,width=4)
    elif kind=="unicorn":
        draw.polygon([(361,691),(360,516),(454,395),(605,386),(677,464),(634,527),(561,510),(599,691)],fill=cream,outline=ink,width=9)
        draw.polygon([(458,399),(535,367),(501,320)],fill=accent,outline=ink,width=7)
        draw.polygon([(565,382),(608,276),(625,403)],fill="#e7c470",outline=ink,width=7)
        draw.polygon([(367,518),(422,465),(451,397),(508,396),(453,490),(405,611),(442,691),(357,691)],fill=accent,outline=ink,width=7)
        draw.arc((557,427,612,475),0,170,fill=ink,width=8)
        draw.ellipse((650,466,660,476),fill=ink)
        for x,y,r in [(304,410,36),(735,578,37),(651,704,24)]: star(x,y,r)
    elif kind=="cicada":
        draw.ellipse((304,411,512,704),fill=cream,outline=ink,width=8)
        draw.ellipse((512,411,720,704),fill=cream,outline=ink,width=8)
        for direction in [-1,1]:
            draw.line((512,440,512+direction*143,658),fill=accent,width=7)
            for y in [496,551,606]: draw.line((512,y,512+direction*98,y+38),fill=accent,width=5)
        draw.ellipse((463,389,561,700),fill=accent,outline=ink,width=9)
        for y in [521,568,615,662]: draw.line((472,y,550,y),fill=ink,width=5)
        ellipse((446,351,578,445),accent)
        for x in [463,561]: draw.ellipse((x-18,374,x+18,410),fill=ink)
        draw.line((472,351,447,323),fill=ink,width=5);draw.line((552,351,577,323),fill=ink,width=5)
        for x,y in [(311,352),(692,346)]: draw.text((x,y),"♪",font=ImageFont.truetype(FONT,63),fill=cream)
    elif kind == "tiger":
        draw.ellipse((266, 300, 414, 448), fill=accent, outline=ink, width=9)
        draw.ellipse((610, 300, 758, 448), fill=accent, outline=ink, width=9)
        draw.ellipse((296, 332, 388, 410), fill="#c97754")
        draw.ellipse((636, 332, 728, 410), fill="#c97754")
        draw.ellipse((305, 331, 719, 704), fill=accent, outline=ink, width=10)
        draw.ellipse((348, 538, 676, 709), fill=cream)
        draw.polygon([(512,342),(481,425),(512,456),(540,425)], fill=ink)
        draw.polygon([(325,432),(407,471),(352,494)], fill=ink)
        draw.polygon([(699,432),(617,471),(672,494)], fill=ink)
        draw.polygon([(318,514),(397,533),(349,560)], fill=ink)
        draw.polygon([(706,514),(627,533),(675,560)], fill=ink)
        for x in [430, 594]:
            draw.arc((x-40,474,x+40,543), 185, 355, fill=ink, width=14)
        draw.ellipse((482, 555, 542, 596), fill=ink)
        draw.line((512,591,512,622), fill=ink, width=10)
        draw.arc((459,585,513,648), 10, 150, fill=ink, width=9)
        draw.arc((511,585,565,648), 30, 170, fill=ink, width=9)
        for x in [399,421,603,625]:
            draw.ellipse((x-4,582,x+4,590), fill=ink)
    elif kind == "rocket":
        draw.polygon([(425,566),(349,663),(452,641)], fill=accent, outline=ink, width=8)
        draw.polygon([(599,566),(675,663),(572,641)], fill=accent, outline=ink, width=8)
        draw.polygon([(460,638),(512,768),(564,638)], fill="#e18b4b", outline=ink, width=8)
        draw.polygon([(486,649),(512,713),(538,649)], fill="#ffdf80")
        draw.ellipse((419,347,605,668), fill=cream, outline=ink, width=10)
        draw.polygon([(420,435),(512,287),(604,435)], fill=accent, outline=ink, width=10)
        draw.ellipse((458,457,566,565), fill="#7fa4b0", outline=ink, width=10)
        draw.ellipse((475,472,507,501), fill="#e2ecda")
        draw.line((440,607,584,607), fill=ink, width=10)
        for x,y,r in [(315,434,30),(699,394,39),(717,605,23)]:
            draw.polygon(polygon_star(x,y,r), fill=accent, outline=ink, width=4)
        draw.arc((301,351,718,724), 35, 153, fill=accent, width=8)
    elif kind == "fish":
        draw.polygon([(397,453),(293,361),(302,486),(281,590),(397,555)], fill=cream, outline=ink, width=9)
        draw.ellipse((356,398,722,631), fill=cream, outline=ink, width=9)
        draw.ellipse((468,427,619,588), fill=accent)
        draw.ellipse((605,427,692,504), fill=accent)
        draw.ellipse((640,475,658,493), fill=ink)
        draw.arc((464,493,549,658), 189, 310, fill=ink, width=6)
        draw.polygon([(515,424),(557,359),(603,420)], fill=accent, outline=ink, width=6)
        draw.arc((646,485,739,569), 83, 158, fill=ink, width=5)
        for x,y in [(321,641),(687,356),(745,548)]:
            draw.ellipse((x-17,y-17,x+17,y+17), outline=cream, width=6)
        for y in [669,707]:
            for x in [300,400,500,600]:
                draw.arc((x,y-30,x+100,y+30),180,360,fill=accent,width=7)
    elif kind == "kite":
        draw.polygon([(512,302),(684,476),(512,656),(340,476)], fill=cream, outline=ink, width=10)
        draw.polygon([(512,302),(684,476),(512,476)], fill=accent)
        draw.polygon([(512,476),(512,656),(340,476)], fill=accent)
        draw.line((512,304,512,654),fill=ink,width=7)
        draw.line((342,476,682,476),fill=ink,width=7)
        draw.arc((402,632,602,777),170,348,fill=ink,width=6)
        draw.polygon([(546,699),(592,677),(576,724),(611,749)],fill=accent,outline=ink,width=5)
        for box in [(243,365,398,426),(646,579,801,633)]:
            draw.rounded_rectangle(box,radius=29,fill=cream)
            draw.ellipse((box[0]+40,box[1]-25,box[0]+100,box[1]+37),fill=cream)
    elif kind == "bolt":
        draw.polygon(polygon_star(512,506,226,8,.68),fill=accent,outline=ink,width=9)
        draw.polygon([(523,310),(398,525),(486,525),(447,705),(636,469),(551,469),(594,310)],fill=cream,outline=ink,width=10)
        for x,y in [(299,342),(736,370),(729,653)]:
            draw.polygon(polygon_star(x,y,28),fill=cream,outline=ink,width=4)
    elif kind == "sun":
        draw.polygon(polygon_star(512,502,228,12,.73),fill=accent,outline=ink,width=8)
        draw.ellipse((352,342,672,662),fill="#f3c877",outline=ink,width=10)
        for x in [453,571]:
            draw.arc((x-27,450,x+27,496),180,355,fill=ink,width=9)
        draw.arc((438,479,586,578), 5, 174, fill=ink, width=10)
        draw.ellipse((399,502,442,525),fill="#dc8b61")
        draw.ellipse((582,502,625,525),fill="#dc8b61")


def make_pog(index, title, kind, bg, accent):
    img = Image.new("RGBA", (SIZE,SIZE), (0,0,0,0))
    draw = ImageDraw.Draw(img)
    draw.ellipse((18,18,1006,1006),fill="#71543a")
    draw.ellipse((30,30,994,994),fill="#dfb780")
    draw.ellipse((49,49,975,975),fill=bg)
    draw.ellipse((65,65,959,959),outline="#f4dc9f",width=6)
    draw.ellipse((83,83,941,941),outline="#f4dc9f",width=2)
    # 小网点和放射线模拟 1990 年代纸印刷，不使用已有品牌角色。
    for x in range(126,920,28):
        for y in range(126,920,28):
            if (x-512)**2+(y-512)**2 < 410**2:
                draw.ellipse((x-2,y-2,x+2,y+2),fill=(255,238,190,28))
    for i in range(48):
        a = i*math.tau/48
        r1 = 368 if i%2 else 343
        draw.line((512+math.cos(a)*r1,512+math.sin(a)*r1,
                   512+math.cos(a)*415,512+math.sin(a)*415),fill=(255,234,185,105),width=3)
    draw.ellipse((255,258,769,772),fill=(254,224,161,27),outline=(255,241,200,95),width=4)
    draw_emblem(draw,kind,accent)
    draw.rounded_rectangle((305,144,719,226),radius=40,fill="#f1d59a",outline="#4f3e30",width=4)
    f = ImageFont.truetype(FONT,36)
    draw.text((512,181),"放学后 · 童年珍藏",fill="#674c34",font=f,anchor="mm")
    draw.rounded_rectangle((262,775,762,881),radius=19,fill="#f7e6bc",outline="#584432",width=5)
    titlefont = ImageFont.truetype(TITLE_FONT,76)
    draw.text((514,829),title,fill="#543e2c",font=titlefont,anchor="mm",stroke_width=1)
    draw.text((512,924),f"SUMMER CLUB  ·  NO. {index+1:02}",fill="#f4dc9f",font=ImageFont.truetype(FONT,25),anchor="mm")
    # 有限纸纹确保在小尺寸下清楚，同时让边缘稍有岁月感。
    arr = np.array(img).astype(np.int16)
    grain = RNG.normal(0,1.6,(SIZE,SIZE,1))
    arr[:,:,:3] = np.clip(arr[:,:,:3]+grain,0,255)
    img=Image.fromarray(arr.astype(np.uint8)).resize((768,768),Image.Resampling.LANCZOS)
    img.save(ART/f"pog_{index:02}.png")


def make_back():
    img=Image.new("RGBA",(SIZE,SIZE),(0,0,0,0));draw=ImageDraw.Draw(img)
    draw.ellipse((18,18,1006,1006),fill="#75573b")
    draw.ellipse((31,31,993,993),fill="#d9ba88")
    draw.ellipse((48,48,976,976),fill="#e7d7af")
    draw.ellipse((69,69,955,955),outline="#96734c",width=5)
    draw.ellipse((89,89,935,935),outline="#b69261",width=2)
    for x in range(124,920,40):
        for y in range(124,920,40):
            if (x-512)**2+(y-512)**2<395**2:
                draw.ellipse((x-3,y-3,x+3,y+3),fill="#c5ad80")
    draw.ellipse((229,221,795,787),fill="#dfc69a",outline="#94714b",width=6)
    draw.ellipse((243,235,781,773),outline="#94714b",width=2)
    # 用低饱和双色印章让翻面结果一眼可辨。
    draw.polygon(polygon_star(512,396,106,12,.76),fill="#ad8351",outline="#846340",width=4)
    draw.ellipse((438,322,586,470),fill="#e7d7af",outline="#846340",width=5)
    for x in [483,541]:draw.arc((x-12,365,x+12,391),180,360,fill="#846340",width=5)
    draw.arc((476,380,548,433),0,180,fill="#846340",width=5)
    draw.text((512,545),"放学后",font=ImageFont.truetype(TITLE_FONT,129),fill="#856341",anchor="mm",stroke_width=1)
    draw.text((512,639),"属于我们的夏天",font=ImageFont.truetype(FONT,39),fill="#997950",anchor="mm")
    draw.line((360,686,664,686),fill="#af8c5d",width=3)
    draw.text((512,729),"SUMMER CLUB",font=ImageFont.truetype(FONT,27),fill="#997950",anchor="mm")
    draw.text((512,859),"珍藏一张片 · 留住一个夏天",font=ImageFont.truetype(TITLE_FONT,35),fill="#96734c",anchor="mm")
    arr=np.array(img).astype(np.int16);arr[:,:,:3]=np.clip(arr[:,:,:3]+RNG.normal(0,1.5,(SIZE,SIZE,1)),0,255)
    Image.fromarray(arr.astype(np.uint8)).resize((768,768),Image.Resampling.LANCZOS).save(ART/"pog_back.png")


def write_wav(name, data, sr=44100, peak=.85):
    data = np.asarray(data,dtype=np.float64)
    if data.ndim==1:
        data=np.stack([data,data],axis=1)
    maxpeak=np.max(np.abs(data))
    if maxpeak>0:
        data *= peak/maxpeak
    pcm=(np.clip(data,-1,1)*32767).astype("<i2")
    with wave.open(str(AUDIO/f"{name}.wav"),"wb") as out:
        out.setnchannels(2);out.setsampwidth(2);out.setframerate(sr);out.writeframes(pcm.tobytes())


def tone(freq,duration,sr=44100,decay=4.0):
    t=np.arange(round(duration*sr))/sr
    env=(1-np.exp(-t*350))*np.exp(-t*decay)
    signal=np.sin(math.tau*freq*t)+.3*np.sin(math.tau*freq*2*t)*np.exp(-t*7)+.09*np.sin(math.tau*freq*4*t)*np.exp(-t*14)
    return signal*env


def make_audio():
    sr=44100
    def noise(d): return RNG.normal(0,1,round(sr*d))
    def impact(d,low,punch):
        t=np.arange(round(sr*d))/sr
        n=noise(d)
        lp=np.convolve(n,np.ones(9)/9,mode="same")
        return lp*np.exp(-t*34)*punch+np.sin(math.tau*(low*t+28*t*np.exp(-t*28)))*np.exp(-t*21)*.7
    write_wav("slap",impact(.38,125,.62),peak=.92)
    write_wav("smash",impact(.62,62,.85)+noise(.62)*np.exp(-np.arange(round(sr*.62))/sr*55)*.11,peak=.98)
    t=np.arange(round(sr*.44))/sr
    write_wav("skim",np.convolve(noise(.44),np.ones(14)/14,mode="same")*np.sin(np.minimum(t*math.pi/.4,math.pi))**2*.4+np.sin(math.tau*(480*t-300*t*t))*np.exp(-t*18)*.15,peak=.6)
    t=np.arange(round(sr*.42))/sr
    flip=impact(.42,260,.3)*.4
    for start,freq in [(.04,660),(.11,880),(.18,1320)]:
        s=tone(freq,.18,decay=16)*.24;at=round(start*sr);flip[at:at+len(s)]+=s[:len(flip)-at]
    write_wav("flip",flip,peak=.66)
    write_wav("ui",tone(880,.13,decay=22)+tone(1320,.13,decay=30)*.22,peak=.3)
    for name,notes in [("win",[72,76,79,84]),("lose",[64,60,57])]:
        data=np.zeros(round(sr*1.35))
        for i,midi in enumerate(notes):
            s=tone(440*2**((midi-69)/12),.65,decay=5)*.6
            at=round(i*.14*sr);data[at:at+len(s)]+=s
        write_wav(name,data,peak=.56 if name=="win" else .36)
    # 室外风声、蝉声与鸟鸣；不包含他人录音。
    duration=24
    n=noise(duration)
    wind=np.convolve(n,np.ones(250)/250,mode="same")*.18
    t=np.arange(len(n))/sr
    ambience=wind+np.sin(math.tau*4700*t)*(.5+.5*np.sin(math.tau*17*t))*(.014+.008*np.sin(math.tau*.13*t))
    for start in [1.2,4.3,9.7,13.8,18.5,21.7]:
        bt=np.arange(round(.31*sr))/sr
        bird=np.sin(math.tau*(2350*bt+1700*bt*bt))*(np.sin(math.pi*bt/.31)**2)*.065
        at=round(start*sr);ambience[at:at+len(bird)]+=bird
    # 短首尾渐变避免点击；鸟鸣和风声低峰值不盖过击打。
    fade=np.minimum(np.arange(len(n))/sr/.6,1)*np.minimum((len(n)-np.arange(len(n)))/sr/.6,1)
    write_wav("ambience",ambience*fade,peak=.17)
    make_fallback_music()


def make_fallback_music():
    """原创 16 小节原型配乐，便于在本地 ACE-Step 不可用时仍有完整体验。"""
    sr=44100;bpm=104;beat=60/bpm;duration=beat*64
    song=np.zeros((round(duration*sr),2))
    def add(midi,when,length,vol=.15,pan=.0):
        freq=440*2**((midi-69)/12)
        s=tone(freq,length,sr,3.8 if midi>=65 else 2.8)*vol
        pos=round(when*sr)
        for c,gain in enumerate([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)]):
            for offset,g in [(0,1),(round(sr*.117),.13),(round(sr*.231),.07)]:
                ids=(np.arange(len(s))+pos+offset)%len(song)
                song[ids,c]+=s*g*gain
    progression=[[48,52,55,59],[45,48,52,55],[53,57,60,64],[55,59,62,65]]
    melody=[[76,79,81,79,76,72,74,76],[74,76,79,76,72,71,69,72],
            [72,76,79,81,79,76,74,72],[74,76,79,76,74,71,72,0]]
    for bar in range(16):
        chord=progression[bar%4]
        base=bar*4*beat
        for i in range(8):
            add(chord[i%4]+12,base+i*beat/2,beat*1.35,.07,-.32)
            midi=melody[(bar//2)%4][i]
            if midi:
                add(midi,base+i*beat/2,beat*1.5,.115,.22)
        add(chord[0]-12,base,beat*2.8,.095,-.1)
        add(chord[0]-12,base+2*beat,beat*1.8,.065,-.1)
    # 循环渲染采用环形累加，尾音跨过边界，音乐可以无缝播放。
    write_wav("summer_loop",song,peak=.42)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--only",choices=["art","audio","all"],default="all")
    args=parser.parse_args()
    ART.mkdir(parents=True,exist_ok=True);AUDIO.mkdir(parents=True,exist_ok=True)
    if args.only in ["art","all"]:
        for i,t in enumerate(THEMES): make_pog(i,*t)
        make_back()
        contact=Image.new("RGB",(6*200,5*232),"#eadcc3")
        for i,t in enumerate(THEMES):
            card=Image.open(ART/f"pog_{i:02}.png").resize((188,188),Image.Resampling.LANCZOS)
            x=(i%6)*200+6;y=(i//6)*232+4;contact.paste(card,(x,y),card)
            ImageDraw.Draw(contact).text((x+94,y+200),f"{i:02}  {t[0]}",anchor="mm",font=ImageFont.truetype(FONT,18),fill="#563f2d")
        contact.save(ROOT/"Tools/pog_contact_sheet.jpg",quality=92)
    if args.only in ["audio","all"]: make_audio()
    bgm_source="original procedural composition; replace with local AI music when generation succeeds"
    ai_status=ROOT/"Tools/LocalAI/BGM/generation_status.json"
    if args.only=="art" and ai_status.exists():
        state=json.loads(ai_status.read_text(encoding="utf-8"))
        if state.get("success"):bgm_source="local ACE-Step 1.5 turbo + 0.6B LM; see Tools/LocalAI/BGM/generation_status.json"
    manifest={"source":"original procedural artwork and feedback sound synthesis; not AI generated", "seed":19980817,
        "pogs":[{"id":f"pog_{i:02}","title":t[0],"motif":t[1],"size":[768,768]} for i,t in enumerate(THEMES)],
        "audio":["slap","smash","skim","flip","win","lose","ui","ambience","summer_loop"],"card_back":"pog_back",
        "fonts":[FONT,TITLE_FONT],"bgm_source":bgm_source}
    (ROOT/"Tools/procedural_asset_manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    print(f"Generated {len(THEMES)} pog artworks and original WAV feedback at {ROOT}")


if __name__=="__main__": main()
