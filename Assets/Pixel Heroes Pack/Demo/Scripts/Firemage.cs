using UnityEngine;

namespace DamageInterface1
{
    public interface IDamageable8
    {
        void TakeDamage(int damage);
        bool IsDead { get; }
        void Die();
    }
}

public class Firemage : MonoBehaviour, DamageInterface1.IDamageable8
{
    [Header("玩家基础属性")]
    [SerializeField] float m_speed = 5.0f;
    [SerializeField] float m_jumpForce = 8.0f;
    [SerializeField] bool m_noBlood = false;

    [Header("玩家攻击属性（距离判定）")]
    [SerializeField] int m_fireballDamage = 50;
    [SerializeField] float m_attackRange = 3.0f; // 攻击有效距离
    [SerializeField] float m_attackCD = 1.5f;
    [SerializeField] LayerMask m_enemyLayer;

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private float m_attackCDTimer = 0.0f;
    private bool m_isDead = false;

    // 玩家血量
    [SerializeField] int m_maxHealth = 500;
    private int m_currentHealth;

    void Start()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();

        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
        }

        m_currentHealth = m_maxHealth;
        Debug.Log($"[火女] 初始化完成 | 攻击距离: {m_attackRange} | 伤害: {m_fireballDamage}");
    }

    void Update()
    {
        if (m_isDead) return;

        if (m_attackCDTimer > 0)
            m_attackCDTimer -= Time.deltaTime;

        HandleMovement();
        HandleGroundCheck();
        HandleAnimations();

        // 鼠标左键攻击（距离判定）
        if (Input.GetMouseButtonDown(0) && m_attackCDTimer <= 0)
        {
            AttackByDistance();
        }

        // 测试按键
        if (Input.GetKeyDown(KeyCode.Q)) TakeDamage(20);
        if (Input.GetKeyDown(KeyCode.E)) Die();
    }

    #region 基础逻辑
    private void HandleMovement()
    {
        float inputX = Input.GetAxis("Horizontal");
        if (inputX != 0)
            m_spriteRenderer.flipX = inputX < 0;

        if (m_body2d != null)
            m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);

        if (Input.GetKeyDown(KeyCode.Space) && m_grounded && m_body2d != null)
        {
            m_animator?.SetTrigger("Jump");
            m_grounded = false;
            m_animator?.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor?.Disable(0.2f);
        }
    }

    private void HandleGroundCheck()
    {
        if (m_groundSensor == null || m_animator == null) return;

        bool sensorState = m_groundSensor.State();
        if (!m_grounded && sensorState)
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", true);
        }
        else if (m_grounded && !sensorState)
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", false);
        }

        m_animator.SetFloat("AirSpeedY", m_body2d?.velocity.y ?? 0);
    }

    private void HandleAnimations()
    {
        if (m_animator == null) return;

        float inputX = Input.GetAxis("Horizontal");
        if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }
    #endregion

    #region 核心修改：距离判定攻击
    private void AttackByDistance()
    {
        m_attackCDTimer = m_attackCD;
        m_animator?.SetTrigger("Attack");
        Debug.Log($"[火女] 发起距离攻击 | 有效距离: {m_attackRange}");

        // 2. 查找场景中所有实现IDamageable8的敌人
        Enemy_Warlock[] warlocks = FindObjectsOfType<Enemy_Warlock>();
        bool hitEnemy = false;

        foreach (var warlock in warlocks)
        {
            // 跳过已死亡的敌人
            if (warlock.IsDead) break;

            // 3. 计算距离
            float distance = Vector2.Distance(transform.position, warlock.transform.position);
            Debug.Log($"[火女] 检测到术士 | 距离: {distance:F1} | 有效距离: {m_attackRange}");

            // 4. 距离判定：小于等于攻击范围则造成伤害
            if (distance <= m_attackRange)
            {
                warlock.TakeDamage(m_fireballDamage);
                Debug.Log($"[火女] 距离判定成功！击中术士，造成 {m_fireballDamage} 伤害");
                hitEnemy = true;
            }
            else
            {
                Debug.LogWarning($"[火女] 距离过远！当前 {distance:F1} > {m_attackRange}");
            }
        }

        if (!hitEnemy)
        {
            Debug.LogWarning("[火女] 攻击范围内无术士！");
        }
    }
    #endregion

    #region IDamageable8接口实现
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[火女] 受击！剩余血量：{m_currentHealth}/{m_maxHealth}");

        m_animator?.SetTrigger("Hurt");
        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[火女] 死亡！");

        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }

        if (m_body2d != null)
        {
            m_body2d.velocity = Vector2.zero;
            m_body2d.bodyType = RigidbodyType2D.Static;
        }

        this.enabled = false;
    }

    public bool IsDead => m_isDead;
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);
    }
}