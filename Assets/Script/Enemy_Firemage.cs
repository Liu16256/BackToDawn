using UnityEngine;
using System.Collections;


// 实现IDamageable6接口，兼容玩家攻击逻辑
public class Enemy_Firemage : MonoBehaviour, IDamageable6
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 2.5f; // 火法师移动速度（偏慢，远程定位）
    [SerializeField] bool m_noBlood = false; // 无血迹效果开关

    [Header("AI行为参数")]
    [SerializeField] float m_detectRange = 7.0f; // 检测玩家范围（远程法师更远）
    [SerializeField] float m_attackRange = 4.0f; // 施法范围（远程）
    [SerializeField] float m_keepDistanceRange = 5.0f; // 保持安全距离
    [SerializeField] float m_idleTime = 1.2f;    // 闲置状态持续时间
    [SerializeField] float m_chaseTime = 15.0f;  // 追击玩家的超时时间
    [SerializeField] float m_attackInterval = 1.5f; // 普通施法间隔
    [SerializeField] float m_fireBurstCD = 8.0f; // 火焰爆发冷却时间
    [SerializeField] float m_patrolRadius = 3.0f; // 巡逻半径

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 250; // 火法师血量（低，脆皮）
    [SerializeField] int m_fireballDamage = 35; // 火球术伤害
    [SerializeField] int m_fireBurstDamage = 60; // 火焰爆发伤害

    // 核心组件引用
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Collider2D m_collider2d;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // AI状态机相关
    private Transform m_playerTransform; // 玩家目标
    private enum AIState { Idle, Patrol, Chase, Attack, KeepDistance, FireBurst }
    private AIState m_currentState = AIState.Idle; // 当前状态
    private float m_stateTimer = 0.0f; // 状态持续计时器
    private Vector2 m_patrolPointA; // 巡逻点A
    private Vector2 m_patrolPointB; // 巡逻点B
    private bool m_movingToB = true; // 巡逻方向标记

    // 战斗状态相关
    private int m_currentHealth; // 当前血量
    private bool m_isDead = false; // 是否死亡
    private bool m_isCasting = false; // 施法锁定
    private float m_fireBurstCDTimer = 0.0f; // 火焰爆发冷却计时器

    void Start()
    {
        // 初始化组件（增加空值保护）
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_collider2d = GetComponent<Collider2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();

        // 地面传感器空值保护
        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[火法师] 未找到GroundSensor子物体，请检查物体层级！");
        }

        // 组件缺失提示
        if (m_animator == null) Debug.LogError("[火法师] 未挂载Animator组件！");
        if (m_body2d == null) Debug.LogError("[火法师] 未挂载Rigidbody2D组件！");

        // 初始化刚体物理（法师更轻，易被击退）
        InitRigidbodySettings();

        // 初始化玩家引用（通过Tag查找）
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            m_playerTransform = playerObj.transform;
        }

        // 初始化巡逻点
        Vector3 initPos = transform.position;
        m_patrolPointA = new Vector2(initPos.x - m_patrolRadius, initPos.y);
        m_patrolPointB = new Vector2(initPos.x + m_patrolRadius, initPos.y);

        // 初始化血量和冷却
        m_currentHealth = m_maxHealth;
        m_fireBurstCDTimer = 0.0f;
        Debug.Log($"[火法师] 初始化完成，血量：{m_currentHealth}/{m_maxHealth}，巡逻半径：{m_patrolRadius}");
    }

    void Update()
    {
        // 死亡状态直接返回，不执行任何逻辑
        if (m_isDead) return;

        // 更新计时器
        UpdateTimers();

        // 基础地面检测（复用原逻辑，增加空值保护）
        GroundCheck();

        // AI状态机核心逻辑更新
        UpdateAIStateMachine();

        // 动画状态更新（闲置/移动）
        UpdateAnimations();
    }

    #region 核心初始化方法
    // 初始化刚体设置（法师脆皮，质量低）
    private void InitRigidbodySettings()
    {
        if (m_body2d == null) return;

        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        m_body2d.mass = 50f; // 远低于野蛮人，易被击退
        m_body2d.drag = 8f;
        m_body2d.angularDrag = 8f;
        m_body2d.freezeRotation = true; // 冻结旋转
    }
    #endregion

    #region 计时器更新
    private void UpdateTimers()
    {
        // 火焰爆发冷却计时器
        if (m_fireBurstCDTimer > 0)
        {
            m_fireBurstCDTimer -= Time.deltaTime;
        }

        // 状态计时器
        m_stateTimer += Time.deltaTime;
    }
    #endregion

    #region 地面检测（复用原逻辑，移除玩家输入，增加空值保护）
    private void GroundCheck()
    {
        if (m_groundSensor == null || m_animator == null || m_body2d == null) return;

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

        //Set AirSpeed in animator（复用原逻辑）
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }
    #endregion

    #region AI状态机核心逻辑（远程法师专属行为）
    private void UpdateAIStateMachine()
    {
        // 如果没有找到玩家，默认进入闲置状态
        if (m_playerTransform == null)
        {
            SwitchState(AIState.Idle);
            return;
        }

        // 计算与玩家的距离
        float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
        bool isPlayerInDetectRange = distanceToPlayer <= m_detectRange;
        bool isPlayerInAttackRange = distanceToPlayer <= m_attackRange;
        bool isPlayerTooClose = distanceToPlayer < m_attackRange - 1.0f; // 玩家过近
        bool isPlayerInSafeRange = distanceToPlayer <= m_keepDistanceRange && distanceToPlayer >= m_attackRange;

        // 根据当前状态执行对应逻辑
        switch (m_currentState)
        {
            case AIState.Idle:
                HandleIdleState(isPlayerInDetectRange);
                break;

            case AIState.Patrol:
                HandlePatrolState(isPlayerInDetectRange);
                break;

            case AIState.Chase:
                HandleChaseState(isPlayerInAttackRange, isPlayerInSafeRange, distanceToPlayer);
                break;

            case AIState.KeepDistance:
                HandleKeepDistanceState(isPlayerInAttackRange, isPlayerTooClose, distanceToPlayer);
                break;

            case AIState.Attack:
                HandleAttackState(isPlayerInAttackRange, isPlayerTooClose, distanceToPlayer);
                break;

            case AIState.FireBurst:
                HandleFireBurstState(isPlayerInAttackRange, distanceToPlayer);
                break;
        }
    }

    // 处理闲置状态
    private void HandleIdleState(bool isPlayerInDetectRange)
    {
        // 停止移动（增加空值保护）
        if (m_body2d != null)
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);

        // 检测到玩家则切换到追击状态
        if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            return;
        }

        // 闲置超时则切换到巡逻状态
        if (m_stateTimer >= m_idleTime)
        {
            SwitchState(AIState.Patrol);
        }
    }

    // 处理巡逻状态
    private void HandlePatrolState(bool isPlayerInDetectRange)
    {
        // 检测到玩家则切换到追击状态
        if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            return;
        }

        // 执行巡逻移动逻辑
        PatrolMovement();

        // 到达巡逻点则切换回闲置状态
        if (m_movingToB && Vector2.Distance(transform.position, m_patrolPointB) < 0.2f)
        {
            SwitchState(AIState.Idle);
            m_movingToB = false;
        }
        else if (!m_movingToB && Vector2.Distance(transform.position, m_patrolPointA) < 0.2f)
        {
            SwitchState(AIState.Idle);
            m_movingToB = true;
        }
    }

    // 处理追击状态（远程法师追击到安全距离）
    private void HandleChaseState(bool isPlayerInAttackRange, bool isPlayerInSafeRange, float distanceToPlayer)
    {
        // 玩家进入攻击范围 → 优先火焰爆发（冷却完成）
        if (isPlayerInAttackRange && m_fireBurstCDTimer <= 0)
        {
            SwitchState(AIState.FireBurst);
            return;
        }
        // 玩家在安全范围 → 开始施法
        else if (isPlayerInAttackRange)
        {
            SwitchState(AIState.Attack);
            return;
        }
        // 玩家在安全距离 → 保持距离
        else if (isPlayerInSafeRange)
        {
            SwitchState(AIState.KeepDistance);
            return;
        }

        // 追击超时或玩家超出检测范围则返回闲置状态
        if (m_stateTimer >= m_chaseTime || distanceToPlayer > m_detectRange)
        {
            SwitchState(AIState.Idle);
            return;
        }

        // 执行追击移动逻辑（法师追击提速10%）
        ChaseMovement();
    }

    // 处理保持距离状态（远程核心：玩家过近则后退）
    private void HandleKeepDistanceState(bool isPlayerInAttackRange, bool isPlayerTooClose, float distanceToPlayer)
    {
        // 玩家进入攻击范围 → 施法
        if (isPlayerInAttackRange && m_fireBurstCDTimer > 0)
        {
            SwitchState(AIState.Attack);
            return;
        }
        // 玩家过近 → 后退
        else if (isPlayerTooClose)
        {
            RetreatFromPlayer();
            return;
        }

        // 保持安全距离，面向玩家
        FacePlayer();
        if (m_body2d != null)
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
    }

    // 处理普通施法状态
    private void HandleAttackState(bool isPlayerInAttackRange, bool isPlayerTooClose, float distanceToPlayer)
    {
        // 停止移动，面向玩家
        FacePlayer();
        if (m_body2d != null)
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);

        // 玩家离开攻击范围/过近 → 切换状态
        if (!isPlayerInAttackRange)
        {
            m_isCasting = false;
            SwitchState(AIState.Chase);
            return;
        }
        else if (isPlayerTooClose)
        {
            m_isCasting = false;
            SwitchState(AIState.KeepDistance);
            return;
        }

        // 施法间隔冷却完成则触发火球术
        if (!m_isCasting && m_stateTimer >= m_attackInterval)
        {
            TriggerFireball();
            m_stateTimer = 0;
            m_isCasting = true;
        }
    }

    // 处理火焰爆发状态（大招）
    private void HandleFireBurstState(bool isPlayerInAttackRange, float distanceToPlayer)
    {
        // 停止移动，面向玩家
        FacePlayer();
        if (m_body2d != null)
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);

        // 玩家离开攻击范围则切换状态
        if (!isPlayerInAttackRange)
        {
            SwitchState(AIState.Chase);
            return;
        }

        // 触发火焰爆发（仅一次）
        if (!m_isCasting)
        {
            TriggerFireBurst();
            m_isCasting = true;
            m_fireBurstCDTimer = m_fireBurstCD; // 重置冷却
        }
    }

    // 切换AI状态
    private void SwitchState(AIState newState)
    {
        if (m_currentState == newState) return;

        m_currentState = newState;
        m_stateTimer = 0.0f;
        m_isCasting = false;
        Debug.Log($"[火法师] 状态切换：{m_currentState}");
    }
    #endregion

    #region 移动逻辑（远程法师专属：后退/保持距离，增加空值保护）
    // 巡逻移动
    private void PatrolMovement()
    {
        Vector2 targetPos = m_movingToB ? m_patrolPointB : m_patrolPointA;
        MoveToTarget(targetPos, m_speed);
    }

    // 追击移动
    private void ChaseMovement()
    {
        if (m_playerTransform == null) return;
        MoveToTarget(m_playerTransform.position, m_speed * 1.1f);
    }

    // 远离玩家（玩家过近时后退）
    private void RetreatFromPlayer()
    {
        if (m_playerTransform == null || !m_grounded || m_body2d == null || m_spriteRenderer == null) return;

        // 计算后退方向
        float retreatDirX = Mathf.Sign(transform.position.x - m_playerTransform.position.x);
        m_body2d.velocity = new Vector2(retreatDirX * m_speed, m_body2d.velocity.y);
        // 翻转精灵朝向
        m_spriteRenderer.flipX = retreatDirX < 0;
    }

    // 面向玩家（不移动，仅转向）
    private void FacePlayer()
    {
        if (m_playerTransform == null || m_spriteRenderer == null) return;

        float faceDirX = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = faceDirX < 0;
    }

    // 通用移动方法
    private void MoveToTarget(Vector2 targetPos, float moveSpeed)
    {
        if (!m_grounded || m_body2d == null || m_spriteRenderer == null) return;

        // 计算移动方向
        float moveDirX = Mathf.Sign(targetPos.x - transform.position.x);
        // 设置移动速度
        m_body2d.velocity = new Vector2(moveDirX * moveSpeed, m_body2d.velocity.y);
        // 翻转精灵朝向
        m_spriteRenderer.flipX = moveDirX < 0;
    }
    #endregion

    #region 攻击逻辑（火焰主题，保留原动画，适配IDamageable6）
    // 触发火球术（普通攻击）
    private void TriggerFireball()
    {
        if (m_isDead || m_playerTransform == null || m_animator == null) return;

        // 复用原Attack动画触发逻辑（火球术）
        m_animator.SetTrigger("Attack");
        Debug.Log($"[火法师] 释放火球术，伤害：{m_fireballDamage}");

        // 给玩家造成伤害（适配IDamageable6）
        if (m_playerTransform.TryGetComponent(out IDamageable6 playerDamage))
        {
            playerDamage.TakeDamage(m_fireballDamage);
        }

        // 延迟解锁施法状态（适配施法动画时长）
        StartCoroutine(UnlockCastingAfterAnimation(1.0f));
    }

    // 触发火焰爆发（大招）
    private void TriggerFireBurst()
    {
        if (m_isDead || m_playerTransform == null || m_animator == null) return;

        // 复用Attack动画（可替换为专属FireBurst动画）
        m_animator.SetTrigger("Attack");
        Debug.Log($"[火法师] 释放火焰爆发，伤害：{m_fireBurstDamage}（冷却{m_fireBurstCD}秒）");

        // 给玩家造成爆发伤害（适配IDamageable6）
        if (m_playerTransform.TryGetComponent(out IDamageable6 playerDamage))
        {
            playerDamage.TakeDamage(m_fireBurstDamage);
        }

        // 延迟解锁施法状态，切换回追击
        StartCoroutine(UnlockFireBurstAfterAnimation(1.5f));
    }

    // 延迟解锁普通施法
    private IEnumerator UnlockCastingAfterAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        m_isCasting = false;
    }

    // 延迟解锁火焰爆发，切换回追击
    private IEnumerator UnlockFireBurstAfterAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        m_isCasting = false;
        SwitchState(AIState.Chase);
    }
    #endregion

    #region 动画更新（复用原逻辑，移除玩家输入，增加空值保护）
    private void UpdateAnimations()
    {
        if (m_animator == null || m_body2d == null) return;

        //Run（复用原逻辑）
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        //Idle（复用原逻辑）
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }
    #endregion

    #region IDamageable6接口实现（受击/死亡，复用原动画逻辑，增加空值保护）
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        // 扣除血量
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[火法师] 受到伤害：{damage}，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 复用原受击动画触发逻辑（空值保护）
        m_animator?.SetTrigger("Hurt");

        // 血量为0触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
        // 法师受击后短暂加速逃跑（远程特色）
        else
        {
            StartCoroutine(HurtRetreat(0.8f));
        }
    }

    // 受击后退：0.8秒内移速翻倍，远离玩家
    private IEnumerator HurtRetreat(float retreatTime)
    {
        float originalSpeed = m_speed;
        m_speed *= 1.8f;
        RetreatFromPlayer(); // 立即后退
        yield return new WaitForSeconds(retreatTime);
        m_speed = originalSpeed;
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[火法师] 已死亡");

        // 复用原死亡动画触发逻辑（空值保护）
        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }

        // 停止移动（空值保护）
        if (m_body2d != null)
        {
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
            m_body2d.bodyType = RigidbodyType2D.Static;
        }

        // 禁用碰撞体（空值保护）
        if (m_collider2d != null)
        {
            m_collider2d.enabled = false;
        }
    }

    // 暴露是否死亡的属性
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos可视化（调试用，远程范围标注）
    private void OnDrawGizmosSelected()
    {
        // 检测范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_detectRange);

        // 施法范围（橙色，火焰主题）
        Gizmos.color = new Color(1f, 0.5f, 0f); // 橙色
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 安全距离（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_keepDistanceRange);

        // 巡逻半径（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, m_patrolRadius);

        // 巡逻点
        Gizmos.DrawSphere(m_patrolPointA, 0.2f);
        Gizmos.DrawSphere(m_patrolPointB, 0.2f);
        Gizmos.DrawLine(m_patrolPointA, m_patrolPointB);
    }
    #endregion
}