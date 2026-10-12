using UnityEngine;

namespace Childhood
{
    // V1 构建器保持原样；新版访问变体只改变围观街区，不改变比赛几何和 Boss 坐标。
    internal sealed partial class NeighborhoodBuilder
    {
        static int StableHash(string value)
        {
            unchecked { int hash = 1998; foreach (char c in value ?? "home") hash = hash * 31 + c; return hash & 0x7fffffff; }
        }

        int Visit { get { return Mathf.Clamp(environment.visitVariant, 0, 2); } }

        Collider BuildV2()
        {
            var floor = Box("V2 · 连续实体基础地面", new Vector3(0, -.16f, 3), new Vector3(27, .32f, 29), p.Ground);
            var collider = floor.AddComponent<BoxCollider>();
            collider.sharedMaterial = (PhysicsMaterial)own(new PhysicsMaterial("基础地面 · " + environment.locationId)
            {
                dynamicFriction = p.Friction, staticFriction = p.Friction + .08f,
                frictionCombine = PhysicsMaterialCombine.Average
            });
            collider.contactOffset = .002f;
            Group("访问布景 " + environment.locationId + " · 变体 " + Visit, Vector3.zero);
            switch (environment.locationId)
            {
                case "home": HomeV2(); break;
                case "school_gate": SchoolV2(); break;
                case "alley": AlleyV2(); break;
                case "old_tree": TreeV2(); break;
                case "square": SquareV2(); break;
                case "shop": ShopV2(); break;
                case "corridor": CorridorV2(); break;
                case "playground": PlaygroundV2(); break;
                default: SchoolV2(); break;
            }
            BuildArenaTerrain(collider);
            ChalkArena();
            VisitProps();
            BackgroundV2();
            Physics.SyncTransforms();
            return collider;
        }

        bool BuildNewArenaTerrain(Transform geometry)
        {
            switch (environment.locationId)
            {
                case "square":
                    arenaTerrain.Description = "水磨石棋盘、拼花砖、树池三级台阶和铸铁井盖";
                    PatternedSlab("广场拼花水磨石", new Vector3(-.83f,0,1.2f), 1.10f,1.06f,.049f,p.Cream,geometry);
                    ArenaSlab("广场棋盘高砖",new Vector3(.83f,0,.25f),1.10f,1.04f,.055f,0,0,.055f,p.Stone,geometry);
                    ArenaSlab("树池缓坡石",new Vector3(1.8f,0,1.65f),1.17f,1.11f,.087f,-.025f,.018f,.09f,p.Paving,geometry);
                    ArenaSteps(geometry,false,false,2.8f);
                    ArenaManhole(new Vector3(-2.45f,0,.43f),.52f,.043f,geometry);
                    break;
                case "shop":
                    arenaTerrain.Description = "遮雨棚下的干水泥、磨旧门槛、缓坡砖和侧边台阶";
                    // 宽磨圆砖沿接到地面，擦地出手可真正爬上砖面。中央支撑面均不小于圆片直径 .62。
                    ArenaSlab("汽水箱旁水泥砖",new Vector3(-.83f,0,1.2f),1.1f,1.05f,.047f,0,0,.20f,p.Stone,geometry,true);
                    PatternedSlab("小卖部门口旧瓷砖",new Vector3(.83f,0,.25f),1.12f,1.04f,.039f,p.Cream,geometry,.20f,true);
                    ArenaSlab("檐下旧水泥缓坡",new Vector3(-1.86f,0,.16f),1.08f,1.03f,.085f,.029f,-.02f,.09f,p.Stone,geometry);
                    ArenaBlock("磨圆的后门槛",new Vector3(.8f,.035f,2.79f),new Vector3(2.25f,.07f,.41f),p.Wood,geometry);
                    ArenaSteps(geometry,true,false,2.83f);
                    break;
                case "corridor":
                    arenaTerrain.Description = "室内干燥水磨石、两块拼花砖、门边缓坡和墙边三级台阶";
                    PatternedSlab("走廊水磨石拼花砖",new Vector3(-.83f,0,1.2f),1.12f,1.06f,.043f,p.Paving,geometry);
                    PatternedSlab("走廊菱形地砖",new Vector3(.83f,0,.25f),1.12f,1.04f,.042f,p.Cream,geometry);
                    ArenaSlab("教室门边磨旧斜砖",new Vector3(1.84f,0,1.54f),1.16f,1.08f,.082f,-.024f,.022f,.08f,p.Stone,geometry);
                    ArenaSteps(geometry,true,true,-2.83f);
                    break;
                case "playground":
                    arenaTerrain.Description = "跑道红砖、宽缓坡、实体排水槽盖板和观台三级台阶";
                    ArenaSlab("跑道平整红砖",new Vector3(-.83f,0,1.2f),1.10f,1.04f,.047f,0,0,.045f,p.TileRed,geometry);
                    ArenaSlab("跑道高起红砖",new Vector3(.83f,0,.25f),1.1f,1.04f,.067f,0,0,.055f,p.TileRed,geometry);
                    ArenaSlab("跑道宽缓坡",new Vector3(-1.94f,0,.22f),1.18f,1.06f,.095f,.027f,-.019f,.12f,p.Brick,geometry);
                    ArenaBlock("排水槽 · 实体完整盖板",new Vector3(2.72f,.019f,1.44f),new Vector3(.56f,.038f,2.06f),p.Iron,geometry);
                    for (int i=0;i<16;i++) ArenaPaintLine("排水槽盖板细缝",new Vector3(2.49f,.039f,.5f+i*.125f),new Vector3(2.95f,.039f,.5f+i*.125f),.018f,p.DarkRoof,geometry);
                    ArenaSteps(geometry,false,false,2.78f);
                    ArenaManhole(new Vector3(-2.48f,0,2.19f),.51f,.045f,geometry);
                    break;
                default: return false;
            }
            return true;
        }

