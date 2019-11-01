using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the freedom of a joint to move/rotate in a specific direction/around a specific axis.
/// If not added to the DegreesOfFreedom of a joint, it is assumed the joint has no freedom in this direction.
/// To have absolutely no freedom, set both minAmt and maxAmt to zero.
/// To have a hard limit, set minAmt to the same as maxAmt. Else you have a soft limit.
/// </summary>
[System.Serializable]
public struct FreedomDegree
{
    public enum FreedomAxis
    {
        moveX = 0x01,
        moveY = 0x02,
        moveZ = 0x04,
        rotX = 0x08,
        rotY = 0x10,
        rotZ = 0x20,
        twist = 0x40
    }
    [SerializeField] public FreedomAxis Axis;
    [SerializeField] public float lowerLim;
    [SerializeField] public float upperLim;
    [SerializeField] public float restingAmt;
}

/// <summary>
/// A struct to hold minimal transform information to use instead of the Transform class.
/// Reason: Transform object cannot easily be copied without creating new gameobjects.
/// And we only need some data of it anyway
/// </summary>
public struct TransformMinimal
{
    public Vector3 localPos;
    public Vector3 pos;
    public Vector3 localRot;
    public Vector3 rot;
    public Transform parent;

    public static TransformMinimal operator-(TransformMinimal a, TransformMinimal b)
    {
        var result = new TransformMinimal();

        result.localPos = a.localPos - b.localPos;
        result.pos = a.pos - b.pos;
        result.localRot = a.localRot - b.localRot;
        result.rot = a.rot - b.rot;

        return result;
    }
}

/// <summary>
/// A script to enable a joint to be controlled by the IKfPA system.
/// Has a few physics parameters as well as configurable degrees of freedom.
/// </summary>
public class Axon_Joint : MonoBehaviour
{
    #region Parameters and Variables
#pragma warning disable 414
#pragma warning disable 649
    [Header("General Axon Joint Settings")]
    [Tooltip("Max angular speed of this joint in degrees/sec")]
    [SerializeField] private float _maxAngularSpeed = 150.0f;
    [Tooltip("Transform at the end of this bone, used for physics and target calculations: A bone will point their end at their target")]
    [SerializeField] private Transform _endPoint;
    // Used by System
    public Transform EndPoint { get { return _endPoint; } }

    [Tooltip("How heavy is this object? Determines limb droop if gravity is turned on...")]
    [SerializeField] private float _weight = 1.0f;
    [Tooltip("Will this bone droop according to gravity? Uses the weight param and the endpoint of the bone")]
    [SerializeField] private bool _doesDroop = true;

    [Tooltip("Do you want this bone to gradually go places or IMMEDIATELY (and unrealistically) reach its target?")]
    [SerializeField] private bool _doGradualMovement = true;

    [Tooltip("Do we or do we not care about the below degrees of freedom?")]
    [SerializeField] private bool _respectsLimits = true;
    [Tooltip("If not specified, system will assume there is no freedom to move on this axis. Can also only have one freedom degree per axis")]
    [SerializeField] private List<FreedomDegree> _degreesOfFreedom = new List<FreedomDegree>();

    [Tooltip("Specify the name of the joint which will get displayed in logging")]
    [SerializeField] private string _name = "";

    [Tooltip("Angular speed multiplier")]
    [SerializeField] private float _rotSpeed = 5.0f;

    [Tooltip("Does this joint return to its resting point if it didn't move?")]
    [SerializeField] private bool _doesReturnToRest = true;
    [Tooltip("How fast does this joint return to its resting pose if its max wasnt reached?")]
    [SerializeField] private float _returnToRestSpeed = 1.0f;

    [Tooltip("Newton's law: Will this object lag a bit behind on catching up with its target position?")]
    [SerializeField] private bool _doesSmoothMotion = false;
    [Tooltip("How fast does it catch up with targetpos? (0: not at all, 1: does not lag at all)")]
    [SerializeField] private float _smoothMotionRate = 0.5f;

    [SerializeField] private float _angularAccuracy = 0.01f;

    [Header("Mainly for monitoring, is safe to change")]

    private bool _valid = false;
    public bool IsValid { get { return _valid; } }

