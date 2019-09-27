using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static class to hold settings for all IKfPA systems
/// </summary>
public sealed class IKfPA_Settings : MonoBehaviour
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
        Debug.Log("Set IKfPA log  setting to " + (logset == LogSetting.Log ? "log" : "no log"));
    }
}
