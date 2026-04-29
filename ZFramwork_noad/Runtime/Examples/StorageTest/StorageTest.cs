using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StorageTest : MonoBehaviour
{
    public Text text1;
    public Text text2;
    public string key1;
    int k = 0;
    Coroutine _coroutine;
    Coroutine _coroutine2;
    public void test1()
    {
        if (_coroutine == null)
        {
            _coroutine = StartCoroutine(TestXingNeng());
        }
    }

    public void test2()
    {
        if (_coroutine2 == null)
        {
            _coroutine2 = StartCoroutine(TestXingNeng2());
        }

    }

    public void delete()
    {
        Storage.Instance.DeleteFile();

    }

    private IEnumerator TestXingNeng()
    {
        while (true)
        {
            yield return null;
            for (int i = 0; i < 100; i++)
            {
                string key = (k + i).ToString();
                SaveUtils.SetStringForKey(key, key);
                key1 = key;
                text1.text = key1;
            }
            k++;
        }
    }
    private IEnumerator TestXingNeng2()
    {
        while (true)
        {
            yield return null;
            for (int i = 0; i < 100; i++)
            {
                string key = (k + i).ToString();
                text2.text = key;
            }
            k++;
        }
    }

    public void ReturnMain()
    {
        ResLoader.Instance.GetScene("ExampleMain", null);
    }
}
