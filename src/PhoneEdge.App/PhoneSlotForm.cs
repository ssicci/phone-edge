using System.Drawing.Drawing2D;
using PhoneEdge.App.Core;
using PhoneEdge.App.Native;

namespace PhoneEdge.App;

/// <summary>
/// "폰 위치 설정" 창. 모니터 배치를 축소해 번호와 함께 보여 주고, 폰을 끌어 모니터의 빈 변에 놓으면
/// 그 변의 빈 구간 전체에 붙는다 (snap). [저장]하면 <see cref="Result"/> 에 고른 후보가 남는다.
/// </summary>
internal sealed class PhoneSlotForm : Form
{
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private readonly SlotCanvas _canvas;
    private readonly Label _hint = new() { AutoSize = true, MaximumSize = new Size(560, 0) };
    private readonly Label _note = new() { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.DimGray };
    private readonly Button _identify = new() { AutoSize = true };
    private readonly Button _save = new() { AutoSize = true, DialogResult = DialogResult.OK };
    private readonly Button _cancel = new() { AutoSize = true, DialogResult = DialogResult.Cancel };
    private readonly bool _firstTime;

    /// <summary>[저장]을 눌렀을 때 고른 폰 자리 후보 (취소면 null).</summary>
    public SlotCandidate? Result { get; private set; }

    /// <summary>지금 고른 후보 (테스트·미리보기용).</summary>
    internal SlotCandidate? Selected => _canvas.Selected;

    /// <param name="current">지금 구성에서 쓰는 폰 자리 (처음 그릴 때 폰을 여기에 놓는다).</param>
    /// <param name="firstTime">처음 보는 모니터 구성이라 정해 달라는 안내를 맨 위에 더 보여 준다.</param>
    public PhoneSlotForm(IReadOnlyList<MonitorInfo> monitors, ResolvedSlot? current, bool firstTime = false)
    {
        _monitors = monitors;
        _firstTime = firstTime;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowIcon = false;
        ClientSize = new Size(620, 470);
        Padding = new Padding(12);

        _canvas = new SlotCanvas(monitors, current) { Dock = DockStyle.Fill };
        _identify.Click += (_, _) => IdentifyOverlay.Show(_monitors);
        _save.Click += (_, _) => Result = _canvas.Selected;
        AcceptButton = _save;
        CancelButton = _cancel;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        buttons.Controls.AddRange(new Control[] { _cancel, _save });
        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        left.Controls.Add(_identify);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.Controls.Add(left, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        top.Controls.AddRange(new Control[] { _hint, _note });

        Controls.Add(_canvas);
        Controls.Add(bottom);
        Controls.Add(top);
        _save.Enabled = _canvas.Selected != null;
        ApplyLanguage();
    }

    /// <summary>현재 앱 언어로 문구를 다시 채운다.</summary>
    public void ApplyLanguage()
    {
        Text = L.T("Slot_Title");
        _hint.Text = (_firstTime ? L.T("Slot_FirstTime") + "\r\n" : "") + L.T("Slot_Hint");
        _note.Text = L.T("Slot_Remember");
        _identify.Text = L.T("Slot_Identify");
        _save.Text = L.T("Slot_Save");
        _cancel.Text = L.T("Slot_Cancel");
        _canvas.PhoneLabel = L.T("Slot_Phone");
        _canvas.Invalidate();
    }

    /// <summary>창을 띄우지 않고 그림만 비트맵으로 (문서 캡처용).</summary>
    internal Bitmap Render()
    {
        CreateControl();
        PerformLayout();
        var bmp = new Bitmap(Width, Height);
        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
        return bmp;
    }
}

/// <summary>모니터 배치 그림. 폰 사각형을 끌어 빈 변에 놓는다.</summary>
internal sealed class SlotCanvas : Control
{
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private readonly List<SlotCandidate> _candidates;
    private readonly Dictionary<string, int> _numbers;
    private SlotCandidate? _selected;
    private SlotCandidate? _hover;      // 끄는 동안 놓으면 붙을 후보
    private bool _dragging;
    private PointF _dragPoint;
    private LayoutView _view;

    public string PhoneLabel { get; set; } = "";
    public SlotCandidate? Selected => _selected;

    public SlotCanvas(IReadOnlyList<MonitorInfo> monitors, ResolvedSlot? current)
    {
        _monitors = monitors;
        _candidates = FreeSpans.Candidates(monitors);
        _numbers = LayoutView.Numbers(monitors);
        _view = LayoutView.Fit(monitors, 600, 330, 50);
        DoubleBuffered = true;
        ResizeRedraw = true;
        MinimumSize = new Size(300, 200);
        _selected = FindCandidate(current) ?? _candidates.FirstOrDefault();
    }

