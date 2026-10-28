using UnityEngine;

[RequireComponent(typeof(Collider2D))]

public static class PlayerControlExtensions
{
    public static void SetAttack(this PlayerControl player, int newAttack)
    {
        var field = typeof(PlayerControl).GetField("m_attack",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
            field.SetValue(player, newAttack);
    }

    // ✅ 新增：设置防御力
    public static void SetDefense(this PlayerControl player, int newDefense)
    {
        var field = typeof(PlayerControl).GetField("m_defense",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
            field.SetValue(player, newDefense);
    }


    // ✅ 同样为攻击提供 Add 方法（更直观）
    public static void AddAttack(this PlayerControl player, int bonus)
    {
        int current = player.GetAttack();
        player.SetAttack(current + bonus);
    }
}

public class TouchRestoreItem : MonoBehaviour
{
    // === 可配置效果开关 ===
    [Header("恢复效果")]
    [SerializeField] private bool applyHeal = true;
    [SerializeField] private int healAmount = 20;

    [SerializeField] private bool applyAttackBonus = true;
    [SerializeField] private int attackBonus = 1;

    [SerializeField] private bool applyDefenseBonus = true;
    [SerializeField] private int defenseBonus = 5;

    [Header("音效与特效")]
    [SerializeField] private AudioClip pickupSFX;
    [SerializeField] private GameObject pickupVFX;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (!other.TryGetComponent<PlayerControl>(out PlayerControl player))
            return;

        // ===== 应用所有启用的效果 =====
        bool hasEffect = false;

        // 1. 回血
        if (applyHeal && player.IsAlive())
        {
            player.Heal(healAmount);
            hasEffect = true;
        }

        // 2. 增加攻击力
        if (applyAttackBonus)
        {
            player.AddAttack(attackBonus);
            hasEffect = true;
        }


        // ===== 只有真正产生了效果才播放音效、粒子并销毁 =====
        if (hasEffect)
        {
            // 播放音效
            if (pickupSFX != null)
            {
                AudioSource.PlayClipAtPoint(pickupSFX, transform.position);
            }

            // 生成特效
            if (pickupVFX != null)
            {
                Instantiate(pickupVFX, transform.position, Quaternion.identity);
            }

            // 销毁物品
            Destroy(gameObject);
        }
    }

    // 编辑器可视化
    private void OnDrawGizmosSelected()
    {
        Color color = Color.green;
        color.a = 0.3f;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = color;

        if (col is BoxCollider2D box)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.matrix = oldMatrix;
        }
        else if (col is CircleCollider2D circle)
        {
            Vector3 center = transform.TransformPoint(circle.offset);
            Gizmos.DrawWireSphere(center, circle.radius);
        }
        else
        {
            Bounds bounds = col.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
