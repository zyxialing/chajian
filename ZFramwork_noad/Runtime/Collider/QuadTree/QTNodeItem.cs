using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QTNodeItem
{
    public Rect bounds;
    public List<QTNode> nodes;
    public QTNode headNode;
    public bool isDeath;
    public AICollider collider;

    public QTNodeItem(Rect bounds,AICollider aICollider)
    {
        this.bounds = bounds;
        isDeath = false;
        nodes = new List<QTNode>();
        collider = aICollider;
    }

    public void UpdateRect(Vector2 pos,Vector2 size)
    {
        bounds.position = pos;
        bounds.size = size;
    }

    public void RemoveSelf()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].RemoveItem(this);
        }
    }

    public void ClearQTNodes()
    {
        nodes.Clear();
    }

    public void UpdateQTNode(QTNode node,bool remove)
    {
        if (remove)
        {
            if (nodes.Contains(node))
            {
                nodes.Remove(node);
            }
            if (nodes.Count == 0)
            {
                isDeath = true;
            }
        }
        else
        {
            if (!nodes.Contains(node))
            {
                nodes.Add(node);
            }
            headNode = node.headNode;
            isDeath = false;
        }
    }



    public void UpdateTree()
    {
        if (!isDeath)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].bounds.Overlaps(bounds))
                {
                    if(!RectUtils.IsFullContain(nodes[i].bounds, bounds))
                    {
                        nodes[i].UpdateTreeByQTNodeItem(this);
                        if (isDeath&&headNode.IsOverlaps(bounds))
                        {

                            headNode.Insert(this);
                        }
                    }
                    else if(nodes[i].IsChildFullContain(bounds))
                    {
                        nodes[i].UpdateTreeByQTNodeItem(this);
                        if (isDeath && headNode.IsOverlaps(bounds))
                        {

                            headNode.Insert(this);
                        }
                    }
                }
            }

        }
        else
        {
            if (headNode.IsOverlaps(bounds))
            {
                
                headNode.Insert(this);
            }
        }

    }
}

