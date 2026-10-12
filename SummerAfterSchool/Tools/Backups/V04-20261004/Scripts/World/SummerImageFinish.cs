using UnityEngine;

namespace Childhood
{
    /// <summary>只完成 3D 画面的色调，不模糊内芯、UI 或操作提示。单个全屏 pass。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SummerImageFinish : MonoBehaviour
    {
        [Range(0,1)] public float Amount = .7f;
        Material finish;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (finish == null)
            {
                var shader = Resources.Load<Shader>("Shaders/SummerImageFinish");
                if (shader != null && shader.isSupported)
                    finish = new Material(shader) { name = "放学后的温柔色调", hideFlags = HideFlags.HideAndDontSave };
            }
            if (finish == null || Amount <= 0f) { Graphics.Blit(source,destination); return; }
            finish.SetFloat("_Amount",Amount);
            Graphics.Blit(source,destination,finish);
        }

        void OnDisable()
        {
            if (finish != null) Destroy(finish);
            finish = null;
        }
    }
}
