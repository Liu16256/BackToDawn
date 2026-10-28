using UnityEngine;
using System.Collections;
using DamageInterface1;

public class Enemy_Warlock : MonoBehaviour, IDamageable8
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 3.0f;
    [SerializeField] float m_jumpForce = 6.5f;
    [SerializeField] bool m_noBlood = false;

    [Header("AI距离判定参数")]
    [SerializeField] float m_detectRange = 8.0f;    // 侦查距离
    [SerializeField] float m_attackRange = 5.0f;    // 攻击距离
    [SerializeField] float m_keepDistanceRange = 6.0f; // 保持距离
    [SerializeField] float m_idleTime = 1.5f;
    [SerializeField] float m_chaseTime = 20.0f;
    [SerializeField] float m_curseInterval = 2.0f;
    [SerializeField] float m_darkBlastCD = 10.0f;
    [SerializeField] float m_patrolRadius = 4.0f;

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 300;
    [SerializeField] int m_curseDamage = 25;
    [SerializeField] int m_darkBlastDamage = 80;
    [SerializeField] float m_curseDuration = 3.0f;

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Collider2D m_collider2d;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // AI状态
    private Transform m_playerTransform;
    private enum AIState { Idle, Patrol, Chase, Attack, KeepDistance, DarkBlast }
    private AIState m_currentState = AIState.Idle;
    private float m_stateTimer = 0.0f;
    private Vector2 m_patrolPointA;
    private Vector2 m_patrolPointB;
    private bool m_movingToB = true;

    // 战斗状态
    private int m_currentHealth;
    private bool m_isDead = false;
    private bool m_isCasting = false;
    private float m_darkBlastCDTimer = 0.0f;
    private Coroutine m_curseCoroutine;

    void Start()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_collider2d = GetComponent<Collider2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();

        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
        }

        InitRigidbodySettings();

        // 查找火女玩家
        Firemage player = FindObjectOfType<Firemage>();
        if (player != null)
        {
            m_playerTransform = player.transform;
            Debug.Log($"[术士] 找到玩家，侦查距离: {m_detectRange}");
        }

        m_patrolPointA = new Vector2(transform.position.x - m_patrolRadius, transform.position.y);
        m_patrolPointB = new Vector2(transform.position.x + m_patrolRadius, transform.position.y);
        m_currentHealth = m_maxHealth;
    }

    void Update()
    {
        if (m_isDead) return;

        UpdateTimers();
        HandleGroundCheck();
        UpdateAIByDistance(); // 核心：基于距离的AI状态机
        UpdateAnimations();
    }

    #region 基础逻辑
    private void InitRigidbodySettings()
    {
        if (m_body2d == null) return;
        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        m_body2d.mass = 70f;
        m_body2d.drag = 10f;
        m_body2d.angularDrag = 10f;
        m_body2d.freezeRotation = true;
    }

    private void UpdateTimers()
    {
        if (m_darkBlastCDTimer > 0)
            m_darkBlastCDTimer -= Time.deltaTime;
        m_stateTimer += Time.deltaTime;
    }

    private void HandleGroundCheck()
    {
        if (m_groundSensor == null || m_animator == null || m_body2d == null) return;

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

        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    private void UpdateAnimations()
    {
        if (m_animator == null || m_body2d == null) return;

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
    #endregion

    #region 核心修改：基于距离的AI状态机
    private void UpdateAIByDistance()
    {
        if (m_playerTransform == null || m_playerTransform.GetComponent<Firemage>().IsDead)
        {
            SwitchState(AIState.Idle);
            return;
        }

        // 核心：计算与玩家的距离
        float distance = Vector2.Distance(transform.position, m_playerTransform.position);
        Debug.DrawLine(transform.position, m_playerTransform.position, Color.blue);

        // 距离判定逻辑
        bool inDetectRange = distance <= m_detectRange;      // 进入侦查范围
        bool inAttackRange = distance <= m_attackRange;      // 进入攻击范围
        bool inKeepRange = distance <= m_keepDistanceRange;  // 进入保持距离范围

        Debug.Log($"[术士] 距离玩家: {distance:F1} | 侦查范围: {m_detectRange} | 攻击范围: {m_attackRange}");

        // 根据距离切换状态
        switch (m_currentState)
        {
            case AIState.Idle:
                if (inDetectRange) SwitchState(AIState.Chase);
                else if (m_stateTimer >= m_idleTime) SwitchState(AIState.Patrol);
                break;

            case AIState.Patrol:
                if (inDetectRange) SwitchState(AIState.Chase);
                else PatrolMovement();
                break;

            case AIState.Chase:
                if (!inDetectRange) SwitchState(AIState.Idle);
                else if (inAttackRange && m_darkBlastCDTimer <= 0) SwitchState(AIState.DarkBlast);
                else if (inAttackRange) SwitchState(AIState.Attack);
                else if (inKeepRange) SwitchState(AIState.KeepDistance);
                else ChaseMovement();
                break;

            case AIState.KeepDistance:
                if (!inKeepRange) SwitchState(AIState.Chase);
                else if (inAttackRange) SwitchState(AIState.Attack);
                else KeepDistanceMovement();
                break;

            case AIState.Attack:
                if (!inAttackRange) SwitchState(AIState.Chase);
                else if (!m_isCasting && m_stateTimer >= m_curseInterval)
                {
                    TriggerCurse();
                    m_stateTimer = 0;
                }
                FacePlayer();
                break;

            case AIState.DarkBlast:
                if (!inAttackRange) SwitchState(AIState.Chase);
                else if (!m_isCasting)
                {
                    TriggerDarkBlast();
                    m_darkBlastCDTimer = m_darkBlastCD;
                }
                FacePlayer();
                break;
        }
    }

    // 巡逻移动
    private void PatrolMovement()
    {
        Vector2 target = m_movingToB ? m_patrolPointB : m_patrolPointA;
        MoveToTarget(target);

        if (Vector2.Distance(transform.position, target) < 0.2f)
        {
            m_movingToB = !m_movingToB;
            SwitchState(AIState.Idle);
        }
    }

    // 追击移动
    private void ChaseMovement()
    {
        MoveToTarget(m_playerTransform.position);
        FacePlayer();
    }

    // 保持距离移动
    private void KeepDistanceMovement()
    {
        Vector2 awayDir = (transform.position - m_playerTransform.position).normalized;
        Vector2 target = transform.position + (Vector3)awayDir * 1f;
        MoveToTarget(target);
        FacePlayer();
    }

    // 通用移动方法
    private void MoveToTarget(Vector2 target)
    {
        if (!m_grounded || m_body2d == null) return;
        float dirX = Mathf.Sign(target.x - transform.position.x);
        m_body2d.velocity = new Vector2(dirX * m_speed, m_body2d.velocity.y);
        m_spriteRenderer.flipX = dirX < 0;
    }

    // 面向玩家
    private void FacePlayer()
    {
        if (m_spriteRenderer == null) return;
        float dirX = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = dirX < 0;
    }

    private void SwitchState(AIState newState)
    {
        if (m_currentState == newState) return;
        Debug.Log($"[术士] 状态切换: {m_currentState} → {newState}");
        m_currentState = newState;
        m_stateTimer = 0;
        m_isCasting = false;
    }
    #endregion

    #region 术士攻击技能（距离判定内释放）
    private void TriggerCurse()
    {
        m_isCasting = true;
        m_animator?.SetTrigger("Spellcast");
        Debug.Log($"[术士] 释放诅咒 | 持续伤害: {m_curseDamage}");

        m_curseCoroutine = StartCoroutine(CurseCoroutine());
    }

    private IEnumerator CurseCoroutine()
    {
        float elapsed = 0;
        while (elapsed < m_curseDuration && !m_isDead && inAttackRangeCheck())
        {
            m_playerTransform.GetComponent<Firemage>().TakeDamage(m_curseDamage);
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }
        m_isCasting = false;
    }

    private void TriggerDarkBlast()
    {
        m_isCasting = true;
        m_animator?.SetTrigger("Attack");
        Debug.Log($"[术士] 释放黑暗冲击 | 伤害: {m_darkBlastDamage}");

        m_playerTransform.GetComponent<Firemage>().TakeDamage(m_darkBlastDamage);
        StartCoroutine(EndDarkBlast());
    }

    private IEnumerator EndDarkBlast()
    {
        yield return new WaitForSeconds(2f);
        m_isCasting = false;
        SwitchState(AIState.Chase);
    }

    // 辅助：检查是否还在攻击范围内
    private bool inAttackRangeCheck()
    {
        return Vector2.Distance(transform.position, m_playerTransform.position) <= m_attackRange;
    }
    #endregion

    #region IDamageable8接口实现
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[术士] 受击！剩余血量: {m_currentHealth}/{m_maxHealth}");

        m_animator?.SetTrigger("Hurt");
        if (m_currentHealth <= 0) Die();
        else
        {
            m_darkBlastCDTimer = 0;
            StartCoroutine(HurtRetreat());
        }
    }

    private IEnumerator HurtRetreat()
    {
        float oriSpeed = m_speed;
        m_speed *= 2;
        Vector2 dir = (transform.position - m_playerTransform.position).normalized;
        m_body2d.velocity = new Vector2(dir.x * m_speed, m_body2d.velocity.y);
        yield return new WaitForSeconds(1f);
        m_speed = oriSpeed;
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[术士] 死亡！");

        //StopCoroutine(m_curseCoroutine);
        m_animator?.SetTrigger("Death");
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static;
        m_collider2d.enabled = false;
        this.enabled = false;
    }

    public bool IsDead => m_isDead;
    #endregion

    private void OnDrawGizmosSelected()
    {
        // 可视化各范围
        Gizmos.color = new Color(0.8f, 0.2f, 0.8f);  // 侦查范围（紫色）
        Gizmos.DrawWireSphere(transform.position, m_detectRange);

        Gizmos.color = new Color(0.6f, 0.1f, 0.6f);  // 攻击范围（深紫）
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        Gizmos.color = new Color(0.9f, 0.5f, 0.9f);  // 保持距离范围（浅紫）
        Gizmos.DrawWireSphere(transform.position, m_keepDistanceRange);
    }
}