using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Axon_System_TwoBones : Axon_System
{
    [Header("Two bone system parameters")]
    [SerializeField] private Axon_Joint _baseBone = null;
    [SerializeField] private Axon_Joint _endBone = null;
    [Tooltip("Required for the system to know which way to bend")]
    [SerializeField] private Transform _swivelTransform = null;
    [SerializeField] private float _minTargetRange = 0.2f;

    // [Tooltip("Should I orient my end bone towards the target instead of trying to reach it with the tip of this end bone?")]
    // [SerializeField] protected bool _orientEndBoneToTarget = false;
    // [Tooltip("Which local axis of the end bone to orient towards the target. Doesn't do anything on single bone systems")]
    // [SerializeField] protected Vector3 _endBoneOrientation = new Vector3();

    protected override void AddBonesToList()
    {
        _bones.Add(_baseBone);
        _bones.Add(_endBone);
    }

    protected override bool CheckSystemValid()
    {
        bool result = (_baseBone != null) && (_endBone != null) && (_swivelTransform != null);
        if (result == false)
            return false;
        
        if (result == false)
        {
            Debug.LogError($"System {_name} has _orientEndBoneToTarget set, but the vector to orient it is null!");
        }

        return result;
    }

    protected override bool MoveToTarget()
    {
        // if (_orientEndBoneToTarget)
        // {
        //     MoveToFaceTarget();
        // }
        // else
        // {
            return RegularMoveToTarget();
        //}
    }
    private bool RegularMoveToTarget()
    {
        Vector3 targetPos = _target.position;
        Vector3 endPos = _endBone.EndPoint.position;
        if (Vector3.Distance(targetPos, endPos) < _minTargetRange)
        {
            return false;
        }
        Vector3 midPos = _endBone.transform.position;
        Vector3 rootPos = _baseBone.transform.position;
        Vector3 swivPos = _swivelTransform.position;

        float distRootToTarget = Vector3.Distance(rootPos, targetPos);
        float rootBoneLength = Vector3.Distance(rootPos, midPos);
        float totalSystemLength = rootBoneLength + Vector3.Distance(midPos, endPos);

        if (totalSystemLength > distRootToTarget)
        {
            // BASE BONE MATH
            // calculate where the elbow joint has to go
            Vector3 midPos1D = Vector3.Lerp(rootPos, targetPos, rootBoneLength / totalSystemLength); // One-dimensional
            // Three-dimensional
            // Find the first axis, going sideways from our endpoint
            Vector3 horAxis = Vector3.Cross(targetPos - midPos1D, swivPos - midPos1D);
            // Find the second axis, which is the one to move the elbow joint across according to the swivel
            Vector3 swivAxis = Vector3.Cross(horAxis, targetPos - midPos1D);
            // Find the distance it has to go out: the height of the triangle formed with (targetPos - rootPos) as its base length,
            // and (midPos - rootPos) and (endPos - midPos) as its side lengths (Heron's Formula)
            float baseLength = (targetPos - rootPos).magnitude;
            float sideLength1 = (midPos - rootPos).magnitude;
            float sideLength2 = (endPos - midPos).magnitude;
            float halfPerim = (baseLength + sideLength1 + sideLength2) * 0.5f;
            float triArea = Mathf.Sqrt(halfPerim * (halfPerim - baseLength) * (halfPerim - sideLength1) * (halfPerim - sideLength2));
            float triHeight = 2 * triArea / baseLength; // Triangle area formula (area = base * height / 2) transformed
            Vector3 elbowPos = midPos1D + swivAxis.normalized * triHeight;

            // Apply this first transformation to the root bone
            Quaternion newRot = Quaternion.FromToRotation(midPos - rootPos, elbowPos - rootPos);
            float angle = Vector3.Angle(midPos - rootPos, elbowPos - rootPos);
            Vector3 newFwd = newRot * _baseBone.transform.forward;
            newRot = Quaternion.LookRotation(newFwd, Vector3.up);
            // float angle = Vector3.Angle(newFwd, _baseBone.transform.forward);
            if (Mathf.Abs(angle) > _minAngleDiff)
                _baseBone.RotateImmediate(newRot, angle);
        }
        else
        {
            // need to fully stretch out towards the target, older one first
            Quaternion newRot = Quaternion.FromToRotation(midPos - rootPos, targetPos - rootPos);
            // Quaternion.AxisAngle();
            Vector3 newFwd = newRot * _baseBone.transform.forward;
            newRot = Quaternion.LookRotation(newFwd, Vector3.up);
            float angle = Vector3.Angle(newFwd, _baseBone.transform.forward);
            if (Mathf.Abs(angle) > _minAngleDiff)
                _baseBone.RotateImmediate(newRot, angle);
        }
        
        // End bone rotation calculation is the same for both cases: orient towards target
        midPos = _endBone.transform.position;
        endPos = _endBone.EndPoint.position;
        Quaternion newEndRot = Quaternion.FromToRotation(endPos - midPos, targetPos - midPos);
        Vector3 newEndFwd = newEndRot * _endBone.transform.forward;
        newEndRot = Quaternion.LookRotation(newEndFwd, Vector3.up);
        float newEndAngle = Vector3.Angle(newEndFwd, _endBone.transform.forward);
        if (Mathf.Abs(newEndAngle) > _minAngleDiff)
            _endBone.Rotate(newEndRot, newEndAngle);

        return true;
    }

    /*
    private void MoveToFaceTarget()
    {
        // Orient base bone to target
        var targetPos = _target.position;
        var midPos = _endBone.transform.position;
        var endPos = _endBone.EndPoint.position;
        var rootPos = _baseBone.transform.position;
        var swivPos = _swivelTransform.position;

        Quaternion newRot = Quaternion.LookRotation(targetPos - rootPos);
        float angle = Vector3.Angle(midPos - rootPos, targetPos - rootPos);
        _baseBone.RotateImmediate(newRot, angle);

        // Make end point face target
        midPos = _endBone.transform.position;
        endPos = _endBone.EndPoint.position;
        Vector3 worldRotation = _endBoneOrientation;
        worldRotation = Quaternion.FromToRotation(_endBone.transform.forward, _endBoneOrientation) * worldRotation;
        Vector3 targetDir = targetPos - midPos;
        newRot = Quaternion.FromToRotation(worldRotation.normalized, targetDir.normalized);
        angle = Vector3.Angle(worldRotation, targetDir);
        if (angle > 1.0f)
            _endBone.RotateImmediate(newRot, angle);
    }
    */
}
