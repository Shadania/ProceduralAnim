using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utils script used in the Axon framework
/// </summary>
public class Axon_Utils : MonoBehaviour
{
    public static float DistPointToLine(Vector3 point, Vector3 lineDir, Vector3 lineOrigin)
    {
        return Vector3.Cross(lineDir.normalized, point - lineOrigin).magnitude;
    }

    public static bool ApproxVec3(Vector3 a, Vector3 b, float accuracy)
    {
        return (Mathf.Abs(a.x - b.x) < accuracy) && (Mathf.Abs(a.y - b.y) < accuracy) && (Mathf.Abs(a.z - b.z) < accuracy);
    }

    public static float AngleAroundAxis(Vector3 a, Vector3 b, Vector3 axis)
    {
        Debug.Log($"Starting vector A: {a.x.ToString()}, {a.y.ToString()}, {a.z.ToString()}\n" +
            $"Starting vector B: {b.x.ToString()}, {b.y.ToString()}, {b.z.ToString()}");

        Vector3 crossA = Vector3.Cross(a, axis);
        Vector3 crossB = Vector3.Cross(b, axis);

        if (Vector3.Angle(crossA, crossB) > 90.0f)
        {
            crossB = -crossB;
        }

        float angle = Vector3.SignedAngle(crossA, crossB, axis);

        Debug.Log($"Vector A: {crossA.x.ToString()}, {crossA.y.ToString()}, {crossA.z.ToString()}\n" +
            $"Vector B: {crossB.x.ToString()}, {crossB.y.ToString()}, {crossB.z.ToString()}");


        return angle;
    }
}