        void Building(string name, Vector3 position, float width, float depth, int floors, Material wall, bool tiled = true)
        {
            var building = Group(name, position);
            float height = floors*1.48f;
            Box("楼体",new Vector3(0,height*.5f,0),new Vector3(width,height,depth),wall,building);
            Box("墙裙",new Vector3(0,.28f,-depth*.5f-.015f),new Vector3(width,.56f,.08f),p.Brick,building);
            int windows=Mathf.Max(1,Mathf.FloorToInt(width/1.75f));
            for(int level=0;level<floors;level++)
            {
                Box("楼层腰线",new Vector3(0,level*1.48f+.08f,-depth*.5f-.06f),new Vector3(width+.09f,.09f,.16f),p.Cream,building);
                for(int col=0;col<windows;col++) Window(new Vector3((col-(windows-1)*.5f)*1.6f,level*1.48f+.86f,-depth*.5f-.08f),1.0f,.95f,building,(col+Visit)%3==1);
            }
            if(tiled) Roof(building,width+.35f,depth+.35f,height,.66f);
            else Box("平屋顶女儿墙",new Vector3(0,height+.15f,0),new Vector3(width+.2f,.3f,depth+.2f),p.Cream,building);
        }

        void HomeV2()
        {
            if(Visit==0) Courtyard();
            else
            {
                Building(Visit==1?"外婆家 · 转角两层小楼":"外婆家 · 白墙瓦房",new Vector3(Visit==1?-2:0,0,7.7f),Visit==1?7.8f:10.8f,4,Visit==1?2:1,p.Plaster);
                Building("院边储物房",new Vector3(Visit==1?6.3f:-7,0,5.8f),3.2f,3,1,p.Brick);
                Text("院门木牌","外婆家的小院",new Vector3(0,1.1f,4.67f),.32f,p.Wood.color);
                LowWall(new Vector3(-4.9f,0,4.9f),2.5f);LowWall(new Vector3(4.9f,0,4.9f),2.5f);
            }
            var pergola=Group("葡萄架与晾晒角",new Vector3(-6.1f,0,1.6f));
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2) Rod("葡萄架木柱",new Vector3(x*1.1f,0,z*1.65f),new Vector3(x*1.1f,2.5f,z*1.65f),.065f,p.Wood,pergola);
            for(int i=0;i<7;i++) Rod("棚架横木",new Vector3(-1.3f,2.5f,-1.65f+i*.55f),new Vector3(1.3f,2.5f,-1.65f+i*.55f),.045f,p.Wood,pergola);
            for(int i=0;i<15;i++) Ball("葡萄叶团",new Vector3(Range(-1.15f,1.15f),2.62f,Range(-1.65f,1.65f)),new Vector3(.65f,.14f,.72f),p.Canopy,pergola);
            for(int i=0;i<4+Visit;i++) FlowerPot(new Vector3(4.65f+i*.55f,.01f,4.8f),.7f);
            Bench(new Vector3(5.7f,0,1.1f),-17);Stool(new Vector3(-5.4f,0,-.9f),.8f);
            Tree(new Vector3(7.4f,0,7.1f),.73f); StreetLamp(new Vector3(7.9f,0,-.3f));
        }

        void SchoolV2()
        {
            School();
            if(Visit==1) Building("校门侧翼 · 图书室",new Vector3(-9,0,8.9f),4,5,2,p.Mint,false);
            if(Visit==2)
            {
                Building("校门侧翼 · 四层实验楼",new Vector3(9.4f,0,9.8f),4.7f,5,4,p.Cream,false);
                Box("连廊顶棚",new Vector3(6.5f,2.9f,7.4f),new Vector3(3,.22f,1.5f),p.Mint);
            }
            for(int i=0;i<3+Visit;i++) Bicycle(new Vector3(-7.5f+i*.68f,0,2.45f),76,p.Blue);
            Tree(new Vector3(6.8f,0,5.2f),.8f); Tree(new Vector3(-8.9f,0,3.4f),.68f);
            Stall(new Vector3(5.85f,0,-.45f),Visit==0?"放学冰棍":Visit==1?"旧书交换":"铅笔橡皮",Visit);
            StreetLamp(new Vector3(-7.6f,0,-1.1f));StreetLamp(new Vector3(8.2f,0,2.8f));
        }

