using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PlaneHeadImage : MonoBehaviour
{
    [SerializeField] private Texture2D headImage;
    [SerializeField] private Color tint = Color.white;

    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        ApplyVisuals();
    }

    private void OnValidate()
    {
        ApplyVisuals();
    }

    public void SetHeadImage(Texture2D image)
    {
        headImage = image;
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", tint);
        propertyBlock.SetColor("_BaseColor", tint);

        if (headImage != null)
        {
            propertyBlock.SetTexture("_MainTex", headImage);
            propertyBlock.SetTexture("_BaseMap", headImage);
        }

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
