using UnityEngine;
using System.Collections;
using DamageInterface1;

// 适配风暴法师的IDamageable10接口，实现盗贼敌人AI
public class Enemy_Rogue : MonoBehaviour, IDamageable10
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 3.5f; // 敌人移动速度（略低于玩家）
    [SerializeField] float m_jumpForce = 6.0f; // 跳跃力（AI极少使用）
    [SerializeField] bool m_noBlood = false; // 无血迹效果开关
    [SerializeField] int m_maxHealth = 150; // 敌人血量
    [SerializeField] int m_attackDamage = 25; // 攻击伤害

    [Header("AI行为参数")]
    [SerializeField] float m_detectRange = 8.0f; // 检测玩家范围
    [SerializeField] float m_attackRange = 1.5f; // 攻击范围（近战）
    [SerializeField] float m_attackInterval = 1.2f; // 攻击间隔
    [SerializeField] float m_patrolRadius = 4.0f; // 巡逻半径
    [SerializeField] float m_dashDistance = 2.5f; // 闪现距离（AI技能）
    [SerializeField] float m_dashCD = 8.0f; // 闪现冷却

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Collider2D m_collider2d;
    private SpriteRenderer m_spriteRenderer;

    // 基础状态
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private int m_currentHealth;
    private bool m_isDead = false; // 死亡状态标记（空A核心）
    private bool m_canAttack = true;
    private float m_attackCooldown = 0f;
    private float m_dashCDTimer = 0f;
    private bool m_isDashing = false;

    // AI目标与状态
    private Transform m_playerTransform;
    private enum AIState { Idle, Patrol, Chase, Attack, Dash, Hurt }
    private AIState m_currentState = AIState.Idle;
    private float m_stateTimer = 0.0f;
    private Vector2 m_patrolOrigin; // 巡逻原点
    private float m_patrolLeftLimit; // 巡逻左边界
    private float m_patrolRightLimit; // 巡逻右边界
    private float m_patrolDirection = 1f; // 巡逻方向（1=右，-1=左）

    void Awake()
    {
        // 组件初始化（带容错）
        m_animator = GetComponent<Animator>() ?? gameObject.AddComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
        m_collider2d = GetComponent<Collider2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();

        // 地面传感器初始化
        if (transform.Find("GroundSensor") != null)
        {
            m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[Enemy_Rogue] 未找到GroundSensor子物体！");
        }

        // 刚体设置
        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.freezeRotation = true;
        m_body2d.drag = 2f;
        m_body2d.gravityScale = 3f; // 略大的重力，防止浮空

        // 玩家目标初始化（通过Player标签，适配StormMage）
        m_playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (m_playerTransform == null)
        {
            Debug.LogError("[Enemy_Rogue] 未找到Player标签的玩家物体！");
        }

        // 巡逻范围初始化
        m_patrolOrigin = transform.position;
        m_patrolLeftLimit = m_patrolOrigin.x - m_patrolRadius;
        m_patrolRightLimit = m_patrolOrigin.x + m_patrolRadius;

        // 血量初始化
        m_currentHealth = m_maxHealth;
    }

    void Update()
    {
        // 死亡状态直接返回，停止所有逻辑
        if (m_isDead) return;

        // 基础逻辑更新
        GroundCheck();
        UpdateCooldowns();
        UpdateAIStateMachine();
        UpdateAnimations();
    }

    #region 核心辅助方法
    // 地面检测（与玩家逻辑一致）
    private void GroundCheck()
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

    // 更新冷却（攻击+闪现）
    private void UpdateCooldowns()
    {
        // 攻击冷却
        if (!m_canAttack)
        {
            m_attackCooldown -= Time.deltaTime;
            if (m_attackCooldown <= 0)
            {
                m_canAttack = true;
                m_attackCooldown = 0;
            }
        }

        // 闪现冷却
        if (m_dashCDTimer > 0)
        {
            m_dashCDTimer -= Time.deltaTime;
        }

        m_stateTimer += Time.deltaTime;
    }

    // 动画状态更新
    private void UpdateAnimations()
    {
        if (m_animator == null) return;

        // 奔跑/移动动画
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon && !m_isDashing)
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

    #region AI状态机核心逻辑
    private void UpdateAIStateMachine()
    {
        // 无玩家则巡逻（适配IDamageable10）
        if (m_playerTransform == null || m_playerTransform.GetComponent<IDamageable10>().IsDead)
        {
            SwitchState(AIState.Patrol);
            HandlePatrolState();
            return;
        }

        // 计算与玩家的距离
        float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
        bool isPlayerInAttackRange = distanceToPlayer <= m_attackRange;
        bool isPlayerInDetectRange = distanceToPlayer <= m_detectRange;
        bool canDash = distanceToPlayer > m_attackRange && distanceToPlayer <= m_detectRange && m_dashCDTimer <= 0 && !m_isDashing;

        // 状态优先级：闪现 > 攻击 > 追击 > 巡逻
        if (canDash)
        {
            SwitchState(AIState.Dash);
            HandleDashState();
        }
        else if (isPlayerInAttackRange && m_canAttack)
        {
            SwitchState(AIState.Attack);
            HandleAttackState();
        }
        else if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            HandleChaseState();
        }
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
        Debug.Log($"[Enemy_Rogue] 状态切换：{newState}");
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
        m_body2d.velocity = new Vector2(m_patrolDirection * m_speed, m_body2d.velocity.y);

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
        m_body2d.velocity = new Vector2(dir * m_speed, m_body2d.velocity.y);
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

    // 闪现状态：快速突进到玩家身边
    private void HandleDashState()
    {
        StartCoroutine(EnemyDashCoroutine());
        m_dashCDTimer = m_dashCD;
    }
    #endregion

    #region 攻击/闪现技能逻辑
    // 近战攻击逻辑（适配IDamageable10）
    private void TriggerAttack()
    {
        if (m_isDead || m_animator == null) return;

        // 播放攻击动画
        m_animator.SetTrigger("Attack");
        Debug.Log($"[Enemy_Rogue] 发动近战攻击，造成{m_attackDamage}伤害");

        // 检测攻击范围内的玩家并造成伤害
        Collider2D hitPlayer = Physics2D.OverlapCircle(
            transform.position,
            m_attackRange,
            LayerMask.GetMask("Player")
        );

        if (hitPlayer != null && hitPlayer.TryGetComponent(out IDamageable10 player))
        {
            if (!player.IsDead)
            {
                player.TakeDamage(m_attackDamage);
            }
        }
    }

    // 敌人闪现协程（模仿玩家闪现逻辑）
    private IEnumerator EnemyDashCoroutine()
    {
        m_isDashing = true;
        m_animator.SetTrigger("SpecialAttack"); // 播放闪现动画

        // 保存原始状态
        RigidbodyType2D originalBodyType = m_body2d.bodyType;
        bool originalColliderEnabled = m_collider2d.enabled;
        float originalGravity = m_body2d.gravityScale;

        // 闪现时的物理状态修改
        m_body2d.bodyType = RigidbodyType2D.Kinematic;
        m_collider2d.enabled = false;
        m_body2d.gravityScale = 0;
        m_body2d.velocity = Vector2.zero;

        // 朝向玩家并计算闪现目标位置
        float dir = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = dir < 0;

        Vector2 dashDirection = new Vector2(dir, 0).normalized;
        Vector2 targetPosition = m_body2d.position + dashDirection * m_dashDistance;

        // 执行闪现
        m_body2d.MovePosition(targetPosition);
        yield return new WaitForSeconds(0.2f); // 闪现持续时间

        // 恢复状态
        m_collider2d.enabled = originalColliderEnabled;
        m_body2d.bodyType = originalBodyType;
        m_body2d.gravityScale = originalGravity;
        m_isDashing = false;

        // 闪现后立即尝试攻击
        if (Vector2.Distance(transform.position, m_playerTransform.position) <= m_attackRange && m_canAttack)
        {
            TriggerAttack();
            m_canAttack = false;
            m_attackCooldown = m_attackInterval;
        }
    }
    #endregion

    #region IDamageable10 接口实现（受击/死亡，兼容玩家攻击）
    public void TakeDamage(int damage)
    {
        if (m_isDead)
        {
            Debug.Log($"[Enemy_Rogue] 已死亡，无法接收伤害");
            return;
        }

        // 扣血并限制最小值为0
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[Enemy_Rogue] 受到{damage}伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

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
        Debug.Log("[Enemy_Rogue] 死亡");

        // 停止所有移动和协程
        StopAllCoroutines();
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static;

        // 播放死亡动画
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");

        // 禁用碰撞体，防止玩家攻击判定
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

        // 攻击范围（白色）
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 巡逻范围（绿色）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector2(m_patrolLeftLimit, transform.position.y),
                        new Vector2(m_patrolRightLimit, transform.position.y));

        // 闪现范围（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, m_dashDistance);
    }
    #endregion

    // 销毁时恢复组件状态（防止场景残留异常）
    private void OnDestroy()
    {
        if (m_collider2d != null) m_collider2d.enabled = true;
        if (m_body2d != null)
        {
            m_body2d.gravityScale = 1;
            m_body2d.bodyType = RigidbodyType2D.Dynamic;
        }
    }
}