    private TransformMinimal _parentTransform = new TransformMinimal();
    private TransformMinimal _actualEndPos = new TransformMinimal();
    private TransformMinimal _startingPos = new TransformMinimal();
    private Vector3 _prevVelocity = new Vector3();

    private Vector3 _prevEulerAngles = new Vector3();
    [SerializeField] private Vector3 _currEulerAngles = new Vector3();
    public Vector3 CurrEulerAngles { get { return _currEulerAngles; } }
    [SerializeField] private Vector3 _idealEulerAngles = new Vector3();
    public Vector3 IdealEulerAngles { get { return _idealEulerAngles; } }
    [SerializeField] private float _currTwistAngle = 0.0f;
    [SerializeField] private float _idealTwistAngle = 0.0f;
    public float IdealTwistAngle { get { return _idealTwistAngle; } }

    private Vector3 _origFwd = new Vector3();
    public Vector3 OrigFwd { get { return transform.parent ? transform.parent.rotation * _origFwd : _origFwd; } }
    public Vector3 OrigFwdPure { get { return _origFwd; } }
    private Vector3 _origUp = new Vector3();
    public Vector3 OrigUp { get { return transform.parent ? transform.parent.rotation * _origUp : _origUp; } }
    private Vector3 _origRight = new Vector3();
    public Vector3 OrigRight { get { return transform.parent ? transform.parent.rotation * _origRight : _origRight; } }

    private Vector3 _origRootToEnd = new Vector3();
    public Vector3 OrigRootToEnd { get { return transform.parent? transform.parent.rotation * _origRootToEnd : _origRootToEnd; } }
    public Vector3 BoneTwistAxis { get { return (_endPoint.position - transform.position).normalized; } }

    private bool _checkLimits = false;
    private bool _hasMoved = false;
    private bool _didLateUpdate = false;
#pragma warning restore
    #endregion


    private void Awake()
    {
        // Check if this joint is valid.
        if (CheckValid() == false)
            return;
        
        _parentTransform = CaptureParentTransform();
        _actualEndPos = CaptureEndPointTransform();

        ClampParameters();

        CaptureStartTransform();

        _currEulerAngles = _prevEulerAngles = _idealEulerAngles = transform.rotation.eulerAngles;
        _origFwd = transform.forward;
        _origRootToEnd = _endPoint.position - transform.position;
        _origUp = transform.up;
        _origRight = transform.right;
    }

    #region Updating
    public void DoEarlyFixedUpdate()
    {
        _hasMoved = false;
        _didLateUpdate = false;
    }
    public void DoLateFixedUpdate()
    {
        // Only do once per frame
        if (_didLateUpdate)
            return;
        _didLateUpdate = true;

        if (_doGradualMovement)
            UpdateRotationValues(); // THIS NEEDS TO ONLY BE DONE EXACTLY ONCE OR WE ARE GONNA HAVE ANGVEL DIFFS

        if (_hasMoved == false && _doesReturnToRest)
        {
            ReturnToRest();
        }

        if (_doesDroop)
        {
            HandleGravity();
        }

        // Smooths over the previous movements
        if (_doesSmoothMotion)
        {
            HandleSmoothMotion();
        }
        
        if (_checkLimits && _respectsLimits)
        {
            CheckLimits(ref _currEulerAngles);
            CheckTwist(ref _currTwistAngle);
            _checkLimits = false;
        }

        _actualEndPos = CaptureEndPointTransform();

        _parentTransform = CaptureParentTransform();
    }
    public void DoFinalFixedUpdate()
    {
        // apply the newest rotation
        if (_doGradualMovement)
        {
            // not needed: seems correct enough
            // Debug.Log($"Bone {_name}'s twist axis: {twistAxis.x}, {twistAxis.y}, {twistAxis.z}");
            transform.localRotation = Quaternion.AngleAxis(_currTwistAngle, BoneTwistAxis) * Quaternion.Euler(_currEulerAngles);
            // old axis: _endpoint.localPosition
            _prevEulerAngles = _currEulerAngles;
        }
    }
    #endregion Updating

