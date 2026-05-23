using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace DistributedAudio.UI.Services
{
    /// <summary>
    /// 系统托盘图标服务
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private bool _disposed;

        public event EventHandler? ShowMainWindow;
        public event EventHandler? ExitApplication;

        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        public void Initialize()
        {
            _contextMenu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("显示主窗口");
            showItem.Click += (s, e) => ShowMainWindow?.Invoke(this, EventArgs.Empty);

            var separator = new ToolStripSeparator();

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitApplication?.Invoke(this, EventArgs.Empty);

            _contextMenu.Items.AddRange(new ToolStripItem[]
            {
                showItem,
                separator,
                exitItem
            });

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateIcon(),
                Text = "分布式音频系统",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowMainWindow?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        /// <summary>
        /// 显示通知
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
        }

        /// <summary>
        /// 更新托盘图标文本
        /// </summary>
        public void UpdateText(string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = text;
            }
        }

        /// <summary>
        /// 设置托盘图标
        /// </summary>
        public void SetIcon(Icon icon)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = icon;
            }
        }

        /// <summary>
        /// 创建默认图标
        /// </summary>
        private Icon CreateIcon()
        {
            // Create a simple icon with audio symbol
            var bitmap = new Bitmap(16, 16);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Draw speaker icon
                using (var pen = new Pen(Color.FromArgb(52, 152, 219), 2))
                {
                    // Speaker body
                    g.DrawRectangle(pen, 2, 5, 4, 6);

                    // Sound waves
                    g.DrawLine(pen, 7, 7, 10, 4);
                    g.DrawLine(pen, 7, 9, 10, 12);
                    g.DrawLine(pen, 11, 6, 14, 3);
                    g.DrawLine(pen, 11, 10, 14, 13);
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose();
                _disposed = true;
            }
        }
    }
}
