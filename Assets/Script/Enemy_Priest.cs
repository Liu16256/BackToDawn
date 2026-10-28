using UnityEngine;

public class Enemy_Priest : MonoBehaviour, IDamageable
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 2.0f;
    [SerializeField] bool m_noBlood = false;

    [Header("AI行为参数")]
    [SerializeField] float m_detectRange = 5.0f;
    [SerializeField] float m_attackRange = 2.0f;
    [SerializeField] float m_idleTime = 2.0f;
    [SerializeField] float m_chaseTime = 3.0f;

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 300; // 牧师基础血量300
    [SerializeField] int m_attackDamage = 15;
    [SerializeField] float m_healCooldown = 2.0f; // 回血动作冷却
    private int m_currentHealth;
    private bool m_isDead = false;
    private int m_healCount = 0; // 已回血次数
    private int m_maxHealCount = 2; // 最大回血次数
    private bool m_isHealing = false; // 回血中标记

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private Collider2D m_collider2d;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // AI状态
    private Transform m_playerTransform;
    private enum AIState { Idle, Patrol, Chase, Attack, Heal }
    private AIState m_currentState = AIState.Idle;
    private float m_stateTimer = 0.0f;
    private Vector2 m_patrolPointA;
    private Vector2 m_patrolPointB;
    private bool m_movingToB = true;

    void Start()
    {
        // 组件初始化
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_collider2d = GetComponent<Collider2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_groundSensor = transform.Find("GroundSensor")?.GetComponent<Sensor_PixelHeroes>();

        // 容错：组件为空提示
        if (m_groundSensor == null)
        {
            Debug.LogError("[Enemy_Priest] 未找到GroundSensor组件，请检查物体子节点！");
        }

        // 刚体配置（防止被玩家推动）
        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        m_body2d.useFullKinematicContacts = false;
        m_body2d.mass = 1000f;
        m_body2d.drag = 10f;
        m_body2d.angularDrag = 10f;
        m_body2d.freezeRotation = true;

        // 玩家引用初始化
        m_playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (m_playerTransform == null)
        {
            Debug.LogWarning("[Enemy_Priest] 未找到玩家对象（Tag: Player）！");
        }

        // 巡逻点初始化
        m_patrolPointA = transform.position + Vector3.left * 2;
        m_patrolPointB = transform.position + Vector3.right * 2;

        // 血量初始化
        m_currentHealth = m_maxHealth;
        Debug.Log($"[敌人初始化] 牧师最大血量：{m_maxHealth}，初始血量：{m_currentHealth}");
    }

    void Update()
    {
        if (m_isDead) return;

        GroundCheck();
        CheckHealCondition();
        UpdateAIState();
        UpdateAnimations();
    }

    // 检查回血条件（血量低于50%且未达最大回血次数）
    void CheckHealCondition()
    {
        if (m_isHealing || m_healCount >= m_maxHealCount) return;

        float healthPercent = (float)m_currentHealth / m_maxHealth;
        // 300血的50%是150，低于150触发回血
        if (healthPercent < 0.5f)
        {
            TriggerHeal();
        }
    }

    // 触发回血逻辑
    void TriggerHeal()
    {
        m_isHealing = true;
        m_healCount++;
        SwitchState(AIState.Heal);
        m_animator.SetTrigger("Heal");
        Debug.Log($"[敌人回血] 血量低于150（300的50%），触发第{m_healCount}次回血！当前血量：{m_currentHealth}/300");

        // 延迟回血（匹配动画时长）
        Invoke(nameof(HealFullHealth), m_healCooldown);
    }

    // 回血至满血
    void HealFullHealth()
    {
        m_currentHealth = m_maxHealth;
        m_isHealing = false;
        Debug.Log($"[敌人回血] 回血完成！血量已回满：{m_currentHealth}/300，剩余回血次数：{m_maxHealCount - m_healCount}");

        // 回血后回到Idle状态
        SwitchState(AIState.Idle);
    }

    void GroundCheck()
    {
        if (m_groundSensor == null) return;

        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    // AI状态更新
    void UpdateAIState()
    {
        m_stateTimer += Time.deltaTime;
        bool isPlayerInRange = (m_playerTransform != null && Vector2.Distance(transform.position, m_playerTransform.position) <= m_detectRange);

        // 回血状态下停止所有移动
        if (m_currentState == AIState.Heal)
        {
            m_body2d.velocity = Vector2.zero;
            return;
        }

        switch (m_currentState)
        {
            case AIState.Idle:
                if (isPlayerInRange)
                    SwitchState(AIState.Chase);
                else if (m_stateTimer >= m_idleTime)
                    SwitchState(AIState.Patrol);
                break;

            case AIState.Patrol:
                if (isPlayerInRange)
                    SwitchState(AIState.Chase);
                else
                {
                    PatrolMovement();
                    // 到达巡逻点则回到Idle
                    if ((m_movingToB && Vector2.Distance(transform.position, m_patrolPointB) < 0.1f) ||
                        (!m_movingToB && Vector2.Distance(transform.position, m_patrolPointA) < 0.1f))
                    {
                        SwitchState(AIState.Idle);
                    }
                }
                break;

            case AIState.Chase:
                if (m_playerTransform == null)
                    SwitchState(AIState.Idle);
                else if (!isPlayerInRange && m_stateTimer >= m_chaseTime)
                    SwitchState(AIState.Idle);
                else
                {
                    ChaseMovement();
                    float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
                    if (distanceToPlayer <= m_attackRange)
                        SwitchState(AIState.Attack);
                }
                break;

            case AIState.Attack:
                if (m_playerTransform == null)
                    SwitchState(AIState.Idle);
                else
                {
                    float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
                    // 超出攻击范围则继续追击
                    if (distanceToPlayer > m_attackRange + 0.5f)
                        SwitchState(AIState.Chase);
                    // 攻击间隔1秒
                    else if (m_stateTimer >= 1.0f)
                    {
                        m_animator.SetTrigger("Attack");
                        EnemyAttackByDistance();
                        m_stateTimer = 0.0f;
                    }
                }
                break;
        }
    }

    // 切换AI状态
    void SwitchState(AIState newState)
    {
        m_currentState = newState;
        m_stateTimer = 0.0f;
    }

    // 巡逻移动
    void PatrolMovement()
    {
        Vector2 targetPos = m_movingToB ? m_patrolPointB : m_patrolPointA;
        MoveToTarget(targetPos);
        m_spriteRenderer.flipX = (targetPos.x < transform.position.x);
    }

    // 追击玩家
    void ChaseMovement()
    {
        if (m_playerTransform == null) return;
        MoveToTarget(m_playerTransform.position);
        m_spriteRenderer.flipX = (m_playerTransform.position.x < transform.position.x);
    }

    // 通用移动逻辑
    void MoveToTarget(Vector2 targetPos)
    {
        if (!m_grounded) return;
        float moveDirX = Mathf.Sign(targetPos.x - transform.position.x);
        m_body2d.velocity = new Vector2(moveDirX * m_speed, m_body2d.velocity.y);
    }

    // 动画更新
    void UpdateAnimations()
    {
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon)
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

    // 敌人攻击逻辑
    void EnemyAttackByDistance()
    {
        if (m_isDead || m_playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, m_playerTransform.position);
        if (distance <= m_attackRange)
        {
            if (m_playerTransform.TryGetComponent(out IDamageable damageable) && !damageable.IsDead)
            {
                Debug.Log($"[敌人攻击] 牧师命中玩家，距离：{distance:F2}，造成{m_attackDamage}点伤害");
                damageable.TakeDamage(m_attackDamage);
            }
        }
    }

    // 实现IDamageable接口 - 受击逻辑
    public void TakeDamage(int damage)
    {
        if (m_isDead || m_isHealing) return;

        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        m_animator?.SetTrigger("Hurt"); // 增加空值判断
        m_stateTimer = 0;

        // 对m_spriteRenderer和m_body2d增加空值判断
        if (m_spriteRenderer != null && m_body2d != null)
        {
            m_body2d.velocity = new Vector2(m_spriteRenderer.flipX ? 1 : -1, m_body2d.velocity.y + 1);
        }

        Debug.Log($"[敌人受击] 牧师受到{damage}点伤害，当前血量：{m_currentHealth}/300");

        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    // 实现IDamageable接口 - 死亡逻辑
    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");

        // 死亡后停止移动并禁用碰撞
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static;
        m_collider2d.enabled = false;

        Debug.Log($"[敌人死亡] 牧师已死亡，共触发{m_healCount}次回血，剩余血量：{m_currentHealth}/300");
    }

    // 实现IDamageable接口 - 是否死亡属性
    public bool IsDead => m_isDead;

    // 绘制攻击范围Gizmo
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);
    }
}