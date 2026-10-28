using UnityEngine;
using UnityEngine.Events;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance;

    [SerializeField] private int currentGold = 1000;
    [SerializeField] private int maxGold = 999999;

    [Header("事件")]
    public UnityEvent<int> onGoldChanged;  // 参数：变化后的金币数量
    public UnityEvent<bool> onPurchaseResult;  // 参数：购买是否成功

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

    // 获取当前金币
    public int GetCurrentGold()
    {
        return currentGold;
    }

    // 增加金币
    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        int oldGold = currentGold;
        currentGold = Mathf.Min(currentGold + amount, maxGold);
        onGoldChanged?.Invoke(currentGold);
        Debug.Log($"获得金币: {amount}, 当前金币: {currentGold}");
    }

    // 花费金币
    public bool SpendGold(int amount)
    {
        if (currentGold < amount)
        {
            Debug.LogWarning($"金币不足! 需要: {amount}, 当前: {currentGold}");
            onPurchaseResult?.Invoke(false);
            return false;
        }

        int oldGold = currentGold;
        currentGold -= amount;
        onGoldChanged?.Invoke(currentGold);
        onPurchaseResult?.Invoke(true);
        Debug.Log($"花费金币: {amount}, 剩余金币: {currentGold}");
        return true;
    }

    // 检查是否足够金币
    public bool HasEnoughGold(int amount)
    {
        return currentGold >= amount;
    }

    // 设置金币（用于测试）
    public void SetGold(int amount)
    {
        currentGold = Mathf.Clamp(amount, 0, maxGold);
        onGoldChanged?.Invoke(currentGold);
    }
}