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
        moveX,
        moveY,
        moveZ,
        rotX,
        rotY,
        rotZ
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
    public Vector3 pos;
    public Vector3 worldPos;
    public Vector3 rot;
    public Vector3 worldRot;
    public Transform parent;

    public static TransformMinimal operator-(TransformMinimal a, TransformMinimal b)
    {
        var result = new TransformMinimal();

        result.pos = a.pos - b.pos;
        result.worldPos = a.worldPos - b.worldPos;
        result.rot = a.rot - b.rot;
        result.worldRot = a.worldRot - b.worldRot;

        return result;
    }
}

/// <summary>
/// A script to enable a joint to be controlled by the IKfPA system.
/// Has a few physics parameters as well as configurable degrees of freedom.
/// </summary>
public class IKfPA_Joint : MonoBehaviour
{
#pragma warning disable 414
    // How much degrees can I move per second?
    [SerializeField] private float _maxAngularSpeed = 150.0f;
    // Simulate the weight of this joint, by how fast it takes on and loses momentum from parents.
    [SerializeField] private float _weightPercent = 50.0f;
    [SerializeField] private bool _usesGravity = true;

    [Tooltip("If not specified, system will assume there is no freedom to move on this axis. Can also only have one freedom degree per axis")]
    [SerializeField] private List<FreedomDegree> _degreesOfFreedom = new List<FreedomDegree>();

    [Tooltip("Specify the name of the joint which will get displayed in logging purposes")]
    [SerializeField] private string _name = "";

    [Tooltip("How fast can this joint rotate?")]
    [SerializeField] private float _rotSpeed = 5.0f;
    [Tooltip("How fast does this joint return to its resting pose if its max wasnt reached?")]
    [SerializeField] private float _returnToRestSpeed = 1.0f;

    private bool _valid = false;
    public bool IsValid { get { return _valid; } }

    private TransformMinimal _startTransform = new TransformMinimal();
    private TransformMinimal _parentTransform = new TransformMinimal();
    private TransformMinimal _previousTransform = new TransformMinimal();

    private bool _checkLimits = false;
    private bool _hasMoved = false;

#pragma warning restore

    private void Awake()
    {
        // Check if this joint is valid.
        if (CheckValid() == false)
            return;

        CaptureStartTransform();
        CaptureParentTransform();
    }

    public void DoEarlyFixedUpdate()
    {
        CapturePreviousTransform();
        _hasMoved = false;
    }
    public void DoLateFixedUpdate()
    {
        if (_checkLimits)  // Dirty flag
        {
            CheckLimits();
        }

        var rot = transform.rotation.eulerAngles;
        if (_hasMoved == false)
        {
            ReturnToRest();
        }
    }

    #region Capture Transforms
    private void CaptureStartTransform()
    {
        _startTransform.parent = transform.parent;
        _startTransform.rot = transform.localRotation.eulerAngles;
        _startTransform.worldRot = transform.rotation.eulerAngles;
        _startTransform.pos = transform.localPosition;
        _startTransform.worldPos = transform.position;
    }
    private void CaptureParentTransform()
    {
        if (transform.parent)
        {
            _parentTransform.parent = transform.parent.parent;
            _parentTransform.rot = transform.parent.localRotation.eulerAngles;
            _parentTransform.worldRot = transform.parent.rotation.eulerAngles;
            _parentTransform.pos = transform.parent.localPosition;
            _parentTransform.worldPos = transform.parent.position;
        }
    }
    private void CapturePreviousTransform()
    {
        _previousTransform.parent = transform.parent;
        _previousTransform.rot = transform.localRotation.eulerAngles;
        _previousTransform.worldRot = transform.rotation.eulerAngles;
        _previousTransform.pos = transform.localPosition;
        _previousTransform.worldPos = transform.position;
    }
    #endregion

    /// <summary>
    /// Called during start, keeps a bool. Invalid joints will not move.
    /// </summary>
    /// <returns>Valid bool</returns>
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

        if (IKfPA_Settings.LogSet == IKfPA_Settings.LogSetting.Log)
        {
            Debug.Log($"Joint {(_name.Length > 0 ? _name : gameObject.name)} is valid and will be simulating.", this);
        }

        // If we reach this point and didn't quit before, this is a valid joint.
        _valid = true;
        return true;
    }

    public void RotateImmediate(Quaternion targetRot, float remainingDegrees)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, Mathf.Min(remainingDegrees * _rotSpeed, _maxAngularSpeed) * Time.deltaTime);
        _checkLimits = true;
        _hasMoved = true;
    }

    private void CheckLimits()
    {
        var rot = transform.rotation;
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
                    {
                        // Debug.Log(newEulerAngles.x);
                        float maxLim = deg.maxAmt + deg.restingAmt;
                        float minLim = deg.restingAmt - deg.maxAmt;
                        bool didChange = false;

                        if (newEulerAngles.x > maxLim)
                        {
                            newEulerAngles.x = maxLim;
                            didChange = true;
                            Debug.Log("Top reached");
                        }
                        else if (newEulerAngles.x < minLim)
                        {
                            newEulerAngles.x = minLim;
                            didChange = true;
                            Debug.Log("Bottom reached");
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
        }

        // Put euler angles back in 0 - 360
        if (newEulerAngles.x < 0.0f)
            newEulerAngles.x += 360;
        if (newEulerAngles.y < 0.0f)
            newEulerAngles.y += 360;
        if (newEulerAngles.z < 0.0f)
            newEulerAngles.z += 360;

        rot.eulerAngles = newEulerAngles;
        transform.rotation = rot;

        _checkLimits = false;
    }
    private void ReturnToRest()
    {
        var rot = transform.rotation;
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
                            newEulerAngles.x = Mathf.Min(newEulerAngles.x + Time.deltaTime * _returnToRestSpeed + deg.restingAmt, deg.restingAmt);
                        }
                        else
                        {
                            newEulerAngles.x = Mathf.Max(newEulerAngles.x - Time.deltaTime * _returnToRestSpeed + deg.restingAmt, deg.restingAmt);
                        }
                    }
                    break;
                case FreedomDegree.FreedomAxis.rotY:
                    if (Mathf.Abs(newEulerAngles.y) > (deg.minAmt + deg.restingAmt))
                    {
                        if (newEulerAngles.y < deg.restingAmt)
                        {
                            newEulerAngles.y = Mathf.Min(newEulerAngles.y + Time.deltaTime * _returnToRestSpeed + deg.restingAmt, deg.restingAmt);
                        }
                        else
                        {
                            newEulerAngles.y = Mathf.Max(newEulerAngles.y - Time.deltaTime * _returnToRestSpeed + deg.restingAmt, deg.restingAmt);
                        }
                    }

                    break;
                case FreedomDegree.FreedomAxis.rotZ:
                    if (Mathf.Abs(newEulerAngles.z) > (deg.minAmt + deg.restingAmt))
                    {
                        if (newEulerAngles.z < deg.restingAmt)
                        {
                            newEulerAngles.z = Mathf.Min(newEulerAngles.z + Time.deltaTime * _returnToRestSpeed + deg.restingAmt, deg.restingAmt);
                        }
                        else
                        {
                            newEulerAngles.z = Mathf.Max(newEulerAngles.z - Time.deltaTime * _returnToRestSpeed + deg.restingAmt, deg.restingAmt);
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
        transform.rotation = rot;
    }
}
