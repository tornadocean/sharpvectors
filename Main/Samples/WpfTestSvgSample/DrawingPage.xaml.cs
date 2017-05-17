using System;
using System.IO;
using System.Collections.Generic;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

using SharpVectors.Runtime;
using SharpVectors.Renderers;
using SharpVectors.Renderers.Wpf;
using SharpVectors.Converters;

namespace WpfTestSvgSample
{
    /// <summary>
    /// Interaction logic for DrawingPage.xaml
    /// </summary>
    public partial class DrawingPage : Page
    {
        #region Private Fields

        private bool _saveXaml;

        private string _drawingDir;
        private DirectoryInfo _directoryInfo;

        private FileSvgReader _fileReader;
        private WpfDrawingSettings _wpfSettings;

        private DirectoryInfo _workingDir;

        /// <summary>
        /// Specifies the current state of the mouse handling logic.
        /// </summary>
        private MouseHandlingMode mouseHandlingMode;

        /// <summary>
        /// The point that was clicked relative to the ZoomAndPanControl.
        /// </summary>
        private Point origZoomAndPanControlMouseDownPoint;

        /// <summary>
        /// The point that was clicked relative to the content that is contained within the ZoomAndPanControl.
        /// </summary>
        private Point origContentMouseDownPoint;

        /// <summary>
        /// Records which mouse button clicked during mouse dragging.
        /// </summary>
        private MouseButton mouseButtonDown;

        #endregion

        #region Constructors and Destructor

        public DrawingPage()
        {
            InitializeComponent();

            _saveXaml            = true;
            _wpfSettings         = new WpfDrawingSettings();
            _wpfSettings.CultureInfo = _wpfSettings.NeutralCultureInfo;

            _fileReader          = new FileSvgReader(_wpfSettings);
            _fileReader.SaveXaml = _saveXaml;
            _fileReader.SaveZaml = false;

            mouseHandlingMode = MouseHandlingMode.None;

            string workDir = Path.Combine(Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location), "XamlDrawings");

            _workingDir = new DirectoryInfo(workDir);

            this.Loaded += new RoutedEventHandler(OnPageLoaded);
        }

        #endregion      

        #region Public Properties

        public string XamlDrawingDir
        {
            get 
            { 
                return _drawingDir; 
            }
            set 
            { 
                _drawingDir = value; 

                if (!String.IsNullOrEmpty(_drawingDir))
                {
                    _directoryInfo = new DirectoryInfo(_drawingDir);

                    if (_fileReader != null)
                    {
                        _fileReader.SaveXaml = Directory.Exists(_drawingDir);
                    }
                }
            }
        }

