using UnityEngine;

[CreateAssetMenu(fileName = "NewBuff", menuName = "Game/Buff Data")]
public class BuffData : ScriptableObject
{
    public string buffName;
    [TextArea] public string description;
    public Sprite icon;

    // 只保留四种属性
    public enum BuffType
    {
        Health,      // 生命
        Damage,      // 攻击
        Defense,     // 防御
        Speed        // 速度
    }

    public BuffType buffType;
    public float value;  // 数值加成
    public bool isPercentage;  // 是否是百分比加成
}