    #region Capture Transforms
    private TransformMinimal CaptureParentTransform()
    {
        TransformMinimal result = new TransformMinimal();
        if (transform.parent)
        {
            result.parent = transform.parent.parent;
            result.localRot = transform.parent.localRotation.eulerAngles;
            result.rot = transform.parent.rotation.eulerAngles;
            result.localPos = transform.parent.localPosition;
            result.pos = transform.parent.position;
        }
        return result;
    }
    private TransformMinimal CaptureEndPointTransform()
    {
        TransformMinimal result = new TransformMinimal();

        if (_endPoint)
        {
            result.pos = _endPoint.position;
            result.localPos = _endPoint.localPosition;
            result.localRot = _endPoint.localRotation.eulerAngles;
            result.rot = _endPoint.rotation.eulerAngles;
            result.parent = _endPoint.parent;
        }

        return result;
    }
    private void CaptureStartTransform()
    {
        _startingPos.pos = transform.position;
        _startingPos.localPos = transform.localPosition;
        _startingPos.rot = transform.rotation.eulerAngles;
        _startingPos.localRot = transform.localRotation.eulerAngles;
        _startingPos.parent = transform.parent;
    }
    #endregion

    #region Awake Functionality
    private bool CheckValid()
    {
        // Check if degrees of freedom are valid.
        {
            List<FreedomDegree.FreedomAxis> axes = new List<FreedomDegree.FreedomAxis>();

            foreach (var degree in _degreesOfFreedom)
            {
                // Min amount should never be greater than max amount.
                if (degree.lowerLim > degree.upperLim)
                {
                    Debug.LogError($"Joint {(_name.Length > 0 ? _name : gameObject.name)} has a degree of freedom with minamt greater than maxamt", this);
                    return false;
                }

                // There should never be more than one freedom degree of one axis on a joint.
                if (axes.Contains(degree.Axis))
                {
                    Debug.LogError($"Joint {(_name.Length > 0 ? _name : gameObject.name)} has two degrees of freedom that describe the same axis", this);
                    return false;
                }

                axes.Add(degree.Axis);
            }
        }

        if (Axon_Settings.LogSet == Axon_Settings.LogSetting.Log)
        {
            Debug.Log($"Joint {(_name.Length > 0 ? _name : gameObject.name)} is valid and will be simulating.", this);
        }

        // If we reach this point and didn't quit before, this is a valid joint.
        _valid = true;
        return true;
    }
    private void ClampParameters() // Should use custom UI for this
    {
        _smoothMotionRate = Mathf.Clamp(_smoothMotionRate, 0.0f, 1.0f);
        _returnToRestSpeed = Mathf.Max(0.0f, _returnToRestSpeed);
        _rotSpeed = Mathf.Max(0.0f, _rotSpeed);
        _weight = Mathf.Max(0.0f, _weight); // TODO: Anti-gravity?
        _maxAngularSpeed = Mathf.Max(0.0f, _maxAngularSpeed);
    }
    #endregion