        void AlleyV2()
        {
            // 两侧窄巷形成完整独立的建筑剪影；中央战场开阔。
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++) Building("汽水胡同 · 排屋 " + side+"/"+i,new Vector3(side*(6.35f+Visit*.22f),0,2.4f+i*4.1f),3.7f,3.7f,1+(i+Visit)%3,i%2==0?p.Brick:p.Plaster,Visit!=2);
                Box("胡同排水沟",new Vector3(side*4.05f,.005f,3.3f),new Vector3(.14f,.008f,13),p.DarkRoof);
            }
            Building("胡同尽头的钟楼",new Vector3(Visit==1?3:-1,0,16),4,3,Visit==2?4:2,p.Mint,false);
            for(int i=0;i<4;i++) Cable("晾衣绳",new Vector3(-4.3f,3.6f+i*.12f,4.8f+i*1.2f),new Vector3(4.3f,3.2f,4.8f+i*1.2f),.22f,.012f);
            for(int i=0;i<7;i++) Box("风里的彩色床单",new Vector3(-2.7f+i*.85f,2.85f,5.8f+Visit*.8f),new Vector3(.5f,.86f,.025f),i%2==0?p.Paper:p.Mint);
            Stall(new Vector3(-5.2f,0,-1.1f),Visit==0?"修车小摊":Visit==1?"夏日汽水":"风车小摊",Visit);
            Bicycle(new Vector3(5.05f,0,-.8f),12,p.Mint);Crate(new Vector3(4.7f,0,3.7f),root,true);
            Text("胡同墙上的粉笔字","汽 水 胡 同",new Vector3(5.2f,1.8f,1.8f),.34f,p.Paper.color,null,Quaternion.Euler(0,-90,0));
            StreetLamp(new Vector3(-5.0f,0,3.3f));
        }

        void TreeV2()
        {
            Tree(new Vector3(Visit==1?-5.1f:-5.5f,0,3.65f),Visit==2?1.45f:1.25f);
            Tree(new Vector3(6.6f,0,8),.81f);
            var pavilion=Group(Visit==0?"树荫六角凉亭":Visit==1?"老槐树旁的长廊":"旧公园戏台",new Vector3(Visit==1?3.8f:.6f,0,9.4f));
            float width=Visit==1?9:Visit==2?8:5;
            for(int side=-1;side<=1;side+=2) for(int z=-1;z<=1;z+=2) Cylinder("凉亭木柱",new Vector3(side*(width*.5f-.25f),1.5f,z*1.65f),.09f,3,p.Wood,pavilion);
            Roof(pavilion,width+1,4.5f,3.1f,Visit==0?1.3f:.65f);
            if(Visit==2) Box("戏台背景屏",new Vector3(0,1.4f,1.6f),new Vector3(7.7f,2.6f,.15f),p.Red,pavilion);
            Bench(new Vector3(4.8f,0,1.15f),-18);Bench(new Vector3(.7f,0,5.7f),0);
            for(int i=0;i<12;i++) FlowerPot(new Vector3(-8.3f+i*.48f,.01f,7.6f),.66f);
            Cylinder("老树圆形树池",new Vector3(-5.5f,.1f,3.65f),1.25f,.2f,p.Stone);
            for(int i=0;i<18;i++) Ball("树池小草",new Vector3(Range(-8,-4.4f),.07f,Range(-1,6)),new Vector3(.35f,.13f,.33f),p.Grass);
            Stall(new Vector3(6.25f,0,-.6f),Visit==0?"爷爷的棋摊":Visit==1?"蒲扇小摊":"纸风车",Visit);
            StreetLamp(new Vector3(6.2f,0,4.1f));
        }

        void SquareV2()
        {
            for(int i=0;i<3;i++) Building("广场住宅塔楼 " + i,new Vector3(-9+i*8.9f,0,12.7f+(i%2)*1.4f),5.8f,5,3+(i+Visit)%4,i==1?p.Mint:p.Plaster,false);
            var fountain=Group(Visit==0?"广场圆形旱喷泉":Visit==1?"广场花钟":"广场露天舞台",new Vector3(Visit==1?-2:1,0,6.3f));
            if(Visit<2)
            {
                Cylinder("水池石台",new Vector3(0,.25f,0),1.85f,.5f,p.Stone,fountain);
                Cylinder("蓝色浅水",new Vector3(0,.51f,0),1.61f,.025f,p.Blue,fountain);
                Cylinder("中央喷泉柱",new Vector3(0,1.15f,0),.15f,1.3f,p.Cream,fountain);
                for(int i=0;i<8;i++) Rod("喷泉水线",new Vector3(0,1.8f,0),new Vector3(Mathf.Cos(i*Mathf.PI/4)*1.3f,.53f,Mathf.Sin(i*Mathf.PI/4)*1.3f),.022f,p.Glass,fountain);
                if(Visit==1) for(int i=0;i<12;i++) FlowerPot(new Vector3(Mathf.Cos(i*Mathf.PI/6)*2.2f,0,Mathf.Sin(i*Mathf.PI/6)*2.2f),.6f,fountain);
            }
            else { Box("舞台",new Vector3(0,.22f,0),new Vector3(5.8f,.44f,2.8f),p.Wood,fountain);Box("舞台背景",new Vector3(0,1.8f,1.3f),new Vector3(5.8f,3.1f,.1f),p.Red,fountain);Text("舞台节目","夏日联欢",new Vector3(0,2,1.23f),.52f,p.Paper.color,fountain); }
            for(int i=0;i<4;i++) Bench(new Vector3(i%2==0?-5.7f:5.7f,0,-.6f+(i/2)*4),i%2==0?90:-90);
            Tree(new Vector3(-8,0,3.9f),.85f);Tree(new Vector3(8,0,3.9f),.85f);
            Stall(new Vector3(6.1f,0,6.3f),Visit==0?"糖画摊":Visit==1?"跳蚤市场":"联欢汽水",Visit);
            StreetLamp(new Vector3(-6.8f,0,1.5f));StreetLamp(new Vector3(6.8f,0,1.5f));
            for(int i=0;i<7;i++) Box("广场棋盘拼色",new Vector3(-3+i,.0007f,4.6f),new Vector3(.92f,.001f,.92f),i%2==0?p.Cream:p.Stone);
        }

        void ShopV2()
        {
            var shop=Group("小卖部 · 遮棚内的店面",new Vector3(0,0,6.9f));
            Box("小卖部楼体",new Vector3(0,1.65f,0),new Vector3(10.7f,3.3f,4.1f),Visit==1?p.Mint:p.Plaster,shop);
            Roof(shop,11.1f,4.5f,3.35f,Visit==2?1.25f:.62f);
            Box("敞开的售货口",new Vector3(0,1.55f,-2.1f),new Vector3(5.8f,1.6f,.07f),p.DarkRoof,shop);
            Box("售货窗台",new Vector3(0,.76f,-2.4f),new Vector3(6.1f,.17f,.7f),p.Wood,shop);
            Box("招牌底板",new Vector3(0,2.85f,-2.15f),new Vector3(7.8f,.64f,.11f),p.Red,shop);
            Text("商店招牌",Visit==0?"夏 日 小 卖 部":Visit==1?"汽 水 · 冰 棍": "文 具 · 小 玩 具",new Vector3(0,2.85f,-2.23f),.46f,p.Paper.color,shop);
            for(int i=0;i<7;i++){Crate(new Vector3(-4.6f+i*1.35f,0,4.43f),root,i%2==0);Bottle(new Vector3(-2.5f+i*.83f,1.02f,4.43f),root);}
            for(int i=0;i<8;i++) Box("货架上的彩色包装",new Vector3(-2.5f+i*.72f,1.93f,4.72f),new Vector3(.4f,.5f,.16f),i%3==0?p.Red:i%3==1?p.Yellow:p.Blue);
            Freezer(new Vector3(5.95f,0,2.2f),root);
            CutawayRoof("小卖部完整遮雨棚",3.3f,9.2f,7.3f,1.2f,Visit==1?p.Mint:p.Red);
            Building("商店侧面的仓房",new Vector3(Visit==2?-9:8.6f,0,7),3.5f,4,Visit==2?2:1,p.Brick,false);
            Stall(new Vector3(-6.0f,0,-.1f),Visit==0?"玻璃珠小柜":Visit==1?"旧书摊":"竹蜻蜓",Visit);
            for(int i=0;i<3;i++) Stool(new Vector3(-5.0f+i*.55f,0,2.4f),.65f);
            StreetLamp(new Vector3(8,0,-.4f));
        }

        void CorridorV2()
        {
            Box("走廊后方教室墙",new Vector3(0,1.7f,5.8f),new Vector3(20,3.4f,.4f),p.Plaster);
            Box("走廊绿色墙裙",new Vector3(0,.57f,5.55f),new Vector3(20,1.14f,.11f),p.Mint);
            for(int i=0;i<5;i++)
            {
                float x=-8+i*4;
                Box("教室木门",new Vector3(x,1.08f,5.5f),new Vector3(1.2f,2.16f,.12f),p.Wood);
                Text("班牌",((i+Visit)%6+1)+" 年 级",new Vector3(x,2.55f,5.42f),.25f,p.Wood.color);
                Window(new Vector3(x+1.65f,1.8f,5.53f),1.5f,1.25f,root,false);
            }
            for(int i=0;i<6;i++)
            {
                float z=-3.5f+i*2;
                Box("走廊窗侧立柱",new Vector3(5.55f,1.7f,z),new Vector3(.3f,3.4f,.3f),p.Cream);
                Rod("窗边栏杆",new Vector3(5.55f,.8f,z),new Vector3(5.55f,.8f,z+1.9f),.035f,p.Iron);
                var sill=Group("开放窗框",new Vector3(5.56f,1.8f,z+.9f),90);
                Window(Vector3.zero,1.55f,1.3f,sill,false);
            }
            CutawayRoof("教学楼走廊 · 剖开顶板",3.55f,11.4f,10,1.0f,p.Cream);
            Box("走廊左端楼梯间",new Vector3(-8.25f,1.7f,1.7f),new Vector3(3,3.4f,8.4f),p.Plaster);
            for(int i=0;i<7;i++) Box("远处楼梯台阶",new Vector3(-5.8f,i*.16f+.08f,5.1f+i*.33f),new Vector3(2.8f,.16f,.36f),p.Stone);
            var notice=Group("走廊布告栏",new Vector3(Visit==1?1.7f:-2,1.65f,5.38f));
            Box("布告栏绿板",Vector3.zero,new Vector3(2.6f,1.25f,.1f),p.LeafDark,notice);
            Text("布告栏内容",Visit==0?"本周课表\n认真学习  快乐游戏":Visit==1?"手工展览\n纸飞机  纸风车":"值日表\n放学记得关窗",new Vector3(0,0,-.07f),.24f,p.Paper.color,notice);
            for(int i=0;i<3+Visit;i++)
            {
                float z=-1.2f+i*1.15f;
                Box(Visit==0?"走廊图书柜":Visit==1?"手工展台":"值日工具柜",new Vector3(-4.8f,.42f,z),new Vector3(.65f,.84f,.75f),p.Wood);
                if(Visit==0)
                    for(int book=0;book<3;book++) Box("同学带来的图书",new Vector3(-4.8f+book*.08f,.865f+book*.035f,z),new Vector3(.41f,.035f,.49f),book%2==0?p.Blue:p.Paper);
                else if(Visit==1)
                {
                    var mesh=new Mesh {name="同学折的纸飞机"};
                    mesh.vertices=new[]{new Vector3(0,0,.23f),new Vector3(-.22f,0,-.15f),new Vector3(0,.065f,-.06f),new Vector3(.22f,0,-.15f),new Vector3(0,-.03f,.02f)};
                    mesh.triangles=new[]{0,2,1,0,3,2,0,1,4,0,4,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
                    var plane=MeshObject("纸飞机展品",mesh,p.Paper);plane.transform.localPosition=new Vector3(-4.8f,.89f,z-.1f);
                    Rod("展览纸风车竹柄",new Vector3(-4.59f,.85f,z+.23f),new Vector3(-4.59f,1.2f,z+.23f),.012f,p.Wood);
                    for(int wing=0;wing<4;wing++)
                    {
                        float angle=wing*Mathf.PI*.5f;
                        var blade=Box("展览纸风车叶片",new Vector3(-4.59f+Mathf.Cos(angle)*.065f,1.2f+Mathf.Sin(angle)*.065f,z+.23f),new Vector3(.12f,.07f,.015f),wing%2==0?p.Red:p.Yellow);
                        blade.transform.localRotation=Quaternion.Euler(0,20,wing*90+28);
                    }
                }
                else
                {
                    Cylinder("值日小水桶",new Vector3(-4.8f,.96f,z),.16f,.22f,p.Blue);
                    Cylinder("水桶开口",new Vector3(-4.8f,1.072f,z),.13f,.008f,p.DarkRoof);
                    Rod("扫帚竹杆",new Vector3(-5.15f,.07f,z+.2f),new Vector3(-4.95f,1.5f,z+.2f),.02f,p.Wood);
                    Ball("扫帚草束",new Vector3(-5.15f,.13f,z+.2f),new Vector3(.3f,.28f,.12f),p.Paper);
                }
            }
            Bench(new Vector3(6.45f,0,.5f),90);
            Tree(new Vector3(9.6f,0,7.7f),.8f); Building("窗外另一栋教学楼",new Vector3(14,0,13),6,5,Visit==2?4:2,p.Cream,false);
            for(int i=0;i<4;i++) FlowerPot(new Vector3(6.35f,0,4.8f-i*.8f),.65f);
            StreetLamp(new Vector3(9.1f,0,1.7f));
        }

        void PlaygroundV2()
        {
            for(int i=0;i<6;i++) ArenaPaintLine("操场跑道白线",new Vector3(-11,.002f,4.4f+i*.83f),new Vector3(11,.002f,4.4f+i*.83f),.055f,p.White,root);
            for(int i=0;i<3;i++) ArenaPaintCircle("弧形跑道",new Vector3(0,.002f,10.5f),6.0f+i*.83f,.055f,p.White,root);
            var hoop=Group("操场篮球架",new Vector3(6.3f,0,5.3f),-17);
            Rod("篮球架立柱",Vector3.zero,new Vector3(0,3.2f,0),.085f,p.Iron,hoop);
            Box("篮板",new Vector3(0,3.22f,-.5f),new Vector3(1.5f,.9f,.09f),p.White,hoop);
            Torus("篮圈",new Vector3(0,2.9f,-.91f),.25f,.02f,p.Red,hoop,Quaternion.Euler(90,0,0));
            for(int i=0;i<6;i++) Rod("篮网",new Vector3(Mathf.Cos(i*Mathf.PI/3)*.24f,2.9f,-.91f+Mathf.Sin(i*Mathf.PI/3)*.24f),new Vector3(Mathf.Cos(i*Mathf.PI/3)*.16f,2.5f,-.91f+Mathf.Sin(i*Mathf.PI/3)*.16f),.008f,p.White,hoop);
            for(int i=0;i<4+Visit;i++) Box("远处阶梯看台",new Vector3(-6.8f,.19f+i*.32f,5.0f+i*.5f),new Vector3(5,.38f,.6f),i%2==0?p.Cream:p.Stone);
            var goal=Group("足球门",new Vector3(Visit==1?2:-2,0,13.5f));
            Rod("左门柱",new Vector3(-2.3f,0,0),new Vector3(-2.3f,2.4f,0),.055f,p.White,goal);
            Rod("右门柱",new Vector3(2.3f,0,0),new Vector3(2.3f,2.4f,0),.055f,p.White,goal);
            Rod("球门横梁",new Vector3(-2.3f,2.4f,0),new Vector3(2.3f,2.4f,0),.055f,p.White,goal);
            for(int i=0;i<10;i++) Rod("球门网竖线",new Vector3(-2.3f+i*.51f,0,.3f),new Vector3(-2.3f+i*.51f,2.4f,.3f),.007f,p.Cream,goal);
            for(int i=0;i<7;i++) Rod("球门网横线",new Vector3(-2.3f,i*.4f,.3f),new Vector3(2.3f,i*.4f,.3f),.007f,p.Cream,goal);
            Building("操场体育器材室",new Vector3(Visit==2?8.5f:-9.2f,0,11.8f),4.5f,4,Visit==2?2:1,p.Mint,false);
            if(Visit==1) for(int i=0;i<3;i++)
            {
                float z=.8f+i*1.7f,height=1.7f+i*.2f;
                Rod("单杠左立柱",new Vector3(7.8f,0,z),new Vector3(7.8f,height,z),.045f,p.Iron);
                Rod("单杠右立柱",new Vector3(9.2f,0,z),new Vector3(9.2f,height,z),.045f,p.Iron);
                Rod("单杠横杆",new Vector3(7.8f,height,z),new Vector3(9.2f,height,z),.035f,p.Stone);
            }
            if(Visit==2) Stall(new Vector3(8.2f,0,1.6f),"运动会补水站",2);
            Text("操场终场牌","夏 日 大 王 · 擂 台",new Vector3(0,1.2f,5.05f),.35f,p.Red.color);
            Tree(new Vector3(-10,0,1),.75f); StreetLamp(new Vector3(9.9f,0,3.6f));
        }

        void CutawayRoof(string name,float height,float width,float depth,float z,Material material)
        {
            var roof=Group(name,new Vector3(0,0,z));
            // 中央顶板只投影，保留俯视操作视野；四周檐条提供完整的遮蔽轮廓。
            var top=Box("遮雨顶板 · 俯视剖面",new Vector3(0,height,0),new Vector3(width,.10f,depth),material,roof);
            top.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            for(int side=-1;side<=1;side+=2)
            {
                Box("侧面檐条",new Vector3(side*(width*.5f),height,0),new Vector3(.2f,.22f,depth),material,roof);
                Cylinder("檐下立柱",new Vector3(side*(width*.5f),height*.5f,depth*.5f-.1f),.075f,height,p.Iron,roof);
            }
            Box("后檐",new Vector3(0,height,depth*.5f),new Vector3(width,.22f,.2f),material,roof);
            for(int i=0;i<3;i++)
            {
                var lamp=Ball("檐下灯",new Vector3(-2.8f+i*2.8f,height-.16f,depth*.5f-.5f),new Vector3(.24f,.12f,.24f),p.Lamp,roof);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=5;light.intensity=0;light.color=new Color(1,.81f,.53f);light.shadows=LightShadows.None;
            }
        }

        void Stall(Vector3 position,string title,int style)
        {
            var stall=Group(title+" · 街边小摊",position,position.x<0?16:-16);
            Box("小摊桌",new Vector3(0,.72f,0),new Vector3(1.45f,.12f,.76f),p.Wood,stall);
            for(int side=-1;side<=1;side+=2) Box("桌腿",new Vector3(side*.58f,.35f,0),new Vector3(.065f,.7f,.6f),p.Wood,stall);
            if(style!=1)
            {
                Rod("小摊伞杆",new Vector3(.45f,0,.22f),new Vector3(.45f,2.12f,.22f),.026f,p.Iron,stall);
                var umbrella=Ball("小摊遮阳伞",new Vector3(.45f,2.14f,.22f),new Vector3(2.25f,.28f,1.75f),style==0?p.Yellow:p.Red,stall);umbrella.AddComponent<WorldWindSway>().Configure(1.2f,.7f,Vector3.forward);
            }
            StallGoods(stall,title,style);
            Text("小摊价签",title,new Vector3(0,.58f,-.41f),.19f,p.Paper.color,stall);
        }

        void StallGoods(Transform stall,string title,int style)
        {
            if(title.Contains("棋"))
            {
                Box("棋盘木框",new Vector3(0,.804f,0),new Vector3(1.24f,.045f,.64f),p.Cream,stall);
                for(int i=0;i<9;i++) Box("棋盘纵线",new Vector3(-.55f+i*.1375f,.828f,0),new Vector3(.008f,.002f,.54f),p.Wood,stall);
                for(int i=0;i<5;i++) Box("棋盘横线",new Vector3(0,.828f,-.25f+i*.125f),new Vector3(1.1f,.002f,.008f),p.Wood,stall);
                for(int i=0;i<8;i++) Cylinder("棋摊圆棋子",new Vector3(-.41f+(i%4)*.275f,.842f,-.125f+(i/4)*.25f),.045f,.025f,i<4?p.Red:p.Iron,stall);
            }
            else if(title.Contains("玻璃珠"))
            {
                Box("玻璃珠收藏格箱",new Vector3(0,.82f,0),new Vector3(1.22f,.08f,.57f),p.Wood,stall);
                Box("格箱内衬",new Vector3(0,.862f,0),new Vector3(1.15f,.006f,.5f),p.Paper,stall);
                for(int i=0;i<5;i++) Box("收藏格隔板",new Vector3(-.58f+i*.29f,.88f,0),new Vector3(.015f,.075f,.56f),p.Cream,stall);
                Box("收藏格横板",new Vector3(0,.88f,0),new Vector3(1.22f,.075f,.018f),p.Cream,stall);
                for(int i=0;i<8;i++)
                {
                    Vector3 center=new Vector3(-.435f+(i%4)*.29f,.925f,i<4?-.135f:.135f);
                    Ball("格箱里的玻璃珠",center,Vector3.one*.135f,p.Glass,stall);
                    Ball("玻璃珠彩色内芯",center+new Vector3(.008f,.018f,-.008f),new Vector3(.07f,.045f,.07f),i%3==0?p.Red:i%3==1?p.Yellow:p.Mint,stall);
                    Ball("玻璃珠反光",center+new Vector3(-.025f,.055f,-.025f),Vector3.one*.027f,p.White,stall);
                }
            }
            else if(title.Contains("修车"))
            {
                Box("修车工具箱",new Vector3(-.2f,.87f,.06f),new Vector3(.69f,.18f,.36f),p.Blue,stall);
                Box("工具箱黑色开口",new Vector3(-.2f,.965f,.06f),new Vector3(.59f,.012f,.27f),p.Iron,stall);
                for(int i=0;i<3;i++)
                {
                    Rod("修车扳手",new Vector3(-.43f+i*.16f,.99f,-.04f),new Vector3(-.42f+i*.16f,1.08f,.1f),.018f,p.Stone,stall);
                    Torus("扳手头",new Vector3(-.42f+i*.16f,1.085f,.1f),.029f,.008f,p.Stone,stall,Quaternion.Euler(90,0,0));
                }
                Rod("打气筒",new Vector3(.44f,.8f,0),new Vector3(.44f,1.18f,0),.035f,p.Iron,stall);
                Rod("打气筒手柄",new Vector3(.34f,1.18f,0),new Vector3(.54f,1.18f,0),.025f,p.Wood,stall);
                Torus("待修的小车轮",new Vector3(0,.34f,.18f),.24f,.033f,p.Rubber,stall);
            }
            else if(title.Contains("糖画"))
            {
                Box("糖画平盘",new Vector3(0,.797f,0),new Vector3(1.25f,.025f,.63f),p.Cream,stall);
                for(int i=0;i<3;i++)
                {
                    Vector3 center=new Vector3(-.4f+i*.4f,.824f,.05f);
                    Rod("糖画竹签",center+new Vector3(0,0,-.32f),center,.014f,p.Wood,stall);
                    for(int n=0;n<8;n++)
                    {
                        float a=n*Mathf.PI/4,b=(n+1)*Mathf.PI/4;
                        float r=n%2==0?.15f:.077f,next=(n+1)%2==0?.15f:.077f;
                        Rod("糖画花瓣",center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),center+new Vector3(Mathf.Cos(b)*next,0,Mathf.Sin(b)*next),.012f,p.Yellow,stall);
                    }
                }
            }
            else if(title.Contains("冰棍"))
            {
                Box("保温冰棍箱",new Vector3(0,.9f,.06f),new Vector3(1.2f,.23f,.55f),p.Cream,stall);
                for(int i=0;i<6;i++)
                {
                    Rod("冰棍木棒",new Vector3(-.48f+i*.19f,.96f,-.35f),new Vector3(-.48f+i*.19f,.96f,-.18f),.012f,p.Wood,stall);
                    Box("纸包冰棍",new Vector3(-.48f+i*.19f,1.036f,-.06f),new Vector3(.14f,.035f,.28f),i%2==0?p.Paper:p.Red,stall);
                }
            }
            else if(title.Contains("风车")||title.Contains("蜻蜓")||title.Contains("蒲扇"))
            {
                for(int i=0;i<5;i++)
                {
                    Vector3 center=new Vector3(-.5f+i*.25f,1.14f,.03f);
                    Rod("玩具竹柄",new Vector3(center.x,.8f,center.z),center,.012f,p.Wood,stall);
                    if(title.Contains("蒲扇")) Ball("叶片蒲扇",center+Vector3.up*.055f,new Vector3(.20f,.25f,.035f),p.Paper,stall);
                    else for(int wing=0;wing<4;wing++)
                    {
                        float angle=wing*Mathf.PI*.5f;
                        var blade=Box("纸风车叶片",center+new Vector3(Mathf.Cos(angle)*.063f,Mathf.Sin(angle)*.063f,0),new Vector3(.11f,.07f,.012f),wing%2==0?p.Red:p.Yellow,stall);
                        blade.transform.localRotation=Quaternion.Euler(0,20,wing*90+28);
                    }
                }
            }
            else if(title.Contains("铅笔"))
            {
                for(int i=0;i<6;i++) Rod("彩色铅笔",new Vector3(-.49f+i*.19f,.815f,-.24f),new Vector3(-.49f+i*.19f,.815f,.22f),.021f,i%2==0?p.Yellow:p.Red,stall);
                for(int i=0;i<3;i++) Box("文具橡皮",new Vector3(-.31f+i*.31f,.85f,.16f),new Vector3(.16f,.065f,.11f),p.White,stall);
            }
            else if(title.Contains("汽水")||title.Contains("补水"))
                for(int i=0;i<6;i++) Bottle(new Vector3(-.52f+i*.20f,.84f,0),stall);
            else for(int i=0;i<6;i++)
            {
                if(style==1) Box("旧书和画册",new Vector3(-.5f+i*.20f,.84f,0),new Vector3(.14f,.12f,.4f),i%2==0?p.Paper:p.Blue,stall);
                else Bottle(new Vector3(-.52f+i*.20f,.84f,0),stall);
            }
        }

        void VisitProps()
        {
            int location=StableHash(environment.locationId)%7;
            for(int i=0;i<3+Visit;i++)
            {
                float side=i%2==0?-1:1;
                var pedestrian=Group("路人 · "+environment.locationId+"/"+Visit+"/"+i,new Vector3(side*(6.8f+(i/2)*.55f),0,3.8f+(i/2)*1.8f),side*20);
                Material shirt=(i+location+Visit)%3==0?p.Mint:(i+location+Visit)%3==1?p.Yellow:p.Blue;
                Ball("路人衣服",new Vector3(0,.83f,0),new Vector3(.4f,.58f,.3f),shirt,pedestrian);
                Ball("路人头",new Vector3(0,1.22f,0),Vector3.one*.29f,p.Cream,pedestrian);
                for(int leg=-1;leg<=1;leg+=2) Rod("路人裤腿",new Vector3(leg*.09f,.58f,0),new Vector3(leg*.10f,.08f,.02f),.065f,p.Iron,pedestrian);
                Rod("路人手臂",new Vector3(-.22f,1.04f,0),new Vector3(-.24f,.67f,.06f),.05f,shirt,pedestrian);
                if(Visit==1) Box("路人提篮",new Vector3(.3f,.62f,0),new Vector3(.24f,.26f,.24f),p.Wood,pedestrian);
                if(Visit==2) Ball("路人的帽子",new Vector3(0,1.39f,0),new Vector3(.36f,.12f,.34f),p.Yellow,pedestrian);
            }
            if(environment.locationId!="corridor")
                for(int i=0;i<5+Visit;i++) GrassTuft(new Vector3(4.5f+(i%3)*.8f,.01f,-1.9f+(i/3)*.65f));
        }

        void BackgroundV2()
        {
            if(environment.locationId=="corridor") return;
            int code=StableHash(environment.locationId)%11;
            for(int i=0;i<6;i++)
            {
                float h=2.4f+((i+code+Visit*2)%5)*.87f;
                var horizon=Group("远景 · "+environment.locationId+" · "+i,new Vector3(-15+i*5.8f,0,23.5f+Visit*.8f));
                Box("远景剪影",new Vector3(0,h*.5f,0),new Vector3(4.8f,h,4),i%2==0?p.Stone:p.Plaster,horizon);
                if(environment.locationId=="home"||environment.locationId=="alley"||environment.locationId=="old_tree") Roof(horizon,5.1f,4.3f,h,.5f+Visit*.2f);
                else Box("远景屋顶",new Vector3(0,h+.12f,0),new Vector3(5,.24f,4.1f),p.Cream,horizon);
            }
            if(environment.locationId=="old_tree"||environment.locationId=="home")
                for(int i=0;i<3;i++) Tree(new Vector3(-11+i*10,0,19),.6f+i*.1f);
            if(environment.locationId=="alley"||environment.locationId=="home") OverheadWires();
        }
    }
}
