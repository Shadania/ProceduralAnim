using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static class to hold settings for all Axon systems
/// </summary>
public sealed class Axon_Settings : MonoBehaviour
{
    /// <summary>
    /// Should systems log debug information?
    /// </summary>
    public enum LogSetting
    {
        NoLog,
        Log
    }

    public static LogSetting LogSet = LogSetting.NoLog;

    public static void SetLogSetting(LogSetting logset)
    {
        if (logset == LogSet)
        {
            return;
        }
        LogSet = logset;
        Debug.Log("Set Axon log  setting to " + (logset == LogSetting.Log ? "log" : "no log"));
    }

    public static Vector3 Gravity = new Vector3(0, -9.81f, 0);

    public static void SetGravity(Vector3 newGrav)
    {
        Gravity = newGrav;
        Debug.Log($"Set gravity to {newGrav.ToString()}");
    }
}
