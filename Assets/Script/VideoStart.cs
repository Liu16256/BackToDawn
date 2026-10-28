using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class VideoStart : MonoBehaviour
{
    public string nextSceneName = "Demo_1";   // 视频播放完毕进入的场景
    private VideoPlayer videoPlayer;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoEnd; // 注册回调事件
    }

    void Update()
    {
        // 任意按键跳过
        if (Input.anyKeyDown)
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // 视频播放结束后回调
    void OnVideoEnd(VideoPlayer vp)
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
