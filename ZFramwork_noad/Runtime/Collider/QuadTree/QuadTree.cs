using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadTree
{
    public static int _maxDepth = 4;
    public static int _maxCountInNode = 4;
    private Dictionary<QTNode, List<QTNodeItem>> _quadTreeMap;
    private List<QTNodeItem> tempNodes;
    private List<QTNodeItem> allNodes;

    QTNode headNode;
   
    public QuadTree(Rect bounds, int maxDepth, int maxCountInNode)
    {
        _maxDepth = maxDepth;
        _maxCountInNode = maxCountInNode;
        tempNodes = new List<QTNodeItem>();
        allNodes = new List<QTNodeItem>();
        _quadTreeMap = new Dictionary<QTNode, List<QTNodeItem>>();
        headNode = new QTNode(bounds,_quadTreeMap,0);
    }


    public void Insert(QTNodeItem node)
    {
        headNode.Insert(node);
        allNodes.Add(node);
    }

    public void Remove(QTNodeItem node)
    {
        if (allNodes.Contains(node))
        {
            allNodes.Remove(node);
        }
        node.RemoveSelf();
    }

    public List<QTNodeItem> GetInsideNode(Rect bounds)
    {
        tempNodes.Clear();
        headNode.GetInsideNode(bounds,ref tempNodes);
        return tempNodes;
    }

    public List<QTNodeItem> GetAllQTNodeItems()
    {
      
        return allNodes;
    }


    public void TestDrawBounds()
    {
        headNode.TestDrawBounds();
    }
}

