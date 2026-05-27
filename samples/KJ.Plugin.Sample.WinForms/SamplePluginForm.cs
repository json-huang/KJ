namespace KJ.Plugin.Sample.WinForms;

public sealed class SamplePluginForm : Form
{
    private readonly Label _status;

    public SamplePluginForm()
    {
        // Keep it as a normal top-level window so it can appear standalone.
        // When KJ embeds it, the host will switch it to WS_CHILD and remove chrome via styles.
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = true;
        Text = "KJ Sample Plugin";
        Width = 760;
        Height = 520;
        BackColor = Color.FromArgb(16, 24, 36);
        ForeColor = Color.White;

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            Text = "KJ 外部进程插件",
            Font = new Font("Microsoft YaHei UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _status = new Label
        {
            Dock = DockStyle.Fill,
            Text = "WinForms 插件窗口已启动，可通过 gRPC 返回 HWND 给 KJ Dock 承载。",
            Font = new Font("Microsoft YaHei UI", 11),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 8, 12, 8),
        };

        var testInfoButton = new Button
        {
            AutoSize = true,
            Height = 36,
            Text = "测试发送插件信息",
        };
        testInfoButton.Click += (_, _) =>
        {
            var message = $"测试信息 {DateTime.Now:HH:mm:ss}";
            SamplePluginService.EnqueueTestInfo(message);
            _status.Text = $"已发送测试插件信息：{message}";
        };

        var heartbeatButton = new Button
        {
            AutoSize = true,
            Height = 36,
            Margin = new Padding(8, 0, 0, 0),
            Text = "发送心跳",
        };
        heartbeatButton.Click += (_, _) =>
        {
            SamplePluginService.EnqueueHeartbeat("manual");
            _status.Text = $"心跳已发送：{DateTime.Now:T}";
        };

        buttonPanel.Controls.Add(testInfoButton);
        buttonPanel.Controls.Add(heartbeatButton);

        Controls.Add(_status);
        Controls.Add(buttonPanel);
        Controls.Add(title);
    }

    public void PrepareForEmbed()
    {
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = false;
        if (!Visible)
            Show();
        PerformLayout();
        Refresh();
    }
}
