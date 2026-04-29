using UnityEngine;
public class AIBox
{
    public Vector2 size;
    public Vector2 offset;
    public Vector2 _offset;
    public Vector2 pos;
    public PlayerCamp playerCamp;
    public Transform player;

    public Vector2[] leftFindPoints;
    public Vector2[] rightFindPoints;

    public Vector2[] leftFarFindPoints;
    public Vector2[] rightFarFindPoints;

    public Vector2[] leftMiddleFindPoints;
    public Vector2[] rightMiddleFindPoints;
    public float hurtCD = 0;
    public bool ignore_check;
    private int nearCount = 3;

    public AIBox(Transform player,PlayerCamp playerCamp,Vector2 size,Vector2 offset,Vector2 pos,float hurtCD = 0,bool ignore_check = false)
    {
        this.player = player;
        this.playerCamp = playerCamp;
        this.size = size;
        this.offset = offset;
        this.pos = pos;
        _offset = new Vector2(offset.x * -1, offset.y);
        this.hurtCD = hurtCD;
        this.ignore_check = ignore_check;
    }

    public Vector2 GetOffset()
    {
        if (player != null)
        {
            if (player.localScale.x < 0)
            {
                return _offset;
            }
            else
            {
                return offset;
            }
        }
        else
        {
            return Vector2.zero;
        }

    }
    public Vector3 GetOffset3()
    {
        if (player.localScale.x < 0)
        {
            return _offset;
        }
        else
        {
            return offset ;
        }
    }

    public void InitGroundFindPos(Vector2 groundSize)
    {
        float halfX = groundSize.x / 2f;
        float y = size.y / 2.1f;
        float[] Ys = new float[nearCount];
        Ys[0] = -y;
        Ys[1] = 0;
        Ys[2] = y;
        leftFindPoints = new Vector2[nearCount];
        for (int i = 0; i < leftFindPoints.Length; i++)
        {
            leftFindPoints[i] = new Vector2(-halfX, Ys[i]);
        }
        rightFindPoints = new Vector2[nearCount];
        for (int i = 0; i < rightFindPoints.Length; i++)
        {
            rightFindPoints[i] = new Vector2(halfX, Ys[i]);
        }

        int index = 0;
        leftFarFindPoints = new Vector2[30];
        for (int i = 0; i < leftFarFindPoints.Length; i++)
        {
            leftFarFindPoints[i] = new Vector2(-Random.Range(15f,21f), Random.Range(-5f,15f));
            index++;
            if (index > 2)
            {
                index = 0;
            }
        }
        index = 0;
        rightFarFindPoints = new Vector2[30];
        for (int i = 0; i < rightFarFindPoints.Length; i++)
        {
            rightFarFindPoints[i] = new Vector2(Random.Range(15f, 21f), Random.Range(-5f, 15f));
            index++;
            if (index > 2)
            {
                index = 0;
            }
        }
        int index2 = 0;
        leftMiddleFindPoints = new Vector2[30];
        for (int i = 0; i < leftMiddleFindPoints.Length; i++)
        {
            leftMiddleFindPoints[i] = new Vector2(-Random.Range(3f,5f), Random.Range(-2,7f));
            index2++;
            if (index2 > 2)
            {
                index2 = 0;
            }
        }
        index2 = 0;
        rightMiddleFindPoints = new Vector2[30];
        for (int i = 0; i < rightMiddleFindPoints.Length; i++)
        {
            rightMiddleFindPoints[i] = new Vector2(Random.Range(3f, 5f), Random.Range(-2f, 7f));
            index2++;
            if (index2 > 2)
            {
                index2 = 0;
            }
        }
    }

    public Vector2 GetCenter()
    {
        return pos + GetOffset();
    }

    public Vector2 GetFindPos(PointDir pointDir)
    {
        if(pointDir == PointDir.Left)
        {
          return  leftFindPoints[Random.Range(0, leftFindPoints.Length)] + pos;
        }
        else
        {
            return rightFindPoints[Random.Range(0, rightFindPoints.Length)] + pos;
        }
    }

    public Vector2 GetFindPos(PointDir pointDir,int Index)
    {
        if(pointDir == PointDir.Left)
        {
          return  leftFindPoints[Index] + pos;
        }
        else
        {
            return rightFindPoints[Index] + pos;
        }
    }
    

    public Vector3 GetFindFarPos(PointDir pointDir,int Index)
    {
        if(pointDir == PointDir.Left)
        {
          return  leftFarFindPoints[Index] + pos;
        }
        else
        {
            return rightFarFindPoints[Index] + pos;
        }
    }

    public int GetNearPointCount()
    {
        return nearCount;
    }

    public Vector3 GetFindRandomPos3()
    {
       var  pointDir = RandomMgr.GetValue() < 0.5f ? PointDir.Left : PointDir.Right;
        if (pointDir == PointDir.Left)
        {
          return  leftFindPoints[Random.Range(0, leftFindPoints.Length)] + pos;
        }
        else
        {
            return rightFindPoints[Random.Range(0, rightFindPoints.Length)] + pos;
        }
    }

    public Vector3 GetFindRandomPos3OutIndex(PointDir pointDir,out int index)
    {
        index = Random.Range(0, leftFindPoints.Length);
        if (pointDir == PointDir.Left)
        {
          return  leftFindPoints[index] + pos;
        }
        else
        {
            return rightFindPoints[index] + pos;
        }
    }
    public Vector3 GetFindFarRandomPos3()
    {
       var  pointDir = RandomMgr.GetValue() < 0.5f ? PointDir.Left : PointDir.Right;
        if (pointDir == PointDir.Left)
        {
          return leftFarFindPoints[Random.Range(0, leftFarFindPoints.Length)] + pos;
        }
        else
        {
            return rightFarFindPoints[Random.Range(0, rightFarFindPoints.Length)] + pos;
        }
    }
    public Vector3 GetFindFarRandomPos3OutIndex(PointDir pointDir, out int index)
    {
        index = Random.Range(0, leftFindPoints.Length);
        if (pointDir == PointDir.Left)
        {
          return leftFarFindPoints[Random.Range(0, leftFarFindPoints.Length)] + pos;
        }
        else
        {
            return rightFarFindPoints[Random.Range(0, rightFarFindPoints.Length)] + pos;
        }
    }

    public Vector3 GetFindMiddleRandomPos3()
    {
       var  pointDir = RandomMgr.GetValue() < 0.5f ? PointDir.Left : PointDir.Right;
        if (pointDir == PointDir.Left)
        {
          return leftMiddleFindPoints[Random.Range(0, leftMiddleFindPoints.Length)] + pos;
        }
        else
        {
            return rightMiddleFindPoints[Random.Range(0, rightMiddleFindPoints.Length)] + pos;
        }
    }
    public Vector2 GetFindFarPos(PointDir pointDir)
    {
        if(pointDir == PointDir.Left)
        {
          return leftFarFindPoints[Random.Range(0, leftFarFindPoints.Length)] + pos;
        }
        else
        {
            return rightFarFindPoints[Random.Range(0, rightFarFindPoints.Length)] + pos;
        }
    }
}