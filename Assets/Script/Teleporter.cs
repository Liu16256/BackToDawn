using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class Teleporter : MonoBehaviour
{
    [Header("传送门设置")]
    [SerializeField] private string targetSceneName;  // 目标场景名称
    [SerializeField] private string teleporterID;     // 传送门ID（用于标识）
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 1, 0);  // 生成位置偏移

    [Header("交互设置")]
    [SerializeField] private KeyCode interactKey = KeyCode.Return;  // 交互键
    [SerializeField] private float interactionRadius = 2f;  // 交互半径

    [Header("视觉效果")]
    [SerializeField] private GameObject portalEffect;  // 传送门特效
    [SerializeField] private ParticleSystem teleportParticles;  // 传送粒子
    [SerializeField] private AudioClip teleportSound;  // 传送音效

    [Header("传送动画")]
    [SerializeField] private float teleportDelay = 1f;  // 传送延迟
    [SerializeField] private GameObject teleportEffectPrefab;  // 传送特效预制体

    [Header("事件")]
    public UnityEvent OnTeleportStart;  // 传送开始事件
    public UnityEvent OnTeleportComplete;  // 传送完成事件

    private bool isPlayerInRange = false;
    private GameObject player;
    private AudioSource audioSource;

    // 静态变量用于跨场景传递数据
    public static string LastTeleporterID { get; set; }
    public static Vector3 LastTeleportPosition { get; set; }

    void Start()
    {
        // 添加音频源组件
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;  // 3D音效
        audioSource.playOnAwake = false;

        // 激活传送门特效
        if (portalEffect != null)
        {
            portalEffect.SetActive(true);
        }

        // 如果有传送粒子，播放
        if (teleportParticles != null)
        {
            teleportParticles.Play();
        }

        // 检查玩家是否从另一个传送门传送过来
        CheckSpawnFromTeleporter();
    }

    void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(interactKey))
        {
            StartTeleport();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            player = other.gameObject;

            // 显示交互提示
            ShowInteractionHint(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            player = null;

            // 隐藏交互提示
            ShowInteractionHint(false);
        }
    }

    void StartTeleport()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("传送门未设置目标场景！");
            return;
        }

        StartCoroutine(TeleportCoroutine());
    }

    IEnumerator TeleportCoroutine()
    {
        // 触发传送开始事件
        OnTeleportStart?.Invoke();

        // 播放音效
        if (teleportSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(teleportSound);
        }

        // 播放传送特效
        if (teleportEffectPrefab != null && player != null)
        {
            Instantiate(teleportEffectPrefab, player.transform.position, Quaternion.identity);
        }

        // 保存当前传送门信息
        LastTeleporterID = teleporterID;
        LastTeleportPosition = transform.position;

        // 等待传送延迟
        yield return new WaitForSeconds(teleportDelay);

        // 触发传送完成事件
        OnTeleportComplete?.Invoke();

        // 加载目标场景
        SceneManager.LoadScene(targetSceneName);
    }

    void CheckSpawnFromTeleporter()
    {
        // 检查是否有传送记录
        if (!string.IsNullOrEmpty(LastTeleporterID))
        {
            // 如果当前传送门是目标传送门
            if (LastTeleporterID == teleporterID)
            {
                // 找到玩家
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    // 设置玩家位置到传送门位置 + 偏移
                    player.transform.position = transform.position + spawnOffset;

                    // 播放出现特效
                    if (teleportEffectPrefab != null)
                    {
                        Instantiate(teleportEffectPrefab, player.transform.position, Quaternion.identity);
                    }
                }

                // 重置传送记录
                LastTeleporterID = null;
            }
        }
    }

    void ShowInteractionHint(bool show)
    {
        // 这里可以添加UI提示的显示/隐藏逻辑
        // 例如：显示"按Enter键传送"的UI

        // 临时使用Debug.Log
        if (show)
        {
            Debug.Log($"靠近传送门，按{interactKey}键传送到{targetSceneName}");
        }
    }

    // 在场景中可视化交互范围
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}