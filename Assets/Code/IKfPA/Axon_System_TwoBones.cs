using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Axon_System_TwoBones : Axon_System
{
    [Header("Two bone system parameters")]
    [SerializeField] private Axon_Joint _baseBone = null;
    [SerializeField] private Axon_Joint _endBone = null;
    [Tooltip("Required for the system to know which way to bend")]
    [SerializeField] private Transform _swivelTransform = null;
    
    protected override void AddBonesToList()
    {
        _bones.Add(_baseBone);
        _bones.Add(_endBone);
    }

    protected override bool CheckSystemValid()
    {
        return (_baseBone != null) && (_endBone != null) && (_swivelTransform != null);
    }

    protected override void MoveToTarget()
    {
        var targetPos = _target.position;
        var midPos = _endBone.transform.position;
        var endPos = _endBone.EndPoint.position;
        var rootPos = _baseBone.transform.position;

        float distRootToTarget = Vector3.Distance(rootPos, targetPos);
        float rootBoneLength = Vector3.Distance(rootPos, midPos);
        float totalSystemLength = rootBoneLength + Vector3.Distance(midPos, endPos);

        if (totalSystemLength > distRootToTarget)
        {
            // bendy time!
            // calculate where the elbow joint has to go
            var dist = Vector3.Lerp(rootPos, midPos, rootBoneLength / totalSystemLength); // One-dimensional

        }
        else
        {
            // need to fully stretch out towards the target, older one first
            Quaternion newRot = Quaternion.LookRotation(targetPos - rootPos);
            float angle = Vector3.Angle(midPos - rootPos, targetPos - rootPos);
            _baseBone.RotateImmediate(newRot, angle);

            midPos = _endBone.transform.position;
            endPos = _endBone.EndPoint.position;
            newRot = Quaternion.LookRotation(targetPos - midPos);
            angle = Vector3.Angle(endPos - midPos, targetPos - midPos);
            _endBone.RotateImmediate(newRot, angle);
        }
    }
}
