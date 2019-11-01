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
        Vector3 crossA = Vector3.Cross(a, axis).normalized;
        Vector3 crossB = Vector3.Cross(b, axis).normalized;

        if (Vector3.Angle(crossA, crossB) > 90.0f)
        {
            crossB = -crossB;
        }

        float angle = Vector3.SignedAngle(crossA, crossB, axis);
        
        return angle;
    }

    public static float AngleBetweenPlanes(Vector3 aVecA, Vector3 aVecB, Vector3 bVecA, Vector3 bVecB)
    {
        Vector3 aNorm = Vector3.Cross(aVecA, aVecB).normalized;
        Vector3 bNorm = Vector3.Cross(bVecA, bVecB).normalized;

        Vector3 axis = Vector3.Cross(aNorm, bNorm);

        Vector3 crossA = Vector3.Cross(aNorm, axis).normalized;
        Vector3 crossB = Vector3.Cross(bNorm, axis).normalized;

        float angle = 180.0f - Vector3.SignedAngle(crossA, crossB, axis);

        if (Vector3.Dot(aNorm, bVecB) > 0.0f)
        {
            angle *= -1.0f;
        }

        return angle;
    }

    public static void DetailedLogVec(Vector3 vec)
    {
        Debug.Log($"{vec.x}, {vec.y}, {vec.z}");
    }
}
