using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Axon_System_SingleBone : Axon_System
{
    [Header("Single Bone System Parameters")]
    [SerializeField] private Axon_Joint _bone = null;

    override protected void MoveToTarget()
    {
        var targetpos = _target.transform.position;
        var endpos = _bone.EndPoint.position;
        var basePos = _bone.transform.position;

        var targetVec = targetpos - basePos;
        var endVec = endpos - basePos;

        if (targetVec.sqrMagnitude > 0.1f && endVec.sqrMagnitude > 0.1f)
        {
            var rotAngle = Vector3.Angle(endVec, targetVec);
            var distToGo = (endpos - targetpos).magnitude;
            
            if (distToGo < _minTargetRange)
            {
                // how??
            }
            else if (distToGo > _maxTargetRange)
            {
                // this is fine, this will be most cases
            }
            else
            {
                // we are in the range!!! do the thing
                float aboveMin = distToGo - _minTargetRange;
                float range = _maxTargetRange - _minTargetRange;
                float percent = (aboveMin / range);
                percent = Mathf.SmoothStep(0, 1.0f, percent);
                rotAngle *= percent;
            }

            if (Mathf.Abs(rotAngle) > 1.0f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetVec.normalized, _bone.transform.up);
                _bone.Rotate(targetRot, rotAngle);
                Debug.Log($"Rotating by {rotAngle}");
            }
        }
        else
        {
            // Nope
        }
    }
    override protected bool CheckSystemValid()
    {
        if (_bone == null || _bone.EndPoint == null)
        {
            Debug.LogError($"System {_name} does not have all its bones set! It will not do anything.", this);
            return false;
        }

        if (_bone.IsValid == false)
        {
            Debug.LogError($"System {_name} has invalid bones added to it. It will not do anything.", this);
            return  false;
        }

        return true;
    }
    override protected void AddBonesToList()
    {
        _bones.Add(_bone);
    }
}
