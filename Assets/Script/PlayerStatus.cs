using UnityEngine;
using UnityEngine.UI;

public class PlayerStatus : MonoBehaviour
{
    [Header("血条设置")]
    [SerializeField] private PlayerControl player; // 拖入玩家对象
    [SerializeField] private float maxBarWidth = 200f; // 血条最大宽度（可在面板调整）

    // UI组件缓存
    private Image hpBar;
    private RectTransform hpBarRect;
    private Text statusText;
    private Canvas canvas;

    void Start()
    {
        // 第一步：检查玩家引用
        if (player == null)
        {
            // 自动查找玩家（备用方案）
            player = FindObjectOfType<PlayerControl>();
            if (player == null)
            {
                Debug.LogError("【PlayerStatus】未找到PlayerControl组件！场景中无玩家对象");
                enabled = false; // 禁用脚本，避免后续报错
                return;
            }
            Debug.LogWarning("【PlayerStatus】自动找到PlayerControl，建议手动在面板绑定");
        }

        // 第二步：创建UI（封装为方法，更清晰）
        CreateStatusUI();

        // 第三步：订阅事件（仅订阅一次）
        player.OnHealthChanged -= OnPlayerHealthChanged; // 先取消，防止重复订阅
        player.OnHealthChanged += OnPlayerHealthChanged;

        // 初始化显示
        OnPlayerHealthChanged(player.GetCurrentHP(), player.GetMaxHP());
    }

    /// <summary>
    /// 创建血量UI（封装逻辑，便于维护）
    /// </summary>
    private void CreateStatusUI()
    {
        // 1. 创建Canvas（检查是否已存在，避免重复创建）
        GameObject canvasGO = GameObject.Find("StatusCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("StatusCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO); // 跨场景保留
        }
        else
        {
            canvas = canvasGO.GetComponent<Canvas>();
        }

        // 2. 创建血条背景（黑色）
        GameObject bgGO = GameObject.Find("HPBackground");
        if (bgGO == null)
        {
            bgGO = new GameObject("HPBackground");
            bgGO.transform.SetParent(canvasGO.transform);

            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.color = Color.black;

            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(0, 0);
            bgRect.pivot = new Vector2(0, 0);
            bgRect.anchoredPosition = new Vector2(10, 40);
            bgRect.sizeDelta = new Vector2(maxBarWidth, 30);
        }

        // 3. 创建血条填充（红色）
        GameObject hpGO = GameObject.Find("HPBar");
        if (hpGO == null)
        {
            hpGO = new GameObject("HPBar");
            hpGO.transform.SetParent(bgGO.transform);

            hpBar = hpGO.AddComponent<Image>();
            hpBar.color = Color.red;

            hpBarRect = hpGO.GetComponent<RectTransform>();
            hpBarRect.anchorMin = new Vector2(0, 0.5f);
            hpBarRect.anchorMax = new Vector2(0, 0.5f);
            hpBarRect.pivot = new Vector2(0, 0.5f);
            hpBarRect.anchoredPosition = Vector2.zero;
            hpBarRect.sizeDelta = new Vector2(maxBarWidth, 30);
        }
        else
        {
            hpBar = hpGO.GetComponent<Image>();
            hpBarRect = hpGO.GetComponent<RectTransform>();
        }

        // 4. 创建血条文字（蓝色）
        GameObject textGO = GameObject.Find("StatusText");
        if (textGO == null)
        {
            textGO = new GameObject("StatusText");
            textGO.transform.SetParent(bgGO.transform);

            statusText = textGO.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 18;
            statusText.color = Color.blue;
            statusText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRect = statusText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
        }
        else
        {
            statusText = textGO.GetComponent<Text>();
        }
    }

    /// <summary>
    /// 玩家血量变化回调（带空值防护）
    /// </summary>
    private void OnPlayerHealthChanged(int currentHP, int maxHP)
    {
        if (hpBarRect == null || statusText == null) return;

        float hpPercent = maxHP <= 0 ? 0 : (float)currentHP / maxHP;
        // 更新血条宽度
        hpBarRect.sizeDelta = new Vector2(maxBarWidth * hpPercent, hpBarRect.sizeDelta.y);
        // 更新文字
        statusText.text = $"HP: {currentHP}/{maxHP}";
    }

    /// <summary>
    /// 取消事件订阅（防止内存泄漏）
    /// </summary>
    private void OnDestroy()
    {
        if (player != null)
            player.OnHealthChanged -= OnPlayerHealthChanged;
    }

    /// <summary>
    /// 防止场景切换后UI重复创建
    /// </summary>
    private void OnDisable()
    {
        if (player != null)
            player.OnHealthChanged -= OnPlayerHealthChanged;
    }
}