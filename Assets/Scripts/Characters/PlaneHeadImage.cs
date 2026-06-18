using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PlaneHeadImage : MonoBehaviour
{
    [SerializeField] private Texture2D headImage;
    [SerializeField] private Color tint = Color.white;

    /// <summary>The teacher's face photo, so other systems (e.g. the placement
    /// map UI) can reuse it at runtime — including in builds where AssetDatabase
    /// is unavailable.</summary>
    public Texture2D HeadImage => headImage;

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

        // Flip the photo vertically. The head's sphere UVs map the photo upside
        // down on the visible side, so set tile.y = -1 and offset.y = 1 to mirror
        // V without mirroring U (which would flip the face left-right).
        Vector4 flipST = new Vector4(1f, -1f, 0f, 1f); // (tileX, tileY, offsetX, offsetY)
        propertyBlock.SetVector("_MainTex_ST", flipST);
        propertyBlock.SetVector("_BaseMap_ST", flipST);

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
