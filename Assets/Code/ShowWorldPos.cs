using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowWorldPos : MonoBehaviour
{
    [SerializeField] private Vector3 _currWorldPos = new Vector3();

    private void Update()
    {
        _currWorldPos = transform.position;
    }
}
