using UnityEngine;

public class ColliderTest : MonoBehaviour
{
    public AICollider self;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var result = ColliderCheck.IsTriggerByTargetType(self, TargetType.Enemy);

            Debug.Log("===== Enemy 测试 =====");

            if (result == null)
            {
                Debug.Log("结果：空");
                return;
            }

            Debug.Log("数量：" + result.Length);

            for (int i = 0; i < result.Length; i++)
            {
                Debug.Log("命中：" + result[i].name + " 阵营：" + result[i].playerCamp);
            }
        }
    }
}