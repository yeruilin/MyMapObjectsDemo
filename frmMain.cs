using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace MyMapObjectsDemo
{
    public partial class frmMain : Form
    {
        #region 字段
        //为了读取shp数据需要用的东西
        OpenFileDialog openFileDialog1;//Open the file window
        MyMapObjects.Table table;
        //选项变量
        private Color mZoomBoxColor = Color.DeepPink;//放大盒的颜色
        private double mZoomBoxWidth = 0.53;
        private Color mSelectBoxColor = Color.DarkGreen;
        private double mSelectBoxWidth = 0.53;
        private double mZoomRatioFixed = 2;//固定缩放系数
        private double mZoomRatioMouseWheel = 2;
        private double mSelectingTolorance = 3;//单位：像素
        private MyMapObjects.moSimpleFillSymbol mSelectingBoxSymbol;
        private MyMapObjects.moSimpleFillSymbol mZoomBoxSymbol;
        private MyMapObjects.moSimpleFillSymbol mMovingPolygonSymbol;
        private MyMapObjects.moSimpleFillSymbol mEditingPolygonSymbol;
        private MyMapObjects.moSimpleMarkerSymbol mEditingVertexSymbol;//顶点手柄符号
        private MyMapObjects.moSimpleLineSymbol mElasticSymbol;//橡皮筋符号

        //与地图操作有关的变量
        private Int32 mMapOpStyle = 0;//1：放大；2：缩小；3：漫游；4：选择；5：查询；6：移动；7：描绘；8：编辑
        private PointF mStartMouseLocation;//拉框时的起点
        private bool mIsInZoomIn = false;
        private bool mIsInPan = false;
        private bool mIsInSelect = false;
        private bool mIsInIdentify = false;
        private bool mIsMovingShapes = false;
        private List<MyMapObjects.moGeometry> mMovingGeometries = new List<MyMapObjects.moGeometry>();//正在移动的图形集合
        private MyMapObjects.moGeometry mEditingGeometry;//正在编辑的图形
        private List<MyMapObjects.moPoints> mSketchingShape;//正在描绘的图形，用一个多点集合存储

        #endregion

        public frmMain()
        {
            InitializeComponent();
            moMap.MouseWheel += MoMap_MouseWheel;
            //绑定图层组件
            this.moLayerControl.Map = this.moMap;
        }

        #region 窗体和按钮事件处理
        private void frmMain_Load(object sender, EventArgs e)
        {
            //初始化符号
            InitializeSymbols();
            //初始化描绘图形
            InitializeSketchingShape();
            //显示比例尺
            ShowMapScale();
            this.moLayerControl.RefreshContext();//刷新显示内容
            table = new MyMapObjects.Table();
        }

        //载入图层
        private void btnLoadLayerFile_Click(object sender, EventArgs e)
        {
            readShp_Click(sender, e);
            this.moLayerControl.RefreshContext();
            //读取老师定义的lay文件
            ////获取文件名
            //OpenFileDialog sDialog = new OpenFileDialog();
            //string sFileName = "";
            //if (sDialog.ShowDialog() == DialogResult.OK)
            //{
            //    sFileName = sDialog.FileName;
            //    sDialog.Dispose();
            //}
            //else
            //{
            //    sDialog.Dispose();
            //    return;
            //}
            //try
            //{
            //    FileStream sStream = new FileStream(sFileName,FileMode.Open);
            //    BinaryReader sr = new BinaryReader(sStream);
            //    MyMapObjects.moMapLayer sLayer = DataIoTools.LoadMapLayer(sr);
            //    moMap.Layers.Add(sLayer);
            //    if (moMap.Layers.Count == 1)
            //    {
            //        moMap.FullExtent();
            //    }
            //    else
            //    {
            //        moMap.RedrawMap();
            //    }
            //    sr.Dispose();
            //    sStream.Dispose();
            //    //更新左侧图层组件
            //    this.moLayerControl.RefreshContext();
            //}
            //catch(Exception error)
            //{
            //    MessageBox.Show(error.ToString());
            //}
        }

        //全范围显示
        private void btnFullExtent_Click(object sender, EventArgs e)
        {
            moMap.FullExtent();
        }

        //放大
        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 1;
        }

        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 2;
        }

        private void btnPan_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 3;
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 4;
        }

        private void btnIdentify_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 5;
        }

        private void btnSimpleRender_Click(object sender, EventArgs e)
        {
            //简单渲染
            //查找多边形图层
            MyMapObjects.moMapLayer sLayer = GetPolygonLayer();
            if(sLayer==null)
            {
                return;
            }
            MyMapObjects.moSimpleRenderer sRenderer = new MyMapObjects.moSimpleRenderer();
            MyMapObjects.moSimpleFillSymbol sSymbol = new MyMapObjects.moSimpleFillSymbol();
            sRenderer.Symbol = sSymbol;
            sLayer.Renderer = sRenderer;
            moMap.RedrawMap();
        }
        //唯一值渲染
        private void btnUniqueValue_Click(object sender, EventArgs e)
        {
            //查找多边形图层
            MyMapObjects.moMapLayer sLayer = GetPolygonLayer();
            if (sLayer == null)
            {
                return;
            }
            //假定第一个字段名为“名称”且为字符型
            MyMapObjects.moUniqueValueRenderer sRenderer = new MyMapObjects.moUniqueValueRenderer();
            sRenderer.Field = "名称";
            List<string> sValues = new List<string>();//存储唯一值
            int sFeatureCount = sLayer.Features.Count;
            //读取所有唯一值
            for(int i=0;i<=sFeatureCount-1;i++)
            { 
                string sValue = (string)sLayer.Features.GetItem(i).Attributes.GetItem(0);
                sValues.Add(sValue);
            }
            //去除重复
            sValues = sValues.Distinct().ToList();
            //生成符号
            for(int i=0;i<=sValues.Count-1;i++)
            {
                MyMapObjects.moSimpleFillSymbol sSymbol = new MyMapObjects.moSimpleFillSymbol();
                sRenderer.AddUniqueValue(sValues[i], sSymbol);
            }
            sRenderer.DefaultSymbol = new MyMapObjects.moSimpleFillSymbol();
            sLayer.Renderer = sRenderer;
            moMap.RedrawMap();
        }
        //分级渲染
        private void btnClassBreaks_Click(object sender, EventArgs e)
        {
            //查找多边形图层
            MyMapObjects.moMapLayer sLayer = GetPolygonLayer();
            if (sLayer == null)
            {
                return;
            }//假定存在“F5”的字段，且为单精度浮点型
            MyMapObjects.moClassBreaksRenderer sRenderer = new MyMapObjects.moClassBreaksRenderer();
            sRenderer.Field = "F5";
            List<double> sValues = new List<double>();
            int sFeatureCount = sLayer.Features.Count;
            int sFieldIndex = sLayer.AttributeFields.FindField(sRenderer.Field);
            //读出所有值
            for(int i=0;i<sFeatureCount;i++)
            {
                double sValue = (float)sLayer.Features.GetItem(i).Attributes.GetItem(sFieldIndex);//只能转化为float，因为实际保存了float类型
                sValues.Add(sValue);
            }
            //获取最小、最大值，分5级
            double sMinValue = sValues.Min();
            double sMaxValue=sValues.Max();
            for (int i=0;i<5;i++)
            {
                double sValue = sMinValue + (sMaxValue - sMinValue) * (i + 1) / 5;
                MyMapObjects.moSimpleFillSymbol sSymbol = new MyMapObjects.moSimpleFillSymbol();
                sRenderer.AddBreakValue(sValue, sSymbol);

            }
            //生成渐变色
            Color sStartColor = Color.FromArgb(255, 255, 192, 192);
            Color sEndColor = Color.Maroon;
            sRenderer.RampColor(sStartColor, sEndColor);
            sRenderer.DefaultSymbol = new MyMapObjects.moSimpleFillSymbol();
            sLayer.Renderer = sRenderer;
            moMap.RedrawMap();
        }
        //显示注记
        private void btnShowLabel_Click(object sender, EventArgs e)
        {
            if (moMap.Layers.Count == 0) { return; }
            //获取第一图层
            MyMapObjects.moMapLayer sLayer = moMap.Layers.GetItem(0);
            MyMapObjects.moLabelRenderer sLabelRenderer = new MyMapObjects.moLabelRenderer();
            sLabelRenderer.Field = sLayer.AttributeFields.GetItem(0).Name;
            Font sOldFont = sLabelRenderer.TextSymbol.Font;
            sLabelRenderer.TextSymbol.Font = new Font(sOldFont.Name, 12);
            sLabelRenderer.TextSymbol.UseMask = true;//使用描边
            sLabelRenderer.LabelFeatures = true;
            sLayer.LabelRenderer = sLabelRenderer;
            moMap.RedrawMap();
        }
        //移动多边形
        private void btnMovePolygon_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 6;
        }
        //描绘多边形
        private void btnSketchPolygon_Click(object sender, EventArgs e)
        {
            mMapOpStyle = 7;
        }
        //结束绘制多边形的Part
        private void btnEndPart_Click(object sender, EventArgs e)
        {
            //判断是否可以结束
            if (mSketchingShape.Last().Count < 3) return;
            //多于3点，往List中增加一个多点对象
            MyMapObjects.moPoints sPoints = new MyMapObjects.moPoints();
            mSketchingShape.Add(sPoints);
            //重绘
            moMap.RedrawTrackingShapes();
        }
        //结束描绘，多边形的输入结束；和Part的区别在于复合多边形（Polygon和Part不同）
        private void btnEndSketch_Click(object sender, EventArgs e)
        {
            //检验是否可以结束
            if(mSketchingShape.Last().Count>1 && mSketchingShape.Last().Count<3)
            {
                return;
            }
            //如果最后一个元素的点数是0，删除最后一个元素
            if (mSketchingShape.Last().Count == 0)
            {
                mSketchingShape.Remove(mSketchingShape.Last());
            }
            //如果用户的确输入了，则加入多边形图层
            if (mSketchingShape.Count > 0)
            {
                //查找一个多边形图层
                MyMapObjects.moMapLayer sLayer = GetPolygonLayer();
                if (sLayer != null)
                {
                    //定义复合多边形
                    MyMapObjects.moMultiPolygon sMultiPolygon = new MyMapObjects.moMultiPolygon();
                    sMultiPolygon.Parts.AddRange(mSketchingShape.ToArray());
                    sMultiPolygon.UpdateExtent();
                    //生成要素并加入图层
                    MyMapObjects.moFeature sFeature= sLayer.GetNewFeature();
                    sFeature.Geometry = sMultiPolygon;
                    sLayer.Features.Add(sFeature);
                    sLayer.UpdateExtent();
                }
            }
            //初始化描绘图形
            InitializeSketchingShape();
            //重绘
            moMap.RedrawMap();//不仅跟踪层发生了变化，要素也发生了变化
        }
        //编辑多边形
        private void btnEditPolygon_Click(object sender, EventArgs e)
        {
            //查找一个多边形图层
            MyMapObjects.moMapLayer sLayer = GetPolygonLayer();
            if (sLayer == null) return;
            //是否有且只有一个选择要素
            if(sLayer.SelectedFeatures.Count!=1)
            {
                return;
            }
            //复制
            MyMapObjects.moMultiPolygon sOriMultiPolygon = (MyMapObjects.moMultiPolygon)sLayer.SelectedFeatures.GetItem(0).Geometry;
            MyMapObjects.moMultiPolygon sDesMultiPolygon = sOriMultiPolygon.Clone();
            mEditingGeometry = sDesMultiPolygon;
            //设置操作类型
            mMapOpStyle = 8;
            //让地图控件重绘跟踪层
            moMap.RedrawTrackingShapes();
        }
        //结束编辑
        private void btnEndEdit_Click(object sender, EventArgs e)
        {
            //修改数据，此处不再编写
            //清除
            mEditingGeometry = null;
            moMap.RedrawMap();
        }
        #endregion

        #region 地图控件事件处理
        private void moMap_MouseDown(object sender, MouseEventArgs e)
        {
            if (mMapOpStyle == 1)//放大
            {
                OnZoomIn_MouseDown(e);
            }
            else if(mMapOpStyle==2)//缩小
            {

            }
            else if (mMapOpStyle == 3)//漫游
            {
                OnPan_MouseDown(e);
            }
            else if (mMapOpStyle == 4)//选择
            {
                OnSelect_MouseDown(e);
            }
            else if (mMapOpStyle == 5)//查询
            {
                OnIdentify_MouseDown(e);
            }
            else if (mMapOpStyle == 6)//移动图形
            {
                OnMoveShape_MouseDown(e);
            }
            else if (mMapOpStyle == 7)//描绘多边形
            {

            }
            else if(mMapOpStyle==8)//编辑多边形
            {

            }
        }
        private void OnMoveShape_MouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { return; }
            //查找一个多边形图层
            MyMapObjects.moMapLayer sLayer = GetPolygonLayer();
            if (sLayer == null) return;
            //判断是否有选中的要素
            int sSelFeatureCount = sLayer.SelectedFeatures.Count;
            if (sSelFeatureCount == 0) return;
            //复制图形
            mMovingGeometries.Clear();
            for(int i=0;i<sSelFeatureCount;i++)
            {
                MyMapObjects.moMultiPolygon sOriPolygon = 
                    (MyMapObjects.moMultiPolygon)sLayer.SelectedFeatures.GetItem(i).Geometry;
                MyMapObjects.moMultiPolygon sDesPolygon = sOriPolygon.Clone();
                mMovingGeometries.Add(sDesPolygon);
            }
            //设置变量
            mStartMouseLocation = e.Location;
            mIsMovingShapes = true;
        }
        private void OnIdentify_MouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mStartMouseLocation = e.Location;
                mIsInIdentify = true;
            }
        }
        private void OnSelect_MouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mStartMouseLocation = e.Location;
                mIsInSelect = true;
            }
        }
        private void OnPan_MouseDown(MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Left)
            {
                mStartMouseLocation = e.Location;
                mIsInPan = true;
            }
        }
        private void OnZoomIn_MouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mStartMouseLocation = e.Location;
                mIsInZoomIn = true;
            }
            else { }
        }

        private void moMap_MouseMove(object sender, MouseEventArgs e)
        {
            ShowCoordinates(e.Location);
            if (mMapOpStyle == 1)//放大
            {
                OnZoomIn_MouseMove(e);
            }
            else if (mMapOpStyle == 2)//缩小
            {

            }
            else if (mMapOpStyle == 3)//漫游
            {
                OnPan_MouseMove(e);
            }
            else if (mMapOpStyle == 4)//选择
            {
                OnSelect_MouseMove(e);
            }
            else if (mMapOpStyle == 5)//查询
            {
                OnIdentify_MouseMove(e);
            }
            else if (mMapOpStyle == 6)//移动图形
            {
                OnMoveShape_MouseMove(e);
            }
            else if (mMapOpStyle == 7)//描绘多边形
            {
                OnSketch_MouseMove(e);
            }
            else if (mMapOpStyle == 8)//编辑多边形
            {

            }
        }
        private void OnSketch_MouseMove(MouseEventArgs e)
        {
            MyMapObjects.moPoint sCurPoint = moMap.ToMapPoint(e.Location.X, e.Location.Y);
            MyMapObjects.moPoints sLastPart = mSketchingShape.Last();
            int sPointCount = sLastPart.Count;
            if (sPointCount == 0) { }//什么都不干
            else if (sPointCount == 1)
            {
                //只有一个顶点，则绘制一条橡皮筋
                moMap.Refresh();
                MyMapObjects.moPoint sFirstPoint = sLastPart.GetItem(0);
                MyMapObjects.moUserDrawingTool sDrawingTool = moMap.GetDrawingTool();
                sDrawingTool.DrawLine(sFirstPoint, sCurPoint, mElasticSymbol);
            }
            else
            {
                //两个或者多个顶点，则绘制两条橡皮筋
                moMap.Refresh();
                MyMapObjects.moPoint sFirstPoint = sLastPart.GetItem(0);
                MyMapObjects.moPoint sLastPoint = sLastPart.GetItem(sPointCount-1);
                MyMapObjects.moUserDrawingTool sDrawingTool = moMap.GetDrawingTool();
                sDrawingTool.DrawLine(sFirstPoint, sCurPoint, mElasticSymbol);
                sDrawingTool.DrawLine(sLastPoint, sCurPoint, mElasticSymbol);
            }
        }
        private void OnMoveShape_MouseMove(MouseEventArgs e)
        {
            if (mIsMovingShapes == false) return;
            //修改移动图形的坐标
            double sDeltaX = moMap.ToMapDistance(e.Location.X - mStartMouseLocation.X);
            double sDeltaY = moMap.ToMapDistance(mStartMouseLocation.Y - e.Location.Y);
            ModifyMovingGeometries(sDeltaX, sDeltaY);
            //绘制移动图形
            moMap.Refresh();
            DrawMovingShapes();
            //重新设置鼠标位置
            mStartMouseLocation = e.Location;

        }
        private void OnIdentify_MouseMove(MouseEventArgs e)
        {
            if(mIsInIdentify==false)
            { return; 
           }
            moMap.Refresh();
            MyMapObjects.moRectangle sRect = GetMapRectByTwoPoints(mStartMouseLocation, e.Location);
            MyMapObjects.moUserDrawingTool tool = moMap.GetDrawingTool();
            tool.DrawRectangle(sRect, mSelectingBoxSymbol);
        }
        private void OnSelect_MouseMove(MouseEventArgs e)
        {
            if (mIsInSelect == false) { return; }
            moMap.Refresh();
            MyMapObjects.moRectangle sRect = GetMapRectByTwoPoints(mStartMouseLocation, e.Location);
            MyMapObjects.moUserDrawingTool sDrawingTool = moMap.GetDrawingTool();
            sDrawingTool.DrawRectangle(sRect, mSelectingBoxSymbol);
            
        }
        private void OnPan_MouseMove(MouseEventArgs e)
        {
            if (mIsInPan == false) { return; }
            //mIsInPan = false;
            moMap.PanMapImageTo(e.Location.X - mStartMouseLocation.X, e.Location.Y - mStartMouseLocation.Y);
        }
        private void OnZoomIn_MouseMove(MouseEventArgs e)
        {
              if(mIsInZoomIn==false)
              {
                return;
              }
            //清除原有矩形
            moMap.Refresh();
            //绘制新的矩形
            MyMapObjects.moRectangle sRect = GetMapRectByTwoPoints(mStartMouseLocation, e.Location);
            MyMapObjects.moUserDrawingTool sDrawingTool = moMap.GetDrawingTool();
            sDrawingTool.DrawRectangle(sRect, mZoomBoxSymbol);
        }
        private void moMap_MouseUp(object sender, MouseEventArgs e)
        {
            if (mMapOpStyle == 1)//放大
            {
                OnZoomIn_MouseUp(e);
            }
            else if (mMapOpStyle == 2)//缩小
            {

            }
            else if (mMapOpStyle == 3)//漫游
            {
                OnPan_MouseUp(e);
            }
            else if (mMapOpStyle == 4)//选择
            {
                OnSelect_MouseUp(e);
            }
            else if (mMapOpStyle == 5)//查询
            {
                OnIdentify_MouseUp(e);
            }
            else if (mMapOpStyle == 6)//移动图形
            {
                OnMoveShape_MouseUp(e);
            }
            else if (mMapOpStyle == 7)//描绘多边形
            {

            }
            else if (mMapOpStyle == 8)//编辑多边形
            {

            }
        }
        private void OnMoveShape_MouseUp(MouseEventArgs e)
        {
            if (mIsMovingShapes == false) return;
            mIsMovingShapes = false;
            //做相应的修改数据的操作，不再编写
            //重绘地图
            moMap.RedrawMap();
            //清除移动图形集合
            mMovingGeometries.Clear();
        }
        private void OnIdentify_MouseUp(MouseEventArgs e)
        {
            if (mIsInIdentify == false)
            {
                return;
            }
            mIsInIdentify = false;
            moMap.Refresh();
            MyMapObjects.moRectangle sBox= GetMapRectByTwoPoints(mStartMouseLocation, e.Location);
            double tolerance = moMap.ToMapDistance(mSelectingTolorance);
            if(moMap.Layers.Count==0)//没有图层
            {
                return;
                
            }
            else { MyMapObjects.moMapLayer sLayer = moMap.Layers.GetItem(0);
                MyMapObjects.moFeatures sFeatures = sLayer.SearchByBox(sBox, tolerance);
                int sSelFeatureCount = sFeatures.Count;
                //本来也应该弹出属性窗口，此处不做了
                if(sSelFeatureCount>0)
                {
                    MyMapObjects.moGeometry[] sGeometries= new MyMapObjects.moGeometry[sSelFeatureCount];
                    for(int i=0;i<sSelFeatureCount;i++)
                    {
                        sGeometries[i] = sFeatures.GetItem(i).Geometry;
                    }
                    moMap.FlashShapes(sGeometries, 3, 800);//选中要素的闪烁
                }
            }
        }

        private void OnSelect_MouseUp(MouseEventArgs e)
        {
            if(mIsInSelect==false)
            {
                return;
            }
            mIsInSelect = false;
            MyMapObjects.moRectangle sBox = GetMapRectByTwoPoints(mStartMouseLocation, e.Location);
            double tolerance = moMap.ToMapDistance(mSelectingTolorance);
            moMap.SelectByBox(sBox, tolerance, 0);
            //重绘地图
            moMap.RedrawTrackingShapes();
        }
        private void OnPan_MouseUp(MouseEventArgs e)
        {
            if (mIsInPan == false) { return; }
            mIsInPan = false;
            double sDeltaX = moMap.ToMapDistance(e.Location.X - mStartMouseLocation.X);
            double sDeltaY= moMap.ToMapDistance(mStartMouseLocation.Y- e.Location.Y);
            moMap.PanDelta(sDeltaX, sDeltaY);
        }
        private void OnZoomIn_MouseUp(MouseEventArgs e)
        {
            if (mIsInZoomIn == false) { return; }
            mIsInZoomIn = false;
            if (mStartMouseLocation.X == e.Location.X && mStartMouseLocation.Y == e.Location.Y)
            {
                //单点放大
                MyMapObjects.moPoint sPoint = moMap.ToMapPoint(mStartMouseLocation.X, mStartMouseLocation.Y);
                moMap.ZoomByCenter(sPoint, mZoomRatioFixed);
            }
            else
            {
                //矩形放大
                MyMapObjects.moRectangle sBox = GetMapRectByTwoPoints(mStartMouseLocation, e.Location);
                moMap.ZoomToExtent(sBox);
            }
        }
        private void moMap_MouseClick(object sender, MouseEventArgs e)
        {
            if (mMapOpStyle == 1)//放大
            {
                //不需要写放大
            }
            else if (mMapOpStyle == 2)//缩小
            {
                OnZoomOut_MouseClick(e);
            }
            else if (mMapOpStyle == 3)//漫游
            {

            }
            else if (mMapOpStyle == 4)//选择
            {

            }
            else if (mMapOpStyle == 5)//查询
            {

            }
            else if (mMapOpStyle == 6)//移动图形
            {

            }
            else if (mMapOpStyle == 7)//描绘多边形
            {
                OnSketch_MouseClick(e);
            }
            else if (mMapOpStyle == 8)//编辑多边形
            {

            }
        }
        private void OnSketch_MouseClick(MouseEventArgs e)
        {
            //将屏幕坐标转换为地图坐标，并加入描绘图形
            MyMapObjects.moPoint sPoint = moMap.ToMapPoint(e.Location.X, e.Location.Y);
            mSketchingShape.Last().Add(sPoint);
            //地图控件重绘跟踪层
            moMap.RedrawTrackingShapes();

        }
        private void OnZoomOut_MouseClick(MouseEventArgs e)
        {
            //单点缩小
            MyMapObjects.moPoint sPoint = moMap.ToMapPoint(e.Location.X, e.Location.Y);
            moMap.ZoomByCenter(sPoint, 1 / mZoomRatioFixed);
        }
        private void MoMap_MouseWheel(object sender, MouseEventArgs e)
        {
            //计算地图控件中心点的地图坐标
            double sX = moMap.ClientRectangle.Width / 2;
            double sY = moMap.ClientRectangle.Height / 2;
            MyMapObjects.moPoint sPoint = moMap.ToMapPoint(sX, sY);
            if (e.Delta > 0)
            {
                moMap.ZoomByCenter(sPoint, mZoomRatioMouseWheel);
            }
            else
            {
                moMap.ZoomByCenter(sPoint, 1 / mZoomRatioMouseWheel);
            }
        }

        private void moMap_MapScaleChanged(object sender)
        {
            ShowMapScale();//更改比例尺
        }
        //绘制持久事件
        private void moMap_AfterTrackingLayerDraw(object sender, MyMapObjects.moUserDrawingTool drawTool)
        {
            DrawSketchingShapes(drawTool);
            DrawEditingShapes(drawTool);
        }
        #endregion

        #region 私有函数

        //初始化符号
        private void InitializeSymbols()
        {
            mSelectingBoxSymbol = new MyMapObjects.moSimpleFillSymbol();
            mSelectingBoxSymbol.Color = Color.Transparent;
            mSelectingBoxSymbol.Outline.Color = mSelectBoxColor;
            mSelectingBoxSymbol.Outline.Size = mSelectBoxWidth;
            mZoomBoxSymbol = new MyMapObjects.moSimpleFillSymbol();
            mZoomBoxSymbol.Color = Color.Transparent;
            mZoomBoxSymbol.Outline.Color = mZoomBoxColor;
            mZoomBoxSymbol.Outline.Size = mZoomBoxWidth;
            mMovingPolygonSymbol = new MyMapObjects.moSimpleFillSymbol();
            mMovingPolygonSymbol.Color = Color.Transparent;
            mMovingPolygonSymbol.Outline.Color = Color.Black;
            mEditingPolygonSymbol = new MyMapObjects.moSimpleFillSymbol();
            mEditingPolygonSymbol.Color = Color.Transparent;
            mEditingPolygonSymbol.Outline.Color = Color.DarkGreen;
            mEditingPolygonSymbol.Outline.Size = 0.53;
            mEditingVertexSymbol = new MyMapObjects.moSimpleMarkerSymbol();
            mEditingVertexSymbol.Color = Color.DarkGreen;
            mEditingVertexSymbol.Style = MyMapObjects.moSimpleMarkerSymbolStyleConstant.SolidSquare;
            mEditingVertexSymbol.Size = 2;
            mElasticSymbol = new MyMapObjects.moSimpleLineSymbol();
            mElasticSymbol.Color = Color.DarkGreen;
            mElasticSymbol.Size = 0.52;
            mElasticSymbol.Style = MyMapObjects.moSimpleLineSymbolStyleConstant.Dash;
        }

        //根据屏幕上的两点获得一个地图坐标下的矩形
        private MyMapObjects.moRectangle GetMapRectByTwoPoints(PointF point1, PointF point2)
        {
            MyMapObjects.moPoint sPoint1 = moMap.ToMapPoint(point1.X, point1.Y);
            MyMapObjects.moPoint sPoint2 = moMap.ToMapPoint(point2.X, point2.Y);
            double sMinX = Math.Min(sPoint1.X, sPoint2.X);
            double sMaxX = Math.Max(sPoint1.X, sPoint2.X);
            double sMinY = Math.Min(sPoint1.Y, sPoint2.Y);
            double sMaxY = Math.Max(sPoint1.Y, sPoint2.Y);
            MyMapObjects.moRectangle sRect = new MyMapObjects.moRectangle(sMinX, sMaxX, sMinY, sMaxY);
            return sRect;
        }

        //获取一个多边形图层
        private MyMapObjects.moMapLayer GetPolygonLayer()
        {
            Int32 sLayerCount = moMap.Layers.Count;
            MyMapObjects.moMapLayer sLayer = null;
            for (Int32 i = 0; i <= sLayerCount - 1; i++)
            {
                if (moMap.Layers.GetItem(i).ShapeType == MyMapObjects.moGeometryTypeConstant.MultiPolygon)
                {
                    sLayer = moMap.Layers.GetItem(i);
                    break;
                }
            }
            return sLayer;
        }

        //修改移动图形的坐标
        private void ModifyMovingGeometries(double deltaX, double deltaY)
        {
            Int32 sCount = mMovingGeometries.Count;
            for (Int32 i = 0; i <= sCount - 1; i++)
            {
                if (mMovingGeometries[i].GetType() == typeof(MyMapObjects.moMultiPolygon))
                {
                    MyMapObjects.moMultiPolygon sMultiPolygon = (MyMapObjects.moMultiPolygon)mMovingGeometries[i];
                    Int32 sPartCount = sMultiPolygon.Parts.Count;
                    for (Int32 j = 0; j <= sPartCount - 1; j++)
                    {
                        MyMapObjects.moPoints sPoints = sMultiPolygon.Parts.GetItem(j);
                        Int32 sPointCount = sPoints.Count;
                        for (Int32 k = 0; k <= sPointCount - 1; k++)
                        {
                            MyMapObjects.moPoint sPoint = sPoints.GetItem(k);
                            sPoint.X = sPoint.X + deltaX;
                            sPoint.Y = sPoint.Y + deltaY;
                        }
                    }
                    sMultiPolygon.UpdateExtent();
                }
            }
        }

        //绘制移动图形
        private void DrawMovingShapes()
        {
            MyMapObjects.moUserDrawingTool sDrawingTool = moMap.GetDrawingTool();
            Int32 sCount = mMovingGeometries.Count;
            for (Int32 i = 0; i <= sCount - 1; i++)
            {
                if (mMovingGeometries[i].GetType() == typeof(MyMapObjects.moMultiPolygon))
                {
                    MyMapObjects.moMultiPolygon sMultiPolygon = (MyMapObjects.moMultiPolygon)mMovingGeometries[i];
                    sDrawingTool.DrawMultiPolygon(sMultiPolygon, mMovingPolygonSymbol);
                }
            }
        }

        //绘制正在描绘的图形
        private void DrawSketchingShapes(MyMapObjects.moUserDrawingTool drawingTool)
        {
            if (mSketchingShape == null)
                return;
            Int32 sPartCount = mSketchingShape.Count;
            //绘制已经描绘完成的部分
            for (Int32 i = 0; i <= sPartCount - 2; i++)
            {
                drawingTool.DrawPolygon(mSketchingShape[i], mEditingPolygonSymbol);
            }
            //正在描绘的部分（只有一个Part）
            MyMapObjects.moPoints sLastPart = mSketchingShape.Last();
            if (sLastPart.Count >= 2)
                drawingTool.DrawPolyline(sLastPart, mEditingPolygonSymbol.Outline);
            //绘制所有顶点手柄
            for (Int32 i = 0; i <= sPartCount - 1; i++)
            {
                MyMapObjects.moPoints sPoints = mSketchingShape[i];
                drawingTool.DrawPoints(sPoints, mEditingVertexSymbol);
            }
        }

        //绘制正在编辑的图形
        private void DrawEditingShapes(MyMapObjects.moUserDrawingTool drawingTool)
        {
            if (mEditingGeometry == null)
                return;
            if (mEditingGeometry.GetType() == typeof(MyMapObjects.moMultiPolygon))
            {
                MyMapObjects.moMultiPolygon sMultiPolygon = (MyMapObjects.moMultiPolygon)mEditingGeometry;
                //绘制边界
                drawingTool.DrawMultiPolygon(sMultiPolygon, mEditingPolygonSymbol);
                //绘制顶点手柄
                Int32 sPartCount = sMultiPolygon.Parts.Count;
                for (Int32 i = 0; i <= sPartCount - 1; i++)
                {
                    MyMapObjects.moPoints sPoints = sMultiPolygon.Parts.GetItem(i);
                    drawingTool.DrawPoints(sPoints, mEditingVertexSymbol);
                }
            }
        }

        //初始化描绘图形
        private void InitializeSketchingShape()
        {
            mSketchingShape = new List<MyMapObjects.moPoints>();
            MyMapObjects.moPoints sPoints = new MyMapObjects.moPoints();
            mSketchingShape.Add(sPoints);
        }

        //根据屏幕坐标显示地图坐标
        private void ShowCoordinates(PointF point)
        {
            MyMapObjects.moPoint sPoint = moMap.ToMapPoint(point.X, point.Y);
            double sX = Math.Round(sPoint.X, 2);
            double sY = Math.Round(sPoint.Y, 2);
            tssCoordinate.Text = "X:" + sX.ToString() + ", Y:" + sY.ToString();
        }

        //显示比例尺
        private void ShowMapScale()
        {
            tssMapScale.Text = "1 :" + moMap.MapScale.ToString("0.00");

        }

        #endregion

        #region 葆茵添加的
        private void readShp_Click(object sender, EventArgs e)
        {
            openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "shapefiles(*.shp)|*.shp|All files(*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            MyMapObjects.moFields sFields = new MyMapObjects.moFields();
            MyMapObjects.moMapLayer shapeFile = new MyMapObjects.moMapLayer();
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                char[] path = openFileDialog1.FileName.ToCharArray();
                if (path.Length != 0)
                {
                    path[path.Length - 1] = 'f';
                    path[path.Length - 2] = 'b';
                    path[path.Length - 3] = 'd';
                    openFileDialog1.FileName = new string(path);
                    sFields = ReadDBFFile(openFileDialog1);
                }
                if (path.Length != 0)
                {
                    path[path.Length - 1] = 'p';
                    path[path.Length - 2] = 'h';
                    path[path.Length - 3] = 's';
                    openFileDialog1.FileName = new string(path);
                    shapeFile = ReadSHPFile(openFileDialog1, sFields);
                    shapeFile.UpdateExtent();
                }
                moMap.Layers.Add(shapeFile);
                if (moMap.Layers.Count == 1)
                {
                    moMap.FullExtent();
                }
                else
                {
                    moMap.RedrawMap();
                }
                panel1.Refresh();
            }
        }

        private MyMapObjects.moFields ReadDBFFile(OpenFileDialog openFileDialog)
        {
            MyMapObjects.moFields sFields = new MyMapObjects.moFields();
            table.dt.Columns.Clear();
            table.dt.Rows.Clear();
            table.columnsName.Clear();
            table.columnsLength.Clear();
            BinaryReader br = new BinaryReader(openFileDialog.OpenFile());
            _ = br.ReadByte();//Current version information
            _ = br.ReadBytes(3);//Last update date
            table.rowCount = br.ReadInt32();//Number of records in the file
            table.columnsCount = (br.ReadInt16() - 33) / 32;//Calculate how many columns or record items there are based on the number of bytes in the file header (how to count, please see the above .dbf format)
            _ = br.ReadInt16();//The length of bytes in a record
            _ = br.ReadBytes(20);//System reserved

            for (int i = 0; i < table.columnsCount; i++)//Read record items
            {
                string name = System.Text.Encoding.Default.GetString(br.ReadBytes(10));//The name of the record item, some say 11 bytes and some say 10 bytes, here are all correct, if some people are incorrect, please change it, so that 5 bytes are discarded below
                table.dt.Columns.Add(new DataColumn(name, typeof(string)));//Add column names to the table
                table.columnsName.Add(name);//Record the following name
                                            //葆茵补充的：
                MyMapObjects.moField sField = new MyMapObjects.moField(name);
                sFields.Append(sField);
                //
                _ = br.ReadBytes(6);//If the above is 11 bytes, here is 5 bytes
                table.columnsLength.Add(br.ReadByte());//Record the length of your data
                _ = br.ReadBytes(15);//This contains precision, which can make the data look better, I am lazy and useless hhh
            }
            //ok: MessageBox.Show("hello_111: " + table.columnsCount.ToString());
            _ = br.ReadBytes(1);//Terminator 0x0D
            for (int i = 0; i < table.rowCount; i++)//Read content
            {
                _ = br.ReadByte();//Placeholder
                DataRow dr;//One line
                dr = table.dt.NewRow();
                for (int j = 0; j < table.columnsCount; j++)//Every item in every row (that's every column)
                {
                    string temp = System.Text.Encoding.Default.GetString(br.ReadBytes(table.columnsLength[j]));//Read the content of this item according to the length of each item in a line. The transcoding is relatively simple to call here. It is said that it is ASCII code but there are Chinese characters, so the Default is used here, and the ASCII Chinese characters will be garbled
                    if (j == 0) table.ID.Add(temp);//Record the first data of each row here to mark it on the map
                    dr[(string)table.columnsName[j]] = temp;//The parameter in square brackets is a string value, which is the name of the column header, and each item is filled in by the name of the column header
                }
                table.dt.Rows.Add(dr);//Add rows to the table
            }

            return sFields;

        }

        private Tuple<MyMapObjects.moGeometryTypeConstant, MyMapObjects.moGeometry> getGeometry(Int32 ShapeType, BinaryReader br)
        {
            MyMapObjects.moGeometryTypeConstant sGeometryType = new MyMapObjects.moGeometryTypeConstant();
            Tuple<MyMapObjects.moGeometryTypeConstant, MyMapObjects.moGeometry> retValue;
            if (ShapeType == 1)
            {
                MyMapObjects.moGeometry sGeometry_p = getPoints(br);
                sGeometryType = MyMapObjects.moGeometryTypeConstant.Point;
                retValue = new Tuple<MyMapObjects.moGeometryTypeConstant, MyMapObjects.moGeometry>(sGeometryType, sGeometry_p);
                return retValue;
            }
            else if (ShapeType == 3)
            {
                MyMapObjects.moGeometry sGeometry_l = getMultiPolyline(br);
                sGeometryType = MyMapObjects.moGeometryTypeConstant.MultiPolyline;
                retValue = new Tuple<MyMapObjects.moGeometryTypeConstant, MyMapObjects.moGeometry>(sGeometryType, sGeometry_l);
                return retValue;
            }
            else//5
            {
                MyMapObjects.moGeometry sGeometry_g = getMultiPolygon(br);
                sGeometryType = MyMapObjects.moGeometryTypeConstant.MultiPolygon;
                retValue = new Tuple<MyMapObjects.moGeometryTypeConstant, MyMapObjects.moGeometry>(sGeometryType, sGeometry_g);
                return retValue;
            }
        }

        private MyMapObjects.moMapLayer ReadSHPFile(OpenFileDialog openFileDialog, MyMapObjects.moFields sFields)
        {

            //Read the main file header
            BinaryReader br = new BinaryReader(openFileDialog.OpenFile());
            _ = br.ReadBytes(24);//File number and unused fields
            _ = br.ReadInt32();//File length
            _ = br.ReadInt32();//version
            Int32 ShapeType = br.ReadInt32();//Storage type number
            #region 读取占位符
            _ = br.ReadDouble();//The following is the maximum and minimum xy of the stored map
                                //if (xmin == 0 || temp < xmin)//Because multiple graphs are superimposed, it is necessary to find the largest xy and smallest xy
                                //    xmin = temp;//There is also the difference in the coordinate system. The origin of the coordinate system of the shp file is in the lower left corner of the x axis and the right y axis
            _ = -br.ReadDouble();//C# in the coordinate axis is the origin in the upper left corner x axis right y axis down
                                 //if (ymax == 0 || temp > ymax)//So the second data should have the smallest y, and a minus sign here is the largest y
                                 //    ymax = temp;//Draw a coordinate axis, it is more troublesome to talk about it, but all the y values ​​are transformed to the second coordinate system with a minus sign
            _ = br.ReadDouble();

            _ = -br.ReadDouble();

            _ = br.ReadBytes(32);//Z, M range
                                 //Read the main file record content
            #endregion

            //1. moFeatures
            MyMapObjects.moFeatures sFeatures = new MyMapObjects.moFeatures();
            Tuple<MyMapObjects.moGeometryTypeConstant, MyMapObjects.moGeometry> tempTuple = getGeometry(ShapeType, br);
            MyMapObjects.moGeometryTypeConstant sGeometryType = tempTuple.Item1;
            MyMapObjects.moGeometry sGeometry = tempTuple.Item2;
            MyMapObjects.moAttributes sAttributes = LoadAttributes(sFields,0);
            MyMapObjects.moFeature sFeature = new MyMapObjects.moFeature(sGeometryType, sGeometry, sAttributes);
            sFeatures.Add(sFeature);
            
            for (Int32 i = 1; i < table.rowCount; ++i)
            {
                tempTuple = getGeometry(ShapeType, br);
                sGeometryType = tempTuple.Item1;
                sGeometry = tempTuple.Item2;
                sAttributes = LoadAttributes(sFields,i);
                sFeature = new MyMapObjects.moFeature(sGeometryType, sGeometry, sAttributes);
                sFeatures.Add(sFeature);
            }
            //2. moMapLayer
            MyMapObjects.moMapLayer sMapLayer = new MyMapObjects.moMapLayer("", tempTuple.Item1, sFields);//目标
            sMapLayer.Features = sFeatures;
            return sMapLayer;
        }

        private MyMapObjects.moAttributes LoadAttributes(MyMapObjects.moFields fields,int j)
        {
            Int32 sFieldCount = fields.Count;
            MyMapObjects.moAttributes sAttributes = new MyMapObjects.moAttributes();
            //按照列顺序进行的
            for (Int32 i = 0; i < sFieldCount; i++)
            {
                //当前列（字段)
                MyMapObjects.moField sField = fields.GetItem(i);
                object sValue = LoadValue(sField.ValueType, table.dt.Rows[j].ItemArray[i]);
                sAttributes.Append(sValue);
            }
            return sAttributes;
        }

        private static object LoadValue(MyMapObjects.moValueTypeConstant valueType, object currentValue)
        {
            return currentValue;
            /*
            if (valueType == MyMapObjects.moValueTypeConstant.dint16)
            {
                Int16 sValue = sr.ReadInt16();
                return sValue;
            }
            else if (valueType == MyMapObjects.moValueTypeConstant.dint32)
            {
                Int32 sValue = sr.ReadInt32();
                return sValue;
            }
            else if (valueType == MyMapObjects.moValueTypeConstant.dint64)
            {
                Int64 sValue = sr.ReadInt64();
                return sValue;
            }
            else if (valueType == MyMapObjects.moValueTypeConstant.dSingle)
            {
                float sValue = sr.ReadSingle();
                return sValue;
            }
            else if (valueType == MyMapObjects.moValueTypeConstant.dDouble)
            {
                double sValue = sr.ReadDouble();
                return sValue;
            }
            else if (valueType == MyMapObjects.moValueTypeConstant.dText)
            {
                string sValue = sr.ReadString();
                return sValue;
            }
            else
            {
                return null;
            }
            */
        }

        private MyMapObjects.moGeometry getPoints(BinaryReader br)
        {
            MyMapObjects.moPoints smoPoints = new MyMapObjects.moPoints();
            while (br.PeekChar() != -1)//Look at the explanation of this function to know
            {
                _ = br.ReadInt32();//Record number
                _ = br.ReadInt32();//Record content length
                _ = br.ReadInt32();//Graphic type number of record content header
                double Xcoord = br.ReadDouble();
                double Ycoord = br.ReadDouble();//Y values ​​are all plus a minus sign
                MyMapObjects.moPoint point = new MyMapObjects.moPoint(Xcoord, Ycoord);
                smoPoints.Add(point);//Storage point
            }
            return smoPoints.GetItem(0);
        }

        private MyMapObjects.moGeometry getMultiPolyline(BinaryReader br)
        {
            MyMapObjects.moMultiPolyline retMultiPolyline = new MyMapObjects.moMultiPolyline();
            _ = br.ReadInt32();//Record number
            _ = br.ReadInt32();//Record content length
            _ = br.ReadInt32();//Graphic type number of record content header
                               //Polyline polyline = new Polyline();
            _ = br.ReadDouble();//Record the maximum and minimum xy of each line
            _ = -br.ReadDouble();//This is Box[3]
            _ = br.ReadDouble();//0, 1, 2, 3 correspond to the following
            _ = -br.ReadDouble();//xmin，ymin，xmax，ymax
            double NumParts = br.ReadInt32();
            //MessageBox.Show("NumParts:" + NumParts.ToString());
            double NumPoints = br.ReadInt32();
            for (int i = 0; i < NumParts; i++)
            {
                _ = br.ReadInt32();//Record the value of each part, it may be a bit difficult to understand the concept of part, let's look at the line drawing function
            }
            MyMapObjects.moPoints sPoints = new MyMapObjects.moPoints();
            for (int j = 0; j < NumPoints; j++)
            {
                MyMapObjects.moPoint point = new MyMapObjects.moPoint(0, 0);
                point.X = br.ReadDouble();
                point.Y = br.ReadDouble();//Also add a minus sign
                sPoints.Add(point);//store
            }
            retMultiPolyline.Parts.Add(sPoints);//store
            retMultiPolyline.UpdateExtent();
            return retMultiPolyline;
        }

        private MyMapObjects.moGeometry getMultiPolygon(BinaryReader br)
        {

            MyMapObjects.moMultiPolygon retMultiPolygon = new MyMapObjects.moMultiPolygon();
            //The records are the same as the above line
            _ = br.ReadInt32();//Record number
            _ = br.ReadInt32();//Record content length
            _ = br.ReadInt32();//Graphic type number of record content header
            _ = br.ReadDouble();
            _ = -br.ReadDouble();
            _ = br.ReadDouble();
            _ = -br.ReadDouble();

            double NumParts = br.ReadInt32();

            double NumPoints = br.ReadInt32();
            List<int> newStart = new List<int>();
            for (int i = 0; i < NumParts; i++)
            {
                newStart.Add(br.ReadInt32());//Record the value of each part, it may be a bit difficult to understand the concept of part, let's look at the line drawing function
            }
            for (int i = 0; i < newStart.Count; ++i)
            {
            }
            for (int i = 0; i < NumParts; ++i)
            {
                MyMapObjects.moPoints sPoints = new MyMapObjects.moPoints();
                if (i == NumParts - 1)
                {
                    for (int j = newStart[i]; j < NumPoints; ++j)
                    {
                        MyMapObjects.moPoint point = new MyMapObjects.moPoint(0, 0);
                        point.X = br.ReadDouble();
                        point.Y = br.ReadDouble();
                        sPoints.Add(point);
                    }
                }
                else
                {
                    for (int j = newStart[i]; j < newStart[i + 1]; j++)
                    {
                        MyMapObjects.moPoint point = new MyMapObjects.moPoint(0, 0);
                        point.X = br.ReadDouble();
                        point.Y = br.ReadDouble();
                        sPoints.Add(point);
                    }

                }
                retMultiPolygon.Parts.Add(sPoints);
            }

            retMultiPolygon.UpdateExtent();
            return retMultiPolygon;
        }

        #endregion
    }
}
