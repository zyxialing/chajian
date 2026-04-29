using EnhancedUI.EnhancedScroller;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScrollTest : MonoBehaviour, IEnhancedScrollerDelegate
{
    /// <summary>
    /// This is our scroller we will be a delegate for
    /// </summary>
    public EnhancedScroller scroller;

    /// <summary>
    /// This will be the prefab of each cell in our scroller. Note that you can use more
    /// than one kind of cell, but this example just has the one type.
    /// </summary>
    public EnhancedScrollerCellView cellViewPrefab;

    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
    {

        cellTest test = scroller.GetCellView(cellViewPrefab) as cellTest;
        test.UpdateDate(cellIndex,dataIndex);
        return test;
    }

    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
    {
       return 300;
    }

    public int GetNumberOfCells(EnhancedScroller scroller)
    {
        return 8;  
    }

    void Start()
    {
        scroller.Delegate = this;
    }

    public void ChangeScene()
    {
        ResLoader.Instance.GetScene("ExampleMain", null);
    }

    // Update is called once per frame
    void Update()
    {
   
    }
}
