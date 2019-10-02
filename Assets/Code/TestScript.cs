using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
#pragma warning disable 414
    [SerializeField] private GameObject _boneRoot = null;
    [SerializeField] private float _lateStartDelay = 1.0f;
    [SerializeField] private bool _doThings = false;
#pragma warning restore


    private void Start()
    {
        if (_doThings == false)
            return;

        Axon_Settings.SetLogSetting(Axon_Settings.LogSetting.Log);
        StartCoroutine(WaitLateStart(_lateStartDelay));
    }

    private IEnumerator WaitLateStart(float waitSec)
    {
        yield return new WaitForSeconds(waitSec);
        LateStart();
    }

    private void LateStart()
    {
        if (_doThings == false)
            return;

        Debug.Log("Late start fired");
    }

    private void Update()
    {
        if (_doThings == false)
            return;

        Vector3 newPosition = new Vector3();
        float angle = Time.realtimeSinceStartup;
        newPosition.z = Mathf.Sin(angle);
        newPosition.y = Mathf.Cos(angle);
        _boneRoot.transform.position = newPosition;
    }
}
