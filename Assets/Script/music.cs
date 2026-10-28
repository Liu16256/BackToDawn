using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class music : MonoBehaviour
{
    private AudioSource audioSource;

    [Header("默认音量（0-1）")]
    public float defaultVolume = 0.7f; // 可在Inspector调整

    void Awake()
    {
        // 获取音频源组件
        audioSource = GetComponent<AudioSource>();

        // 强制开启音量（覆盖所有默认/脚本的静音/音量设置）
        audioSource.mute = false; // 取消静音
        audioSource.volume = defaultVolume; // 设置默认音量
        audioSource.loop = true; // 强制循环（避免播完停止）
    }

    void Start()
    {
        // 确保游戏运行时主动播放
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }
}