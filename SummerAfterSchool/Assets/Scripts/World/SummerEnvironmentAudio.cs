using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    public sealed partial class SummerAudio
    {
        readonly Dictionary<string,AudioClip> generatedAmbience=new Dictionary<string,AudioClip>();
        EnvironmentContext audioEnvironment;
        LoopPair visitSound, weatherSound;
        public string EnvironmentSoundSignature { get { return audioEnvironment==null?LocationId:audioEnvironment.locationId+":"+audioEnvironment.visitVariant+":"+audioEnvironment.period+":"+audioEnvironment.weather; } }

        public void SetEnvironment(EnvironmentContext context)
        {
            if(context==null) return;
            audioEnvironment=new EnvironmentContext
            {
                locationId=context.locationId,visitVariant=context.visitVariant,period=context.period,weather=context.weather,
                day=context.day,weatherSeed=context.weatherSeed,geometryVersion=context.geometryVersion,frictionMultiplier=context.frictionMultiplier
            };
            LocationId=context.locationId;
            if(visitSound==null) visitSound=MakePair("街区访问细节");
            if(weatherSound==null) weatherSound=MakePair("天气声景");
            ApplyEnvironmentAudio();
        }

        void ApplyEnvironmentAudio()
        {
            if(audioEnvironment==null||environment==null) return;
            string id=audioEnvironment.locationId;
            string key=id=="old_tree"?"ambience_tree":id=="alley"?"ambience_alley":id=="school_gate"||id=="playground"||id=="corridor"?"ambience_school":id=="home"?"ambience_courtyard":"ambience";
            environment.Set(Clip(key)??Clip("ambience"),1.9f);
            if(visitSound!=null)
            {
                visitSound.Set(AmbienceClip(id+"/"+audioEnvironment.visitVariant,id,audioEnvironment.visitVariant,false),1.7f);
                float pitch=audioEnvironment.period==DayPeriod.Morning?1.06f:audioEnvironment.period==DayPeriod.Evening?.92f:1;
                foreach(var source in visitSound.sources) source.pitch=pitch;
            }
            bool sheltered=id=="corridor"||id=="shop";
            if(weatherSound!=null)
                weatherSound.Set(audioEnvironment.weather==WeatherKind.Rainy?AmbienceClip(sheltered?"rain_outside":"rain","rain",sheltered?1:0,true):null,1.4f);
            windContextGain=sheltered?.07f:id=="old_tree"?.80f:id=="playground"?.73f:id=="square"?.60f:id=="alley"?.30f:.45f;
            if(audioEnvironment.weather==WeatherKind.Cloudy) windContextGain*=1.3f;
            if(audioEnvironment.weather==WeatherKind.Rainy) windContextGain*=.5f;
        }

        void ClearEnvironmentAudio()
        {
            audioEnvironment=null;
            if(visitSound!=null) visitSound.Set(null,.9f);
            if(weatherSound!=null) weatherSound.Set(null,.9f);
        }

        void TickEnvironmentAudio(float dt,float gain)
        {
            if(visitSound!=null) visitSound.Tick(dt,gain*.47f);
            if(weatherSound!=null) weatherSound.Tick(dt,gain*.66f);
        }

        // 原创程序音景：地点声部各有鸟鸣、脚步、店铃、球场弹跳或喷泉；固定种子可重现。
        AudioClip AmbienceClip(string key,string id,int variant,bool rain)
        {
            if(generatedAmbience.TryGetValue(key,out AudioClip existing)) return existing;
            const int rate=22050,seconds=12;
            float[] samples=new float[rate*seconds];
            int seed=1998;unchecked {foreach(char c in key) seed=seed*31+c;}
            var noise=new System.Random(seed);
            float filtered=0,slow=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;
                float white=(float)noise.NextDouble()*2-1;
                filtered=Mathf.Lerp(filtered,white,rain?.23f:.035f);
                slow=Mathf.Lerp(slow,white,.004f);
                float edge=Mathf.Min(1,Mathf.Min(t,seconds-t)*5);
                float value;
                if(rain)
                {
                    value=filtered*(variant==1?.10f:.20f)+slow*.10f;
                    if((i+variant*73)%6615<90) value+=white*.08f;
                }
                else
                {
                    value=slow*.06f;
                    float cycle=(t+variant*.83f)%4;
                    if(id=="home"||id=="old_tree")
                    {
                        float chirp=(t+variant*.3f)%2.8f;
                        if(chirp<.23f) value+=Mathf.Sin(2*Mathf.PI*(1850+600*chirp)*t)*Mathf.Sin(chirp/.23f*Mathf.PI)*.10f;
                        value+=filtered*.025f*(.5f+.5f*Mathf.Sin(t*5.2f));
                    }
                    else if(id=="square")
                    {
                        value+=filtered*.08f;
                        if(cycle<.18f) value+=Mathf.Sin(t*2*Mathf.PI*620)*Mathf.Exp(-cycle*26)*.11f;
                        if(variant==2&&cycle>.6f&&cycle<.8f) value+=Mathf.Sin(t*2*Mathf.PI*825)*.035f;
                    }
                    else if(id=="shop")
                    {
                        if(cycle<.6f) value+=(Mathf.Sin(t*2*Mathf.PI*1174)+Mathf.Sin(t*2*Mathf.PI*1568)*.48f)*Mathf.Exp(-cycle*6)*.085f;
                        float clink=(t+.9f)%3.1f;if(clink<.08f) value+=Mathf.Sin(t*2*Mathf.PI*(2300+variant*210))*Mathf.Exp(-clink*48)*.11f;
                    }
                    else if(id=="playground")
                    {
                        float bounce=(t+variant*.1f)%1.7f;if(bounce<.15f) value+=(Mathf.Sin(t*2*Mathf.PI*125)*.18f+filtered*.2f)*Mathf.Exp(-bounce*28);
                        if(cycle>3.7f) value+=Mathf.Sin(t*2*Mathf.PI*2300)*.022f;
                    }
                    else
                    {
                        float step=(t+variant*.18f)%(id=="corridor"?.62f:.84f);
                        if(step<.065f) value+=filtered*Mathf.Exp(-step*42)*(id=="corridor"?.33f:.23f);
                        if(cycle<.5f&&id=="school_gate") value+=(Mathf.Sin(t*2*Mathf.PI*783)+Mathf.Sin(t*2*Mathf.PI*1046)*.4f)*Mathf.Exp(-cycle*7)*.08f;
                        if(cycle<.13f&&id=="alley") value+=Mathf.Sin(t*2*Mathf.PI*(1400+variant*140))*Mathf.Exp(-cycle*19)*.07f;
                        if(id=="corridor") {float echo=(t+variant*.18f+.14f)%.62f;if(echo<.065f) value+=filtered*Mathf.Exp(-echo*40)*.055f;}
                    }
                    // 重访声景增加与当次布景对应的声源，不只移动同一个循环的相位。
                    float eventTime=t%6;
                    if(variant==1)
                    {
                        if(id=="home"&&eventTime<1.2f) value+=filtered*.12f*Mathf.Sin(eventTime/1.2f*Mathf.PI);
                        if((id=="old_tree"||id=="shop")&&eventTime<.16f) value+=Mathf.Sin(t*2*Mathf.PI*(id=="old_tree"?410:210))*Mathf.Exp(-eventTime*31)*.16f;
                        if(id=="school_gate"&&eventTime<.45f) value+=(Mathf.Sin(t*2*Mathf.PI*1950)+Mathf.Sin(t*2*Mathf.PI*2740)*.3f)*Mathf.Exp(-eventTime*9)*.09f;
                        if(id=="alley"&&eventTime<.22f) value+=Mathf.Sin(t*2*Mathf.PI*2570)*Mathf.Exp(-eventTime*17)*.12f;
                        if((id=="corridor"||id=="square")&&eventTime<.42f) value+=filtered*Mathf.Sin(eventTime/.42f*Mathf.PI)*.17f;
                        if(id=="playground"&&eventTime<.18f) value+=Mathf.Sin(t*2*Mathf.PI*1750)*Mathf.Sin(eventTime/.18f*Mathf.PI)*.055f;
                    }
                    else if(variant==2)
                    {
                        if((id=="old_tree"||id=="square")&&eventTime<1.1f) value+=(Mathf.Sin(t*2*Mathf.PI*330)+Mathf.Sin(t*2*Mathf.PI*495)*.55f+Mathf.Sin(t*2*Mathf.PI*660)*.32f)*Mathf.Exp(-eventTime*3)*.045f;
                        if((id=="shop"||id=="alley")&&eventTime<.65f) value+=(Mathf.Sin(t*2*Mathf.PI*880)+Mathf.Sin(t*2*Mathf.PI*1174)*.4f)*Mathf.Exp(-eventTime*6)*.09f;
                        if((id=="home"||id=="corridor")&&eventTime<1.5f) value+=filtered*.12f*(.5f+.5f*Mathf.Sin(eventTime*13));
                        if((id=="school_gate"||id=="playground")&&eventTime<.38f) value+=Mathf.Sin(t*2*Mathf.PI*(2100+120*Mathf.Sin(t*17)))*Mathf.Sin(eventTime/.38f*Mathf.PI)*.055f;
                    }
                }
                samples[i]=Mathf.Clamp(value*edge,-.8f,.8f);
            }
            var clip=AudioClip.Create("原创环境 · "+key,samples.Length,1,rate,false);clip.SetData(samples,0);
            generatedAmbience.Add(key,clip);return clip;
        }

        void OnDestroy()
        {
            if(visitSound!=null) foreach(var source in visitSound.sources) if(source!=null) source.Stop();
            if(weatherSound!=null) foreach(var source in weatherSound.sources) if(source!=null) source.Stop();
            foreach(var clip in generatedAmbience.Values) if(clip!=null) Destroy(clip);
            generatedAmbience.Clear();
        }
    }
}
