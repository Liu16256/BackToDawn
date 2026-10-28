using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    [Header("UI组件")]
    public Image buffIcon;
    public TextMeshProUGUI buffNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI stockText;
    public Button buyButton;

    [Header("状态颜色")]
    public Color affordableColor = Color.white;
    public Color unaffordableColor = Color.gray;
    public Color soldOutColor = Color.red;

    private SimpleShopItem shopItem;
    private SimpleShopUI shopUI;

    private void Start()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyButtonClicked);
        }
    }

    // 初始化
    public void Initialize(SimpleShopItem item, SimpleShopUI ui)
    {
        shopItem = item;
        shopUI = ui;

        if (item == null || item.buffData == null) return;

        // 设置BUFF信息
        if (buffIcon != null && item.buffData.icon != null)
            buffIcon.sprite = item.buffData.icon;

        if (buffNameText != null)
            buffNameText.text = item.buffData.buffName;

        if (descriptionText != null)
            descriptionText.text = item.buffData.description;

        // 设置价格
        if (priceText != null)
        {
            priceText.text = $"{item.price} 金币";
        }

        // 设置库存显示
        if (stockText != null && item.limitedStock)
        {
            stockText.text = $"库存: {item.currentStock}";
            stockText.gameObject.SetActive(true);
        }
        else if (stockText != null)
        {
            stockText.gameObject.SetActive(false);
        }

        UpdateBuyButtonState();
    }

    // 更新购买按钮状态
    public void UpdateBuyButtonState()
    {
        if (buyButton == null) return;

        bool canBuy = SimpleShopManager.Instance.CanBuyItem(shopItem);
        buyButton.interactable = canBuy;

        if (priceText != null)
        {
            priceText.color = canBuy ? affordableColor : unaffordableColor;
        }

        if (shopItem.limitedStock && shopItem.currentStock <= 0)
        {
            buyButton.interactable = false;
            if (priceText != null) priceText.color = soldOutColor;
            if (stockText != null) stockText.text = "已售罄";
        }
    }

    // 购买按钮点击
    private void OnBuyButtonClicked()
    {
        shopUI?.PurchaseItem(shopItem);
    }

    // 鼠标悬停效果
    public void OnPointerEnter()
    {
        transform.localScale = Vector3.one * 1.05f;
    }

    public void OnPointerExit()
    {
        transform.localScale = Vector3.one;
    }
}