        public bool SaveXaml
        {
            get
            {
                return _saveXaml;
            }
            set
            {
                _saveXaml = value;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 根据类型查找子元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="typename"></param>
        /// <returns></returns>
        public List<T> GetChildObjects<T>(DependencyObject obj, Type typename) where T : Drawing
        {
            DependencyObject child = null;
            List<T> childList = new List<T>();

            for (int i = 0; i <= VisualTreeHelper.GetChildrenCount(obj) - 1; i++)
            {
                child = VisualTreeHelper.GetChild(obj, i);

                if (child is T && (((T)child).GetType() == typename))
                {
                    childList.Add((T)child);
                }
                childList.AddRange(GetChildObjects<T>(child, typename));
            }
            return childList;
        }

        /// <summary>
        /// 查找父元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T FindParent<T>(DependencyObject i_dp) where T : DependencyObject
        {
            DependencyObject dobj = (DependencyObject)VisualTreeHelper.GetParent(i_dp);
            if (dobj != null)
            {
                if (dobj is T)
                {
                    return (T)dobj;
                }
                else
                {
                    dobj = FindParent<T>(dobj);
                    if (dobj != null && dobj is T)
                    {
                        return (T)dobj;
                    }
                }
            }
            return null;
        }

        public Drawing FindChildObject(Drawing d, string name)
        {
            var dg = d as DrawingGroup;
            if (dg == null)
                return null;

            foreach(Drawing item in dg.Children)
            {
                var gName = item.GetValue(FrameworkElement.NameProperty);
                if(name.CompareTo(gName.ToString()) == 0)
                {
                    return item;
                }
                else
                {
                    var itemSub =  FindChildObject(item, name);
                    if(itemSub != null)
                    {
                        return itemSub;
                    }
                }
            }

            return null;
        }

        private System.Timers.Timer tmBink = new System.Timers.Timer();

        private void Settimer()
        {
            tmBink.Interval = 500;
            tmBink.Elapsed += TmBink_Elapsed;
            tmBink.Start();
        }

        private Drawing drValve = null;
        private Drawing drText = null;
        private int nBlink = 0;
        private void TmBink_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {

            var dgr = drValve as GeometryDrawing;
            if (dgr == null)
                return;

            dgr.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (nBlink++ % 2 == 0)
                {
                    dgr.Brush = Brushes.Blue;
                }
                else
                {
                    dgr.Brush = Brushes.Red;
                }

                try
                {
                    var drt = drText as GlyphRunDrawing;
                   // drt.GlyphRun = new GlyphRun();
                    
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                }
            }));

          
           
        }

        static public T DeepCopyDrawing<T>(T imageItem) where T : Drawing
        {
            var stream = new System.IO.MemoryStream();
            System.Windows.Markup.XamlWriter.Save(imageItem, stream);
            stream.Seek(0, SeekOrigin.Begin);
            return (T)System.Windows.Markup.XamlReader.Load(stream);
        }

        public bool LoadDocument(string svgFilePath)
        {
            if (String.IsNullOrEmpty(svgFilePath) || !File.Exists(svgFilePath))
            {
                return false;
            }

            DirectoryInfo workingDir = _workingDir;
            if (_directoryInfo != null)
            {
                workingDir = _directoryInfo;
            }

           //double currentZoom = zoomSlider.Value;

            svgViewer.UnloadDiagrams();

            //zoomSlider.Value = 1.0;

            string fileExt = Path.GetExtension(svgFilePath);

            if (String.Equals(fileExt, ".svgz", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(fileExt, ".svg", StringComparison.OrdinalIgnoreCase))
            {
                if (_fileReader != null)
                {
                    _fileReader.SaveXaml = _saveXaml;
                    _fileReader.SaveZaml = false;

                    DrawingGroup drawing = _fileReader.Read(svgFilePath, workingDir);
                    //SharpVectors.Dom.Svg.SvgDocument sdoc = new SharpVectors.Dom.Svg.SvgDocument();
                    var child = drawing.Children[0] as DrawingGroup;
                    //gd.SetValue(FrameworkElement.NameProperty, "gd1");
                    var name = child.GetValue(FrameworkElement.NameProperty);
                    //var subchild = child.Children[0] as DrawingGroup;
                    //var val1 = subchild.Children[0];

                   
                    
                    if (drawing != null)
                    {
                        try
                        {
                            var dgFind = FindChildObject(drawing, "shape10_4");
                            if (dgFind != null)
                            {
                                var dg = dgFind as DrawingGroup;
                                var gdr = dg.Children[0] as GeometryDrawing;
                                gdr.Brush = Brushes.Blue;
                                drValve = gdr;
                                Settimer();
                            }

                            dgFind = FindChildObject(drawing, "shape33_39");
                            if(dgFind != null)
                            {
                                var dg = dgFind as DrawingGroup;
                                var dgr = dg.Children[1] as DrawingGroup;
                                var dgr2 = dgr.Children[0] as DrawingGroup;
                                var dgr3 = dgr2.Children[0] as GlyphRunDrawing;
                                //var drun = dgr3.GlyphRun;
                                //var drun = dgr3.GlyphRun;
                                //var newRun = new GlyphRun(
                                //    drun.GlyphTypeface,
                                //    drun.BidiLevel,
                                //    drun.IsSideways,
                                //    drun.FontRenderingEmSize,
                                //   new ushort[] { 43, 72, 79, 79, 82, 3, 58, 82, 85, 79, 71 },
                                //    drun.BaselineOrigin,
                                //    new double[]{
                                //        9.62666666666667, 7.41333333333333, 2.96,
                                //        2.96, 7.41333333333333, 3.70666666666667,
                                //        12.5866666666667, 7.41333333333333,
                                //        4.44, 2.96, 7.41333333333333},
                                //    null, 
                                //    null,
                                //    null, 
                                //    null, 
                                //    null, 
                                //    null);

                                //GlyphRun theGlyphRun = new GlyphRun(
                                //    drun.GlyphTypeface,
                                //    0,
                                //    false,
                                //    13.333333333333334,
                                //    new ushort[] { 43, 72, 79, 79, 82, 3, 58, 82, 85, 79, 71 },
                                //    new Point(0, 12.29),
                                //    new double[]{
                                //        9.62666666666667, 7.41333333333333, 2.96,
                                //        2.96, 7.41333333333333, 3.70666666666667,
                                //        12.5866666666667, 7.41333333333333,
                                //        4.44, 2.96, 7.41333333333333},
                                //    null,
                                //    null,
                                //    null,
                                //    null,
                                //    null,
                                //    null
                                //    );

                                //dgr3.GlyphRun = newRun;
                                //dgr3.GlyphRun.Characters = "test".ToCharArray();
                            }
                        }
                        catch(Exception ex)
                        {
                            var msg = ex.Message;
                        }
              
                        //var listChilds = GetChildObjects<DrawingGroup>(drawing, typeof(DrawingGroup));

                        svgViewer.RenderDiagrams(drawing);

                        //zoomSlider.Value = currentZoom;

                        Rect bounds = svgViewer.Bounds;

                        //Rect rect = new Rect(0, 0,
                        //    mainFrame.RenderSize.Width, mainFrame.RenderSize.Height);
                        //Rect rect = new Rect(0, 0,
                        //    bounds.Width, bounds.Height);
                        if (bounds.IsEmpty)
                        {
                            bounds = new Rect(0, 0, 
                                canvasScroller.ActualWidth, canvasScroller.ActualHeight);
                        }
                        zoomPanControl.AnimatedZoomTo(bounds);

                        return true;
                    }
                }
            }
            else if (String.Equals(fileExt, ".xaml", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(fileExt, ".zaml", StringComparison.OrdinalIgnoreCase))
            {
                svgViewer.LoadDiagrams(svgFilePath);

                //zoomSlider.Value = currentZoom;

                svgViewer.InvalidateMeasure();

                return true;
            }

            return false;
        }

        public void UnloadDocument()
        {
            if (svgViewer != null)
            {
                svgViewer.UnloadDiagrams();
            }
        }

        public bool SaveDocument(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (_fileReader == null || _fileReader.Drawing == null)
            {
                return false;
            }

            return _fileReader.Save(fileName, true, false);
        }

        public void PageSelected(bool isSelected)
        {   
        }

        #endregion

        #region Protected Methods

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }

        #endregion

        #region Private Event Handlers

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            zoomSlider.Value = 100;

            if (zoomPanControl != null)
            {
                zoomPanControl.IsMouseWheelScrollingEnabled = true;
            }
        }

        private void OnZoomSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (zoomPanControl != null)
            {
                zoomPanControl.AnimatedZoomTo(zoomSlider.Value / 100.0);
            }
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            this.ZoomIn();
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            this.ZoomOut();
        }

        private void OnResetZoom(object sender, RoutedEventArgs e)
        {
            if (zoomPanControl == null)
            {
                return;
            }

            zoomPanControl.ContentScale = 1.0;
        }

        /// <summary>
        /// The 'ZoomIn' command (bound to the plus key) was executed.
        /// </summary>
        private void OnZoomFitClick(object sender, RoutedEventArgs e)
        {
            if (svgViewer == null || zoomPanControl == null)
            {
                return;
            }

            Rect bounds = svgViewer.Bounds;

            //Rect rect = new Rect(0, 0,
            //    mainFrame.RenderSize.Width, mainFrame.RenderSize.Height);
            //Rect rect = new Rect(0, 0,
            //    bounds.Width, bounds.Height);
            if (bounds.IsEmpty)
            {
                bounds = new Rect(0, 0,
                    canvasScroller.ActualWidth, canvasScroller.ActualHeight);
            }
            zoomPanControl.AnimatedZoomTo(bounds);
        }

        private void OnPanClick(object sender, RoutedEventArgs e)
        {
            //if (drawScrollView == null)
            //{
            //    return;
            //}

            //drawScrollView.ZoomableCanvas.IsPanning = 
            //    (tbbPanning.IsChecked != null && tbbPanning.IsChecked.Value);
        }

        #region Private Zoom Panel Handlers

        /// <summary>
        /// Event raised on mouse down in the ZoomAndPanControl.
        /// </summary>
        private void OnZoomPanMouseDown(object sender, MouseButtonEventArgs e)
        {
            svgViewer.Focus();
            Keyboard.Focus(svgViewer);

            mouseButtonDown = e.ChangedButton;
            origZoomAndPanControlMouseDownPoint = e.GetPosition(zoomPanControl);
            origContentMouseDownPoint = e.GetPosition(svgViewer);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
                (e.ChangedButton == MouseButton.Left ||
                 e.ChangedButton == MouseButton.Right))
            {
                // Shift + left- or right-down initiates zooming mode.
                mouseHandlingMode = MouseHandlingMode.Zooming;
            }
            else if (mouseButtonDown == MouseButton.Left)
            {
                // Just a plain old left-down initiates panning mode.
                mouseHandlingMode = MouseHandlingMode.Panning;
            }

            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                // Capture the mouse so that we eventually receive the mouse up event.
                zoomPanControl.CaptureMouse();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised on mouse up in the ZoomAndPanControl.
        /// </summary>
        private void OnZoomPanMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseHandlingMode != MouseHandlingMode.None)
            {
                if (mouseHandlingMode == MouseHandlingMode.Zooming)
                {
                    if (mouseButtonDown == MouseButton.Left)
                    {
                        // Shift + left-click zooms in on the content.
                        ZoomIn();
                    }
                    else if (mouseButtonDown == MouseButton.Right)
                    {
                        // Shift + left-click zooms out from the content.
                        ZoomOut();
                    }
                }

                zoomPanControl.ReleaseMouseCapture();
                mouseHandlingMode = MouseHandlingMode.None;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised on mouse move in the ZoomAndPanControl.
        /// </summary>
        private void OnZoomPanMouseMove(object sender, MouseEventArgs e)
        {
            if (mouseHandlingMode == MouseHandlingMode.Panning)
            {
                //
                // The user is left-dragging the mouse.
                // Pan the viewport by the appropriate amount.
                //
                Point curContentMousePoint = e.GetPosition(svgViewer);
                Vector dragOffset = curContentMousePoint - origContentMouseDownPoint;

                zoomPanControl.ContentOffsetX -= dragOffset.X;
                zoomPanControl.ContentOffsetY -= dragOffset.Y;

                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised by rotating the mouse wheel
        /// </summary>
        private void OnZoomPanMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (e.Delta > 0)
            {
                ZoomIn();
            }
            else if (e.Delta < 0)
            {
                ZoomOut();
            }
        }

        /// <summary>
        /// The 'ZoomIn' command (bound to the plus key) was executed.
        /// </summary>
        private void OnZoomFit(object sender, RoutedEventArgs e)
        {
            if (svgViewer == null || zoomPanControl == null)
            {
                return;
            }

            Rect bounds = svgViewer.Bounds;

            //Rect rect = new Rect(0, 0,
            //    mainFrame.RenderSize.Width, mainFrame.RenderSize.Height);
            //Rect rect = new Rect(0, 0,
            //    bounds.Width, bounds.Height);
            if (bounds.IsEmpty)
            {
                bounds = new Rect(0, 0,
                    canvasScroller.ActualWidth, canvasScroller.ActualHeight);
            }
            zoomPanControl.AnimatedZoomTo(bounds);
        }

        /// <summary>
        /// The 'ZoomIn' command (bound to the plus key) was executed.
        /// </summary>
        private void OnZoomIn(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        /// <summary>
        /// The 'ZoomOut' command (bound to the minus key) was executed.
        /// </summary>
        private void OnZoomOut(object sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        /// <summary>
        /// Zoom the viewport out by a small increment.
        /// </summary>
        private void ZoomOut()
        {
            if (zoomPanControl == null)
            {
                return;
            }

            zoomPanControl.ContentScale -= 0.1;
        }

        /// <summary>
        /// Zoom the viewport in by a small increment.
        /// </summary>
        private void ZoomIn()
        {
            if (zoomPanControl == null)
            {
                return;
            }

            zoomPanControl.ContentScale += 0.1;
        }

        #endregion

        #endregion
    }
}
