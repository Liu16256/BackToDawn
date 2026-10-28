using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [Header("行为设置")]
    [Tooltip("是否只对玩家生效（通过标签判断）")]
    [SerializeField] private bool affectPlayerOnly = true;

    [Tooltip("接触后延迟死亡时间（秒），设为0则立即死亡")]
    [SerializeField] private float delayBeforeKill = 0f;

    [Header("音效与特效（可选）")]
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private GameObject deathVFX;

    private PlayerControl m_playerCtrl; // 缓存引用用于 Invoke

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (affectPlayerOnly && !other.CompareTag("Player"))
            return;

        HandleDeath(other.gameObject);
    }

    private void HandleDeath(GameObject target)
    {
        m_playerCtrl = target.GetComponent<PlayerControl>();
        if (m_playerCtrl == null)
        {
            Debug.LogWarning($"目标 {target.name} 没有 PlayerControl 组件，无法执行死亡逻辑。");
            return;
        }

        // 播放音效
        if (deathSFX != null)
        {
            AudioSource.PlayClipAtPoint(deathSFX, target.transform.position);
        }

        // 生成特效
        if (deathVFX != null)
        {
            Instantiate(deathVFX, target.transform.position, Quaternion.identity);
        }

        // 延迟调用成员方法
        if (delayBeforeKill > 0)
        {
            Invoke("DoKillAfterDelay", delayBeforeKill);
        }
        else
        {
            // 立即死亡
            m_playerCtrl.TakeDamage(99999);
        }
    }

    // ✅ 成员方法，可供 Invoke 调用
    private void DoKillAfterDelay()
    {
        if (m_playerCtrl != null && m_playerCtrl.IsAlive())
        {
            m_playerCtrl.TakeDamage(99999);
        }
    }

    // 编辑器可视化
    private void OnDrawGizmos()
    {
        Color color = new Color(1, 0, 0, 0.3f);
        Collider2D col = GetComponent<Collider2D>();

        Gizmos.color = color;
        if (col is BoxCollider2D box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
        }
        else if (col is CircleCollider2D circle)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(Vector3.zero, circle.radius);
        }
        else
        {
            if (col != null)
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