    private SlotCandidate? FindCandidate(ResolvedSlot? s)
    {
        if (s is not { } slot) return null;
        return _candidates.FirstOrDefault(c => c.Monitor.Bounds == slot.Monitor && c.Side == slot.Side
            && c.Span.Start <= slot.SpanStart + 2 && c.Span.End >= slot.SpanEnd - 2);
    }

    private float Dpi => DeviceDpi / 96f;
    private float PhoneThickness => 22 * Dpi;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _view = LayoutView.Fit(_monitors, Width, Height, PhoneThickness + 28 * Dpi);
    }

    /// <summary>후보가 가리키는 변 바깥에 그려지는 폰 사각형 (그림 좌표).</summary>
    private RectangleF PhoneRect(SlotCandidate c)
    {
        var b = c.Monitor.Bounds;
        float gap = 4 * Dpi, th = PhoneThickness;
        float s = (float)(c.Span.Start * _view.Scale), t = (float)(c.Span.End * _view.Scale);
        float along0 = (float)(c.Side is PhoneSide.Right or PhoneSide.Left ? _view.Y(c.Span.Start) : _view.X(c.Span.Start));
        float len = Math.Max(40 * Dpi, t - s);
        switch (c.Side)
        {
            case PhoneSide.Right: return new RectangleF((float)_view.X(b.Right) + gap, along0, th, len);
            case PhoneSide.Left: return new RectangleF((float)_view.X(b.Left) - gap - th, along0, th, len);
            case PhoneSide.Top: return new RectangleF(along0, (float)_view.Y(b.Top) - gap - th, len, th);
            default: return new RectangleF(along0, (float)_view.Y(b.Bottom) + gap, len, th);
        }
    }

