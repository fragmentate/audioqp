namespace AudIOQP.UI;

internal sealed class OverlayForm : Form
{
    const int WsExNoActivate = 0x08000000;
    const int WsExToolWindow = 0x00000080;
    const int WsExTopMost = 0x00000008;
    const int WsExTransparent = 0x00000020;

    static OverlayForm? _current;

    readonly Panel _accent;
    readonly Label _title;
    readonly Label _subtitle;
    readonly System.Windows.Forms.Timer _timer;
    Screen? _screen;
    string _lastTitle = "";
    string _lastSubtitle = "";
    bool _layingOut;

    OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Theme.Overlay;
        ForeColor = Theme.OverlayText;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.None;

        _accent = new Panel
        {
            BackColor = Theme.Accent,
            Width = 6
        };
        _subtitle = new Label
        {
            AutoSize = false,
            ForeColor = Theme.OverlayMuted,
            BackColor = Theme.Overlay,
            Font = new Font("Segoe UI", 9f),
            Text = "Audio output"
        };
        _title = new Label
        {
            AutoSize = false,
            ForeColor = Theme.OverlayText,
            BackColor = Theme.Overlay,
            Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold)
        };
        Controls.Add(_title);
        Controls.Add(_subtitle);
        Controls.Add(_accent);

        _timer = new System.Windows.Forms.Timer { Interval = 1600 };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Hide();
        };
        DpiChanged += (_, _) =>
        {
            if (_screen != null && _lastTitle.Length > 0)
                LayoutContent(_screen, _lastTitle, _lastSubtitle);
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExToolWindow | WsExTopMost | WsExTransparent;
            return cp;
        }
    }

    public static void ShowMessage(string title, string subtitle)
    {
        var screen = ScreenPlacement.ScreenAtCursor();
        _current ??= new OverlayForm();
        _current.Present(screen, title, subtitle);
    }

    void Present(Screen screen, string title, string subtitle)
    {
        _screen = screen;
        _lastTitle = title;
        _lastSubtitle = subtitle;
        LayoutContent(screen, title, subtitle);
        if (!Visible)
            Show();
        _timer.Stop();
        _timer.Start();
    }

    void LayoutContent(Screen screen, string title, string subtitle)
    {
        if (_layingOut)
            return;
        _layingOut = true;
        try
        {
            var area = screen.WorkingArea;
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            var titleSize = TextRenderer.MeasureText(title, _title.Font, new Size(int.MaxValue, int.MaxValue), flags);
            var subtitleSize = TextRenderer.MeasureText(subtitle, _subtitle.Font, new Size(int.MaxValue, int.MaxValue), flags);
            // Slack covers ClearType overhang so the last glyph is not wrapped onto a clipped second line.
            var natural = Math.Max(titleSize.Width, subtitleSize.Width) + 8;
            var maxText = Math.Max(220, area.Width - 96);
            var textWidth = Math.Min(maxText, Math.Max(220, natural));
            var wrapped = titleSize.Width + 8 > textWidth;
            var titleHeight = wrapped
                ? TextRenderer.MeasureText(title, _title.Font, new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 2
                : titleSize.Height + 2;

            const int padX = 18;
            const int padY = 14;
            var left = _accent.Width + padX;
            _subtitle.Text = subtitle;
            _title.Text = title;
            _subtitle.SetBounds(left, padY, textWidth, subtitleSize.Height + 2);
            _title.SetBounds(left, _subtitle.Bottom + 2, textWidth, titleHeight);
            ClientSize = new Size(left + textWidth + padX, Math.Max(72, _title.Bottom + padY));
            _accent.SetBounds(0, 0, _accent.Width, ClientSize.Height);
            ScreenPlacement.Center(this, screen);
        }
        finally
        {
            _layingOut = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}
