using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserControl : MonoBehaviour
{
    public float delayStart;

    public float laserLoopIntenal;   // 注意拼写错误：应为 Interval，但保留原名避免断连
    public float laserDelayCollider;
    public float laserDelayClose;

    public Transform laser;
    public Collider2D laserCollider;

    [Header("伤害设置")]
    [Tooltip("每次触发对玩家造成的伤害")]
    public int damagePerHit = 20;

    // Start is called before the first frame update
    void Start()
    {
        if (laser)
        {
            laser.gameObject.SetActive(false);
            StartCoroutine(colDelayStart(delayStart));
        }
    }

    private IEnumerator colDelayStart(float t)
    {
        yield return new WaitForSeconds(t);

        laser.gameObject.SetActive(true);

        StartCoroutine(colDelayClose(laserDelayClose));
        StartCoroutine(colDelayCollider(laserDelayCollider));

        StartCoroutine(colDelayStart(laserLoopIntenal));
    }

    private IEnumerator colDelayClose(float t)
    {
        yield return new WaitForSeconds(t);

        laser.gameObject.SetActive(false);
        if (laserCollider)
        {
            laserCollider.enabled = false;
        }
    }

    private IEnumerator colDelayCollider(float t)
    {
        yield return new WaitForSeconds(t);

        if (laserCollider)
        {
            laserCollider.enabled = true;
        }
    }

    // ================================
    // ✅ 新增：仅此一处，实现伤害
    // ================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只处理玩家
        if (!other.CompareTag("Player"))
            return;

        PlayerControl playerCtrl = other.GetComponent<PlayerControl>();
        if (playerCtrl == null || !playerCtrl.IsAlive())
            return;

        // 只有当激光的碰撞体「当前是启用状态」才造成伤害
        // 表示正处于“可伤人阶段”
        if (laserCollider != null && laserCollider.enabled)
        {
            playerCtrl.TakeDamage(damagePerHit);
        }
    }

    // Optional: 绘制 Gizmos 提高可读性（不影响运行）
    private void OnDrawGizmos()
    {
        if (laserCollider == null) return;

        Color color = laserCollider.enabled ? Color.red : new Color(1, 0.3f, 0, 0.2f);
        Collider2D col = laserCollider;

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
