using System.Collections.Generic;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance;

    [System.Serializable]
    public class ActiveBuff
    {
        public BuffData buffData;
    }

    [Header("当前激活的BUFF")]
    public List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    // 玩家属性引用（这里需要你根据实际项目修改）
    private PlayerStats playerStats;

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
        // 获取玩家属性组件（需要你根据项目实际情况修改）
        // playerStats = FindObjectOfType<PlayerStats>();
    }

    // 应用BUFF
    public void ApplyBuff(BuffData buff)
    {
        if (buff == null) return;

        // 检查是否已经存在相同类型的BUFF
        ActiveBuff existingBuff = activeBuffs.Find(ab => ab.buffData.buffType == buff.buffType);

        if (existingBuff != null)
        {
            // 如果已存在，先移除旧效果
            RemoveBuffEffect(existingBuff.buffData);
            activeBuffs.Remove(existingBuff);
        }

        // 添加新BUFF
        ActiveBuff newActiveBuff = new ActiveBuff
        {
            buffData = buff
        };

        activeBuffs.Add(newActiveBuff);

        // 应用BUFF效果
        ApplyBuffEffect(buff);

        Debug.Log($"BUFF应用成功: {buff.buffName}");
    }

    // 应用BUFF效果
    private void ApplyBuffEffect(BuffData buff)
    {
        // 这里需要你根据实际的玩家属性系统来修改
        if (playerStats == null) return;

        switch (buff.buffType)
        {
            case BuffData.BuffType.Health:
                if (buff.isPercentage)
                    playerStats.maxHealth *= (1 + buff.value / 100f);
                else
                    playerStats.maxHealth += buff.value;
                break;

            case BuffData.BuffType.Damage:
                if (buff.isPercentage)
                    playerStats.attack *= (1 + buff.value / 100f);
                else
                    playerStats.attack += buff.value;
                break;

            case BuffData.BuffType.Defense:
                if (buff.isPercentage)
                    playerStats.defense *= (1 + buff.value / 100f);
                else
                    playerStats.defense += buff.value;
                break;

            case BuffData.BuffType.Speed:
                if (buff.isPercentage)
                    playerStats.speed *= (1 + buff.value / 100f);
                else
                    playerStats.speed += buff.value;
                break;
        }
    }

    // 移除BUFF效果
    private void RemoveBuffEffect(BuffData buff)
    {
        if (playerStats == null) return;

        switch (buff.buffType)
        {
            case BuffData.BuffType.Health:
                if (buff.isPercentage)
                    playerStats.maxHealth /= (1 + buff.value / 100f);
                else
                    playerStats.maxHealth -= buff.value;
                break;

            case BuffData.BuffType.Damage:
                if (buff.isPercentage)
                    playerStats.attack /= (1 + buff.value / 100f);
                else
                    playerStats.attack -= buff.value;
                break;

            case BuffData.BuffType.Defense:
                if (buff.isPercentage)
                    playerStats.defense /= (1 + buff.value / 100f);
                else
                    playerStats.defense -= buff.value;
                break;

            case BuffData.BuffType.Speed:
                if (buff.isPercentage)
                    playerStats.speed /= (1 + buff.value / 100f);
                else
                    playerStats.speed -= buff.value;
                break;
        }
    }

    // 获取指定类型的BUFF总加成
    public float GetTotalBonus(BuffData.BuffType type)
    {
        float total = 0;
        foreach (var activeBuff in activeBuffs)
        {
            if (activeBuff.buffData.buffType == type)
            {
                total += activeBuff.buffData.value;
            }
        }
        return total;
    }
}

// 示例玩家属性类（需要你根据实际项目修改）
public class PlayerStats : MonoBehaviour
{
    public float maxHealth = 100;
    public float attack = 10;
    public float defense = 5;
    public float speed = 5;
}