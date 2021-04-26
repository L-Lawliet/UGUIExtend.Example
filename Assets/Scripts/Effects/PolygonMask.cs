using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;
using Waiting.UGUI.Collections;
using Waiting.UGUI.Components;

/// <summary>
///
/// name:PolygonMask
/// author:Lawliet
/// date:2019/10/1 11:53:01
/// versions:
/// introduce:
/// note:
/// 
/// </summary>
namespace Waiting.UGUI.Effects
{
    [AddComponentMenu("UI/Effects/PolygonMask", 21)]
    [RequireComponent(typeof(Graphic))]
    public class PolygonMask : BaseMeshEffect
    {
        public enum MaskType
        {
            /// <summary>
            /// RectTransform
            /// </summary>
            //Rect,
            /// <summary>
            /// 正多边形
            /// </summary>
            RegularPolygon,
            /// <summary>
            /// 不规则多边形
            /// </summary>
            Polygon,
        }

        /// <summary>
        /// 镜像类型
        /// </summary>
        [SerializeField]
        private MaskType m_MaskType = MaskType.Polygon;

        public MaskType maskType
        {
            get { return m_MaskType; }
            set
            {
                if (m_MaskType != value)
                {
                    m_MaskType = value;

                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }

        [SerializeField]
        private RectTransform m_MaskRect;

        public RectTransform maskRect
        {
            get { return m_MaskRect == null ? this.transform.parent as RectTransform : m_MaskRect; }
            set
            {
                if (m_MaskRect != value)
                {
                    m_MaskRect = value;

                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }

        [SerializeField]
        private RegularPolygon m_RegularPolygon;

        public RegularPolygon regularPolygon
        {
            get { return m_RegularPolygon; }
            set
            {
                if (m_RegularPolygon != value)
                {
                    m_RegularPolygon = value;

                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }
        //不算精准区域面积时，会把碰撞器的点做锚点偏差赋值给目标图片做顶点
        //算精准区域面积时，因为还要计算目标图Rect的四边，所以目标图片锚点的位置会影响结果， 最好把这个组件附着在目标图片上，这样锚点相同，就不会出现影响结果的问题
        [SerializeField]
        private PolygonCollider2D m_PolygonCollider2D;//2D碰撞器会随着锚点的移动而移动,是以锚点的位置为(0,0)

        public PolygonCollider2D polygonCollider2D
        {
            get { return m_PolygonCollider2D; }
            set
            {
                if (m_PolygonCollider2D != value)
                {
                    m_PolygonCollider2D = value;

                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }

        /// <summary>
        /// 采用局部坐标
        /// 不采用的话，会根据Mask的RectTransform做偏移
        /// </summary>
        [SerializeField]
        public bool m_IsLocal;

        public bool isLocal
        {
            get { return m_IsLocal; }
            set
            {
                if (m_IsLocal != value)
                {
                    m_IsLocal = value;

                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }

        [NonSerialized]
        private RectTransform m_RectTransform;

        public RectTransform rectTransform
        {
            get { return m_RectTransform ?? (m_RectTransform = GetComponent<RectTransform>()); }
        }

        private List<Vector2> m_commonAreaVerList = new List<Vector2>();  //相交区域所有的顶点

        //这两个选择保留，根据需求勾选，可以少些算法
        public bool IsOpenCommonAreaMask; //裁剪得到正真的相交区域
        public bool IsOpenTight;//考虑图片开启了Tight，裁剪了四周透明块

        /*[SerializeField]     
        private int m_DrawStep;*/

#if UNITY_EDITOR
        public void SetDirty()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }
#endif

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }



            var original = ListPool<UIVertex>.Get();
            var output = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(original);

            int count = original.Count;

            switch (m_MaskType)
            {
                case MaskType.RegularPolygon:
                    if (regularPolygon != null)
                    {
                        DrawRegularPolygon(original, output, count);
                    }
                    else
                    {
                        return;
                    }

                    break;
                case MaskType.Polygon:
                    if (m_PolygonCollider2D == null)
                    {
                        return;
                    }
                    DrawPolygon(original, output, count);
                    break;
                default:
                    break;
            }


            vh.Clear();
            output.Reverse();
            vh.AddUIVertexTriangleStream(output);

            ListPool<UIVertex>.Recycle(original);
            ListPool<UIVertex>.Recycle(output);
        }

        private void DrawRect(List<UIVertex> original, List<UIVertex> output, int count)
        {
            Sprite overrideSprite = null;

            Rect rect = graphic.GetPixelAdjustedRect();

            Vector4 outer = new Vector4();

            if (graphic is Image)
            {
                overrideSprite = (graphic as Image).overrideSprite;

                if (overrideSprite != null)
                {
                    outer = DataUtility.GetOuterUV(overrideSprite);
                }
            }

            Vector2 offset = GetRectTransformOffset(rectTransform, maskRect);

            Vector2 v0 = new Vector2(maskRect.rect.xMin, maskRect.rect.yMin) - offset;
            Vector2 v1 = new Vector2(maskRect.rect.xMax, maskRect.rect.yMin) - offset;
            Vector2 v2 = new Vector2(maskRect.rect.xMax, maskRect.rect.yMax) - offset;
            Vector2 v3 = new Vector2(maskRect.rect.xMin, maskRect.rect.yMax) - offset;

            output.Add(GetVertex(v0, rect, overrideSprite, outer));
            output.Add(GetVertex(v1, rect, overrideSprite, outer));
            output.Add(GetVertex(v2, rect, overrideSprite, outer));

            output.Add(GetVertex(v0, rect, overrideSprite, outer));
            output.Add(GetVertex(v2, rect, overrideSprite, outer));
            output.Add(GetVertex(v3, rect, overrideSprite, outer));
        }

        private void DrawRegularPolygon(List<UIVertex> original, List<UIVertex> output, int count)
        {
            Sprite overrideSprite = null;

            Rect rect = graphic.GetPixelAdjustedRect();

            Vector4 inner = new Vector4();

            if (graphic is Image)
            {
                overrideSprite = (graphic as Image).overrideSprite;

                if (overrideSprite != null)
                {
                    inner = DataUtility.GetInnerUV(overrideSprite);
                }
            }

            uint side = regularPolygon.side;
            float innerPercent = regularPolygon.innerPercent;

            Vector2 offset = new Vector2();

            if (m_IsLocal)
            {
                offset = GetRectTransformOffset(rectTransform, regularPolygon.rectTransform);
            }

            float size = Mathf.Min(regularPolygon.rectTransform.sizeDelta.x, regularPolygon.rectTransform.sizeDelta.y);

            float angle = 360.0f / side;

            uint len = side * 2;

            int sideCount = (int)side;

            Vector2[] points = new Vector2[len];

            for (int i = 0; i < sideCount; i++)
            {
                Vector2 point = new Vector2();

                float outerRadius = size * 0.5f;
                float innerRadius = size * 0.5f * innerPercent;

                ///添加外点
                point.x = Mathf.Cos((angle * i + 90) * Mathf.Deg2Rad) * outerRadius;
                point.y = Mathf.Sin((angle * i + 90) * Mathf.Deg2Rad) * outerRadius;

                points[i] = point;

                ///添加内点
                point.x = Mathf.Cos((angle * i + 90) * Mathf.Deg2Rad) * innerRadius;
                point.y = Mathf.Sin((angle * i + 90) * Mathf.Deg2Rad) * innerRadius;

                points[i + sideCount] = point;
            }

            for (int i = 0; i < sideCount; i++)
            {
                int a = i + 0;
                int b = i + 1;
                int c = i + 0 + sideCount;
                int d = i + 1 + sideCount;

                if (i == sideCount - 1)
                {
                    b = 0;
                    d = sideCount;
                }

                output.Add(GetVertex(points, c, offset, rect, overrideSprite, inner));
                output.Add(GetVertex(points, b, offset, rect, overrideSprite, inner));
                output.Add(GetVertex(points, a, offset, rect, overrideSprite, inner));

                output.Add(GetVertex(points, b, offset, rect, overrideSprite, inner));
                output.Add(GetVertex(points, d, offset, rect, overrideSprite, inner));
                output.Add(GetVertex(points, c, offset, rect, overrideSprite, inner));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DrawPolygon(List<UIVertex> original, List<UIVertex> output, int count)
        {
            Vector2[] points;
            if(IsOpenCommonAreaMask)
            {
                m_commonAreaVerList.Clear();
                GetCommonAreaVer();
                points = m_commonAreaVerList.ToArray();
            }
            else
            {
                points = m_PolygonCollider2D.points;
            }


            Sprite overrideSprite = null;

            Rect rect = graphic.GetPixelAdjustedRect();

            Vector4 inner = new Vector4();
            if (graphic is Image)
            {
                overrideSprite = (graphic as Image).overrideSprite;

                if (overrideSprite != null)
                {
                    inner = DataUtility.GetInnerUV(overrideSprite);
                }

            }

            Vector2 offset = new Vector2();

            if (!m_IsLocal)
            {
                offset = GetRectTransformOffset(rectTransform, m_PolygonCollider2D.transform as RectTransform);
            }

            var len = points.Length;

            List<int> indexList = new List<int>(len);

            /*for (int i = len - 1; i >= 0; i--)
            {
                indexList.Add(i);
            }*/

            for (int i = 0; i < len; i++)
            {
                indexList.Add(i);
            }

            while (indexList.Count > 2) //indexList.Count > points.Length - m_DrawStep
            {
                int i;

                len = indexList.Count;

                bool isLeft = false;

                for (i = 0; i < len; i++)
                {
                    int p = indexList[(i + 0) % len];
                    int s = indexList[(i + 1) % len];
                    int q = indexList[(i + 2) % len];

                    if (len == 3)  //只剩下三个点了,直接绘制
                    {
                        output.Add(GetVertex(points, p, offset, rect, overrideSprite, inner));
                        output.Add(GetVertex(points, s, offset, rect, overrideSprite, inner));
                        output.Add(GetVertex(points, q, offset, rect, overrideSprite, inner));

                        indexList.RemoveAt(i + 1);

                        break;
                    }

                    isLeft = ToLeftTest(points, p, q, s);

                    if (isLeft) // s在左边，表示为嘴巴,对上一个三角形切耳
                    {
                        p = indexList[(i + len - 1) % len];
                        s = indexList[(i + 0) % len];
                        q = indexList[(i + 1) % len];

                        output.Add(GetVertex(points, p, offset, rect, overrideSprite, inner));
                        output.Add(GetVertex(points, s, offset, rect, overrideSprite, inner));
                        output.Add(GetVertex(points, q, offset, rect, overrideSprite, inner));

                        indexList.RemoveAt(i);

                        break;
                    }
                }

                if (!isLeft && indexList.Count > 2) //没有嘴巴，直接绘制
                {
                    for (i = 0; i < len - 2; i++)
                    {
                        int p = indexList[0];
                        int s = indexList[(i + 1) % len];
                        int q = indexList[(i + 2) % len];

                        output.Add(GetVertex(points, p, offset, rect, overrideSprite, inner));
                        output.Add(GetVertex(points, s, offset, rect, overrideSprite, inner));
                        output.Add(GetVertex(points, q, offset, rect, overrideSprite, inner));

                    }

                    break;
                }
            }

        }

        private UIVertex GetVertex(Vector2[] list, int index, Vector2 offset, Rect rect, Sprite overrideSprite, Vector4 inner)
        {
            return GetVertex(list[index] - offset, rect, overrideSprite, inner);
        }

        private UIVertex GetVertex(Vector2 vector, Rect rect, Sprite overrideSprite, Vector4 inner)
        {
            UIVertex vertex = new UIVertex();
            vertex.position = vector;
            vertex.color = graphic.color;
            vertex.normal = new Vector3(0, 0, -1);

            float u = (vertex.position.x - rect.x) / rect.width * (inner.z - inner.x) + inner.x;
            float v = (vertex.position.y - rect.y) / rect.height * (inner.w - inner.y) + inner.y;

            vertex.uv0 = new Vector2(u, v);

            return vertex;
        }

        private bool ToLeftTest(Vector2[] points, int pIndex, int qIndex, int sIndex)
        {
            return ToLeftTest(points[pIndex], points[qIndex], points[sIndex]);
        }

        private bool ToLeftTest(Vector2 p, Vector2 q, Vector2 s)
        {
            return Area2(p, q, s) > 0;
        }

        private float Area2(Vector2 p, Vector2 q, Vector2 s)
        {
            return p.x * q.y - p.y * q.x + q.x * s.y - q.y * s.x + s.x * p.y - s.y * p.x;
        }

        /// <summary>
        /// 返回两个RectTransform的偏移值，相对rect1来说。
        /// 只对同一个Canvas下的两个RectTransform有效
        /// </summary>
        /// <param name="rect1"></param>
        /// <param name="rect2"></param>
        /// <returns></returns>
        private Vector2 GetRectTransformOffset(RectTransform rect1, RectTransform rect2)
        {
            Vector2 offset1 = Vector2.zero;
            Vector2 offset2 = Vector2.zero;

            RectTransform temp = rect1;

            while (temp != null)
            {
                if (temp == rect2)
                {
                    return offset1;
                }

                offset1 += temp.anchoredPosition;

                temp = temp.parent as RectTransform;
            }

            temp = rect2;

            while (temp != null)
            {
                if (temp == rect1)
                {
                    return -offset2;
                }

                offset2 += temp.anchoredPosition;

                temp = temp.parent as RectTransform;
            }

            return offset1 - offset2;
        }

        /// <summary>
        /// 得到真实Mesh组成的Rect
        /// </summary>
        private Rect GetRealRect()
        {
            Rect rect = graphic.GetPixelAdjustedRect();//得到锚点和各个顶点位置
            Sprite overrideSprite = (graphic as Image).overrideSprite;
            Rect targetRect=new Rect();


            //SpritePackingMode mode = overrideSprite.packingMode;不顶用原因未知
            if (IsOpenTight)
            {
                Vector2 center = rect.center - new Vector2(rect.width / 2, rect.height / 2);//转化中心点
                float ratio_w = rect.width / overrideSprite.rect.width; //计算比例
                float ratio_h = rect.height / overrideSprite.rect.height;
                float x = overrideSprite.textureRectOffset.x * ratio_w + center.x;
                float y = overrideSprite.textureRectOffset.y * ratio_h + center.y;
                targetRect.x = center.x; //x,y为两个三角面最左下角的值
                targetRect.y = center.y;
                targetRect.xMin = x;
                targetRect.yMin = y;
                targetRect.min = new Vector2(x, y);
                targetRect.xMax = x + overrideSprite.textureRect.width * ratio_w;
                targetRect.yMax = y + overrideSprite.textureRect.height * ratio_h;
                targetRect.max = new Vector2(targetRect.xMax, targetRect.yMax);
                targetRect.width = overrideSprite.textureRect.width * ratio_w;
                targetRect.height = overrideSprite.textureRect.height * ratio_h;
            }
            else
            {
                targetRect = rect;
            }
            return targetRect;

        }

        /// <summary>
        /// 得到相交区域的所有顶点
        /// </summary>
        private void GetCommonAreaVer()
        {
            Rect rect = GetRealRect();
            Vector2[] rectPoint = new Vector2[] { rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin) };

            Vector2[] points = m_PolygonCollider2D.points; //相当于当前所属transform的本地坐标
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 line1_start = points[i];
                Vector2 line1_end;

                if (i == points.Length - 1) //收尾相连
                {
                    line1_end = points[0];
                }
                else
                {
                    line1_end = points[i + 1];
                }

                if (IsInRect(line1_start)) //添加点在rect内的线段左坐标
                {
                    if (m_commonAreaVerList.Contains(line1_start) == false)
                        m_commonAreaVerList.Add(line1_start);
                }
                if (IsInRect(line1_start) && IsInRect(line1_end))//边在矩形内,跳过，没有交点
                {
                    continue;
                }

                List<Vector2> crossList = new List<Vector2>(); //缓存所有交点
                for (int j = 0; j < rectPoint.Length; j++) //矩形4条边
                {
                    Vector2 line2_start = rectPoint[j];
                    Vector2 line2_end;

                    if (j == rectPoint.Length - 1) //收尾相连
                    {
                        line2_end = rectPoint[0];
                    }
                    else
                    {
                        line2_end = rectPoint[j + 1];
                    }

                    Vector2 crossPos = Vector2.zero;
                    bool hasCrossPos = GetOnePos(line1_start, line1_end, line2_start, line2_end, ref crossPos);
                    if (hasCrossPos)  //添加交点坐标，最多2个不同的交点
                    {
                        if (crossList.Contains(crossPos) == false)
                            crossList.Add(crossPos);
                    }
                }
                if(crossList.Count==1)
                {
                    m_commonAreaVerList.Add(crossList[0]);
                }
                else if(crossList.Count==2)
                {
                    SortCrossPos(line1_start, line1_end, crossList);
                }
            }
            //添加可用顶点
            for (int i = 0; i <rectPoint.Length ; i++)
            {
                bool isInGraph = PosInGraph(rectPoint[i], points.ToList());
                if(isInGraph==false)
                {
                    continue;
                }
                for (int j = 0; j < m_commonAreaVerList.Count; j++)
                {
                    Vector2 firstPos= m_commonAreaVerList[j];
                    Vector2 secondPos;
                    if(j== m_commonAreaVerList.Count - 1)
                    {
                        secondPos = m_commonAreaVerList[0];
                    }
                    else
                    {
                        secondPos = m_commonAreaVerList[j+1];
                    }

                    if((firstPos.x==rectPoint[i].x&&secondPos.y==rectPoint[i].y)
                        ||(firstPos.y == rectPoint[i].y && secondPos.x == rectPoint[i].x))
                    {
                        m_commonAreaVerList.Insert(j + 1, rectPoint[i]);
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// 两个交点排序
        /// </summary>
        private void SortCrossPos(Vector2 line1_start,Vector2 line1_end,List<Vector2> crossList)
        {
            if(crossList.Count!=2)
            {
                Debug.LogError("交点数据有问题，请核查");
                return;
            }
            if(line1_end.x-line1_start.x>0) //顺序左往右
            {
                if (crossList[0].x > crossList[1].x)
                    crossList.Reverse();
               m_commonAreaVerList.AddRange(crossList);
            }
            else if(line1_end.x - line1_start.x < 0) //右往左
            {
                if (crossList[0].x < crossList[1].x)
                    crossList.Reverse();
                m_commonAreaVerList.AddRange(crossList);
            }
            else if(line1_end.x - line1_start.x == 0&& line1_end.y - line1_start.y > 0) //竖直下往上
            {
                if (crossList[0].y > crossList[1].y)
                    crossList.Reverse();
                m_commonAreaVerList.AddRange(crossList);
            }
            else if(line1_end.x - line1_start.x == 0 && line1_end.y - line1_start.y < 0) //上往下
            {
                if (crossList[0].y < crossList[1].y)
                    crossList.Reverse();
                m_commonAreaVerList.AddRange(crossList);
            }
        }
        /// <summary>
        /// 点在图形内
        /// </summary>
        private bool PosInGraph(Vector2 targerPos, List<Vector2> vers)
        {
            int passSideCount = 0;
            for (int i = 0; i < vers.Count; i++)
            {

                int index1 = i;
                int index2 = i + 1;
                if (i == vers.Count - 1)
                    index2 = 0;
                if (targerPos == vers[index1] || targerPos == vers[index2]) //点击点是图形顶点
                {
                    return false;
                }
                Vector2 p1 = vers[index1] - targerPos;
                Vector2 p2 = vers[index2] - targerPos;
                if (Vector2.Dot(p1, p2) == -1)  //点击点再边上
                {
                    return false;
                }
                if (((targerPos.y <= vers[index1].y) && (targerPos.y > vers[index2].y))   //排除穿过交点时算成2次穿过
                        || ((targerPos.y <= vers[index2].y) && (targerPos.y > vers[index1].y)))
                {
                    if (targerPos.x < vers[index1].x + (targerPos.y - vers[index1].y) / (vers[index2].y - vers[index1].y) * (vers[index2].x - vers[index1].x))  //点斜式
                    {
                        passSideCount += 1;
                    }
                }
            }
            return passSideCount % 2 == 1;
        }

        /// <summary>
        /// 两条线段是否相交都得到一个真实坐标点
        /// </summary>
        private bool GetOnePos(Vector2 line1_start, Vector2 line1_end, Vector2 line2_start, Vector2 line2_end, ref Vector2 crossPos)
        {
            Vector2 samePos = Vector2.zero; //直线的交点

            float a1; //直线斜率
            float b1;

            if ((line1_start.x == line1_end.x && line2_start.x == line2_end.x)
                || (line1_start.y == line1_end.y && line2_start.y == line2_end.y))//平行
            {
                return false;
            }
            else
            {
                if(line1_start.x == line1_end.x)//没有斜率
                {
                    samePos = new Vector2(line1_start.x, line2_start.y);
                }
                else                           //有斜率
                {
                    a1 = (line1_start.y - line1_end.y) / (line1_start.x - line1_end.x);
                    b1 = line1_start.y - a1 * line1_start.x;
                    if(line2_start.x == line2_end.x) //Rect竖直边
                    {
                        samePos = new Vector2(line2_start.x, (a1 * line2_start.x + b1));
                    }
                    if(line2_start.y==line2_end.y) //Rect水平边
                    {
                        samePos = new Vector2((line2_start.y - b1) / a1, line2_start.y);
                    }
                }

                if (IsRectPos(line1_start, line1_end, line2_start, line2_end, samePos)) //有真实交点
                {
                    crossPos = samePos;
                    return true;
                }

                return false;
            }

        }
        /// <summary>
        /// 点是否在Rect中
        /// </summary>
        private bool IsInRect(Vector2 pos)
        {

            Rect rect = GetRealRect();

            if (pos.x >= rect.xMin && pos.x <= rect.xMax && pos.y >= rect.yMin && pos.y <= rect.yMax)
            {
                return true;
            }
            return false;

        }

        /// <summary>
        /// 两条线段是否有交点
        /// </summary>
        private bool IsRectPos(Vector2 line1_start, Vector2 line1_end, Vector2 line2_start, Vector2 line2_end, Vector2 pos)
        {
            float line1_xmix = Mathf.Min(line1_start.x, line1_end.x);
            float line1_xmax = Mathf.Max(line1_start.x, line1_end.x);
            float line1_ymix = Mathf.Min(line1_start.y, line1_end.y);
            float line1_ymax = Mathf.Max(line1_start.y, line1_end.y);

            float line2_xmix = Mathf.Min(line2_start.x, line2_end.x);
            float line2_xmax = Mathf.Max(line2_start.x, line2_end.x);
            float line2_ymix = Mathf.Min(line2_start.y, line2_end.y);
            float line2_ymax = Mathf.Max(line2_start.y, line2_end.y);

            if (pos.x >= line1_xmix && pos.x <= line1_xmax && pos.y >= line1_ymix && pos.y <= line1_ymax  //交点分别在两条线段上
              && pos.x >= line2_xmix && pos.x <= line2_xmax && pos.y >= line2_ymix && pos.y <= line2_ymax)
            {
                return true;
            }

            return false;
        }
    }
}
