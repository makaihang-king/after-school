using UnityEngine;

namespace Childhood
{
    /// <summary>动态中文字体更新图集时共享一次材质；不用每字创建材质。</summary>
    internal sealed class WorldSignFontAtlas : MonoBehaviour
    {
        Font font;
        Material material;
        bool subscribed;

        public void Configure(Font source, Material target)
        {
            Unsubscribe();
            font = source; material = target;
            Refresh(source);
            if (isActiveAndEnabled) Subscribe();
        }

        void Refresh(Font rebuilt)
        {
            if (rebuilt == font && font != null && material != null)
                material.mainTexture = font.material.mainTexture;
        }
        void Subscribe()
        {
            if (subscribed || font == null) return;
            Font.textureRebuilt += Refresh; subscribed = true;
            Refresh(font);
        }
        void Unsubscribe()
        {
            if (!subscribed) return;
            Font.textureRebuilt -= Refresh; subscribed = false;
        }
        void OnEnable() { Subscribe(); }
        void OnDisable() { Unsubscribe(); }
        void OnDestroy() { Unsubscribe(); }
    }
}
