using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIColliderCondition
{
    public static bool IsCanAttack(AICollider collider1,AICollider collider2)
    {
        bool skyTrigger = ColliderCheck.IsTrigger(collider1.skyBox, collider2.skyBox);
        bool groundTrigger = ColliderCheck.IsTrigger(collider1.groundBox, collider2.groundBox);
        bool attackLineTrigger = ColliderCheck.AttackRangeTrigger(collider1, collider2);

        return skyTrigger && groundTrigger && attackLineTrigger;
    }
    public static bool IsCanAttackNoSky(AICollider collider1,AICollider collider2)
    {
        //bool skyTrigger = ColliderCheck.IsTrigger(collider1.skyBox, collider2.skyBox);
        bool groundTrigger = ColliderCheck.IsTrigger(collider1.groundBox, collider2.groundBox);
        bool attackLineTrigger = ColliderCheck.AttackRangeTrigger(collider1, collider2);

        return groundTrigger && attackLineTrigger;
    }
    public static bool IsCanFarAttack(AICollider collider1,AICollider collider2,float minDis,float maxDis)
    {

        //bool groundTrigger = ColliderCheck.IsTrigger(collider1.groundBox, collider2.groundBox);
        float disX = Mathf.Abs(collider1.tempX - collider2.tempX);
        bool inArea = disX >= minDis && disX < maxDis;
        return inArea;
    }
    public static bool IsBeHurtTrigger(AICollider collider1,AICollider collider2)
    {

        bool skyTrigger = collider1.skyBox.ignore_check?true:ColliderCheck.IsTrigger(collider1.skyBox, collider2.skyBox);
        bool groundTrigger = collider1.groundBox.ignore_check ? true : ColliderCheck.IsTrigger(collider1.groundBox, collider2.groundBox);
        bool attackTrigger = false;
        if (collider1.bodyBox != null)
        {
            attackTrigger = ColliderCheck.IsTrigger(collider1.bodyBox, collider2.bodyBox);
        }

        return skyTrigger && groundTrigger && attackTrigger;
    }


}
