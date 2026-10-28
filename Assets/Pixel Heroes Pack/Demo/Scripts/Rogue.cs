using UnityEngine;
using System.Collections;

public class Rogue : MonoBehaviour, IDamageable
{
    [SerializeField] float m_speed = 4.0f;
    [SerializeField] float m_jumpForce = 7.5f;
    [SerializeField] bool m_noBlood = false;
    [SerializeField] GameObject m_projectile;
    [SerializeField] Vector3 m_projectionSpawnOffset;

    // 闪现配置
    [SerializeField] float m_dashDistance = 3.0f;
    [SerializeField] float m_dashColliderDisableTime = 0.2f;
    [SerializeField] float m_dashCooldown = 0.5f;
    [SerializeField] float m_dashGlobalCD = 10.0f;
    private float m_dashCDTimer = 0.0f;

    // 投掷物技能配置
    [SerializeField] float m_projectileGlobalCD = 10.0f;
    [SerializeField] float m_projectileDamageMultiplier = 3.0f;
    private float m_projectileCDTimer = 0.0f;

    // 左键攻击缓冲配置
    [SerializeField] float m_attackBufferTime = 0.7f;
    private float m_attackBufferTimer = 0.0f;

    // 战斗属性
    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 100;
    [SerializeField] int m_attackDamage = 20;
    [SerializeField] float m_attackRange = 2.0f;
    [SerializeField] float m_buffDuration = 5.0f;
    private int m_currentHealth;
    private bool m_isDead = false;
    private bool m_isDamageBuffed = false;
    private float m_buffTimer = 0.0f;

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Collider2D m_collider2d;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private int m_facingDirection = 1;
    private bool m_isDashing = false;

    void Start()
    {
        // 组件初始化
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_collider2d = GetComponent<Collider2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_groundSensor = transform.Find("GroundSensor")?.GetComponent<Sensor_PixelHeroes>();

        // 战斗属性初始化
        m_currentHealth = m_maxHealth;
        m_dashCDTimer = 0.0f;
        m_projectileCDTimer = 0.0f;
        m_attackBufferTimer = 0.0f;

        // 容错：防止GroundSensor为空导致报错
        if (m_groundSensor == null)
        {
            Debug.LogError("[Rogue] 未找到GroundSensor组件，请检查物体子节点！");
        }

        // 容错：检查攻击范围是否合理
        if (m_attackRange <= 0)
        {
            Debug.LogWarning("[Rogue] 攻击范围设置为0或负数，已默认设为2.0f");
            m_attackRange = 2.0f;
        }
    }

    void Update()
    {
        if (m_isDead) return;

        GroundCheck();
        MoveControl();
        SkillControl();
        UpdateBuffTimer();
        UpdateDashCD();
        UpdateProjectileCD();
        UpdateAttackBuffer();
    }

    // 闪现冷却更新
    void UpdateDashCD()
    {
        if (m_dashCDTimer > 0)
        {
            m_dashCDTimer -= Time.deltaTime;
        }
    }

    // 投掷物冷却更新
    void UpdateProjectileCD()
    {
        if (m_projectileCDTimer > 0)
        {
            m_projectileCDTimer -= Time.deltaTime;
        }
    }

    // 左键攻击缓冲更新
    void UpdateAttackBuffer()
    {
        if (m_attackBufferTimer > 0)
        {
            m_attackBufferTimer -= Time.deltaTime;
        }
    }

