﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for IKfPA systems. Has basic functionality as well as calling the methods used by systems.
/// </summary>
abstract public class Axon_System : MonoBehaviour
{ 
    [Header("General Axon System Parameters")]
    [SerializeField] protected Transform _target = null; // Target transform to follow around
    [SerializeField] protected bool _followsTarget = false; // Is this system currently trying to follow a target?
    [SerializeField] protected string _name = "Axon_System"; // Users can set a name for their system in editor for easy debugging
    [Tooltip("How much does the angle have to be before the system does it? Helps against jitter, is the bane of accuracy")]
    [SerializeField] protected float _minAngleDiff = 1.0f;
    
    
    private bool _valid = false;
    public bool IsValid { get { return _valid; } }

    protected List<Axon_Joint> _bones = new List<Axon_Joint>();



    virtual public void SetTarget(Transform target)
    {
        if (target != null)
        {
            _target = target;
        }

        if (Axon_Settings.LogSet == Axon_Settings.LogSetting.Log)
        {
            Debug.Log($"System {_name} has received new target {_target.ToString()}", this);
        }
    }

    private void FixedUpdate()
    {
        foreach (var bone in _bones)
        {
            bone.DoEarlyFixedUpdate();
        }

        if (_valid && _followsTarget)
        {
            if (MoveToTarget())
            {
                foreach (var bone in _bones)
                {
                    bone.SetMoved(true);
                }
            }
        }
        
        foreach (var bone in _bones)
        {
            bone.DoLateFixedUpdate();
        }

        foreach (var bone in _bones)
        {
            bone.DoFinalFixedUpdate();
        }
    }
    private void Start()
    {
        _valid = CheckSystemValid();

        if (_valid)
        {
            AddBonesToList();
        }
    }

    /// <summary>
    /// Function that should be used by systems. Gets called by System's FixedUpdate if the system is valid. Do not use FixedUpdate, use this function.
    /// </summary>
    virtual protected bool MoveToTarget() { return false; }
    /// <summary>
    /// Called in Start, because Joints check if they're valid in Awake. Do not call Start.
    /// </summary>
    /// <returns></returns>
    virtual protected bool CheckSystemValid() { return false; }
    /// <summary>
    /// Child classes should implement this to ensure correct joint updates.
    /// Add bones to the _bones variable in the order you need them to be updated: PARENT BEFORE CHILD!
    /// </summary>
    virtual protected void AddBonesToList() { }
}
