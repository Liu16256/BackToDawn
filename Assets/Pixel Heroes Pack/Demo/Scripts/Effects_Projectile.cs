//using UnityEngine;
//using System.Collections;

//public class Effects_Projectile : MonoBehaviour
//{

//    [SerializeField]
//    float speed = 5.0f;

//    private Rigidbody2D body2d;

//    // Use this for initialization
//    void Start()
//    {
//        body2d = GetComponent<Rigidbody2D>();
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        body2d.velocity = new Vector2(speed * transform.localScale.x, body2d.velocity.y);
//    }

//    private void OnTriggerEnter2D(Collider2D collision)
//    {
//        if (!collision.CompareTag("Player"))
//            Destroy(gameObject);
//    }
//}
using UnityEngine;

public class Effects_Projectile : MonoBehaviour
{
    [SerializeField] float speed = 5.0f;
    private Rigidbody2D body2d;

    // 新增：投掷物伤害（由玩家脚本传值）
    public int projectileDamage;
    // 目标标签（只对敌人造成伤害）
    private string targetTag = "Enemy";

    void Start()
    {
        body2d = GetComponent<Rigidbody2D>();
        // 保留原逻辑：2秒后自动销毁投掷物（防止内存泄漏）
        Destroy(gameObject, 2.0f);
    }

    void Update()
    {
        // 保留你原有的移动逻辑
        body2d.velocity = new Vector2(speed * transform.localScale.x, body2d.velocity.y);
    }

    // 改造碰撞逻辑：保留原销毁逻辑 + 新增伤害触发
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 保留你原有的销毁逻辑：不撞玩家就销毁
        if (!collision.CompareTag("Player"))
        {
            // 2. 新增：命中敌人时触发伤害
            if (collision.CompareTag(targetTag))
            {
                if (collision.TryGetComponent(out IDamageable damageable) && !damageable.IsDead)
                {
                    Debug.Log($"[投掷命中] 对{collision.gameObject.name}造成{projectileDamage}点伤害");
                    damageable.TakeDamage(projectileDamage);
                }
            }
            Destroy(gameObject);
        }
    }
}