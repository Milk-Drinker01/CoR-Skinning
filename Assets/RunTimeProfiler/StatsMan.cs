using UnityEngine;
using System.Collections;
using System.Text;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_5
using UnityEngine.Profiling;
#endif
//-----------------------------------------------------------------------------------------------------
public class StatsMan : MonoBehaviour
{
    public int maxFPS = 300;
    public Color tx_Color = Color.white;
    StringBuilder tx;
    public UnityEngine.UI.Text gui;

    float updateInterval = 1.0f;
    float lastInterval; // Last interval end time
    float frames = 0; // Frames over current interval

    float framesavtick = 0;
    float framesav = 0.0f;

    // Use this for initialization
    void Start()
    {
        if (maxFPS > 10)
        {
            Application.targetFrameRate = maxFPS;
        }
        lastInterval = Time.realtimeSinceStartup;
        frames = 0;
        framesav = 0;
        tx = new StringBuilder();
        tx.Capacity = 200;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        gui.color = tx_Color;
    }

    void OnDisable()
    {
        if (gui)
            DestroyImmediate(gui.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        ++frames;

        var timeNow = Time.realtimeSinceStartup;

        if (timeNow > lastInterval + updateInterval)
        {
            float fps = frames / (timeNow - lastInterval);
            float ms = 1000.0f / Mathf.Max(fps, 0.00001f);

            ++framesavtick;
            framesav += fps;
            float fpsav = framesav / framesavtick;

            tx.Length = 0;

            tx.AppendFormat("Time : {0} ms     Current FPS: {1}     AvgFPS: {2}\nGPU memory : {3}    Sys Memory : {4}\n", ms, fps, fpsav, SystemInfo.graphicsMemorySize, SystemInfo.systemMemorySize)

            .AppendFormat("TotalAllocatedMemory : {0}mb\nTotalReservedMemory : {1}mb\nTotalUnusedReservedMemory : {2}mb",
            Profiler.GetTotalAllocatedMemoryLong() / 1048576,
            Profiler.GetTotalReservedMemoryLong() / 1048576,
            Profiler.GetTotalUnusedReservedMemoryLong() / 1048576
            );

#if UNITY_EDITOR
            tx.AppendFormat("\nDrawCalls : {0}\nUsed Texture Memory : {1}\nrenderedTextureCount : {2}", UnityStats.drawCalls, UnityStats.usedTextureMemorySize / 1048576, UnityStats.usedTextureCount);
#endif

            gui.text = tx.ToString();
            frames = 0;
            lastInterval = timeNow;
        }

    }
}


