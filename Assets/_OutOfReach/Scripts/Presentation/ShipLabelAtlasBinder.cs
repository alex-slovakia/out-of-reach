using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation
{
    /// <summary>Keeps a depth-tested label on Unity's current dynamic font atlas without modifying font materials.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class ShipLabelAtlasBinder : MonoBehaviour
    {
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        [SerializeField] private TextMesh labelMesh;
        [SerializeField] private MeshRenderer labelRenderer;
        [SerializeField] private Font labelFont;
        [SerializeField] private Material depthMaterial;
        private MaterialPropertyBlock properties;
        private bool applying;
        private bool subscribed;

        public bool IsConfigured => labelMesh && labelRenderer && labelFont && depthMaterial
            && labelMesh.gameObject == gameObject && labelRenderer.gameObject == gameObject
            && labelMesh.font == labelFont && labelRenderer.sharedMaterial == depthMaterial;

        public void Configure(TextMesh mesh, MeshRenderer renderer, Font font, Material material)
        {
            labelMesh = mesh;
            labelRenderer = renderer;
            labelFont = font;
            depthMaterial = material;
            RefreshAtlas();
        }

        private void OnEnable()
        {
            if (!subscribed)
            {
                Font.textureRebuilt += OnFontTextureRebuilt;
                subscribed = true;
            }
            RefreshAtlas();
        }

        private void OnDisable()
        {
            if (!subscribed) return;
            Font.textureRebuilt -= OnFontTextureRebuilt;
            subscribed = false;
        }

        /// <summary>Call after explicitly changing this static label's text, font size or style.</summary>
        public void RefreshAtlas()
        {
            if (!labelMesh || !labelRenderer || !labelFont || !depthMaterial || applying) return;
            applying = true;
            try
            {
                // An atlas rebuild may occur synchronously here. Its callback only rebinds the texture.
                labelFont.RequestCharactersInTexture(labelMesh.text, labelMesh.fontSize, labelMesh.fontStyle);
                BindCurrentAtlas();
            }
            finally { applying = false; }
        }

        private void OnFontTextureRebuilt(Font changedFont)
        {
            if (changedFont == labelFont) BindCurrentAtlas();
        }

        private void BindCurrentAtlas()
        {
            if (!labelRenderer || !labelFont || !depthMaterial) return;
            var fontMaterial = labelFont.material;
            if (!fontMaterial || !fontMaterial.mainTexture) return;
            // Unity may assign its overlay font material when rebuilding TextMesh; restore only our renderer.
            if (labelRenderer.sharedMaterial != depthMaterial) labelRenderer.sharedMaterial = depthMaterial;
            labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
            labelRenderer.receiveShadows = false;
            if (properties == null) properties = new MaterialPropertyBlock();
            labelRenderer.GetPropertyBlock(properties);
            properties.SetTexture(MainTextureId, fontMaterial.mainTexture);
            labelRenderer.SetPropertyBlock(properties);
        }
    }
}
