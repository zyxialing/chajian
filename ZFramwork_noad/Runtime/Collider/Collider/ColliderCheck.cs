using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderCheck
{
    public static List<AICollider> aIColliders = new List<AICollider>();

    public static bool IsTrigger(AIBox box1, AIBox box2)
    {

        var center1 = box1.GetCenter();
        var center2 = box2.GetCenter();

        if (Mathf.Abs(center1.x - center2.x) < box1.size.x * 0.5f + box2.size.x * 0.5f &&
            Mathf.Abs(center1.y - center2.y) < box1.size.y * 0.5f + box2.size.y * 0.5f
            )
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bodyBox1">攻击方</param>
    /// <param name="bodyBox2">被攻击方</param>
    /// <param name="isRight">右侧方向</param>
    /// <returns></returns>
    public static bool AttackRangeTrigger(AICollider aICollider1, AICollider aICollider2)
    {
        var center1 = aICollider1.attackRangeBox;
        var center2 = aICollider2.bodyBox;
        return IsTrigger(center1, center2);
    }
    public static AICollider[] IsTriggerAllByCamp(AICollider selfCollider, bool isEnemy = true, AICollider enemy = null)
    {
        aIColliders.Clear();

        if (enemy == null)
        {
            // ? 1. 确定目标阵营
            PlayerCamp targetCamp = isEnemy
                ? GetEnemyCamp(selfCollider.playerCamp)
                : selfCollider.playerCamp;

            // ? 2. 只拿目标阵营
            var list = ColliderMgr.GetByCamp(targetCamp);

            if (list == null) return null;

            for (int i = 0; i < list.Count; i++)
            {
                var other = list[i];

                // ? 3. 基础过滤
                if (other.IsSkill() || other.isDead)
                    continue;

                // ? 4. 空间过滤（替代原QuadTree粗筛）
                if (!selfCollider.qtnodeItem.bounds.Overlaps(other.qtnodeItem.bounds))
                    continue;

                // ? 5. 业务条件
                if (!AIColliderCondition.IsBeHurtTrigger(selfCollider, other))
                    continue;

                aIColliders.Add(other);
            }
        }
        else
        {
            if (enemy.IsSkill() &&
                enemy.playerCamp != selfCollider.playerCamp &&
                AIColliderCondition.IsBeHurtTrigger(selfCollider, enemy))
            {
                aIColliders.Add(enemy);
            }
        }

        return aIColliders.Count > 0 ? aIColliders.ToArray() : null;
    }

    public static AICollider[] IsTriggerByTargetType(AICollider self, TargetType type)
    {
        aIColliders.Clear();

        List<AICollider> list = null;

        switch (type)
        {
            case TargetType.Enemy:
                list = ColliderMgr.GetByCamp(GetEnemyCamp(self.playerCamp));
                break;

            case TargetType.Friend:
                list = ColliderMgr.GetByCamp(self.playerCamp);
                break;

            case TargetType.All:
                list = ColliderMgr.GetAll();
                break;

            case TargetType.NotSelf:
                list = ColliderMgr.GetAll();
                break;

            case TargetType.Self:
                aIColliders.Add(self);
                return aIColliders.ToArray();
        }

        if (list == null) return null;

        for (int i = 0; i < list.Count; i++)
        {
            var other = list[i];

            if (other.isDead || other.IsSkill())
                continue;

            if (type == TargetType.NotSelf && other == self)
                continue;

            if (!self.qtnodeItem.bounds.Overlaps(other.qtnodeItem.bounds))
                continue;

            if (!AIColliderCondition.IsBeHurtTrigger(self, other))
                continue;

            aIColliders.Add(other);
        }

        return aIColliders.Count > 0 ? aIColliders.ToArray() : null;
    }

    private static PlayerCamp GetEnemyCamp(PlayerCamp self)
    {
        // ? 根据你项目扩展
        return self == PlayerCamp.PlayerCampA ? PlayerCamp.PlayerCampB : PlayerCamp.PlayerCampA;
    }
}