    #region Rotation
    [System.Obsolete("This functionality is going to be outdated soon because of bad system architecture")]
    public void Rotate(Quaternion targetRot, float remainingDegrees)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, Mathf.Min(remainingDegrees * _rotSpeed, _maxAngularSpeed) * Time.deltaTime);
        _checkLimits = true;
        _didLateUpdate = false;
    }
    [System.Obsolete("This functionality is going to be outdated soon because of bad system architecture")]
    public void RotateImmediate(Quaternion targetRot, float remainingDegrees)
    {
        Rotate(targetRot, remainingDegrees);
        _hasMoved = true;
        DoLateFixedUpdate();
    }

    public void EulerLookDirection(Vector3 dir, float? fwdTwistAngle)
    {
        // dir = Quaternion.Inverse(GetTwistQuat()) * dir.normalized;

        dir = dir.normalized;

        Vector3 rotation = new Vector3();

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        rotation = rot.eulerAngles;
        
        // Limited Rotation Fixes
        List<FreedomDegree.FreedomAxis> axes = CheckLimits(ref rotation);

        // we don't care about the twist here
        if (axes.Contains(FreedomDegree.FreedomAxis.twist))
            axes.Remove(FreedomDegree.FreedomAxis.twist);

        if (axes.Count > 0)
        {
            // Is it all axes or just one or two
            if (axes.Count == 3)
            {
                // There is nothing to be done if everything is at their limits already
                _idealEulerAngles = rotation;
                return;
            }

            
            // Check which axes are not okay, and find a way to still reach the destination without them
            foreach (var axis in axes)
            {
                switch (axis)
                {
                    case FreedomDegree.FreedomAxis.rotX:

                        if (axes.Contains(FreedomDegree.FreedomAxis.rotY))
                        {
                            if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else if (axes.Contains(FreedomDegree.FreedomAxis.rotZ))
                        {
                            if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else
                        {
                            // Check which axis is closest to the point
                            float distY = Vector3.Angle(dir, new Vector3(0, 1, 0));
                            float distZ = Vector3.Angle(dir, new Vector3(0, 0, 1));

                            if (distY < distZ)
                            {
                                if (RotateAroundY(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                            else
                            {
                                if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                        }

                        break;
                    case FreedomDegree.FreedomAxis.rotY:

                        if (axes.Contains(FreedomDegree.FreedomAxis.rotZ))
                        {
                            if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else if (axes.Contains(FreedomDegree.FreedomAxis.rotX))
                        {
                            if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else
                        {
                            // Check which axis is closest to the point
                            float distX = Vector3.Angle(dir, new Vector3(1, 0, 0));
                            float distZ = Vector3.Angle(dir, new Vector3(0, 0, 1));

                            if (distX < distZ)
                            {
                                if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                            else
                            {
                                if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                        }

                        break;
                    case FreedomDegree.FreedomAxis.rotZ:
                        if (axes.Contains(FreedomDegree.FreedomAxis.rotX))
                        {
                            if (RotateAroundY(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else if (axes.Contains(FreedomDegree.FreedomAxis.rotY))
                        {
                            if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else
                        {
                            // Check which axis is closest to the point
                            float distX = Vector3.Angle(dir, new Vector3(1, 0, 0));
                            float distY = Vector3.Angle(dir, new Vector3(0, 1, 0));

                            if (distX < distY)
                            {
                                if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                            else
                            {
                                if (RotateAroundY(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                        }
                        break;
                }
            }
        }
        
        if (fwdTwistAngle.HasValue)
        {
            rotation.z += fwdTwistAngle.Value;
        }

        CheckLimits(ref rotation);

        _idealEulerAngles = rotation;
    }
    public void EulerTwist(float degrees)
    {
        CheckTwist(ref degrees);
        _idealTwistAngle = degrees;
    }

    public void EulerLookDirWithTwist(Vector3 dir, float fwdTwistAngle)
    {
        // when is this function called?
        // -> orienting a bone towards dir: twist is incorporated into the bone it affects
        // what should it do?
        // -> set the rotation and twist to point at dir respecting limits

        
        dir = dir.normalized;
        Vector3 rotation = new Vector3();

        // unlimited rotation: need to limit before joint can use
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        rotation = rot.eulerAngles;

        // limit rotation & save limited axes
        List<FreedomDegree.FreedomAxis> axes = CheckLimits(ref rotation);

        // should calc twist here?

        // we don't care about the twist here
        if (axes.Contains(FreedomDegree.FreedomAxis.twist))
            axes.Remove(FreedomDegree.FreedomAxis.twist);

        if (axes.Count > 0)
        {
            // Is it all axes or just one or two
            if (axes.Count == 3)
            {
                // There is nothing to be done if everything is at their limits already
                _idealEulerAngles = rotation;
                return;
            }


            // Check which axes are not okay, and find a way to still reach the destination without them
            foreach (var axis in axes)
            {
                switch (axis)
                {
                    case FreedomDegree.FreedomAxis.rotX:

                        if (axes.Contains(FreedomDegree.FreedomAxis.rotY))
                        {
                            if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else if (axes.Contains(FreedomDegree.FreedomAxis.rotZ))
                        {
                            if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else
                        {
                            // Check which axis is closest to the point
                            float distY = Vector3.Angle(dir, new Vector3(0, 1, 0));
                            float distZ = Vector3.Angle(dir, new Vector3(0, 0, 1));

                            if (distY < distZ)
                            {
                                if (RotateAroundY(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                            else
                            {
                                if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                        }

                        break;
                    case FreedomDegree.FreedomAxis.rotY:

                        if (axes.Contains(FreedomDegree.FreedomAxis.rotZ))
                        {
                            if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else if (axes.Contains(FreedomDegree.FreedomAxis.rotX))
                        {
                            if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else
                        {
                            // Check which axis is closest to the point
                            float distX = Vector3.Angle(dir, new Vector3(1, 0, 0));
                            float distZ = Vector3.Angle(dir, new Vector3(0, 0, 1));

                            if (distX < distZ)
                            {
                                if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                            else
                            {
                                if (RotateAroundZ(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                        }

                        break;
                    case FreedomDegree.FreedomAxis.rotZ:
                        if (axes.Contains(FreedomDegree.FreedomAxis.rotX))
                        {
                            if (RotateAroundY(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else if (axes.Contains(FreedomDegree.FreedomAxis.rotY))
                        {
                            if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                        }
                        else
                        {
                            // Check which axis is closest to the point
                            float distX = Vector3.Angle(dir, new Vector3(1, 0, 0));
                            float distY = Vector3.Angle(dir, new Vector3(0, 1, 0));

                            if (distX < distY)
                            {
                                if (RotateAroundX(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                            else
                            {
                                if (RotateAroundY(ref rotation, dir).Count == 0) { /* Success */ }
                            }
                        }
                        break;
                }
            }
        }

        //if (fwdTwistAngle.HasValue)
        //{
        //    rotation.z = fwdTwistAngle.Value;
        //}

        _idealEulerAngles = rotation;
    }

    private List<FreedomDegree.FreedomAxis> RotateAroundX(ref Vector3 rotation, Vector3 dir)
    {
        rotation.x = Mathf.Atan2(-dir.y, dir.z) * Mathf.Rad2Deg;
        
        return CheckLimits(ref rotation);
    }
    private List<FreedomDegree.FreedomAxis> RotateAroundY(ref Vector3 rotation, Vector3 dir)
    {
        rotation.y = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        return CheckLimits(ref rotation);
    }
    private List<FreedomDegree.FreedomAxis> RotateAroundZ(ref Vector3 rotation, Vector3 dir)
    {
        rotation.z = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
        // Debug.Log($"Trying to rotate {rotation.z.ToString()} degrees on Z");
        return CheckLimits(ref rotation);
    }
    #endregion



    public void SetMoved(bool moved)
    {
        _hasMoved = moved;
    }

    #region FixedUpdate
    /// <summary>
    /// Check if the given angles fit in the limits of this joint and changes them to be so if they are not.
    /// </summary>
    /// <param name="angles"> What angles do you want to check the limits of? </param>
    /// <returns> Did the function change the value of the parameters? </returns>
    private List<FreedomDegree.FreedomAxis> CheckLimits(ref Vector3 angles)
    {
        if (_respectsLimits == false)
        {
            return new List<FreedomDegree.FreedomAxis>();
        }

        if (angles.x > 180)
            angles.x -= 360;
        if (angles.y > 180)
            angles.y -= 360;
        if (angles.z > 180)
            angles.z -= 360;

        List<FreedomDegree.FreedomAxis> res = new List<FreedomDegree.FreedomAxis>();

        int usedAxes = 0;

        foreach (var deg in _degreesOfFreedom)
        {
            switch (deg.Axis)
            {
                case FreedomDegree.FreedomAxis.moveX:
                    //TODO
                    break;
                case FreedomDegree.FreedomAxis.moveY:
                    //TODO
                    break;
                case FreedomDegree.FreedomAxis.moveZ:
                    //TODO
                    break;  
                case FreedomDegree.FreedomAxis.rotX:
                    {
                        bool didChange = false;

                        if (angles.x > deg.upperLim)
                        {
                            angles.x = deg.upperLim;
                            didChange = true;
                        }
                        else if (angles.x < deg.lowerLim)
                        {
                            angles.x = deg.lowerLim;
                            didChange = true;
                        }

                        if (didChange)
                        {
                            if (angles.x > 180.0f)
                                angles.x -= 360.0f;
                            else if (angles.x < -180.0f)
                                angles.x += 360.0f;

                            res.Add(FreedomDegree.FreedomAxis.rotX);
                        }
                    }
                    break;

                case FreedomDegree.FreedomAxis.rotY:
                    {
                        bool didChange = false;

                        if (angles.y > deg.upperLim)
                        {
                            angles.y = deg.upperLim;
                            didChange = true;
                        }
                        else if (angles.y < deg.lowerLim)
                        {
                            angles.y = deg.lowerLim;
                            didChange = true;
                        }

                        if (didChange)
                        {
                            if (angles.y > 180.0f)
                                angles.y -= 360.0f;
                            else if (angles.y < -180.0f)
                                angles.y += 360.0f;

                            res.Add(FreedomDegree.FreedomAxis.rotY);
                        }
                    }
                    break;

                case FreedomDegree.FreedomAxis.rotZ:
                    {
                        bool didChange = false;

                        if (angles.z > deg.upperLim)
                        {
                            angles.z = deg.upperLim;
                            didChange = true;
                        }
                        else if (angles.z < deg.lowerLim)
                        {
                            angles.z = deg.lowerLim;
                            didChange = true;
                        }

                        if (didChange)
                        {
                            if (angles.z > 180.0f)
                                angles.z -= 360.0f;
                            else if (angles.z < -180.0f)
                                angles.z += 360.0f;

                            res.Add(FreedomDegree.FreedomAxis.rotZ);
                        }
                    }
                    break;
            }
            usedAxes = usedAxes | (int)deg.Axis;
        }

        var newPos = transform.position;

        for (int i = 1; i < 0x40; i *= 2)
        {
            var result = (FreedomDegree.FreedomAxis)(usedAxes & i);
            // We don't want to limit what we've already gone over
            if ((usedAxes & i) > 0)
            {
                continue;
            }

            switch ((FreedomDegree.FreedomAxis)i)
            {
                case FreedomDegree.FreedomAxis.moveX:
                    newPos.x = _startingPos.localPos.x; // TODO
                    break;
                case FreedomDegree.FreedomAxis.moveY:
                    newPos.y = _startingPos.localPos.y; // TODO
                    break;
                case FreedomDegree.FreedomAxis.moveZ:
                    newPos.z = _startingPos.localPos.z; // TODO
                    break;
                case FreedomDegree.FreedomAxis.rotX:
                    angles.x = _startingPos.localRot.x;
                    res.Add(FreedomDegree.FreedomAxis.rotZ);
                    break;
                case FreedomDegree.FreedomAxis.rotY:
                    res.Add(FreedomDegree.FreedomAxis.rotY);
                    angles.y = _startingPos.localRot.y;
                    break;
                case FreedomDegree.FreedomAxis.rotZ:
                    res.Add(FreedomDegree.FreedomAxis.rotZ);
                    angles.z = _startingPos.localRot.z;
                    break;
            }
        }

        // Put euler angles back in 0 - 360
        if (angles.x < 0.0f)
            angles.x += 360;
        if (angles.y < 0.0f)
            angles.y += 360;
        if (angles.z < 0.0f)
            angles.z += 360;

        return res;
    }
    private bool CheckTwist(ref float twist)
    {
        foreach (var deg in _degreesOfFreedom)
        {
            if (deg.Axis == FreedomDegree.FreedomAxis.twist)
            {
                if (twist > 180)
                    twist -= 360.0f;

                bool didChange = false;

                if (twist > deg.upperLim)
                {
                    twist = deg.upperLim;
                    didChange = true;
                }
                else if (twist < deg.lowerLim)
                {
                    twist = deg.lowerLim;
                    didChange = true;
                }

                if (didChange)
                {
                    if (twist > 180.0f)
                        twist -= 360.0f;
                    else if (twist < -180.0f)
                        twist += 360.0f;
                }

                if (twist < 0.0f)
                    twist += 360.0f;

                return didChange;
            }
        }

        twist = 0.0f;
        return false;
    }
    private void ReturnToRest()
    {
        var rot = transform.localRotation;
        var newEulerAngles = rot.eulerAngles;

        if (newEulerAngles.x > 180)
            newEulerAngles.x -= 360;
        if (newEulerAngles.y > 180)
            newEulerAngles.y -= 360;
        if (newEulerAngles.z > 180)
            newEulerAngles.z -= 360;

        foreach (var deg in _degreesOfFreedom)
        {
            switch (deg.Axis)
            {
                case FreedomDegree.FreedomAxis.moveX:
                    //TODO
                    break;
                case FreedomDegree.FreedomAxis.moveY:
                    //TODO
                    break;
                case FreedomDegree.FreedomAxis.moveZ:
                    //TODO
                    break;
                case FreedomDegree.FreedomAxis.rotX:
                    if (Mathf.Abs(newEulerAngles.x) > (deg.lowerLim + deg.restingAmt))
                    {
                        if (newEulerAngles.x < deg.restingAmt)
                        {
                            newEulerAngles.x = Mathf.Min(newEulerAngles.x + Time.deltaTime * _returnToRestSpeed, deg.restingAmt);
                        }
                        else
                        {
                            newEulerAngles.x = Mathf.Max(newEulerAngles.x - Time.deltaTime * _returnToRestSpeed, deg.restingAmt);
                        }
                    }
                    break;
                case FreedomDegree.FreedomAxis.rotY:
                    if (Mathf.Abs(newEulerAngles.y) > (deg.lowerLim + deg.restingAmt))
                    {
                        if (newEulerAngles.y < deg.restingAmt)
                        {
                            newEulerAngles.y = Mathf.Min(newEulerAngles.y + Time.deltaTime * _returnToRestSpeed, deg.restingAmt);
                        }
                        else
                        {
                            newEulerAngles.y = Mathf.Max(newEulerAngles.y - Time.deltaTime * _returnToRestSpeed, deg.restingAmt);
                        }
                    }

                    break;
                case FreedomDegree.FreedomAxis.rotZ:
                    if (Mathf.Abs(newEulerAngles.z) > (deg.lowerLim + deg.restingAmt))
                    {
                        if (newEulerAngles.z < deg.restingAmt)
                        {
                            newEulerAngles.z = Mathf.Min(newEulerAngles.z + Time.deltaTime * _returnToRestSpeed, deg.restingAmt);
                        }
                        else
                        {
                            newEulerAngles.z = Mathf.Max(newEulerAngles.z - Time.deltaTime * _returnToRestSpeed, deg.restingAmt);
                        }
                    }

                    break;
            }
        }


        // Put euler angles back in 0 - 360
        if (newEulerAngles.x < 0.0f)
            newEulerAngles.x += 360;
        if (newEulerAngles.y < 0.0f)
            newEulerAngles.y += 360;
        if (newEulerAngles.z < 0.0f)
            newEulerAngles.z += 360;

        rot.eulerAngles = newEulerAngles;
        transform.localRotation = rot;
    }
    private void HandleGravity()
    {
        float jointLength = _endPoint.localPosition.magnitude;
        var fwd = transform.forward;
        float angleHorizontal = Vector3.Angle(fwd, new Vector3(0, fwd.y, 0));

        // NOTE: Asked this on reddit: https://www.reddit.com/r/askscience/comments/db8eed/what_is_a_good_formula_for_limb_drooping_by/
        float droopMagnitude = jointLength * _weight;
        Mathf.Lerp(droopMagnitude, 0.0f, Mathf.Max(angleHorizontal / 45.0f, 1.0f));
        var droop = droopMagnitude * Axon_Settings.Gravity * Time.deltaTime;
        var targetEnd = _endPoint.transform.position + droop * Time.deltaTime;
        transform.LookAt(targetEnd);

        _checkLimits = true;
    }
    private void HandleSmoothMotion()
    {
        Vector3 velocity = _endPoint.position - _actualEndPos.pos;

        Vector3 offset = velocity * (1.0f - _smoothMotionRate);
        
        Vector3 endTargetPos = _endPoint.position - offset;

        transform.LookAt(endTargetPos);

        _checkLimits = true;
    }
    private void UpdateRotationValues()
    {
        if (Mathf.Abs(_currEulerAngles.x) > 360.0f)
        {
            _currEulerAngles.x -= ((int)_currEulerAngles.x / 360) * 360;
        }
        if (Mathf.Abs(_currEulerAngles.y) > 360.0f)
        {
            _currEulerAngles.y -= ((int)_currEulerAngles.y / 360) * 360;
        }
        if (Mathf.Abs(_currEulerAngles.z) > 360.0f)
        {
            _currEulerAngles.z -= ((int)_currEulerAngles.z / 360) * 360;
        }

        // X ANGLE
        float diffRotX = _idealEulerAngles.x - _currEulerAngles.x;
        if (Mathf.Abs(diffRotX) > _angularAccuracy)
        {
            if (Mathf.Abs(diffRotX) > 180.0f)
            {
                if (diffRotX > 0.0f)
                    diffRotX -= 360.0f;
                else
                    diffRotX += 360.0f;
            }
            if (diffRotX > 0.0f)
            {
                _currEulerAngles.x += Mathf.Min(diffRotX, _rotSpeed);
                _checkLimits = true;
            }
            else if (diffRotX < 0.0f)
            {
                _currEulerAngles.x -= Mathf.Min(-diffRotX, _rotSpeed);
                _checkLimits = true;
            }
        }

        // Y ANGLE
        float diffRotY = _idealEulerAngles.y - _currEulerAngles.y;
        if (Mathf.Abs(diffRotY) > _angularAccuracy)
        {
            if (Mathf.Abs(diffRotY) > 180.0f)
            {
                if (diffRotY > 0.0f)
                    diffRotY -= 360.0f;
                else
                    diffRotY += 360.0f;
            }
            if (diffRotY > 0.0f)
            {
                _currEulerAngles.y += Mathf.Min(diffRotY, _rotSpeed);
                _checkLimits = true;
            }
            else if (diffRotY < 0.0f)
            {
                _currEulerAngles.y -= Mathf.Min(-diffRotY, _rotSpeed);
                _checkLimits = true;
            }
        }

        // Z ANGLE
        float diffRotZ = _idealEulerAngles.z - _currEulerAngles.z;
        if (Mathf.Abs(diffRotZ) > _angularAccuracy)
        {
            if (Mathf.Abs(diffRotZ) > 180.0f)
            {
                if (diffRotZ > 0.0f)
                    diffRotZ -= 360.0f;
                else
                    diffRotZ += 360.0f;
            }
            if (diffRotZ > 0.0f)
            {
                _currEulerAngles.z += Mathf.Min(diffRotZ, _rotSpeed);
                _checkLimits = true;
            }
            else if (diffRotZ < 0.0f)
            {
                _currEulerAngles.z -= Mathf.Min(-diffRotZ, _rotSpeed);
                _checkLimits = true;
            }
        }

        // TWIST
        var diffTwist = _idealTwistAngle - _currTwistAngle;
        if (Mathf.Abs(diffTwist) > _angularAccuracy)
        {
            if (Mathf.Abs(diffTwist) > 180.0f)
            {
                if (diffTwist > 0.0f)
                    diffTwist -= 360.0f;
                else
                    diffTwist += 360.0f;
            }
            if (diffTwist > 0.0f)
            {
                _currTwistAngle += Mathf.Min(diffTwist, _rotSpeed);
                _checkLimits = true;
            }
            else if (diffTwist < 0.0f)
            {
                _currTwistAngle -= Mathf.Min(-diffTwist, _rotSpeed);
                _checkLimits = true;
            }
        }
    }
    #endregion

    #region Utility
    public FreedomDegree? GetDegree(FreedomDegree.FreedomAxis axis)
    {
        foreach (var deg in _degreesOfFreedom)
        {
            if (deg.Axis == axis)
                return deg;
        }

        return null;
    }
    public List<FreedomDegree> GetDegrees()
    {
        return _degreesOfFreedom;
    }
    public List<FreedomDegree.FreedomAxis> GetFreeAxes()
    {
        List<FreedomDegree.FreedomAxis> result = new List<FreedomDegree.FreedomAxis>();

        foreach (var deg in _degreesOfFreedom)
        {
            result.Add(deg.Axis);
        }

        return result;
    }
    public void SetIdealRot(bool withTwist = true)
    {
        if (withTwist)
        {
            transform.localRotation = GetTwistQuat() * Quaternion.Euler(_idealEulerAngles);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(_idealEulerAngles);
        }
    }
    public Quaternion GetTwistQuat()
    {
        return Quaternion.AngleAxis(_idealTwistAngle, BoneTwistAxis);
    }
    #endregion
}
