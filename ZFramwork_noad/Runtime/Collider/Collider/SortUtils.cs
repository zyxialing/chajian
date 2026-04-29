using UnityEngine;

public static class SortUtils
{
    public static int SetSoringBody(Vector3 pos, int extraSort = 0)
    {
        return 20001 - Mathf.FloorToInt(pos.y * 100) + extraSort;
    }
}
