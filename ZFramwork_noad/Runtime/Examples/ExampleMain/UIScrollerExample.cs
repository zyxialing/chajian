using EnhancedUI.EnhancedScroller;
using UnityEngine;

[RequireComponent(typeof(EnhancedScroller))]
public class UIScrollerExample : MonoBehaviour, IEnhancedScrollerDelegate
{
    /// <summary>
    /// This is our scroller we will be a delegate for
    /// </summary>
    private EnhancedScroller scroller;
    private RectTransform rectTransform;

    /// <summary>
    /// This will be the prefab of each cell in our scroller. Note that you can use more
    /// than one kind of cell, but this example just has the one type.
    /// </summary>
    public EnhancedScrollerCellView cellViewPrefab;

    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
    {
        ExampleCell test = scroller.GetCellView(cellViewPrefab) as ExampleCell;
        return test;
    }

    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
    {
        return rectTransform.rect.height / 6;
    }

    public int GetNumberOfCells(EnhancedScroller scroller)
    {
        return 6;
    }

    void Start()
    {
        scroller = GetComponent<EnhancedScroller>();
        rectTransform = GetComponent<RectTransform>();
        scroller.Delegate = this;
    }
}
