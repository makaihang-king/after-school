using UnityEngine;

namespace Childhood
{
    /// <summary>两只小蝴蝶只活动在花盆附近，没有 collider 或游戏状态。</summary>
    internal sealed class WorldButterfly : MonoBehaviour
    {
        Transform left,right;
        Vector3 home;
        float phase;
        public void Configure(Transform leftWing,Transform rightWing,float offset)
        {
            left=leftWing; right=rightWing; phase=offset; home=transform.localPosition;
        }
        void Update()
        {
            if (left==null || right==null || Time.timeScale<=0f) return;
            float t=Time.time+phase;
            float hover=.55f+.45f*Mathf.Sin(t*.46f);
            transform.localPosition=home+new Vector3(Mathf.Sin(t*.73f)*.27f,Mathf.Sin(t*1.18f)*.12f*hover,Mathf.Cos(t*.61f)*.23f);
            transform.localRotation=Quaternion.Euler(7f*Mathf.Sin(t*.8f),Mathf.Sin(t*.31f)*65f,0);
            float flap=21f+Mathf.Sin(t*(18f+hover*5f))*43f;
            left.localRotation=Quaternion.Euler(0,0,-flap);
            right.localRotation=Quaternion.Euler(0,0,flap);
        }
    }

    internal sealed class WorldDriftingLeaf : MonoBehaviour
    {
        Vector3 home;
        Renderer leaf;
        float duration,phase;
        public void Configure(float seconds,float offset)
        {
            home=transform.localPosition; duration=seconds; phase=offset;
            leaf=GetComponent<Renderer>();
        }
        void Update()
        {
            if (Time.timeScale<=0f) return;
            float progress=Mathf.Repeat(Time.time/duration+phase,1f);
            float flutter=progress*Mathf.PI*5f;
            transform.localPosition=home+new Vector3(Mathf.Sin(flutter)*.31f+progress*.18f,-progress*(home.y-.11f),Mathf.Sin(flutter*.7f)*.30f);
            transform.localRotation=Quaternion.Euler(Mathf.Sin(flutter)*22f,progress*225f+phase*360f,Mathf.Cos(flutter*1.5f)*37f);
            if (leaf!=null) leaf.enabled=progress>.025f && progress<.98f;
        }
    }
}
