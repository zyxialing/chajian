using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrefabInstanceTest : MonoBehaviour
{
    public List<string> prefabPath;
    public bool isPreLoad = false;
    void Start()
    {
        if(isPreLoad)
        PreLoad();
    }

    void PreLoad()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PrefabUtils.Instance(prefabPath[Random.Range(0,prefabPath.Count)], Vector3.zero, Quaternion.identity, null);
        }

        if (Input.GetMouseButtonDown(1))
        {
            PrefabUtils.Instance(prefabPath[Random.Range(0, prefabPath.Count)], Vector3.zero, Quaternion.identity, null,false);
        }


    }


    public void ChangeScene()
    {
        ResLoader.Instance.GetScene("ExampleMain", null);
    }
}
