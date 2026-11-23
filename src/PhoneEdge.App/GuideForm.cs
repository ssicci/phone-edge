using System.Diagnostics;
using PhoneEdge.App.Core;
using PhoneEdge.Contracts;

namespace PhoneEdge.App;

/// <summary>연결 문제 안내 창. 진단이 바뀌면 Apply 로 내용을 갱신한다.</summary>
internal sealed class GuideForm : Form
{
    private readonly IPhoneLink _link;
    private readonly Label _title = new() { AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold), MaximumSize = new Size(480, 0) };
    private readonly Label _steps = new() { AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(0, 8, 0, 8) };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = Color.DimGray };
    private readonly GroupBox _warnBox = new() { Text = "확인할 설정", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 22, 8, 8), Width = 480 };
    private readonly Label _warnText = new() { AutoSize = true, MaximumSize = new Size(450, 0) };
    private readonly Button _fixAccel = new() { Text = "폰 설정 자동으로 끄기", AutoSize = true };
    private readonly Button _retry = new() { Text = "다시 시도", AutoSize = true };
    private readonly LinkLabel _detailToggle = new() { Text = "자세한 정보 ▸", AutoSize = true };
    private readonly TextBox _detail = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 480, Height = 110, Visible = false };
    private readonly Button _openLog = new() { Text = "로그 파일 열기", AutoSize = true, Visible = false };
    private bool _busy;

    public GuideForm(IPhoneLink link)
    {
        _link = link;
        Text = "PhoneEdge - 연결 문제 해결";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        ShowIcon = false;

        // Show() 로 여는 모달리스 창이라 DialogResult 로는 닫히지 않는다. 직접 닫는다.
        var close = new Button { Text = "닫기", AutoSize = true };
        close.Click += (_, _) => Close();
        CancelButton = close;   // Esc 키로도 닫힌다
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _retry, close });

        var warnInner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        warnInner.Controls.AddRange(new Control[] { _warnText, _fixAccel });
        _warnBox.Controls.Add(warnInner);

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        root.Controls.AddRange(new Control[] { _title, _steps, _status, _warnBox, buttons, _detailToggle, _detail, _openLog });
        Controls.Add(root);

        _retry.Click += async (_, _) => await RunFixAsync(PhoneLinkFix.Retry, "다시 연결을 시도했습니다. 결과를 기다리는 중…", "다시 시도를 완료하지 못했습니다.");
        _fixAccel.Click += async (_, _) => await RunFixAsync(PhoneLinkFix.DisablePointerAcceleration, "포인터 가속을 껐습니다.", "자동으로 끄지 못했습니다. 위의 방법으로 직접 바꿔 주세요.");
        _detailToggle.LinkClicked += (_, _) =>
        {
            bool show = !_detail.Visible;
            _detail.Visible = _openLog.Visible = show;
            _detailToggle.Text = show ? "자세한 정보 ▾" : "자세한 정보 ▸";
        };
        _openLog.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Logger.FilePath}\"") { UseShellExecute = true }); } catch { }
        };

        Apply(link.State, link.Diagnosis);
    }

    /// <summary>진단/상태가 바뀔 때마다 UI 스레드에서 호출한다.</summary>
    public void Apply(PhoneLinkState state, PhoneLinkDiagnosis d)
    {
        var guide = GuidanceTable.For(d.Problem);
        bool ok = guide == null && state == PhoneLinkState.Ready;

        if (guide != null)
        {
            _title.Text = guide.Cause;
            _title.ForeColor = Color.Firebrick;
            _steps.Text = string.Join(Environment.NewLine, guide.Steps.Select((s, i) => $"{i + 1}. {s}"));
            _steps.Visible = true;
        }
        else if (ok)
        {
            _title.Text = "폰이 연결되었습니다. 이 창은 닫아도 됩니다.";
            _title.ForeColor = Color.SeaGreen;
            _steps.Visible = false;
        }
        else
        {
            _title.Text = "폰에 연결하는 중입니다…";
            _title.ForeColor = SystemColors.ControlText;
            _steps.Visible = false;
        }

        bool accel = (d.Warnings & PhoneLinkWarnings.PointerAcceleration) != 0;
        _warnBox.Visible = accel;
        _warnText.Text = GuidanceTable.PointerAccelerationCause + Environment.NewLine + Environment.NewLine + GuidanceTable.PointerAccelerationManual;
        _retry.Visible = guide?.CanRetry ?? false;
        _detail.Text = string.IsNullOrWhiteSpace(d.Detail) ? "(자세한 정보 없음)" : d.Detail.Replace("\n", Environment.NewLine);
    }

    private async Task RunFixAsync(PhoneLinkFix fix, string okText, string failText)
    {
        if (_busy) return;
        _busy = true;
        _retry.Enabled = _fixAccel.Enabled = false;
        _status.Text = "처리 중…";
        try
        {
            bool done = await _link.TryFixAsync(fix);
            _status.Text = done ? okText : failText;
        }
        catch (Exception ex)
        {
            Logger.Info("자동 조치 실패: " + ex.Message);
            _status.Text = failText;
        }
        finally
        {
            _busy = false;
            if (!IsDisposed) _retry.Enabled = _fixAccel.Enabled = true;
        }
    }
}
