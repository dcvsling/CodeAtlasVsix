﻿using Microsoft.Msagl.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Interaction logic for CodeView.xaml.
    /// </summary>
    [ProvideToolboxControl("CodeAtlasVSIX.CodeView", true)]
    public partial class CodeView : Canvas
    {
        public double scaleValue = 1.0;
        public double m_lastMoveOffset = 0.0;
        DispatcherOperation m_moveState;
        public bool m_isMouseDown = false;
        Point m_backgroundMovePos = new Point();
        DateTime m_mouseMoveTime = new DateTime();
        public bool m_isMouseInView = true;

        public CodeView()
        {
            InitializeComponent();
            var scene = UIManager.Instance().GetScene();
            scene.View = this;

            ScaleCanvas(0.94, new Point());
        }

        private void canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var element = this.canvas as UIElement;
            var position = e.GetPosition(element);
            var scale = e.Delta >= 0 ? 1.1 : (1.0 / 1.1); // choose appropriate scaling factor
            ScaleCanvas(scale, position);
        }

        void ScaleCanvas(double scale, Point position)
        {
            var element = this.canvas as UIElement;
            var transform = element.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;

            matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);
            transform.Matrix = matrix;
        }

        public void MoveView(Point center)
        {
            bool shouldMove = !m_isMouseInView || (DateTime.Now - m_mouseMoveTime).TotalSeconds > 5;
            if (!shouldMove)
            {
                return;
            }

            m_moveState = Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                var transform = this.canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;

                var centerPnt = new Point(ActualWidth * 0.5, ActualHeight * 0.5);
                var currentPnt = matrix.Transform(center);

                var dist = centerPnt - currentPnt;
                var distLength = dist.Length / transform.Matrix.M11;
                if (distLength < 2.0)
                {
                    return;
                }

                dist.Normalize();
                var speedLimit = distLength * 0.25;
                var minSpeed = 1.0;
                var offsetLength = Math.Min(Math.Max(minSpeed, speedLimit), distLength);
                var offset = offsetLength * dist;
                m_lastMoveOffset = offsetLength;

                matrix.TranslatePrepend(offset.X, offset.Y);
                transform.Matrix = matrix;
            });
        }

        private void background_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            m_isMouseDown = true;
            m_backgroundMovePos = e.GetPosition(background);
            var source = e.OriginalSource;
            if (source == this)
            {
                UIManager.Instance().GetScene().ClearSelection();
            }
        }

        private void background_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var source = e.OriginalSource;
            m_isMouseDown = false;
            ReleaseMouseCapture();
        }

        private void background_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var source = e.OriginalSource;
            if (source == this)
            {
                var point = e.GetPosition(background);
                if (!Point.Equals(point, m_backgroundMovePos))
                {
                    m_mouseMoveTime = DateTime.Now;
                }
                
                var scene = UIManager.Instance().GetScene();
                var selectedItems = scene.SelectedItems();
                if (selectedItems.Count == 0 &&
                    e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    CaptureMouse();
                    var element = this.canvas as UIElement;
                    var transform = this.canvas.RenderTransform as MatrixTransform;
                    var matrix = transform.Matrix;
                    var offset = (point - m_backgroundMovePos) / matrix.M11;
                    matrix.TranslatePrepend(offset.X, offset.Y);
                    transform.Matrix = matrix;
                }
                else
                {
                    m_isMouseDown = false;
                }
                m_backgroundMovePos = point;
            }
        }

        private void background_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            m_isMouseInView = true;
        }

        private void background_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            m_isMouseInView= false;
        }

        public void InvalidateLegend()
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            legend.BuildLegend();
            scene.ReleaseLock();
            //this.Dispatcher.Invoke((ThreadStart)delegate
            //{
            //    legend.InvalidateVisual();
            //});
        }

        public void InvalidateScheme()
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            scheme.BuildSchemeLegend();
            scene.ReleaseLock();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
        }

        private void background_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            InvalidateLegend();
            InvalidateScheme();
        }
    }
}
