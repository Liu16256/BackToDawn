using UnityEngine;

public class WindZone : MonoBehaviour
{
    [Header("风力设置")]
    [Tooltip("每帧施加的向上力大小（建议值：3~10）")]
    [SerializeField] private float windStrength = 5f;

    [Tooltip("是否只影响玩家（通过标签判断）")]
    [SerializeField] private bool affectPlayerOnly = true;

    [Header("视觉反馈")]
    [SerializeField] private Color gizmosColor = new Color(0, 1, 0, 0.3f);

    private void OnTriggerStay2D(Collider2D other)
    {
        // 可选：只影响玩家
        if (affectPlayerOnly && !other.CompareTag("Player"))
            return;

        if (!other.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            return;

        // 施加向上的力（使用 ForceMode2D.Force 以实现平滑加速）
        Vector2 windForce = Vector2.up * windStrength;
        rb.AddForce(windForce, ForceMode2D.Force);
    }

    // Optional: Exit to ensure no lingering effects (though not required for Force)
    private void OnTriggerExit2D(Collider2D other)
    {
        // 力是瞬时的，离开后自然停止。无需清除。
        // 如果你用了协程或持续状态标记，则需要清理 —— 当前不需要。
    }

    // 编辑器中绘制区域颜色
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmosColor;
        if (GetComponent<Collider2D>() is BoxCollider2D boxCol)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCol.offset, boxCol.size);
        }
        else if (GetComponent<Collider2D>() is CircleCollider2D circleCol)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, circleCol.radius);
        }
        else
        {
            // 其他 Collider 类型可用默认包围盒
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && col.bounds.size.sqrMagnitude > 0)
            {
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            }
        }
    }
}
