using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Sorter
{
    public sealed class VisualizerControl : Panel
    {
        private sealed class AnimationFrame
        {
            public double[] Values;
            public int ActiveA;
            public int ActiveB;
            public string Status;
            public string Time;
        }

        private readonly object sync = new object();
        private readonly Queue<AnimationFrame> frames = new Queue<AnimationFrame>();
        private readonly Timer timer;
        private readonly string title;
        private const int MaxQueuedFrames = 5000;

        private double[] values = new double[0];
        private string status = "Ожидание";
        private string time = "—";
        private int activeA = -1;
        private int activeB = -1;
        private string queuedFinalStatus = null;
        private string queuedFinalTime = null;
        private bool isReleased;

        public VisualizerControl(string title)
        {
            this.title = title;
            DoubleBuffered = true;
            BackColor = Color.White;
            BorderStyle = BorderStyle.FixedSingle;
            Height = 250;
            Margin = new Padding(6);

            timer = new Timer();
            timer.Interval = 100;
            timer.Tick += OnAnimationTick;
            timer.Start();
        }

        public void SetData(double[] source)
        {
            lock (sync)
            {
                values = source == null ? new double[0] : (double[])source.Clone();
                frames.Clear();
                activeA = -1;
                activeB = -1;
                queuedFinalStatus = null;
                queuedFinalTime = null;
            }
            Invalidate();
        }

        public void SetStatus(string newStatus, string newTime)
        {
            lock (sync)
            {
                status = newStatus ?? "Ожидание";
                time = newTime ?? "—";
                activeA = -1;
                activeB = -1;
                queuedFinalStatus = null;
                queuedFinalTime = null;
            }
            Invalidate();
        }

        public void EnqueueFrame(double[] source, int first, int second, string operation)
        {
            if (source == null) return;

            AnimationFrame frame = new AnimationFrame
            {
                Values = (double[])source.Clone(),
                ActiveA = first,
                ActiveB = second,
                Status = operation ?? "Выполняется",
                Time = null
            };

            lock (sync)
            {
                if (isReleased)
                    return;
                if (frames.Count >= MaxQueuedFrames)
                    frames.Dequeue();
                frames.Enqueue(frame);
            }
        }

        public void SetFinalState(double[] source, string finalStatus, string finalTime)
        {
            if (source == null) source = new double[0];

            AnimationFrame frame = new AnimationFrame
            {
                Values = (double[])source.Clone(),
                ActiveA = -1,
                ActiveB = -1,
                Status = finalStatus ?? "Готово",
                Time = finalTime ?? "—"
            };

            lock (sync)
            {
                if (isReleased)
                    return;
                if (frames.Count >= MaxQueuedFrames)
                    frames.Dequeue();
                frames.Enqueue(frame);
                queuedFinalStatus = finalStatus;
                queuedFinalTime = finalTime;
            }
        }

        private void OnAnimationTick(object sender, EventArgs eventArgs)
        {
            AnimationFrame frame = null;
            lock (sync)
            {
                if (frames.Count > 0)
                    frame = frames.Dequeue();

                if (frame == null && queuedFinalStatus != null && frames.Count == 0)
                {
                    status = queuedFinalStatus;
                    time = queuedFinalTime ?? "—";
                    queuedFinalStatus = null;
                    queuedFinalTime = null;
                    activeA = -1;
                    activeB = -1;
                }
            }

            if (frame != null)
            {
                lock (sync)
                {
                    values = frame.Values;
                    activeA = frame.ActiveA;
                    activeB = frame.ActiveB;
                    status = frame.Status;
                    if (frame.Time != null)
                        time = frame.Time;
                }
                Invalidate();
            }
            else
            {
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (sync)
                {
                    isReleased = true;
                    frames.Clear();
                }

                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs paintEventArgs)
        {
            base.OnPaint(paintEventArgs);

            double[] local;
            string localStatus;
            string localTime;
            int localActiveA;
            int localActiveB;
            lock (sync)
            {
                local = (double[])values.Clone();
                localStatus = status;
                localTime = time;
                localActiveA = activeA;
                localActiveB = activeB;
            }

            Graphics graphics = paintEventArgs.Graphics;
            Rectangle clientRectangle = ClientRectangle;
            using (Font titleFont = new Font(Font, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(30, 30, 30)))
            using (Brush mutedBrush = new SolidBrush(Color.FromArgb(90, 90, 90)))
            {
                graphics.DrawString(title, titleFont, textBrush, 10, 8);
                graphics.DrawString(localStatus + "    Время: " + localTime, Font, mutedBrush, 10, 30);
            }

            Rectangle chart = new Rectangle(10, 58, Math.Max(1, clientRectangle.Width - 20), Math.Max(1, clientRectangle.Height - 68));
            using (Pen axisPen = new Pen(Color.FromArgb(180, 180, 180)))
            {
                graphics.DrawRectangle(axisPen, chart);
            }

            if (local.Length == 0) return;

            double min = local[0];
            double max = local[0];
            for (int valueIndex = 1; valueIndex < local.Length; ++valueIndex)
            {
                if (local[valueIndex] < min) min = local[valueIndex];
                if (local[valueIndex] > max) max = local[valueIndex];
            }
            if (Math.Abs(max - min) < double.Epsilon)
            {
                min -= 1.0;
                max += 1.0;
            }

            double range = max - min;
            double zeroY = chart.Bottom;
            if (min < 0 && max > 0)
                zeroY = chart.Bottom - ((0 - min) / range) * chart.Height;
            else if (max <= 0)
                zeroY = chart.Top;

            if (min < 0 && max > 0)
            {
                using (Pen zeroPen = new Pen(Color.FromArgb(150, 150, 150)))
                    graphics.DrawLine(zeroPen, chart.Left, (float)zeroY, chart.Right, (float)zeroY);
            }

            int count = local.Length;
            float barWidth = Math.Max(1f, (float)chart.Width / count);

            using (Brush normalBrush = new SolidBrush(Color.FromArgb(85, 110, 190)))
            using (Brush activeBrush = new SolidBrush(Color.FromArgb(235, 150, 55)))
            using (Brush pivotBrush = new SolidBrush(Color.FromArgb(145, 95, 190)))
            {
                for (int valueIndex = 0; valueIndex < count; ++valueIndex)
                {
                    double normalized = (local[valueIndex] - min) / range;
                    float topCoordinate = (float)(chart.Bottom - normalized * chart.Height);
                    float top;
                    float bottom;
                    if (local[valueIndex] >= 0)
                    {
                        top = topCoordinate;
                        bottom = (float)zeroY;
                    }
                    else
                    {
                        top = (float)zeroY;
                        bottom = topCoordinate;
                    }

                    float leftCoordinate = chart.Left + valueIndex * barWidth;
                    float barHeight = Math.Max(1f, bottom - top);
                    Brush brush = normalBrush;
                    if (valueIndex == localActiveA && valueIndex == localActiveB)
                        brush = pivotBrush;
                    else if (valueIndex == localActiveA || valueIndex == localActiveB)
                        brush = activeBrush;

                    graphics.FillRectangle(brush, leftCoordinate, top, Math.Max(1f, barWidth - 1f), barHeight);
                }
            }
        }
    }
}
