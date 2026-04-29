using System.Collections.Generic;
using UnityEngine;
using UnityTimer;
public class AICollider : MonoBehaviour
{
    public ColliderType colliderType = ColliderType.Character;
    public PlayerCamp playerCamp;
    [HideInInspector]
    public bool isDead = false;
    [HideInInspector]
    public int attackSort = 0;


    public AIBox skyBox;
    public AIBox bodyBox;
    public AIBox groundBox;
    public AIBox attackRangeBox;

    private Dictionary<AICollider, bool> _enemyColliderMap;
    public QTNodeItem qtnodeItem;
    private float halfQTWidth;
    private float halfQTHeight;
    [HideInInspector]
    public bool isFinished = false;
    [HideInInspector]
    public void Start()
    {
        Init();
    }
    public bool IsSkill()
    {
        return colliderType == ColliderType.Skill;
    }
    private void InitQtRect()
    {
        Vector2 vector2 = bodyBox.size + new Vector2(attackLine * 2, attackLine * 2);
        halfQTWidth = vector2.x / 2f;
        halfQTHeight = vector2.y / 2f;

        Vector2 center = bodyBox.GetCenter();

        Vector2 pos = new Vector2(
            center.x - halfQTWidth,
            center.y - halfQTHeight
        );

        if (qtnodeItem == null)
        {
            qtnodeItem = new QTNodeItem(new Rect(pos, vector2), this);
        }
        else
        {
            qtnodeItem.UpdateRect(pos, vector2);
        }
    }

    private void InitFlyQtRect()
    {
        Vector2 vector2 = bodyBox.size;
        halfQTWidth = vector2.x / 2f;
        halfQTHeight = vector2.y / 2f;
        Vector2 pos = new Vector2(bodyBox.GetCenter().x - halfQTWidth, bodyBox.GetCenter().y - halfQTHeight);
        qtnodeItem = new QTNodeItem(new Rect(pos, vector2), this);
    }

    public QTNodeItem GetQT()
    {
        return qtnodeItem;
    }

    public void Init()
    {
        if (!IsSkill())
        {
            if (_enemyColliderMap == null)
            {
                _enemyColliderMap = new Dictionary<AICollider, bool>();
            }
            UpdateColliderData();
            _OnStart();
        }
        else
        {
            InitFlyQtRect();
        }
        isFinished = true;
    }
    private void UpdateColliderData()
    {
        skyBox = new AIBox(transform, playerCamp, skySize, skyOffset, transform.position);
        bodyBox = new AIBox(transform, playerCamp, bodySize, bodyOffset, transform.position);
        groundBox = new AIBox(transform, playerCamp, groundSize, groundOffset, transform.position);
        groundBox.InitGroundFindPos(bodyBox.size);

        float emY = bodyBox.GetCenter().y - groundBox.GetCenter().y;
        float emX = bodyBox.size.x * 0.5f + attackLine;

        attackRangeBox = new AIBox(transform, playerCamp, new Vector2(emX, emY), new Vector2(-emX / 2, emY / 2f), transform.position);

        InitQtRect();
    }
    public void _Destory()
    {
        ColliderMgr.RemoveCollider(this);
    }

    public void _OnStart()
    {
        ColliderMgr.AddCollider(this);
    }

    private void OnEnable()
    {
        isDead = false;
    }

    private void OnDisable()
    {
        isDead = true;
        if (!IsSkill())
        {
            _Destory();
        }
    }

    public void ClearHurtCDMap()
    {
        if (_enemyColliderMap != null)
        {
            _enemyColliderMap.Clear();
        }

    }

    public void SetDead(bool death = true)
    {
        isDead = death;
    }

    public float tempX;
    public float tempY;
    public float tempZ;

    public void _FixedUpdate()
    {
        UpdateAIClollider();
    }
    //如果是空中阶段，z表示 空中的高度
    private void UpdateAIClollider()
    {
        if (!isFinished) return;
        tempX = transform.position.x;
        tempY = transform.position.y;
        tempZ = transform.position.z;
        if (tempZ > 0)
        {
            skyBox.pos = new Vector2(0, tempZ);
            bodyBox.pos = new Vector2(tempX, tempY + tempZ);
            groundBox.pos = new Vector2(tempX, tempY);
        }
        else
        {
            skyBox.pos = Vector2.zero;
            bodyBox.pos = new Vector2(tempX, tempY);
            groundBox.pos = new Vector2(tempX, tempY);
        }
        if (!IsSkill())
        {
            attackRangeBox.pos = groundBox.pos;
        }

        Vector2 center = bodyBox.GetCenter();
        qtnodeItem.bounds.position = new Vector2(center.x - halfQTWidth, center.y - halfQTHeight);
        qtnodeItem.UpdateTree();
    }

    public Vector3 GetHurtPos()
    {
        return bodyBox.GetCenter();
    }
    public Vector3 GetGroundPos()
    {
        return groundBox.GetCenter();
    }

    public void AddBeAttack(AICollider enemyCollider)
    {
        if (_enemyColliderMap.ContainsKey(enemyCollider))
        {
            return;
        }
        else
        {
            _enemyColliderMap.Add(enemyCollider, true);
            if (bodyBox.hurtCD > 0)
            {
                Timer.Register(bodyBox.hurtCD, () =>
                {
                    if (_enemyColliderMap.ContainsKey(enemyCollider))
                    {
                        _enemyColliderMap.Remove(enemyCollider);
                    }
                }, null, false, false, this);
            }
        }
    }



    //#region 编辑器控制
    [HideInInspector] public bool editSky;
    [HideInInspector] public bool editBody;
    [HideInInspector] public bool editGround;
    [HideInInspector] public bool editAttack;
    public Vector2 skyOffset;

    public Vector2 skySize = Vector2.one;

    public Vector2 bodyOffset;

    public Vector2 bodySize = Vector2.one;

    public Vector2 groundOffset;

    public Vector2 groundSize = Vector2.one;

    public float attackLine = 0;
    //#endregion
}


