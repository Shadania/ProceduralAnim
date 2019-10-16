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
            // ELBOW JOINT CALCULATIONS
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

            // TWIST ANGLE
            // Find the angle between the planes defined by (Root, Mid, End) and (Root, Mid, Target)

            // Vector3 twistAxis = (midPos - rootPos).normalized;
            // Vector3 planeNormal1 = Vector3.Cross(endPos - swivPos, twistAxis); // Can't use midpos here: if midpos is colinear with end and root, this vec is nullvec
            // Vector3 planeNormal2 = Vector3.Cross(targetPos - swivPos, twistAxis); // Swiv should be out of the way most of the time
            // float twistAngle = Vector3.Angle(planeNormal1, planeNormal2);
            // float dotProd = Vector3.Dot(planeNormal1, targetPos - rootPos);
            // if (dotProd < 0)
            //     twistAngle *= -1.0f;
            // Quaternion twistQuat = Quaternion.AngleAxis(twistAngle, twistAxis);
            // // Transform twist to forward
            // Quaternion rotToTwist = Quaternion.FromToRotation(_baseBone.transform.forward, twistAxis);
            // Quaternion rotToFwd = Quaternion.FromToRotation(twistAxis, _baseBone.transform.forward);
            // Quaternion fwdTwistQuat = rotToTwist * twistQuat * rotToFwd;
            // if (Mathf.Abs(twistAngle) > _minAngleDiff)
            //     _baseBone.RotateImmediate(fwdTwistQuat, twistAngle);
            // midPos = _endBone.transform.position;
            // endPos = _endBone.EndPoint.position;

            // if (Mathf.Abs(twistAngle) > _minAngleDiff)
            // {
            //     _baseBone.RotateImmediate(twistQuat, twistAngle);
            // }

            // Debug.Log($"Base bone angle: {_baseBone.transform.rotation.eulerAngles.x}, Twist angle to go: {twistAngle}");

            // Apply this first transformation to the root bone
            Quaternion newRot = Quaternion.FromToRotation(midPos - rootPos, elbowPos - rootPos);
            float angle = Vector3.Angle(midPos - rootPos, elbowPos - rootPos);
            Vector3 newFwd = newRot * _baseBone.transform.forward;
            newRot = Quaternion.LookRotation(newFwd, Vector3.up);
            /// if (Mathf.Abs(angle) > _minAngleDiff)    
                /// _baseBone.RotateImmediate(newRot, angle);

            // Debug.Log($"Angle: {twistAngle.ToString()}, Axis: {twistAxis.ToString()}, Current angle: {_baseBone.transform.rotation.eulerAngles.ToString()}");
        }
        else
        {
            // need to fully stretch out towards the target, older one first
            Quaternion newRot = Quaternion.FromToRotation(midPos - rootPos, targetPos - rootPos);
            float angle = Vector3.Angle(midPos - rootPos, targetPos - rootPos);
            Vector3 newFwd = newRot * _baseBone.transform.forward;
            newRot = Quaternion.LookRotation(newFwd, Vector3.up);
            /// if (Mathf.Abs(angle) > _minAngleDiff)
                /// _baseBone.RotateImmediate(newRot, angle);
        }
        
        // End bone rotation calculation is the same for both cases: orient towards target
        midPos = _endBone.transform.position;
        endPos = _endBone.EndPoint.position;
        float deltaAngle = Vector3.Angle(endPos - midPos, targetPos - midPos);
        Quaternion newEndRot = Quaternion.FromToRotation(endPos - midPos, targetPos - midPos);
        // Quaternion newEndRot = Quaternion.AngleAxis(deltaAngle, Vector3.Cross(endPos - midPos, targetPos - midPos));
        Vector3 newEndFwd = newEndRot * _endBone.transform.forward;
        newEndRot = Quaternion.LookRotation(newEndFwd, Vector3.up);

        // Vector3 newEulerAngles = new Vector3(0, 0, 290);
        // newEndRot = Quaternion.Euler(newEulerAngles);

        if (Mathf.Abs(deltaAngle) > _minAngleDiff)
            /// _endBone.Rotate(_endBone.transform.rotation * newEndRot, deltaAngle);


        Debug.Log(deltaAngle);

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
