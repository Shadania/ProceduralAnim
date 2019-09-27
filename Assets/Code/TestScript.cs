﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
#pragma warning disable 414
    [SerializeField] private IKfPA_System_SingleBone _singleBoneTestSystem = null;
    [SerializeField] private float _lateStartDelay = 5.0f;
#pragma warning restore


    private void Start()
    {
        IKfPA_Settings.SetLogSetting(IKfPA_Settings.LogSetting.Log);
        StartCoroutine(WaitLateStart(_lateStartDelay));
    }

    private IEnumerator WaitLateStart(float waitSec)
    {
        yield return new WaitForSeconds(waitSec);
        LateStart();
    }

    private void LateStart()
    {
        Debug.Log("Late start fired");
    }

}