    // 增伤倒计时更新
    void UpdateBuffTimer()
    {
        if (m_isDamageBuffed)
        {
            m_buffTimer -= Time.deltaTime;
            if (m_buffTimer <= 0)
            {
                m_isDamageBuffed = false;
                Debug.Log("[增伤状态] 5秒增伤时间结束，下次攻击恢复基础伤害");
            }
        }
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

    void MoveControl()
    {
        float inputX = Input.GetAxis("Horizontal");

        // 朝向控制
        if (inputX > 0)
        {
            m_spriteRenderer.flipX = false;
            m_facingDirection = 1;
        }
        else if (inputX < 0)
        {
            m_spriteRenderer.flipX = true;
            m_facingDirection = -1;
        }

        // 移动控制
        m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);

        // 动画控制
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

    void SkillControl()
    {
        // 测试死亡
        if (Input.GetKeyDown(KeyCode.E))
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
            Die();
        }
        // 测试受击
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            m_animator.SetTrigger("Hurt");
            TakeDamage(10);
        }
        // 鼠标左键攻击（带缓冲）
        else if (Input.GetMouseButtonDown(0) && m_attackBufferTimer <= 0)
        {
            m_animator.SetTrigger("Attack");
            PlayerAttackByDistance();
            m_attackBufferTimer = m_attackBufferTime;
            Debug.Log($"[左键攻击] 已使用，缓冲时间{m_attackBufferTime}秒");
        }
        // 鼠标右键增伤
        else if (Input.GetMouseButtonDown(1))
        {
            m_animator.SetTrigger("Spellcast");
            ActivateDamageBuff();
        }
        // F键闪现
        else if (Input.GetKeyDown(KeyCode.F) && !m_isDashing && m_dashCDTimer <= 0)
        {
            m_animator.SetTrigger("SpecialAttack");
            StartCoroutine(SpaceDashCoroutine());
            m_dashCDTimer = m_dashGlobalCD;
            Debug.Log($"[闪现] 已使用，冷却时间{m_dashGlobalCD}秒");
        }
        // G键投掷
        else if (Input.GetKeyDown(KeyCode.G) && m_projectileCDTimer <= 0)
        {
            m_animator.SetTrigger("Throw");
            SpawnProjectileWithDamage();
            m_projectileCDTimer = m_projectileGlobalCD;
            Debug.Log($"[投掷技能] 已使用，冷却时间{m_projectileGlobalCD}秒，伤害为左键的{m_projectileDamageMultiplier}倍");
        }
        // 跳跃
        else if (Input.GetKeyDown(KeyCode.Space) && m_grounded)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor?.Disable(0.2f);
        }
    }

    // 激活增伤buff
    void ActivateDamageBuff()
    {
        if (m_isDamageBuffed)
        {
            Debug.Log("[增伤状态] 已处于增伤中，刷新持续时间");
        }
        m_isDamageBuffed = true;
        m_buffTimer = m_buffDuration;
        Debug.Log($"[增伤状态] 开启5秒增伤，下次左键攻击伤害翻倍！剩余时间：{m_buffTimer:F1}秒");
    }

    // 修复后的左键攻击逻辑（核心修改）
    void PlayerAttackByDistance()
    {
        if (m_isDead) return;

        int finalDamage = m_isDamageBuffed ? m_attackDamage * 2 : m_attackDamage;
        string buffTip = m_isDamageBuffed ? "[增伤翻倍]" : "[基础伤害]";

        // 调试日志：确认攻击触发
        Debug.Log($"[玩家攻击] 检测攻击范围：{m_attackRange}，目标伤害：{finalDamage}");

        // 改用OverlapCircleAll检测（替代FindGameObjectsWithTag，更可靠）
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, m_attackRange);
        bool hasHitEnemy = false;

        foreach (Collider2D coll in hitColliders)
        {
            // 排除自身碰撞体 + 检测Enemy标签
            if (coll != m_collider2d && coll.CompareTag("Enemy"))
            {
                // 检测是否实现IDamageable接口
                if (coll.TryGetComponent(out IDamageable damageable))
                {
                    if (damageable.IsDead)
                    {
                        Debug.LogWarning($"[玩家攻击] 目标{coll.name}已死亡，跳过");
                        continue;
                    }

                    // 造成伤害
                    damageable.TakeDamage(finalDamage);
                    hasHitEnemy = true;
                    Debug.Log($"[玩家攻击] {buffTip} 命中{coll.name}，造成 {finalDamage} 点伤害");

                    // 触发增伤后关闭buff
                    if (m_isDamageBuffed)
                    {
                        m_isDamageBuffed = false;
                        Debug.Log("[增伤状态] 增伤攻击已触发，buff关闭");
                    }
                    break; // 一次攻击只命中一个敌人
                }
                else
                {
                    Debug.LogWarning($"[玩家攻击] 目标{coll.name}是Enemy标签，但未实现IDamageable接口");
                }
            }
        }

        // 未命中任何敌人的调试日志
        if (!hasHitEnemy)
        {
            Debug.Log($"[玩家攻击] 攻击范围内未找到有效Enemy目标（检测到{hitColliders.Length}个碰撞体）");
        }
    }

    // 投掷物技能（带伤害赋值）
    void SpawnProjectileWithDamage()
    {
        if (m_projectile == null)
        {
            Debug.LogError("[投掷技能] 未配置投掷物预制体！");
            return;
        }

        int projectileDamage = Mathf.RoundToInt(m_attackDamage * m_projectileDamageMultiplier);
        Debug.Log($"[投掷技能] 生成投掷物，伤害为左键的{m_projectileDamageMultiplier}倍：{projectileDamage}点");

        Vector3 facingVector = new Vector3(m_facingDirection, 1, 1);
        Vector3 projectionSpawnPosition = transform.localPosition + Vector3.Scale(m_projectionSpawnOffset, facingVector);
        GameObject bolt = Instantiate(m_projectile, projectionSpawnPosition, gameObject.transform.localRotation);
        bolt.transform.localScale = facingVector;

        // 给投掷物赋值伤害
        Effects_Projectile projScript = bolt.GetComponent<Effects_Projectile>();
        if (projScript != null)
        {
            projScript.projectileDamage = projectileDamage;
        }
        else
        {
            Debug.LogWarning("[投掷技能] 投掷物缺少Effects_Projectile脚本，无法赋值伤害！");
        }
    }

    // 闪现协程
    private IEnumerator SpaceDashCoroutine()
    {
        m_isDashing = true;
        RigidbodyType2D originalBodyType = m_body2d.bodyType;
        bool originalColliderEnabled = m_collider2d.enabled;
        float originalGravity = m_body2d.gravityScale;

        // 闪现时的物理状态修改
        m_body2d.bodyType = RigidbodyType2D.Kinematic;
        m_collider2d.enabled = false;
        m_body2d.gravityScale = 0;
        m_body2d.velocity = Vector2.zero;

        // 计算闪现方向（45度斜向）
        Vector2 dashDirection = new Vector2(
            m_facingDirection * Mathf.Cos(Mathf.PI / 4),
            Mathf.Sin(Mathf.PI / 4)
        ).normalized;
        Vector2 targetPosition = m_body2d.position + dashDirection * m_dashDistance;
        m_body2d.MovePosition(targetPosition);

        // 恢复碰撞体
        yield return new WaitForSeconds(m_dashColliderDisableTime);
        m_collider2d.enabled = originalColliderEnabled;
        m_body2d.bodyType = originalBodyType;
        m_body2d.gravityScale = originalGravity;

        // 恢复可闪现状态
        yield return new WaitForSeconds(m_dashCooldown - m_dashColliderDisableTime);
        m_isDashing = false;
    }

    // 兼容动画事件的投掷物生成方法
    public void SpawnProjectile()
    {
        SpawnProjectileWithDamage();
    }

    // 实现IDamageable接口 - 受击逻辑
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        m_animator.SetTrigger("Hurt");

        Debug.Log($"[玩家受击] 受到{damage}点伤害，当前血量：{m_currentHealth}/{m_maxHealth}");

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

        // 死亡后停止所有移动
        m_body2d.velocity = Vector2.zero;
        m_collider2d.enabled = false;

        Debug.Log("[玩家死亡] 角色已死亡！");
    }

    // 实现IDamageable接口 - 是否死亡属性
    public bool IsDead => m_isDead;

    // 绘制攻击范围Gizmo（可视化攻击范围）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);
    }

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