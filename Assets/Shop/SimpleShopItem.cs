using UnityEngine;

[CreateAssetMenu(fileName = "NewShopItem", menuName = "Game/Shop Item")]
public class SimpleShopItem : ScriptableObject
{
    [Header("BUFF信息")]
    public BuffData buffData;  // 关联的BUFF

    [Header("价格")]
    public int price = 100;  // 固定价格

    [Header("库存")]
    public bool limitedStock = false;  // 是否限量
    public int stock = 1;             // 库存数量

    [System.NonSerialized] public int currentStock = 1;  // 当前库存（不序列化）

    // 重置库存
    public void ResetStock()
    {
        currentStock = stock;
    }

    // 购买
    public void Purchase()
    {
        if (limitedStock)
        {
            currentStock--;
        }
    }

    // 是否可购买
    public bool CanBuy(int playerGold)
    {
        if (playerGold < price) return false;
        if (limitedStock && currentStock <= 0) return false;
        return true;
    }
}