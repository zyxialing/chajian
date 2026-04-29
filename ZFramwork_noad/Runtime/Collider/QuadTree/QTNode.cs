using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QTNode
{
    private List<QTNodeItem> _items;
    private Dictionary<QTNode, List<QTNodeItem>> _parentMap;
    private int _currDepth;
    private bool haveChild ;

    QTNode parentNode;
    public QTNode headNode;

    public Rect bounds;
    QTNode topLeftTree;
    QTNode topRightTree;
    QTNode botLeftTree;
    QTNode botRightTree;

    public QTNode(Rect bounds, Dictionary<QTNode, List<QTNodeItem>> map,int numberChild,QTNode parent = null)
    {
        parentNode = parent;
        this._currDepth = numberChild+1;
        this.bounds = bounds;
        _items = new List<QTNodeItem>();
        _parentMap = map;
        _parentMap.Add(this,_items);
        haveChild = false;
        headNode = InitHeadNode();
    }

    private QTNode InitHeadNode()
    {
        if (parentNode == null)
        {
            return this;
        }
        else
        {
            return parentNode.InitHeadNode();
        }
    }

    public bool IsChildFullContain(Rect rect)
    {
        if (topLeftTree != null&&RectUtils.IsFullContain(topLeftTree.bounds, rect))
        {
            return true;
        }
        if (topRightTree != null && RectUtils.IsFullContain(topRightTree.bounds, rect))
        {
            return true;
        }
        if (botLeftTree != null && RectUtils.IsFullContain(botLeftTree.bounds, rect))
        {
            return true;
        }
        if (botRightTree != null && RectUtils.IsFullContain(botRightTree.bounds, rect))
        {
            return true;
        }
        return false;
    }

    public void Build()
    {
        topLeftTree = new QTNode(new Rect(bounds.x,bounds.y+bounds.height/2f,bounds.width/2f,bounds.height/2f), _parentMap, _currDepth,this);
        topRightTree = new QTNode(new Rect(bounds.x+bounds.width/ 2f, bounds.y + bounds.height / 2f, bounds.width/2f,bounds.height/2f), _parentMap, _currDepth, this);
        botLeftTree = new QTNode(new Rect(bounds.x,bounds.y,bounds.width/2f,bounds.height/2f), _parentMap, _currDepth, this);
        botRightTree = new QTNode(new Rect(bounds.x + bounds.width / 2f, bounds.y,bounds.width/2f,bounds.height/2f), _parentMap, _currDepth, this);
        List<QTNodeItem> tempNodes = new List<QTNodeItem>(_items);
        _items.Clear();
        for (int i = 0; i < tempNodes.Count; i++)
        {
            tempNodes[i].ClearQTNodes();
            if(!ChildInsert(tempNodes[i]))
            {
                _items.Add(tempNodes[i]);
                tempNodes[i].UpdateQTNode(this, false);
            }
        }
        haveChild = true;
    }

    private bool ChildInsert(QTNodeItem nodeItem)
    {
        if (RectUtils.IsFullContain(topLeftTree.bounds, nodeItem.bounds))
        {
            topLeftTree.Insert(nodeItem);
            return true;
        }
        else if(RectUtils.IsFullContain(topRightTree.bounds, nodeItem.bounds))
        {
            topRightTree.Insert(nodeItem);
            return true;
        }
        else if(RectUtils.IsFullContain(botLeftTree.bounds, nodeItem.bounds))
        {
            botLeftTree.Insert(nodeItem);
            return true;
        }
        else if(RectUtils.IsFullContain(botRightTree.bounds, nodeItem.bounds))
        {
            botRightTree.Insert(nodeItem);
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Insert(QTNodeItem node)
    {
       
            if (haveChild)
            {
                if (!ChildInsert(node))
                {
                    _items.Add(node);
                    node.UpdateQTNode(this, false);
                }
            }
            else{
                bool isContain = false;
                if (parentNode == null)
                {
                  isContain = this.bounds.Overlaps(node.bounds);
                }
                else
                {
                  isContain = RectUtils.IsFullContain(bounds, node.bounds);
                }
                if (isContain)
                {
                    _items.Add(node);
                    if (_items.Count > QuadTree._maxCountInNode && _currDepth < QuadTree._maxDepth)
                    {
                        Build();
                    }
                    else
                    {
                        node.UpdateQTNode(this,false);
                     }
                }
           }
    }

    public void UpdateTreeByQTNodeItem(QTNodeItem node)
    {
        RemoveItem(node);
        node.UpdateQTNode(this,true);
    }

    public void RemoveItem(QTNodeItem nodeItem)
    {
        _items.Remove(nodeItem);
    }
    public void GetInsideNode(Rect rect,ref List<QTNodeItem> tempNodes)
    {
        if (IsOverlaps(rect))
        {
            OutQTNodeItems(tempNodes);
            if (haveChild)
            {
                topLeftTree.GetInsideNode(rect, ref tempNodes);
                topRightTree.GetInsideNode(rect, ref tempNodes);
                botLeftTree.GetInsideNode(rect, ref tempNodes);
                botRightTree.GetInsideNode(rect, ref tempNodes);
            }
        }
    }

    public void OutQTNodeItems(List<QTNodeItem> tempNoda)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            tempNoda.Add(_items[i]);
        }
    }
    //不可修改内容，仅读
    public List<QTNodeItem> GetCurrentQTNodeItems()
    {
        return _items;
    }

    public bool IsOverlaps(Rect rect)
    {
        return bounds.Overlaps(rect);
    }

    public void TestDrawBounds()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.color = Color.red;
        for (int i = 0; i < _items.Count; i++)
        {
            Gizmos.DrawWireSphere(_items[i].bounds.center, 0.5f);
        }

        if (haveChild)
        {
            topLeftTree.TestDrawBounds();
            topRightTree.TestDrawBounds();
            botLeftTree.TestDrawBounds();
            botRightTree.TestDrawBounds();
        }
     }
}

