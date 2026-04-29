using Pathfinding;
using Pathfinding.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class PathFindMgr : MonoBehaviour
{
    public static PathFindMgr _instance;
    public float maxWidth = 0;
    public float maxHeight = 0;
    public float minX;
    public float minY;
    public float maxX;
    public float maxY;
    AstarPath astarPath;

    public static void Init()
    {
        GameObject obj = new GameObject("PathFindMgr");
        _instance = obj.AddComponent<PathFindMgr>();
        _instance.InitMap();
    }

    private void InitMap()
    {
        var textAsset = TextAssetUtils.GetTextAsset("Assets/Game/AssetDynamic/Config/Map/map1");
        astarPath = gameObject.AddComponent<AstarPath>();
        astarPath.data.DeserializeGraphs(textAsset.bytes);
        astarPath.logPathResults = PathLog.None;
        astarPath.Scan();
        astarPath.showNavGraphs = false;
        maxWidth = astarPath.data.gridGraph.nodeSize * astarPath.data.gridGraph.Width;
        maxHeight = astarPath.data.gridGraph.nodeSize * astarPath.data.gridGraph.Depth;
        minX = -(maxWidth * 0.5f);
        minY = -(maxHeight * 0.5f);
        maxX = -minX;
        maxY = -minY;
        minX = minX + astarPath.data.gridGraph.center.x;
        maxX = maxX + astarPath.data.gridGraph.center.x;
        minY = minY + astarPath.data.gridGraph.center.y;
        maxY = maxY + astarPath.data.gridGraph.center.y;
        QuadTreeMgr.Init(2, 3, new Rect(minX, minY, maxWidth, maxHeight));
    }

    public GraphNode GetCloserPoint(Vector3 pos)
    {
        return astarPath.GetNearest(pos).node;
    }
    
    public Vector3 GetCloserPos(Vector3 pos)
    {
        return (Vector3)astarPath.GetNearest(pos).node.position ;
    }

    public Stack<Vector3> GetPatrolPath(Vector3 targetPos)
    {
        List<GraphNode> graphNodes = new List<GraphNode>();
        Vector3[] randomPos = new Vector3[9];
        randomPos[0] = targetPos;
        randomPos[1] = targetPos + Vector3.up * Random.Range(1f, 20f);
        randomPos[2] = targetPos + Vector3.down * Random.Range(1f, 20f);
        randomPos[3] = targetPos + Vector3.left * Random.Range(1f, 20f);
        randomPos[4] = targetPos + Vector3.right * Random.Range(1f, 20f);
        randomPos[5] = targetPos + new Vector3(-1,1) * Random.Range(1f, 20f);
        randomPos[6] = targetPos + new Vector3(1,1) * Random.Range(1f, 20f);
        randomPos[7] = targetPos + new Vector3(-1,-1) * Random.Range(1f, 20f);
        randomPos[8] = targetPos + new Vector3(1,-1) * Random.Range(1f, 20f);

        for (int i = 0; i < randomPos.Length; i++)
        {
            graphNodes.Add(GetCloserPoint(randomPos[i]));

        }
        List<Vector3> path = PathUtilities.GetPointsOnNodes(graphNodes,Random.Range(2,randomPos.Length), 10f);
        Stack<Vector3> stackPath = new Stack<Vector3>();
        for (int i = 0; i < path.Count; i++)
        {
            stackPath.Push(path[i]);
        }

        return stackPath;
    }

}
