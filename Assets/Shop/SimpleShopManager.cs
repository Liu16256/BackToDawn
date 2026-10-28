using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SimpleShopManager : MonoBehaviour
{
    public static SimpleShopManager Instance;

    [Header("商店商品")]
    public List<SimpleShopItem> shopItems = new List<SimpleShopItem>();

    [Header("事件")]
    public UnityEvent<SimpleShopItem> onPurchaseSuccess;  // 购买成功事件
    public UnityEvent<string> onPurchaseFailed;           // 购买失败事件

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 初始化所有商品的库存
        foreach (var item in shopItems)
        {
            item.ResetStock();
        }
    }

    // 购买商品
    public bool PurchaseItem(SimpleShopItem shopItem)
    {
        if (shopItem == null)
        {
            onPurchaseFailed?.Invoke("商品不存在");
            return false;
        }

        // 检查库存
        if (shopItem.limitedStock && shopItem.currentStock <= 0)
        {
            onPurchaseFailed?.Invoke("已售罄");
            return false;
        }

        // 检查金币
        if (GoldManager.Instance == null || !GoldManager.Instance.SpendGold(shopItem.price))
        {
            onPurchaseFailed?.Invoke("金币不足");
            return false;
        }

        // 应用BUFF
        if (BuffManager.Instance != null && shopItem.buffData != null)
        {
            BuffManager.Instance.ApplyBuff(shopItem.buffData);
        }

        // 更新库存
        shopItem.Purchase();

        // 触发事件
        onPurchaseSuccess?.Invoke(shopItem);

        Debug.Log($"购买成功: {shopItem.name} 价格: {shopItem.price}");
        return true;
    }

    // 获取可购买的商品列表
    public List<SimpleShopItem> GetAvailableItems()
    {
        List<SimpleShopItem> availableItems = new List<SimpleShopItem>();

        foreach (var item in shopItems)
        {
            if (!item.limitedStock || item.currentStock > 0)
            {
                availableItems.Add(item);
            }
        }

        return availableItems;
    }

    // 检查是否可以购买
    public bool CanBuyItem(SimpleShopItem item)
    {
        if (GoldManager.Instance == null) return false;
        return item.CanBuy(GoldManager.Instance.GetCurrentGold());
    }

    // 重置所有库存
    public void ResetAllStock()
    {
        foreach (var item in shopItems)
        {
            item.ResetStock();
        }
    }
}