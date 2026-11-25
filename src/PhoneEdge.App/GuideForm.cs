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
    private readonly GroupBox _warnBox = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 22, 8, 8), Width = 480 };
    private readonly Label _warnText = new() { AutoSize = true, MaximumSize = new Size(450, 0) };
    private readonly Button _fixAccel = new() { AutoSize = true };
    private readonly Button _retry = new() { AutoSize = true };
    private readonly LinkLabel _detailToggle = new() { AutoSize = true };
    private readonly TextBox _detail = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 480, Height = 110, Visible = false };
    private readonly Button _openLog = new() { AutoSize = true, Visible = false };
    private readonly Button _close = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _autoClose = new();
    private bool _busy;
    private string _statusKey = "";

    /// <summary>연결된 뒤 창이 저절로 닫힐 때까지 기다리는 시간 (사용자가 "연결됨"을 읽을 수 있을 만큼).</summary>
    internal TimeSpan AutoCloseDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>제목 줄 문구 (테스트용).</summary>
    internal string TitleText => _title.Text;
    private PhoneLinkState _lastState;
    private PhoneLinkDiagnosis _lastDiagnosis = PhoneLinkDiagnosis.Healthy;

    public GuideForm(IPhoneLink link)
    {
        _link = link;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        ShowIcon = false;

        // Show() 로 여는 모달리스 창이라 DialogResult 로는 닫히지 않는다. 직접 닫는다.
        var close = _close;
        close.Click += (_, _) => Close();
        CancelButton = close;   // Esc 키로도 닫힌다
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange(new Control[] { _retry, close });

        var warnInner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Location = new Point(8, 22) };
        warnInner.Controls.AddRange(new Control[] { _warnText, _fixAccel });
        _warnBox.Controls.Add(warnInner);

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        root.Controls.AddRange(new Control[] { _title, _steps, _status, _warnBox, buttons, _detailToggle, _detail, _openLog });
        Controls.Add(root);

        _retry.Click += async (_, _) => await RunFixAsync(PhoneLinkFix.Retry, "Guide_RetryRunning", "Guide_RetryFail");
        _fixAccel.Click += async (_, _) => await RunFixAsync(PhoneLinkFix.DisablePointerAcceleration, "Guide_AccelDone", "Guide_AccelFail");
        _detailToggle.LinkClicked += (_, _) =>
        {
            bool show = !_detail.Visible;
            _detail.Visible = _openLog.Visible = show;
            _detailToggle.Text = L.T(show ? "Guide_DetailsHide" : "Guide_DetailsShow");
        };
        _openLog.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Logger.FilePath}\"") { UseShellExecute = true }); } catch { }
        };

        _autoClose.Tick += (_, _) =>
        {
            _autoClose.Stop();
            if (!IsDisposed && !_busy) Close();
        };
        FormClosed += (_, _) => _autoClose.Dispose();

        Apply(link.State, link.Diagnosis);
    }

    /// <summary>언어가 바뀔 때 호출한다. 고정 문구와 현재 내용을 모두 새 언어로 다시 채운다.</summary>
    public void ApplyLanguage() => Apply(_lastState, _lastDiagnosis);

    /// <summary>진단/상태가 바뀔 때마다 UI 스레드에서 호출한다.</summary>
    public void Apply(PhoneLinkState state, PhoneLinkDiagnosis d)
    {
        _lastState = state;
        _lastDiagnosis = d;
        Text = L.T("Guide_Title");
        _warnBox.Text = L.T("Guide_WarnBox");
        _fixAccel.Text = L.T("Guide_FixAccel");
        _retry.Text = L.T("Guide_Retry");
        _close.Text = L.T("Common_Close");
        _openLog.Text = L.T("Guide_OpenLog");
        _detailToggle.Text = L.T(_detail.Visible ? "Guide_DetailsHide" : "Guide_DetailsShow");
        _status.Text = _statusKey.Length == 0 ? "" : L.T(_statusKey);
        var guide = GuidanceTable.For(d.Problem);
        bool ok = guide == null && state == PhoneLinkState.Ready;
        bool accel = (d.Warnings & PhoneLinkWarnings.PointerAcceleration) != 0;

        // 연결되면 "연결됨"을 잠깐 보여 주고 저절로 닫는다. 확인할 설정(경고)이 있으면 열어 둔다.
        bool autoClose = ok && !accel;
        if (!autoClose) _autoClose.Stop();
        else if (!_autoClose.Enabled)
        {
            _autoClose.Interval = Math.Max(1, (int)AutoCloseDelay.TotalMilliseconds);
            _autoClose.Start();
        }

        if (guide != null)
        {
            _title.Text = guide.Cause;
            _title.ForeColor = Color.Firebrick;
            _steps.Text = string.Join(Environment.NewLine, guide.Steps.Select((s, i) => $"{i + 1}. {s}"));
            _steps.Visible = true;
        }
        else if (ok)
        {
            _title.Text = L.T(accel ? "Guide_ConnectedCheck" : "Guide_Connected");
            _title.ForeColor = Color.SeaGreen;
            _steps.Visible = false;
        }
        else
        {
            _title.Text = L.T("Guide_Connecting");
            _title.ForeColor = SystemColors.ControlText;
            _steps.Visible = false;
        }

        _warnBox.Visible = accel;
        _warnText.Text = GuidanceTable.PointerAccelerationCause() + Environment.NewLine + Environment.NewLine + GuidanceTable.PointerAccelerationManual();
        _retry.Visible = guide?.CanRetry ?? false;
        _detail.Text = string.IsNullOrWhiteSpace(d.Detail) ? L.T("Guide_NoDetail") : d.Detail.Replace("\n", Environment.NewLine);
    }

    private async Task RunFixAsync(PhoneLinkFix fix, string okKey, string failKey)
    {
        if (_busy) return;
        _busy = true;
        _retry.Enabled = _fixAccel.Enabled = false;
        SetStatus("Guide_Working");
        try
        {
            bool done = await _link.TryFixAsync(fix);
            SetStatus(done ? okKey : failKey);
        }
        catch (Exception ex)
        {
            Logger.Info("auto-fix failed: " + ex.Message);
            SetStatus(failKey);
        }
        finally
        {
            _busy = false;
            if (!IsDisposed) _retry.Enabled = _fixAccel.Enabled = true;
        }
    }

    private void SetStatus(string key)
    {
        _statusKey = key;
        _status.Text = L.T(key);
    }
}
