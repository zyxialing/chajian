using Pathfinding;
using Pathfinding.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothPath
{
    [Tooltip("Toggle to divide all lines in equal length segments")]
    public static bool uniformLength = true;
    [Tooltip("The length of each segment in the smoothed path. A high value yields rough paths and low value yields very smooth paths, but is slower")]
    public static float maxSegmentLength = 2F;
    [Tooltip("The number of times to subdivide (divide in half) the path segments. [0...inf] (recommended [1...10])")]
    public static int subdivisions = 2;
    [Tooltip("Determines how much smoothing to apply in each smooth iteration. 0.5 usually produces the nicest looking curves")]
    [Range(0, 1)]
    public static float strength = 0.5F;
    [Tooltip("Number of times to apply smoothing")]
    public static int iterations = 2;

    public static List<Vector3> SmoothSimple(List<Vector3> path)
    {
        if (path.Count < 2) return path;

        List<Vector3> subdivided;

        if (uniformLength)
        {
            // Clamp to a small value to avoid the path being divided into a huge number of segments
            maxSegmentLength = Mathf.Max(maxSegmentLength, 0.005f);

            float pathLength = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                pathLength += Vector3.Distance(path[i], path[i + 1]);
            }

            int estimatedNumberOfSegments = Mathf.FloorToInt(pathLength / maxSegmentLength);
            // Get a list with an initial capacity high enough so that we can add all points
            subdivided = ListPool<Vector3>.Claim(estimatedNumberOfSegments + 2);

            float distanceAlong = 0;

            // Sample points every [maxSegmentLength] world units along the path
            for (int i = 0; i < path.Count - 1; i++)
            {
                var start = path[i];
                var end = path[i + 1];

                float length = Vector3.Distance(start, end);

                while (distanceAlong < length)
                {
                    subdivided.Add(Vector3.Lerp(start, end, distanceAlong / length));
                    distanceAlong += maxSegmentLength;
                }

                distanceAlong -= length;
            }

            // Make sure we get the exact position of the last point
            subdivided.Add(path[path.Count - 1]);
        }
        else
        {
            subdivisions = Mathf.Max(subdivisions, 0);

            if (subdivisions > 10)
            {
                Debug.LogWarning("Very large number of subdivisions. Cowardly refusing to subdivide every segment into more than " + (1 << subdivisions) + " subsegments");
                subdivisions = 10;
            }

            int steps = 1 << subdivisions;
            subdivided = ListPool<Vector3>.Claim((path.Count - 1) * steps + 1);
            Polygon.Subdivide(path, subdivided, steps);
        }

        if (strength > 0)
        {
            for (int it = 0; it < iterations; it++)
            {
                Vector3 prev = subdivided[0];

                for (int i = 1; i < subdivided.Count - 1; i++)
                {
                    Vector3 tmp = subdivided[i];

                    // prev is at this point set to the value that subdivided[i-1] had before this loop started
                    // Move the point closer to the average of the adjacent points
                    subdivided[i] = Vector3.Lerp(tmp, (prev + subdivided[i + 1]) / 2F, strength);

                    prev = tmp;
                }
            }
        }

        return subdivided;
    }
}
