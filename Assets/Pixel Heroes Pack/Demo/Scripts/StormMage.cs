using UnityEngine;
using System.Collections;
using DamageInterface1;

// 风暴法师玩家实现 IDamageable10 接口
public interface IDamageable10
{
    void TakeDamage(int damage);
    bool IsDead { get; }
    void Die();
}
public class StormMage : MonoBehaviour, IDamageable10
{
    [SerializeField] float m_speed = 3.0f;
    [SerializeField] float m_jumpForce = 5.0f;
    [SerializeField] bool m_noBlood = false;

    // 新增：战斗属性（适配接口）
    [SerializeField] int m_maxHealth = 300;
    private int m_currentHealth;
    private bool m_isDead = false; // 死亡状态标记

    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // Use this for initialization
    void Start()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_PixelHeroes>();

        // 初始化血量
        m_currentHealth = m_maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        // 死亡后停止所有操作
        if (m_isDead) return;

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // -- Handle input and movement --
        float inputX = Input.GetAxis("Horizontal");

        // Swap direction of sprite depending on walk direction
        if (inputX > 0)
            GetComponent<SpriteRenderer>().flipX = false;
        else if (inputX < 0)
            GetComponent<SpriteRenderer>().flipX = true;

        // Move
        m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);

        //Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);

        // -- Handle Animations --
        //Death（调用接口的Die方法）
        if (Input.GetKeyDown("e"))
        {
            Die();
        }

        //Hurt（调用接口的TakeDamage方法）
        else if (Input.GetKeyDown("q"))
        {
            TakeDamage(20); // 测试受击伤害
        }

        //Attack（空A核心：先播动画，再检测敌人）
        else if (Input.GetMouseButtonDown(0))
        {
            m_animator.SetTrigger("Attack");
            // 可选：添加攻击敌人逻辑（检测Enemy_Rogue并造成伤害）
            AttackEnemyRogue();
        }

        //Jump
        else if (Input.GetKeyDown("space") && m_grounded)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }

        //Run
        else if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            // Reset timer
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }

        //Idle
        else
        {
            // Prevents flickering transitions to idle
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }

    // 新增：风暴法师攻击盗贼敌人的逻辑（支持空A）
    private void AttackEnemyRogue()
    {
        Enemy_Rogue[] rogueEnemies = FindObjectsOfType<Enemy_Rogue>();
        foreach (var rogue in rogueEnemies)
        {
            // 跳过已死亡的盗贼（空A核心）
            if (rogue.IsDead) continue;

            // 检测攻击距离（自定义为3f）
            float distance = Vector2.Distance(transform.position, rogue.transform.position);
            if (distance <= 3f)
            {
                rogue.TakeDamage(40); // 风暴法师攻击伤害，可自定义
            }
        }
    }

    #region IDamageable10 接口实现
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[风暴法师] 受到{damage}伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");
        m_animator.SetTrigger("Hurt");

        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static;
        Debug.Log("[风暴法师] 死亡");
    }

    // 空A核心：暴露死亡状态
    public bool IsDead => m_isDead;
    #endregion
}