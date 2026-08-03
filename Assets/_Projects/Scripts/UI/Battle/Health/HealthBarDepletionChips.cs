using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 扣血时在损失区间生成白色小方块，缓慢坠落并渐隐（死亡细胞风格）。
/// </summary>
[DisallowMultipleComponent]
public class HealthBarDepletionChips : MonoBehaviour
{
    [Header("方块")]
    [SerializeField] private Vector2 chipSizeRange = new Vector2(3f, 7f);
    [SerializeField] private Color chipColor = Color.white;
    [Tooltip("每损失 1% 血量大约生成的方块数")]
    [SerializeField] private float chipsPerRatioPoint = 2.5f;
    [SerializeField] private int minChipsPerHit = 2;
    [SerializeField] private int maxChipsPerHit = 24;

    [Header("运动")]
    [SerializeField] private float fallSpeed = 28f;
    [SerializeField] private float horizontalDrift = 12f;
    [SerializeField] private float lifetime = 0.85f;

    [SerializeField] private HealthBarView healthBar;
    [SerializeField] private RectTransform chipLayer;

    private static Sprite sharedWhiteSprite;

    private readonly List<ChipInstance> activeChips = new List<ChipInstance>();
    private readonly Stack<ChipInstance> pool = new Stack<ChipInstance>();

    private sealed class ChipInstance
    {
        public RectTransform rect;
        public Image image;
        public float life;
        public float maxLife;
        public Vector2 velocity;
        public float startAlpha;
    }

    void Awake()
    {
        healthBar ??= GetComponent<HealthBarView>() ?? GetComponentInParent<HealthBarView>();
        EnsureChipLayer();
    }

    void OnEnable()
    {
        if (healthBar != null)
            healthBar.OnRatioDecreased += HandleRatioDecreased;
    }

    void OnDisable()
    {
        if (healthBar != null)
            healthBar.OnRatioDecreased -= HandleRatioDecreased;

        ClearActiveChips();
    }

    public void Bind(HealthBarView view, RectTransform layer = null)
    {
        if (healthBar != null)
            healthBar.OnRatioDecreased -= HandleRatioDecreased;

        healthBar = view;
        if (layer != null)
            chipLayer = layer;
        else
            EnsureChipLayer();

        if (isActiveAndEnabled && healthBar != null)
            healthBar.OnRatioDecreased += HandleRatioDecreased;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = activeChips.Count - 1; i >= 0; i--)
        {
            var chip = activeChips[i];
            chip.life -= dt;
            if (chip.life <= 0f)
            {
                ReleaseChip(i);
                continue;
            }

            chip.rect.anchoredPosition += chip.velocity * dt;

            float t = chip.life / chip.maxLife;
            var color = chip.image.color;
            color.a = chip.startAlpha * t;
            chip.image.color = color;
        }
    }

    private void HandleRatioDecreased(float oldRatio, float newRatio)
    {
        if (healthBar == null || chipLayer == null)
            return;

        if (!healthBar.TryGetLostHealthBand(oldRatio, newRatio, out var band))
            return;

        float lost = oldRatio - newRatio;
        int count = Mathf.Clamp(
            Mathf.RoundToInt(lost * 100f * chipsPerRatioPoint),
            minChipsPerHit,
            maxChipsPerHit);

        for (int i = 0; i < count; i++)
        {
            float x = UnityEngine.Random.Range(band.xMin, band.xMax);
            float y = UnityEngine.Random.Range(band.yMin, band.yMax);
            SpawnChip(new Vector2(x, y));
        }
    }

    private void SpawnChip(Vector2 anchoredPosition)
    {
        var chip = RentChip();
        float size = UnityEngine.Random.Range(chipSizeRange.x, chipSizeRange.y);
        chip.rect.SetParent(chipLayer, false);
        chip.rect.anchorMin = new Vector2(0f, 0.5f);
        chip.rect.anchorMax = new Vector2(0f, 0.5f);
        chip.rect.pivot = new Vector2(0.5f, 0.5f);
        chip.rect.anchoredPosition = anchoredPosition;
        chip.rect.sizeDelta = new Vector2(size, size);
        chip.rect.localScale = Vector3.one;
        chip.rect.localRotation = Quaternion.identity;

        chip.maxLife = lifetime * UnityEngine.Random.Range(0.85f, 1.15f);
        chip.life = chip.maxLife;
        chip.startAlpha = UnityEngine.Random.Range(0.65f, 1f);
        chip.velocity = new Vector2(
            UnityEngine.Random.Range(-horizontalDrift, horizontalDrift),
            -fallSpeed * UnityEngine.Random.Range(0.75f, 1.25f));

        chip.image.color = new Color(chipColor.r, chipColor.g, chipColor.b, chip.startAlpha);
        chip.image.raycastTarget = false;
        chip.rect.gameObject.SetActive(true);
        activeChips.Add(chip);
    }

    private ChipInstance RentChip()
    {
        if (pool.Count > 0)
            return pool.Pop();

        var go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();

        return new ChipInstance
        {
            rect = go.GetComponent<RectTransform>(),
            image = image
        };
    }

    private void ReleaseChip(int index)
    {
        var chip = activeChips[index];
        activeChips.RemoveAt(index);
        chip.rect.gameObject.SetActive(false);
        pool.Push(chip);
    }

    private void ClearActiveChips()
    {
        for (int i = activeChips.Count - 1; i >= 0; i--)
            ReleaseChip(i);
    }

    private void EnsureChipLayer()
    {
        if (chipLayer != null)
            return;

        Transform background = healthBar != null ? healthBar.transform.Find("Background") : null;
        if (background == null && healthBar != null)
            background = healthBar.transform;

        var layerGo = new GameObject("ChipLayer", typeof(RectTransform));
        layerGo.transform.SetParent(background, false);

        chipLayer = layerGo.GetComponent<RectTransform>();
        chipLayer.anchorMin = Vector2.zero;
        chipLayer.anchorMax = Vector2.one;
        chipLayer.offsetMin = Vector2.zero;
        chipLayer.offsetMax = Vector2.zero;
        chipLayer.SetAsLastSibling();
    }

    private static Sprite GetWhiteSprite()
    {
        if (sharedWhiteSprite != null)
            return sharedWhiteSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        sharedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return sharedWhiteSprite;
    }
}
