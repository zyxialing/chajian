using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class ColliderMgr
{
    private static Dictionary<PlayerCamp, List<AICollider>> _campMap;
    private static List<AICollider> _allColliders = new List<AICollider>();
    public static void Clear()
    {
        if (_campMap != null)
            _campMap.Clear();
    }
    public static int GetCount()
    {
        int count = 0;
        if (_campMap != null)
            foreach (var item in _campMap)
            {
                count += item.Value.Count;
            }
        return count;
    }
    public static void AddCollider(AICollider collider)
    {
        if (_campMap == null)
        {
            _campMap = new Dictionary<PlayerCamp, List<AICollider>>();
        }
        if (_campMap.ContainsKey(collider.playerCamp))
        {
            if (!_campMap[collider.playerCamp].Contains(collider))
            {
                _campMap[collider.playerCamp].Add(collider);
                _allColliders.Add(collider);
                QuadTreeMgr._instance.Insert(collider.GetQT());
            }
        }
        else
        {
            List<AICollider> list = new List<AICollider> { collider };
            _allColliders.Add(collider);
            QuadTreeMgr._instance.Insert(collider.GetQT());
            _campMap.Add(collider.playerCamp, list);
        }
    }

    public static void RemoveCollider(AICollider collider)
    {
        if (_campMap == null)
        {
            return;
        }
        if (_campMap.ContainsKey(collider.playerCamp))
        {
            if (_campMap[collider.playerCamp].Contains(collider))
            {
                _campMap[collider.playerCamp].Remove(collider);
                _allColliders.Remove(collider);
                QuadTreeMgr._instance.Remove(collider.GetQT());
            }
        }
    }

    public static List<AICollider> GetByCamp(PlayerCamp camp)
    {
        if (_campMap != null && _campMap.ContainsKey(camp))
        {
            return _campMap[camp];
        }
        return null;
    }

    public static List<AICollider> GetAll()
    {
        return _allColliders;
    }
}
