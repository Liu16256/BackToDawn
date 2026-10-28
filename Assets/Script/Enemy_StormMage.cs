using UnityEngine;
using System.Collections;
using DamageInterface1;

// 实现 IDamageable9 接口（替换原 IDamageable）
public class Enemy_StormMage : MonoBehaviour, IDamageable9
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 3.0f; // 风暴法师移动速度
    [SerializeField] float m_jumpForce = 5.0f; // 跳跃力（AI极少使用）
    [SerializeField] bool m_noBlood = false; // 无血迹效果开关

    [Header("AI行为参数")]
    [SerializeField] float m_detectRange = 7.0f; // 检测玩家范围
    [SerializeField] float m_attackRange = 2.5f; // 攻击范围
    [SerializeField] float m_attackInterval = 1.5f; // 攻击间隔
    [SerializeField] float m_patrolRadius = 3.0f; // 巡逻半径

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 200; // 风暴法师血量
    [SerializeField] int m_attackDamage = 35; // 攻击伤害

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private Collider2D m_collider2d;

    // 基础状态
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private int m_currentHealth;
    private bool m_isDead = false; // 死亡状态标记（空A核心）
    private bool m_canAttack = true;
    private float m_attackCooldown = 0f;

    // AI目标与状态
    private Transform m_playerTransform;
    private enum AIState { Idle, Patrol, Chase, Attack, Hurt }
    private AIState m_currentState = AIState.Idle;
    private float m_stateTimer = 0.0f;
    private Vector2 m_patrolOrigin; // 巡逻原点
    private float m_patrolLeftLimit; // 巡逻左边界
    private float m_patrolRightLimit; // 巡逻右边界
    private float m_patrolDirection = 1f; // 巡逻方向（1=右，-1=左）

    void Awake()
    {
        // 初始化组件
        m_animator = GetComponent<Animator>() ?? gameObject.AddComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        m_collider2d = GetComponent<Collider2D>();

        // 初始化地面传感器
        if (transform.Find("GroundSensor") != null)
        {
            m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[风暴法师] 未找到GroundSensor子物体！");
        }

        // 初始化刚体设置
        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.freezeRotation = true;
        m_body2d.drag = 2f;
        m_body2d.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 初始化玩家目标（通过Player标签）
        m_playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (m_playerTransform == null)
        {
            Debug.LogError("[风暴法师] 未找到Player标签的玩家物体！");
        }

        // 初始化巡逻范围
        m_patrolOrigin = transform.position;
        m_patrolLeftLimit = m_patrolOrigin.x - m_patrolRadius;
        m_patrolRightLimit = m_patrolOrigin.x + m_patrolRadius;

        // 初始化血量
        m_currentHealth = m_maxHealth;
    }

    void Update()
    {
        // 死亡状态直接返回，停止所有逻辑
        if (m_isDead) return;

        // 更新地面检测
        UpdateGroundCheck();

        // 更新冷却计时器
        UpdateCooldowns();

        // AI状态机核心逻辑
        UpdateAIStateMachine();

        // 更新动画状态
        UpdateAnimations();
    }

    #region 核心辅助方法
    // 地面检测逻辑（保留原核心）
    private void UpdateGroundCheck()
    {
        if (m_groundSensor == null) return;

        bool newGrounded = m_groundSensor.State();
        if (!m_grounded && newGrounded)
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }
        else if (m_grounded && !newGrounded)
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    // 更新冷却计时器（攻击CD）
    private void UpdateCooldowns()
    {
        if (!m_canAttack)
        {
            m_attackCooldown -= Time.deltaTime;
            if (m_attackCooldown <= 0)
            {
                m_canAttack = true;
                m_attackCooldown = 0;
            }
        }
        m_stateTimer += Time.deltaTime;
    }
    #endregion

    #region AI状态机逻辑
    private void UpdateAIStateMachine()
    {
        // 无玩家则巡逻
        if (m_playerTransform == null)
        {
            SwitchState(AIState.Patrol);
            HandlePatrolState();
            return;
        }

        // 计算与玩家的距离
        float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
        bool isPlayerInAttackRange = distanceToPlayer <= m_attackRange;
        bool isPlayerInDetectRange = distanceToPlayer <= m_detectRange;

        // 优先攻击
        if (isPlayerInAttackRange && m_canAttack)
        {
            SwitchState(AIState.Attack);
            HandleAttackState();
        }
        // 玩家在检测范围则追击
        else if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            HandleChaseState();
        }
        // 无玩家则巡逻/闲置
        else
        {
            SwitchState(m_stateTimer > 1f ? AIState.Patrol : AIState.Idle);
            if (m_currentState == AIState.Patrol) HandlePatrolState();
            else HandleIdleState();
        }
    }

    // 切换AI状态（避免重复切换）
    private void SwitchState(AIState newState)
    {
        if (m_currentState == newState) return;

        m_currentState = newState;
        m_stateTimer = 0f;
        Debug.Log($"[风暴法师] 状态切换：{newState}");
    }

    // 闲置状态：停止移动
    private void HandleIdleState()
    {
        m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
    }

    // 巡逻状态：左右往返移动
    private void HandlePatrolState()
    {
        float currentX = transform.position.x;

        // 到达边界则反向
        if (currentX <= m_patrolLeftLimit) m_patrolDirection = 1f;
        else if (currentX >= m_patrolRightLimit) m_patrolDirection = -1f;

        // 移动逻辑
        float targetX = currentX + m_patrolDirection * m_speed * Time.deltaTime;
        m_body2d.velocity = new Vector2((targetX - currentX) / Time.deltaTime, m_body2d.velocity.y);

        // 翻转朝向
        m_spriteRenderer.flipX = m_patrolDirection < 0;
    }

    // 追击状态：向玩家移动
    private void HandleChaseState()
    {
        // 朝向玩家
        float dir = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = dir < 0;

        // 向玩家移动
        float targetX = transform.position.x + dir * m_speed * Time.deltaTime;
        m_body2d.velocity = new Vector2((targetX - transform.position.x) / Time.deltaTime, m_body2d.velocity.y);
    }

    // 攻击状态：停止移动+触发攻击
    private void HandleAttackState()
    {
        // 停止移动，面向玩家
        m_body2d.velocity = Vector2.zero;
        float dir = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = dir < 0;

        // 触发攻击
        TriggerAttack();
        m_canAttack = false;
        m_attackCooldown = m_attackInterval;
    }
    #endregion

    #region 攻击逻辑（适配 IDamageable9）
    private void TriggerAttack()
    {
        if (m_isDead || m_animator == null) return;

        // 播放攻击动画
        m_animator.SetTrigger("Attack");
        Debug.Log($"[风暴法师] 发动攻击，造成{m_attackDamage}伤害");

        // 检测攻击范围内的玩家并造成伤害（适配 IDamageable9）
        Collider2D hitPlayer = Physics2D.OverlapCircle(
            transform.position,
            m_attackRange,
            LayerMask.GetMask("Player")
        );

        if (hitPlayer != null && hitPlayer.TryGetComponent(out IDamageable9 player))
        {
            player.TakeDamage(m_attackDamage);
        }
    }
    #endregion

    #region 动画更新
    private void UpdateAnimations()
    {
        if (m_animator == null) return;

        // 奔跑/移动动画
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        // 闲置动画
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }
    #endregion

    #region IDamageable9 接口实现（受击/死亡，空A核心）
    public void TakeDamage(int damage)
    {
        if (m_isDead)
        {
            Debug.Log($"[风暴法师] 已死亡，无法接收伤害");
            return;
        }

        // 扣血并限制最小值为0
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[风暴法师] 受到{damage}伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 播放受击动画，切换受击状态
        m_animator.SetTrigger("Hurt");
        SwitchState(AIState.Hurt);

        // 血量为0触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[风暴法师] 死亡");

        // 停止移动，冻结刚体
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static;

        // 播放死亡动画
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");

        // 禁用碰撞体
        if (m_collider2d != null) m_collider2d.enabled = false;
    }

    // 空A核心：暴露死亡状态，玩家攻击时跳过
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos可视化（调试用）
    private void OnDrawGizmosSelected()
    {
        // 检测范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_detectRange);

        // 攻击范围（橙色）
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 巡逻范围（绿色）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector2(m_patrolLeftLimit, transform.position.y),
                        new Vector2(m_patrolRightLimit, transform.position.y));
    }
    #endregion
}