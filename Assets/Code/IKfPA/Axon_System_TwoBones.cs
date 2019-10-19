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
            // Vector3 horAxis = Vector3.Cross(targetPos - midPos1D, swivPos - midPos1D);
            Vector3 horAxis = Vector3.Cross(targetPos - rootPos, swivPos - rootPos); // less chance of 3 colinear points
            // Find the second axis, which is the one to move the elbow joint across according to the swivel
            Vector3 swivAxis = Vector3.Cross(horAxis, targetPos - midPos1D);
            // Find the distance it has to go out: the height of the triangle formed with (targetPos - rootPos) as its base length,
            // and (midPos - rootPos) and (endPos - midPos) as its side lengths (Heron's Formula)
            float baseLength = distRootToTarget;
            float sideLength1 = rootBoneLength;
            float sideLength2 = (endPos - midPos).magnitude;
            float halfPerim = (baseLength + sideLength1 + sideLength2) * 0.5f;
            float triArea = Mathf.Sqrt(halfPerim * (halfPerim - baseLength) * (halfPerim - sideLength1) * (halfPerim - sideLength2));
            float triHeight = 2 * triArea / baseLength; // Triangle area formula (area = base * height / 2) transformed
            Vector3 elbowPos = midPos1D + swivAxis.normalized * triHeight;
            
            Debug.Log($"{horAxis.x}, {horAxis.y}, {horAxis.z}");

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

            /*

            // Get root bone's ideal fwd vector
            Quaternion newRot = Quaternion.FromToRotation(_baseBone.OrigRootToEnd, elbowPos - rootPos);
            Vector3 newFwd = newRot * _baseBone.OrigFwd;

            // Get root bone's twist around fwd vectors
            Quaternion baseBoneRotationTemp = _baseBone.transform.rotation;
            _baseBone.transform.rotation = Quaternion.LookRotation(newFwd);
            Vector3 tempMidPos = _baseBone.EndPoint.position;
            float fwdTwistAngle = Vector3.SignedAngle(tempMidPos - rootPos, elbowPos - rootPos, newFwd);
            _baseBone.transform.rotation = baseBoneRotationTemp;

            if (Mathf.Abs(Vector3.Angle(_baseBone.OrigRootToEnd, elbowPos - rootPos)) + Mathf.Abs(fwdTwistAngle) > _minAngleDiff)
                _baseBone.EulerLookDirection(newFwd, fwdTwistAngle);

            */

            // Calculate new fwd
            Vector3 rootToEndNorm = _baseBone.OrigRootToEnd.normalized;
            if (Axon_Utils.ApproxVec3(rootToEndNorm, _baseBone.OrigFwd, 0.001f) == false)
            {
                Vector3 rootToTargetNorm = (elbowPos - rootPos).normalized;
                Quaternion rot = Quaternion.FromToRotation(rootToEndNorm, rootToTargetNorm);
                Vector3 endToFwd = (_baseBone.OrigFwd - rootToEndNorm);
                Vector3 rotatedEndToFwd = rot * endToFwd;
                Vector3 newBaseFwd = rootToTargetNorm + rotatedEndToFwd;

                // Calculate twist around fwd
                _baseBone.transform.rotation = Quaternion.LookRotation(newBaseFwd);
                Vector3 rootToEnd = _endBone.EndPoint.position - rootPos;
                float twistAngle = Vector3.SignedAngle(rootToEnd, elbowPos - rootPos, newBaseFwd);

                _baseBone.EulerLookDirection(newBaseFwd, twistAngle);
            }
            else
            {
                _baseBone.EulerLookDirection(elbowPos - rootPos, null);
            }
            
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
        float deltaAngle = Vector3.Angle(_endBone.OrigRootToEnd, targetPos - midPos);
        Quaternion newEndRot = Quaternion.FromToRotation(_endBone.OrigRootToEnd, targetPos - midPos);
        Vector3 newEndFwd = newEndRot * _endBone.OrigFwd;

        // End bone fwd twist angle calculation
        _endBone.transform.rotation = Quaternion.LookRotation(newEndFwd);
        Vector3 tempEndPos = _endBone.EndPoint.position;
        float endFwdTwistAngle = Vector3.SignedAngle(tempEndPos - midPos, targetPos - midPos, newEndFwd);

        if (Mathf.Abs(deltaAngle) > _minAngleDiff)
            _endBone.EulerLookDirection(newEndFwd, endFwdTwistAngle);
        

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
