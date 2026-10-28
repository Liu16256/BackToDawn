using UnityEngine;

public class EnemyControl : MonoBehaviour
{
    // =========================
    // 1. 状态定义
    // =========================
    public enum EnemyState
    {
        Idle = 0,
        Walk = 1,
        Jump = 2,
        Attack1 = 3,
        Attack2 = 4,
        Dead = 5
    }

    [Header("State")]
    public EnemyState currentState = EnemyState.Idle;

    // =========================
    // 2. 组件引用
    // =========================
    private Animator anim;
    private Rigidbody2D rb;
    private Collider2D col;

    // =========================
    // 3. 参数配置
    // =========================
    [Header("Move")]
    public float moveSpeed = 2f;
    public float jumpForce = 6f;

    [Header("Check")]
    public Transform groundCheck;
    public float groundRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Target")]
    public Transform player;
    public float chaseRange = 5f;
    public float attackRange = 1.2f;

    [Header("FX")]
    public GameObject attack1Fx;
    public GameObject attack2Fx;

    private bool isGrounded;

    // =========================
    // 4. 生命周期
    // =========================
    void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (currentState == EnemyState.Dead)
            return;

        CheckGround();
        StateUpdate();
        SyncAnimator();
    }

    // =========================
    // 5. FSM 主逻辑
    // =========================
    void StateUpdate()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                if (PlayerInRange(chaseRange))
                    ChangeState(EnemyState.Walk);
                break;

            case EnemyState.Walk:
                MoveToPlayer();

                if (PlayerInRange(attackRange))
                    StartAttack();

                if (!PlayerInRange(chaseRange))
                    ChangeState(EnemyState.Idle);
                break;

            case EnemyState.Jump:
                if (isGrounded)
                    ChangeState(EnemyState.Idle);
                break;

            case EnemyState.Attack1:
            case EnemyState.Attack2:
                // 等动画事件回调
                break;
        }
    }

    // =========================
    // 6. 状态切换
    // =========================
    void ChangeState(EnemyState newState)
    {
        currentState = newState;
    }

    // =========================
    // 7. 行为函数
    // =========================
    void MoveToPlayer()
    {
        if (player == null) return;

        float dir = Mathf.Sign(player.position.x - transform.position.x);
        rb.velocity = new Vector2(dir * moveSpeed, rb.velocity.y);

        // 翻转
        transform.localScale = new Vector3(dir, 1, 1);
    }

    void StartAttack()
    {
        int index = Random.Range(1, 3); // attack1 / attack2
        anim.SetInteger("AttackIndex", index);

        ChangeState(index == 1 ? EnemyState.Attack1 : EnemyState.Attack2);
    }

    public void Jump()
    {
        if (!isGrounded) return;

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        ChangeState(EnemyState.Jump);
    }

    // =========================
    // 8. Animator 同步
    // =========================
    void SyncAnimator()
    {
        anim.SetInteger("State", (int)currentState);
        anim.SetFloat("YSpeed", rb.velocity.y);
    }

    // =========================
    // 9. 检测函数
    // =========================
    void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundRadius,
            groundLayer
        );
    }

    bool PlayerInRange(float range)
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) <= range;
    }

    // =========================
    // 10. 动画事件（Animation Event）
    // =========================

    // 攻击命中帧
    public void DoDamage()
    {
        Debug.Log("Enemy Attack Hit");
        // 这里开启 HitBox 或直接对玩家造成伤害
    }

    // 播放攻击特效
    public void PlayAttackFx()
    {
        GameObject fx = currentState == EnemyState.Attack1 ? attack1Fx : attack2Fx;
        if (fx != null)
            Instantiate(fx, transform.position, Quaternion.identity);
    }

    // 攻击结束帧
    public void OnAttackEnd()
    {
        ChangeState(EnemyState.Idle);
    }

    // =========================
    // 11. 死亡
    // =========================
    public void Die()
    {
        currentState = EnemyState.Dead;
        anim.SetBool("Dead", true);

        rb.velocity = Vector2.zero;
        rb.simulated = false;
        col.enabled = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
    }
#endif
}
