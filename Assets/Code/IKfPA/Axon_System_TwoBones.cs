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
            Quaternion newRot = Quaternion.FromToRotation(_baseBone.OrigRootToEnd, targetPos - rootPos);
            Vector3 newFwd = newRot * _baseBone.OrigFwd;
            _baseBone.EulerLookDirection(newFwd, 0);
            _baseBone.SetIdealRot(false);
            float twistAngle = Axon_Utils.AngleAroundAxis(_baseBone.EndPoint.position - rootPos, targetPos - rootPos, newFwd);
            _baseBone.EulerLookDirection(newFwd, twistAngle);
        }
        
        _baseBone.SetIdealRot();

        RotateEndBone();

        _endBone.SetIdealRot();

        TryTwistBaseBoneForEndBone();

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
        Vector3 newBaseFwd = elbowPos - rootPos;
        Quaternion newRot = Quaternion.FromToRotation(_baseBone.OrigRootToEnd, newBaseFwd);
        newBaseFwd = newRot * _baseBone.OrigFwd;
        
        // Axon_Utils.DetailedLogVec(newBaseFwd);
        // Debug.Log(_baseBone.IdealTwistAngle);
        // newBaseFwd = Quaternion.Inverse(_baseBone.GetTwistQuat()) * newBaseFwd;
        // newBaseFwd = _baseBone.GetTwistQuat() * newBaseFwd;
        // Axon_Utils.DetailedLogVec(_baseBone.GetTwistQuat().eulerAngles);

        // Apply parent rotation
        if (_baseBone.transform.parent)
        {
            newBaseFwd = Quaternion.Inverse(_baseBone.transform.parent.rotation) * newBaseFwd;
        }

        Axon_Utils.DetailedLogVec(newBaseFwd);

        _baseBone.EulerLookDirection(newBaseFwd, null);

        // Calculate twist around fwd
        _baseBone.SetIdealRot(false);
        Vector3 rootToMid = _baseBone.EndPoint.position - _baseBone.transform.position;
        
        float twistAngle = Vector3.SignedAngle(rootToMid, elbowPos - rootPos, newBaseFwd);
        
        _baseBone.EulerLookDirection(newBaseFwd, twistAngle);
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
        
        newEndFwd = Quaternion.Inverse(_baseBone.transform.rotation) * newEndFwd;

        // End bone fwd twist angle calculation
        _endBone.EulerLookDirection(newEndFwd, 0);
        _endBone.SetIdealRot();
        Vector3 tempEndPos = _endBone.EndPoint.position;
        midPos = _endBone.transform.position;
        endPos = _endBone.EndPoint.position;
        float endFwdTwistAngle = Vector3.SignedAngle(tempEndPos - midPos, targetPos - midPos, newEndFwd);
        
        _endBone.EulerLookDirection(newEndFwd, endFwdTwistAngle);
    }

    private void TryTwistBaseBoneForEndBone()
    {
        // Does endbone require a twist of basebone to get to target?
        if (_baseBone.GetDegree(FreedomDegree.FreedomAxis.twist).HasValue)
        {
            _baseBone.SetIdealRot(false);
            _endBone.SetIdealRot();

            Vector3 midPos = _endBone.transform.position;
            Vector3 endPos = _endBone.EndPoint.position;
            Vector3 rootPos = _baseBone.transform.position;
            Vector3 targetPos = _target.position;

            List<FreedomDegree.FreedomAxis> degrees = _endBone.GetFreeAxes();
            if (degrees.Contains(FreedomDegree.FreedomAxis.twist))
                degrees.Remove(FreedomDegree.FreedomAxis.twist);

            float boneTwistAngle = 0.0f;

            if (degrees.Count == 1)
            {
                switch (degrees[0])
                {
                    case FreedomDegree.FreedomAxis.rotX:
                        boneTwistAngle = Axon_Utils.AngleBetweenPlanes(_baseBone.OrigFwd, _baseBone.OrigUp, _baseBone.BoneTwistAxis, targetPos - rootPos);
                        break;

                    case FreedomDegree.FreedomAxis.rotY:
                        boneTwistAngle = Axon_Utils.AngleBetweenPlanes(_baseBone.OrigRight, _baseBone.OrigFwd, _baseBone.BoneTwistAxis, targetPos - rootPos);
                        break;

                    case FreedomDegree.FreedomAxis.rotZ:
                        boneTwistAngle = Axon_Utils.AngleBetweenPlanes(_baseBone.OrigUp, _baseBone.OrigRight, _baseBone.BoneTwistAxis, targetPos - rootPos);
                        break;
                }


                if (boneTwistAngle >= 180.0f)
                {
                    boneTwistAngle -= 180.0f;
                }
                if (boneTwistAngle <= -180.0f)
                {
                    boneTwistAngle += 180.0f;
                }


                // Debug.Log(boneTwistAngle);

                _baseBone.EulerTwist(boneTwistAngle);
            }
            else
            {
                // ????
            }
        }
    }
}
