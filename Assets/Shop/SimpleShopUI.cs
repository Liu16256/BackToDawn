using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimpleShopUI : MonoBehaviour
{
    [Header("UI组件")]
    public GameObject shopPanel;
    public Transform itemsContainer;
    public GameObject itemPrefab;

    [Header("金币显示")]
    public TextMeshProUGUI goldText;

    [Header("按钮")]
    public Button closeButton;

    [Header("商店信息")]
    public string shopName = "BUFF商店";
    public TextMeshProUGUI shopTitle;

    [Header("空商店提示")]
    public GameObject emptyShopPanel;
    public TextMeshProUGUI emptyShopText;

    private List<ShopItemUI> itemUIList = new List<ShopItemUI>();

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseShop);
        }

        if (shopTitle != null)
        {
            shopTitle.text = shopName;
        }

        // 监听金币变化 - 修正这里：传入正确的方法
        if (GoldManager.Instance != null)
        {
            // 之前错误：GoldManager.Instance.onGoldChanged.AddListener(UpdateGoldDisplay);
            // 正确：因为onGoldChanged需要一个接受int参数的方法
            GoldManager.Instance.onGoldChanged.AddListener(OnGoldChanged);
        }

        // 默认隐藏
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    // 金币变化时的回调方法
    private void OnGoldChanged(int newGold)
    {
        UpdateGoldDisplay(newGold);
    }

    // 打开商店
    public void OpenShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            UpdateShopDisplay();
        }
    }

    // 关闭商店
    public void CloseShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    // 更新商店显示
    public void UpdateShopDisplay()
    {
        if (SimpleShopManager.Instance == null) return;

        // 清除现有UI
        ClearItems();

        // 获取可购买的商品
        var availableItems = SimpleShopManager.Instance.GetAvailableItems();

        // 显示空商店提示
        if (availableItems.Count == 0)
        {
            if (emptyShopPanel != null)
            {
                emptyShopPanel.SetActive(true);
                if (emptyShopText != null)
                {
                    emptyShopText.text = "商店已售罄，请下次再来！";
                }
            }
            return;
        }
        else if (emptyShopPanel != null)
        {
            emptyShopPanel.SetActive(false);
        }

        // 创建商品UI
        foreach (var item in availableItems)
        {
            CreateShopItemUI(item);
        }

        // 更新金币显示
        UpdateGoldDisplay(GoldManager.Instance.GetCurrentGold());
    }

    // 创建单个商品UI
    private void CreateShopItemUI(SimpleShopItem shopItem)
    {
        if (itemPrefab == null || itemsContainer == null) return;

        GameObject itemObj = Instantiate(itemPrefab, itemsContainer);
        ShopItemUI itemUI = itemObj.GetComponent<ShopItemUI>();

        if (itemUI != null)
        {
            itemUI.Initialize(shopItem, this);
            itemUIList.Add(itemUI);
        }
    }

    // 清除所有商品UI
    private void ClearItems()
    {
        foreach (var itemUI in itemUIList)
        {
            if (itemUI != null && itemUI.gameObject != null)
            {
                Destroy(itemUI.gameObject);
            }
        }
        itemUIList.Clear();
    }

    // 更新金币显示
    private void UpdateGoldDisplay(int currentGold)
    {
        if (goldText != null)
        {
            goldText.text = currentGold.ToString();
        }
    }

    // 购买商品
    public void PurchaseItem(SimpleShopItem shopItem)
    {
        if (SimpleShopManager.Instance != null)
        {
            bool success = SimpleShopManager.Instance.PurchaseItem(shopItem);
            if (success)
            {
                // 重新刷新商店显示
                UpdateShopDisplay();
            }
        }
    }

    // 切换商店显示
    public void ToggleShop()
    {
        if (shopPanel != null && shopPanel.activeSelf)
        {
            CloseShop();
        }
        else
        {
            OpenShop();
        }
    }

    private void OnDestroy()
    {
        // 移除监听 - 修正这里
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.onGoldChanged.RemoveListener(OnGoldChanged);
        }
    }
}