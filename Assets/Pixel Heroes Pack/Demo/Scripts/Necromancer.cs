using UnityEngine;
using System.Collections;
using DamageInterface1;

// 玩家亡灵法师实现 IDamageable9 接口
public interface IDamageable9
{
    void TakeDamage(int damage);
    bool IsDead { get; }
    void Die();
}
public class Necromancer : MonoBehaviour, IDamageable9
{
    [SerializeField] float m_speed = 4.0f;
    [SerializeField] float m_jumpForce = 7.5f;
    [SerializeField] bool m_noBlood = false;

    // 新增：玩家血量（可根据需求调整）
    [SerializeField] int m_maxHealth = 500;
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

        // 初始化玩家血量
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
        //Death
        if (Input.GetKeyDown("e"))
        {
            Die(); // 调用接口的Die方法
        }

        //Hurt
        else if (Input.GetKeyDown("q"))
            m_animator.SetTrigger("Hurt");

        //Attack（空A核心：无论是否有目标，先播动画）
        else if (Input.GetMouseButtonDown(0))
        {
            m_animator.SetTrigger("Attack");
            // 可选：添加玩家攻击逻辑（检测风暴法师并造成伤害）
            AttackStormMage();
        }

        //Spellcast
        else if (Input.GetMouseButtonDown(1))
            m_animator.SetTrigger("Spellcast");

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

    // 新增：玩家攻击风暴法师的逻辑（支持空A）
    private void AttackStormMage()
    {
        Enemy_StormMage[] stormMages = FindObjectsOfType<Enemy_StormMage>();
        foreach (var mage in stormMages)
        {
            // 跳过已死亡的风暴法师（空A核心）
            if (mage.IsDead) continue;

            // 检测距离（可自定义攻击范围，示例为3f）
            float distance = Vector2.Distance(transform.position, mage.transform.position);
            if (distance <= 3f)
            {
                mage.TakeDamage(50); // 玩家攻击伤害，可自定义
            }
        }
    }

    #region IDamageable9 接口实现
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[玩家] 受到{damage}伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");
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
        Debug.Log("[玩家] 死亡");
    }

    public bool IsDead => m_isDead;
    #endregion
}