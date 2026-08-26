using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Scene-authored bed health display. All layout and visuals live in MainScene.
public sealed class BedHealthUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Damageable damageable;
    [SerializeField] private string bedTag = "Bed";
    [SerializeField] private string bedName = "Bed";

    [Header("Scene UI References")]
    [SerializeField] private GameObject displayRoot;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text valueText;

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(.35f, .9f, .4f, 1f);
    [SerializeField] private Color midColor = new Color(1f, .85f, .25f, 1f);
    [SerializeField] private Color lowColor = new Color(1f, .3f, .3f, 1f);

    [Header("Animation")]
    [SerializeField, Min(.01f)] private float animationSpeed = 2.5f;

    private float _displayedFill = 1f;
    private float _targetFill = 1f;

    private void Start()
    {
        FindDamageable();
        if (displayRoot == null || fillImage == null || valueText == null)
            Debug.LogError("BedHealthUI의 씬 UI 참조가 비어 있습니다. MainScene의 Bed Health Scene UI를 연결하세요.", this);
        if (damageable == null) return;
        ConfigureFillImage();
        damageable.OnHealthChanged += UpdateBar;
        _displayedFill = _targetFill = damageable.MaxHealth > 0f
            ? Mathf.Clamp01(damageable.CurrentHealth / damageable.MaxHealth) : 0f;
        ApplyFillVisual();
    }

    private void Update()
    {
        bool isNight = DayNightManager.Instance != null
            && DayNightManager.Instance.CurrentPhase == DayNightManager.Phase.Night;
        if (displayRoot != null && displayRoot.activeSelf != isNight) displayRoot.SetActive(isNight);
        if (fillImage == null || Mathf.Approximately(_displayedFill, _targetFill)) return;
        _displayedFill = Mathf.MoveTowards(_displayedFill, _targetFill, animationSpeed * Time.deltaTime);
        ApplyFillVisual();
    }

    private void OnDestroy()
    {
        if (damageable != null) damageable.OnHealthChanged -= UpdateBar;
    }

    private void FindDamageable()
    {
        if (damageable != null) return;
        GameObject bed = null;
        if (!string.IsNullOrEmpty(bedTag))
        {
            try { bed = GameObject.FindWithTag(bedTag); } catch { }
        }
        if (bed == null) bed = GameObject.Find(bedName);
        if (bed != null) damageable = bed.GetComponent<Damageable>();
    }

    private void UpdateBar(float current, float maximum) =>
        _targetFill = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

    private void ApplyFillVisual()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = _displayedFill;
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.pivot = new Vector2(0f, .5f);
            fillRect.localScale = new Vector3(_displayedFill, 1f, 1f);
            fillImage.color = _displayedFill > .6f ? fillColor : _displayedFill > .3f ? midColor : lowColor;
        }
        if (valueText != null) valueText.text = $"{Mathf.CeilToInt(_displayedFill * 100f)}%";
    }

    private void ConfigureFillImage()
    {
        if (fillImage == null) return;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillClockwise = true;
    }
}
