using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuadTreeMgr : MonoBehaviour
{
    public int count = 1;
    public static QuadTreeMgr _instance;
    //#region ≈‰÷√
    //public static int maxDepth = 4;
    //public static int maxCountInNode = 10;
    private Rect area;
    //#endregion

    public AICollider selectItem;
    QuadTree quadTree;
    public static void Init(int maxDepth, int maxCountInNode, Rect area)
    {
        GameObject obj = new GameObject("QuadTreeMgr");
        _instance = obj.AddComponent<QuadTreeMgr>();
        _instance.quadTree = new QuadTree(area, maxDepth, maxCountInNode);
        _instance.area = area;
    }

    public void Clear()
    {
        quadTree = null;
        _instance = null;
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    public int GetCount()
    {
        return quadTree.GetAllQTNodeItems().Count;
    }



    public void Insert(QTNodeItem node)
    {
        quadTree.Insert(node);
    }
    public void Remove(QTNodeItem node)
    {
        quadTree.Remove(node);
    }
    public List<QTNodeItem> GetInsideNode(Rect bounds)
    {
      return  quadTree.GetInsideNode(bounds);
    }
    
    public List<QTNodeItem> GetAllItem()
    {
      return  quadTree.GetAllQTNodeItems();
    }


    private void OnDrawGizmos()
    {

        if (!ZDefine._ShowTuadTreeGizmos) return;

        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(area.center, area.size);



        if (quadTree != null)
        {
            Gizmos.color = Color.black;
            quadTree.TestDrawBounds();

#if UNITY_EDITOR
            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<AICollider>() != null)
            {
                selectItem = Selection.activeGameObject.GetComponent<AICollider>();
            }
#endif

            if (selectItem != null && selectItem.isFinished)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(selectItem.qtnodeItem.bounds.center, selectItem.qtnodeItem.bounds.size);

                var items = quadTree.GetInsideNode(selectItem.qtnodeItem.bounds);
                this.count = items.Count;
                foreach (var item in items)
                {
                    Gizmos.DrawWireSphere(item.bounds.center, 0.5f);
                }
            }


            var qtnodes = quadTree.GetAllQTNodeItems();

            Gizmos.color = Color.white;
            foreach (var item in qtnodes)
            {
                Gizmos.DrawWireCube(item.bounds.center, item.bounds.size);
            }
        }
    }
}