    private SlotCandidate? NearestTo(float cx, float cy)
        => FreeSpans.Nearest(_candidates, _view.DesktopX(cx), _view.DesktopY(cy));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (_selected != null && RectangleF.Inflate(PhoneRect(_selected), 6 * Dpi, 6 * Dpi).Contains(e.Location))
        {
            _dragging = true;
            _dragPoint = e.Location;
            Capture = true;
            Invalidate();
        }
        else if (NearestTo(e.X, e.Y) is { } c && DistanceCanvas(c, e.X, e.Y) <= 24 * Dpi)
        {
            _selected = c;   // 변 근처를 누르기만 해도 폰이 그리로 간다
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) { Cursor = _selected != null && PhoneRect(_selected).Contains(e.Location) ? Cursors.SizeAll : Cursors.Default; return; }
        _dragPoint = e.Location;
        _hover = NearestTo(e.X, e.Y);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Capture = false;
        if (_hover != null) _selected = _hover;
        _hover = null;
        Invalidate();
    }

    private double DistanceCanvas(SlotCandidate c, float cx, float cy) => FreeSpans.DistanceToSegment(c, _view.DesktopX(cx), _view.DesktopY(cy)) * _view.Scale;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SystemColors.Control);

        using var monFill = new SolidBrush(Color.FromArgb(234, 241, 251));
        using var monPen = new Pen(Color.FromArgb(138, 148, 163), 1.5f * Dpi);
        using var numFont = new Font("Segoe UI", 22 * Dpi, FontStyle.Bold, GraphicsUnit.Pixel);
        using var numBrush = new SolidBrush(Color.FromArgb(89, 99, 110));
        var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        foreach (var m in _monitors)
        {
            var r = new RectangleF((float)_view.X(m.Bounds.Left), (float)_view.Y(m.Bounds.Top), (float)(m.Bounds.Right - m.Bounds.Left) * (float)_view.Scale, (float)(m.Bounds.Bottom - m.Bounds.Top) * (float)_view.Scale);
            g.FillRectangle(monFill, r);
            g.DrawRectangle(monPen, r.X, r.Y, r.Width, r.Height);
            g.DrawString(_numbers.TryGetValue(m.Id, out int n) ? n.ToString() : "?", numFont, numBrush, r, center);
        }

        // 폰 자리가 될 수 있는 빈 변: 연한 선. 끄는 동안 붙을 후보는 진하게.
        using var freePen = new Pen(Color.FromArgb(120, 30, 136, 229), 3 * Dpi) { DashStyle = DashStyle.Dash };
        using var hotPen = new Pen(Color.FromArgb(30, 136, 229), 5 * Dpi);
        foreach (var c in _candidates) DrawSegment(g, c, ReferenceEquals(c, _hover) ? hotPen : freePen);

        using var phoneFill = new SolidBrush(Color.FromArgb(43, 51, 64));
        using var phoneText = new SolidBrush(Color.White);
        using var smallFont = new Font("Segoe UI", 11 * Dpi, FontStyle.Regular, GraphicsUnit.Pixel);
        if (_selected != null)
        {
            var pr = PhoneRect(_selected);
            using var path = Round(pr, 5 * Dpi);
            g.FillPath(phoneFill, path);
            // 폰 아이콘: 긴 쪽 방향의 스피커 슬릿 하나. 글자는 좁은 사각형에 들어가지 않으므로 아래 범례에 둔다.
            bool vertical = _selected.Side is PhoneSide.Right or PhoneSide.Left;
            using var slit = new Pen(Color.FromArgb(200, 255, 255, 255), 2 * Dpi) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float cx = pr.X + pr.Width / 2, cy = pr.Y + pr.Height / 2, half = Math.Min(10 * Dpi, (vertical ? pr.Height : pr.Width) / 2 - 6 * Dpi);
            if (half > 0)
            {
                if (vertical) g.DrawLine(slit, cx, cy - half, cx, cy + half);
                else g.DrawLine(slit, cx - half, cy, cx + half, cy);
            }
        }
        // 범례: 작은 폰 아이콘과 이름 (왼쪽 아래)
        {
            float lx = 8 * Dpi, ly = Height - 26 * Dpi;
            using var path = Round(new RectangleF(lx, ly, 12 * Dpi, 18 * Dpi), 3 * Dpi);
            g.FillPath(phoneFill, path);
            using var legendBrush = new SolidBrush(Color.FromArgb(89, 99, 110));
            g.DrawString(PhoneLabel, smallFont, legendBrush, lx + 18 * Dpi, ly + 2 * Dpi);
        }
        if (_dragging)
        {
            var d = new RectangleF(_dragPoint.X - 13 * Dpi, _dragPoint.Y - 22 * Dpi, 26 * Dpi, 44 * Dpi);
            using var path = Round(d, 5 * Dpi);
            using var dragFill = new SolidBrush(Color.FromArgb(200, 43, 51, 64));
            g.FillPath(dragFill, path);
        }
    }

    private void DrawSegment(Graphics g, SlotCandidate c, Pen pen)
    {
        var b = c.Monitor.Bounds;
        if (c.Side is PhoneSide.Right or PhoneSide.Left)
        {
            float x = (float)_view.X(c.Side == PhoneSide.Right ? b.Right : b.Left);
            g.DrawLine(pen, x, (float)_view.Y(c.Span.Start), x, (float)_view.Y(c.Span.End));
        }
        else
        {
            float y = (float)_view.Y(c.Side == PhoneSide.Top ? b.Top : b.Bottom);
            g.DrawLine(pen, (float)_view.X(c.Span.Start), y, (float)_view.X(c.Span.End), y);
        }
    }

    private static GraphicsPath Round(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>"식별": 각 모니터에 큰 번호를 2초간 띄운다.</summary>
internal static class IdentifyOverlay
{
    public static void Show(IReadOnlyList<MonitorInfo> monitors, int milliseconds = 2000)
    {
        var numbers = LayoutView.Numbers(monitors);
        var forms = new List<Form>();
        foreach (var m in monitors)
        {
            int side = Math.Max(120, Math.Min(m.Bounds.Right - m.Bounds.Left, m.Bounds.Bottom - m.Bounds.Top) / 4);   // 물리 픽셀
            var f = new NumberForm(numbers.TryGetValue(m.Id, out int n) ? n : 0, side);
            f.Show();
            // 모니터마다 배율이 달라도 같은 물리 픽셀 위치·크기로 놓는다.
            NativeBounds.Set(f.Handle, m.Bounds.Left + (m.Bounds.Right - m.Bounds.Left - side) / 2, m.Bounds.Top + (m.Bounds.Bottom - m.Bounds.Top - side) / 2, side, side);
            forms.Add(f);
        }
        var timer = new System.Windows.Forms.Timer { Interval = milliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            foreach (var f in forms) f.Close();
        };
        timer.Start();
    }

    private sealed class NumberForm : Form
    {
        private readonly int _number;
        public NumberForm(int number, int side)
        {
            _number = number;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(30, 136, 229);
            Opacity = 0.9;
            Size = new Size(side, side);
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var font = new Font("Segoe UI", ClientSize.Height * 0.6f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(_number.ToString(), font, Brushes.White, ClientRectangle, fmt);
        }
    }
}
