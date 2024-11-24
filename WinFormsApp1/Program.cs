using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TransparentOverlay
{
    public class OverlayForm : Form
    {
        private bool isDraggingMiddle = false;
        private bool isDraggingEndpoint = false;
        private int draggedEndpointIndex = -1;
        private Point middlePoint;
        private Point[] endpoints = new Point[4];
        private System.Windows.Forms.Timer longPressTimer;
        private bool isClickThrough = true;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;
        private bool isMouseDown = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int HOTKEY_ID = 1;
        private const int MOD_CONTROL = 0x0002;
        private const int VK_0 = 0x30;

        public OverlayForm()
        {
            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;

            // Initialize endpoints at corners of a rectangle
            int padding = 100;
            endpoints[0] = new Point(padding, padding);
            endpoints[1] = new Point(Screen.PrimaryScreen.Bounds.Width - padding, padding);
            endpoints[2] = new Point(Screen.PrimaryScreen.Bounds.Width - padding, Screen.PrimaryScreen.Bounds.Height - padding);
            endpoints[3] = new Point(padding, Screen.PrimaryScreen.Bounds.Height - padding);

            // Initialize middle point
            middlePoint = new Point(
                Screen.PrimaryScreen.Bounds.Width / 2,
                Screen.PrimaryScreen.Bounds.Height / 2
            );

            // Set up long press timer
            longPressTimer = new System.Windows.Forms.Timer();
            longPressTimer.Interval = 500; // 0.5 seconds
            longPressTimer.Tick += LongPressTimer_Tick;

            // Enable painting and mouse events
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint, true);

            // Register events
            this.Paint += OverlayForm_Paint;
            this.MouseDown += OverlayForm_MouseDown;
            this.MouseUp += OverlayForm_MouseUp;
            this.MouseMove += OverlayForm_MouseMove;

            // Register global hotkey
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL, VK_0);

            SetClickThrough(true);
        }

        private void SetClickThrough(bool enable)
        {
            isClickThrough = enable;
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);

            if (enable)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;

            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleClickThrough();
            }

            base.WndProc(ref m);
        }

        private void ToggleClickThrough()
        {
            SetClickThrough(!isClickThrough);
        }

        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            if (isMouseDown)
            {
                isDraggingMiddle = true;
                longPressTimer.Stop();
            }
        }

        private void OverlayForm_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw the rectangle
            using (Pen pen = new Pen(Color.White, 2))
            {
                for (int i = 0; i < endpoints.Length; i++)
                {
                    int next = (i + 1) % endpoints.Length;
                    e.Graphics.DrawLine(pen, endpoints[i], endpoints[next]);
                }
            }

            // Draw diagonal lines
            using (Pen pen = new Pen(Color.Red, 2))
            {
                foreach (Point endpoint in endpoints)
                {
                    e.Graphics.DrawLine(pen, endpoint, middlePoint);
                }
            }

            // Draw endpoints
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                foreach (Point endpoint in endpoints)
                {
                    e.Graphics.FillEllipse(brush, endpoint.X - 5, endpoint.Y - 5, 10, 10);
                }
            }

            // Draw middle point
            using (SolidBrush brush = new SolidBrush(Color.Red))
            {
                e.Graphics.FillEllipse(brush, middlePoint.X - 7, middlePoint.Y - 7, 14, 14);
            }
        }

        private void OverlayForm_MouseDown(object sender, MouseEventArgs e)
        {
            isMouseDown = true;

            // Check if clicking on endpoints
            for (int i = 0; i < endpoints.Length; i++)
            {
                if (Math.Abs(e.X - endpoints[i].X) < 10 && Math.Abs(e.Y - endpoints[i].Y) < 10)
                {
                    isDraggingEndpoint = true;
                    draggedEndpointIndex = i;
                    return;
                }
            }

            // Check if clicking near middle point
            if (Math.Abs(e.X - middlePoint.X) < 10 && Math.Abs(e.Y - middlePoint.Y) < 10)
            {
                longPressTimer.Start();
            }
        }

        private void OverlayForm_MouseUp(object sender, MouseEventArgs e)
        {
            isMouseDown = false;
            isDraggingMiddle = false;
            isDraggingEndpoint = false;
            draggedEndpointIndex = -1;
            longPressTimer.Stop();
        }

        private void OverlayForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingMiddle)
            {
                middlePoint = new Point(e.X, e.Y);
                this.Invalidate();
            }
            else if (isDraggingEndpoint && draggedEndpointIndex != -1)
            {
                endpoints[draggedEndpointIndex] = new Point(e.X, e.Y);
                this.Invalidate();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OverlayForm());
        }
    }
}