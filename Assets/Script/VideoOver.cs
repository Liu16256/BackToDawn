using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class VideoOver : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    void Start()
    {
        // 获取当前物体上的VideoPlayer组件
        videoPlayer = GetComponent<VideoPlayer>();
        // 注册视频播放结束的回调事件
        videoPlayer.loopPointReached += OnVideoEnd;
    }

    void Update()
    {
        // 任意按键跳过视频并直接结束游戏
        if (Input.anyKeyDown)
        {
            EndGame();
        }
    }

    // 视频播放结束后回调
    void OnVideoEnd(VideoPlayer vp)
    {
        EndGame();
    }

    // 封装结束游戏的逻辑，便于复用和维护
    private void EndGame()
    {
        // 编辑器模式下停止播放，打包后的游戏直接退出
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}