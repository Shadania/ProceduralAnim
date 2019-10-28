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

    private Vector3 _origRootToSwiv = new Vector3();

    // [Tooltip("Should I orient my end bone towards the target instead of trying to reach it with the tip of this end bone?")]
    // [SerializeField] protected bool _orientEndBoneToTarget = false;
    // [Tooltip("Which local axis of the end bone to orient towards the target. Doesn't do anything on single bone systems")]
    // [SerializeField] protected Vector3 _endBoneOrientation = new Vector3();

    protected override void AddBonesToList()
    {
        _bones.Add(_baseBone);
        _bones.Add(_endBone);

        _origRootToSwiv = _swivelTransform.position - _baseBone.transform.position;
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
        return RegularMoveToTarget();
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
            Vector3 elbowPos = GetElbowPos();

            RotateBaseBoneInRange(elbowPos);
        }
        else
        {
            // need to fully stretch out towards the target, older one first
            Quaternion newRot = Quaternion.FromToRotation(_baseBone.OrigRootToEnd, targetPos - rootPos);
            Vector3 newFwd = newRot * _baseBone.OrigFwd;
            _baseBone.EulerLookDirection(newFwd, 0);

            // new twist calc
            _baseBone.transform.rotation = Quaternion.LookRotation(newFwd);
            float twistAngle = Vector3.SignedAngle(_baseBone.EndPoint.position - _baseBone.transform.position, targetPos - rootPos, newFwd);
            _baseBone.EulerLookDirection(newFwd, twistAngle);
        }

        // set basebone's transform to the ideal one (get values from basebone to get clamped angles)
        _baseBone.SetIdealRot();

        RotateEndBone();

        _endBone.SetIdealRot();

        TwistBaseBone();

        return true;
    }

    private Vector3 GetElbowPos()
    {
        Vector3 targetPos = _target.position;
        Vector3 endPos = _endBone.EndPoint.position;
        Vector3 midPos = _endBone.transform.position;
        Vector3 rootPos = _baseBone.transform.position;
        Vector3 swivPos = _swivelTransform.position;

        float distRootToTarget = Vector3.Distance(rootPos, targetPos);
        float rootBoneLength = Vector3.Distance(rootPos, midPos);
        float totalSystemLength = rootBoneLength + Vector3.Distance(midPos, endPos);
        
        // One-dimensional:
        Vector3 midPos1D = Vector3.Lerp(rootPos, targetPos, rootBoneLength / totalSystemLength); 
        // Three-dimensional:
        // Find the first axis, going sideways from our endpoint
        Vector3 horAxis = Vector3.Cross(targetPos - midPos1D, swivPos - midPos1D);
        // Vector3 horAxis = Vector3.Cross(targetPos - rootPos, swivPos - rootPos); // less chance of 3 colinear points
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

        return elbowPos;
    }

    private void RotateBaseBoneInRange(Vector3 elbowPos)
    {
        Vector3 rootPos = _baseBone.transform.position;

        // Calculate new fwd
        Vector3 rootToEndNorm = _baseBone.OrigRootToEnd.normalized;
        // if (Axon_Utils.ApproxVec3(rootToEndNorm, _baseBone.OrigFwd, 0.001f) == false)
        // {
        //     // Vector3 rootToTargetNorm = (elbowPos - rootPos).normalized;
        //     // Quaternion rot = Quaternion.FromToRotation(rootToEndNorm, rootToTargetNorm);
        //     // Vector3 endToFwd = (_baseBone.OrigFwd - rootToEndNorm);
        //     // Vector3 rotatedEndToFwd = rot * endToFwd;
        //     // Vector3 newBaseFwd = rootToTargetNorm + rotatedEndToFwd;
        // 
        //     Quaternion newRot = Quaternion.FromToRotation(_baseBone.EndPoint.position - _baseBone.transform.position, elbowPos - _baseBone.transform.position);
        //     Vector3 newBaseFwd = newRot * _baseBone.OrigFwd;
        // 
        //     // Apply parent rotation
        //     if (_baseBone.transform.parent)
        //     {
        //         newBaseFwd = Quaternion.Inverse(_baseBone.transform.parent.rotation) * newBaseFwd;
        //     }
        // 
        //     // Calculate twist around fwd
        //     _baseBone.transform.rotation = Quaternion.LookRotation(newBaseFwd);
        //     Vector3 rootToMid = _baseBone.EndPoint.position - _baseBone.transform.position;
        //     float twistAngle = Vector3.SignedAngle(rootToMid, elbowPos - rootPos, newBaseFwd);
        // 
        //     _baseBone.EulerLookDirection(newBaseFwd, twistAngle);
        // }
        // else
        // {
        //     Vector3 newBaseFwd = elbowPos - rootPos;
        //     Quaternion newRot = Quaternion.FromToRotation(_baseBone.OrigRootToEnd, newBaseFwd);
        //     newBaseFwd = newRot * _baseBone.OrigFwd;
        // 
        //     // newBaseFwd = Quaternion.Inverse(Quaternion.AngleAxis(_baseBone.IdealTwistAngle, _baseBone.EndPoint.localPosition)) * newBaseFwd;
        //     
        //     // Apply parent rotation
        //     if (_baseBone.transform.parent)
        //     {
        //         newBaseFwd = Quaternion.Inverse(_baseBone.transform.parent.rotation) * newBaseFwd;
        //     }
        // 
        //     // Calculate twist around fwd
        //     _baseBone.transform.rotation = Quaternion.LookRotation(newBaseFwd);
        //     Vector3 rootToMid = _baseBone.EndPoint.position - _baseBone.transform.position;
        //     float twistAngle = Vector3.SignedAngle(rootToMid, elbowPos - rootPos, newBaseFwd);
        // 
        //     _baseBone.EulerLookDirection(newBaseFwd, twistAngle);
        // }

        Vector3 newBaseFwd = elbowPos - rootPos;
        Quaternion newRot = Quaternion.FromToRotation(_baseBone.OrigRootToEnd, newBaseFwd);
        newBaseFwd = newRot * _baseBone.OrigFwd;

        // newBaseFwd = Quaternion.Inverse(Quaternion.AngleAxis(_baseBone.IdealTwistAngle, _baseBone.EndPoint.localPosition)) * newBaseFwd;

        // Apply twist to new fwd


        // Apply parent rotation
        if (_baseBone.transform.parent)
        {
            newBaseFwd = Quaternion.Inverse(_baseBone.transform.parent.rotation) * newBaseFwd;
        }

        // Calculate twist around fwd
        _baseBone.transform.rotation = Quaternion.LookRotation(newBaseFwd);
        Vector3 rootToMid = _baseBone.EndPoint.position - _baseBone.transform.position;
        float twistAngle = Vector3.SignedAngle(rootToMid, elbowPos - rootPos, newBaseFwd);

        _baseBone.EulerLookDirection(newBaseFwd, twistAngle);
    }
    private void TwistBaseBone()
    {
        Vector3 targetPos = _target.position;

        _baseBone.transform.localRotation = Quaternion.Euler(_baseBone.IdealEulerAngles);
        // Calculate required twist of base bone if possible
        var twistDeg = _baseBone.GetDegree(FreedomDegree.FreedomAxis.twist);
        if (twistDeg.HasValue)
        {
            // calculate required twist
            Vector3 twistAxis = _baseBone.EndPoint.localPosition;
            // Vector3 midToEnd = _endBone.EndPoint.position - _endBone.transform.position;
            // Vector3 midToTarget = targetPos - _endBone.transform.position;
            Vector3 rootToEnd = _endBone.EndPoint.position - _baseBone.transform.position;
            Vector3 rootToTarget = targetPos - _baseBone.transform.position;

            float twistAngle = Axon_Utils.AngleAroundAxis(rootToEnd.normalized, rootToTarget.normalized, twistAxis.normalized);
            _baseBone.EulerTwist(twistAngle);
        }
    }

    private void RotateEndBone()
    {
        // End bone rotation calculation is the same for both cases: orient towards target
        Vector3 midPos = _endBone.transform.position;
        Vector3 endPos = _endBone.EndPoint.position;
        Vector3 targetPos = _target.position;

        float deltaAngle = Vector3.Angle(_endBone.OrigRootToEnd, targetPos - midPos);
        Quaternion newEndRot = Quaternion.FromToRotation(_endBone.OrigRootToEnd, targetPos - midPos);
        Vector3 newEndFwd = newEndRot * _endBone.OrigFwd;

        // End bone fwd twist angle calculation
        _endBone.transform.localRotation = Quaternion.LookRotation(newEndFwd);
        Vector3 tempEndPos = _endBone.EndPoint.position;
        float endFwdTwistAngle = Vector3.SignedAngle(tempEndPos - midPos, targetPos - midPos, newEndFwd);

        newEndFwd = Quaternion.Inverse(_baseBone.transform.rotation) * newEndFwd;

        if (Mathf.Abs(deltaAngle) > _minAngleDiff)
            _endBone.EulerLookDirection(newEndFwd, endFwdTwistAngle);
    }
}
