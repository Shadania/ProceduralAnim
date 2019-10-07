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
        rotZ = 0x20
    }
    [SerializeField] public FreedomAxis Axis;
    [SerializeField] public float minAmt;
    [SerializeField] public float maxAmt;
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
#pragma warning disable 414
#pragma warning disable 649
    [Header("General Axon Joint Settings")]
    [Tooltip("Max angular speed of this joint in degrees/sec")]
    [SerializeField] private float _maxAngularSpeed = 150.0f;
    [Tooltip("Transform at the end of this bone, used for physics calculations involving weight, gravity, etc")]
    [SerializeField] private Transform _endPoint;
    // Used by System
    public Transform EndPoint { get { return _endPoint; } }

    [Tooltip("How heavy is this object? Determines limb droop if gravity is turned on...")]
    [SerializeField] private float _weight = 1.0f;
    [Tooltip("Will this bone droop according to gravity? Uses the weight param and the endpoint of the bone")]
    [SerializeField] private bool _doesDroop = true;

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


    private bool _valid = false;
    public bool IsValid { get { return _valid; } }

    private TransformMinimal _parentTransform = new TransformMinimal();
    private TransformMinimal _actualEndPos = new TransformMinimal();
    private Vector3 _prevVelocity = new Vector3();

    private bool _checkLimits = false;
    private bool _hasMoved = false;
    private bool _didLateUpdate = false;
#pragma warning restore


    private void Awake()
    {
        // Check if this joint is valid.
        if (CheckValid() == false)
            return;
        
        _parentTransform = CaptureParentTransform();
        _actualEndPos = CaptureEndPointTransform();

        ClampParameters();
    }

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
        
        if (_checkLimits)
        {
            CheckLimits();
        }
    }
    public void DoFinalFixedUpdate()
    {
        // if this is left inside the does smooth motion if statement, there is jitter on turning off and on
        _actualEndPos = CaptureEndPointTransform();

        _parentTransform = CaptureParentTransform();
    }


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
                if (degree.minAmt > degree.maxAmt)
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

    public void Rotate(Quaternion targetRot, float remainingDegrees)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, Mathf.Min(remainingDegrees * _rotSpeed, _maxAngularSpeed) * Time.deltaTime);
        _checkLimits = true;
        _didLateUpdate = false;
    }
    // Only use this one if you're sure this is the last move function called on this joint, else resources are wasted,
    // unless you need to check twice for some reason.
    public void RotateImmediate(Quaternion targetRot, float remainingDegrees)
    {
        Rotate(targetRot, remainingDegrees);
        _hasMoved = true;
        DoLateFixedUpdate();
    }
    public void SetMoved(bool moved)
    {
        _hasMoved = moved;
    }

    #region FixedUpdate
    private void CheckLimits()
    {
        var rot = transform.localRotation;
        var newEulerAngles = rot.eulerAngles;

        if (newEulerAngles.x > 180)
            newEulerAngles.x -= 360;
        if (newEulerAngles.y > 180)
            newEulerAngles.y -= 360;
        if (newEulerAngles.z > 180)
            newEulerAngles.z -= 360;

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
                        float maxLim = deg.maxAmt + deg.restingAmt;
                        float minLim = deg.restingAmt - deg.maxAmt;
                        bool didChange = false;

                        if (newEulerAngles.x > maxLim)
                        {
                            newEulerAngles.x = maxLim;
                            didChange = true;
                        }
                        else if (newEulerAngles.x < minLim)
                        {
                            newEulerAngles.x = minLim;
                            didChange = true;
                        }

                        if (didChange)
                        {
                            if (newEulerAngles.x > 180.0f)
                                newEulerAngles.x -= 360.0f;
                            else if (newEulerAngles.x < -180.0f)
                                newEulerAngles.x += 360.0f;
                        }
                    }

                    break;
                case FreedomDegree.FreedomAxis.rotY:
                    {
                        float maxLim = deg.maxAmt + deg.restingAmt;
                        float minLim = -deg.maxAmt + deg.restingAmt;
                        bool didChange = false;

                        if (newEulerAngles.y > maxLim)
                        {
                            newEulerAngles.y = maxLim;
                            didChange = true;
                        }
                        else if (newEulerAngles.y < minLim)
                        {
                            newEulerAngles.y = minLim;
                            didChange = true;
                        }

                        if (didChange)
                        {
                            if (newEulerAngles.y > 180.0f)
                                newEulerAngles.y -= 360.0f;
                            else if (newEulerAngles.y < -180.0f)
                                newEulerAngles.y += 360.0f;
                        }
                    }

                    break;
                case FreedomDegree.FreedomAxis.rotZ:
                    {
                        float maxLim = deg.maxAmt + deg.restingAmt;
                        float minLim = -deg.maxAmt + deg.restingAmt;
                        bool didChange = false;

                        if (newEulerAngles.z > maxLim)
                        {
                            newEulerAngles.z = maxLim;
                            didChange = true;
                        }
                        else if (newEulerAngles.z < minLim)
                        {
                            newEulerAngles.z = minLim;
                            didChange = true;
                        }

                        if (didChange)
                        {
                            if (newEulerAngles.z > 180.0f)
                                newEulerAngles.z -= 360.0f;
                            else if (newEulerAngles.z < -180.0f)
                                newEulerAngles.z += 360.0f;
                        }
                    }

                    break;
            }
            usedAxes = usedAxes | (int)deg.Axis;
        }

        var newPos = transform.position;

        for (int i = 1; i < 0x20; i *= 2)
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
                    newPos.x = 0;
                    break;
                case FreedomDegree.FreedomAxis.moveY:
                    newPos.y = 0;
                    break;
                case FreedomDegree.FreedomAxis.moveZ:
                    newPos.z = 0;
                    break;
                case FreedomDegree.FreedomAxis.rotX:
                    newEulerAngles.x = 0;
                    break;
                case FreedomDegree.FreedomAxis.rotY:
                    newEulerAngles.y = 0;
                    break;
                case FreedomDegree.FreedomAxis.rotZ:
                    newEulerAngles.z = 0;
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

        _checkLimits = false;
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
                    if (Mathf.Abs(newEulerAngles.x) > (deg.minAmt + deg.restingAmt))
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
                    if (Mathf.Abs(newEulerAngles.y) > (deg.minAmt + deg.restingAmt))
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
                    if (Mathf.Abs(newEulerAngles.z) > (deg.minAmt + deg.restingAmt))
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
    #endregion
}
