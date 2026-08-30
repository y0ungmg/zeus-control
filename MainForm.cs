using System.Runtime.InteropServices;
using System.Text.Json;

namespace ZeusControl;

internal sealed class MainForm : Form
{
    private readonly bool selfTest;
    private readonly Dictionary<string, Panel> pages = new();
    private readonly Dictionary<string, ZeusButton> nav = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 1200 };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeusControl", "settings.json");
    private ZeusSlider outputSlider = null!, micSlider = null!;
    private Label outputValue = null!, micValue = null!, outputName = null!, micName = null!, deviceBadge = null!, heroStatus = null!, diagnostics = null!;
    private ZeusButton outputMute = null!, micMute = null!;
    private HeadsetVisual visual = null!;
    private EqualizerControl equalizer = null!;
    private AudioSnapshot snapshot = new(false, 0, false, 0, false, "", "", null);
    private bool syncing;
    private AppSettings settings = new();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainForm(bool selfTest)
    {
        this.selfTest = selfTest;
        Text = "ZEUS CONTROL — Redragon H510 Pro"; BackColor = Theme.Background; ForeColor = Theme.Text;
        Font = Theme.Body; ClientSize = new Size(1180, 760); MinimumSize = new Size(1020, 700); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; DoubleBuffered = true; Icon = SystemIcons.Application;
        LoadSettings(); BuildInterface(); SetPage("PULPIT");
        if (!selfTest)
        {
            Shown += (_, _) => { EnableDarkTitleBar(); RefreshAudio(); refreshTimer.Start(); };
            refreshTimer.Tick += (_, _) => RefreshAudio();
            FormClosed += (_, _) => { refreshTimer.Stop(); SaveSettings(); };
        }
    }

    public bool RunSelfTest(out string failure)
    {
        try
        {
            if (pages.Count != 4) throw new InvalidOperationException("Nie utworzono czterech stron interfejsu.");
            outputSlider.Value = 63; if (outputSlider.Value != 63) throw new InvalidOperationException("Suwak głośności nie zachował wartości.");
            micSlider.Value = 81; if (micSlider.Value != 81) throw new InvalidOperationException("Suwak mikrofonu nie zachował wartości.");
            equalizer.Values = [-2, -1, 0, 1, 2, 3, 4, 3, 1, -1]; if (equalizer.Values[6] != 4) throw new InvalidOperationException("Korektor nie zachował profilu.");
            PerformLayout();
            if (!IsHandleCreated) CreateControl();
            failure = ""; return true;
        }
        catch (Exception ex) { failure = ex.ToString(); return false; }
    }

    private void BuildInterface()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Theme.Sidebar, Padding = new Padding(16, 20, 16, 18) };
        var logo = Label("ZEUS", 26, FontStyle.Bold, Theme.Purple); logo.Dock = DockStyle.Top; logo.Height = 40;
        var logoSub = Label("CONTROL  /  NATIVE .NET", 8.5f, FontStyle.Bold, Theme.Muted); logoSub.Dock = DockStyle.Top; logoSub.Height = 44;
        var navPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 250, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 18, 0, 0) };
        foreach (var name in new[] { "PULPIT", "KOREKTOR", "PROFILE", "DIAGNOSTYKA" })
        {
            var button = new ZeusButton { Text = "●   " + name, Width = 178, Height = 44, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, 8) };
            button.Click += (_, _) => SetPage(name); nav[name] = button; navPanel.Controls.Add(button);
        }
        deviceBadge = Label("●  ŁĄCZENIE…", 8.5f, FontStyle.Bold, Theme.Gold); deviceBadge.Dock = DockStyle.Bottom; deviceBadge.Height = 58; deviceBadge.Padding = new Padding(10, 15, 8, 0); deviceBadge.BackColor = Theme.Panel;
        sidebar.Controls.Add(deviceBadge); sidebar.Controls.Add(navPanel); sidebar.Controls.Add(logoSub); sidebar.Controls.Add(logo);

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(28, 20, 28, 24) };
        foreach (var name in new[] { "PULPIT", "KOREKTOR", "PROFILE", "DIAGNOSTYKA" }) { pages[name] = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Visible = false }; host.Controls.Add(pages[name]); }
        BuildDashboard(pages["PULPIT"]); BuildEqualizer(pages["KOREKTOR"]); BuildProfiles(pages["PROFILE"]); BuildDiagnostics(pages["DIAGNOSTYKA"]);
        Controls.Add(host); Controls.Add(sidebar);
    }

    private void BuildDashboard(Panel page)
    {
        var header = Header("Centrum dowodzenia", "REDRAGON ZEUS PRO H510 PRO  •  WINDOWS 10 x64"); page.Controls.Add(header);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 92, 0, 0), ColumnCount = 2, RowCount = 1 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        var hero = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) };
        visual = new HeadsetVisual { Dock = DockStyle.Fill, RgbColor = settings.RgbColor };
        heroStatus = Label("WYKRYWANIE URZĄDZENIA…", 10f, FontStyle.Bold, Theme.Gold); heroStatus.Dock = DockStyle.Bottom; heroStatus.Height = 58; heroStatus.TextAlign = ContentAlignment.MiddleCenter;
        hero.Controls.Add(visual); hero.Controls.Add(heroStatus); grid.Controls.Add(hero, 0, 0);

        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0), Margin = new Padding(0) };
        right.SizeChanged += (_, _) => { foreach (Control c in right.Controls) c.Width = Math.Max(300, right.ClientSize.Width - 20); };
        right.Controls.Add(BuildAudioCard(false)); right.Controls.Add(BuildAudioCard(true)); right.Controls.Add(BuildRgbCard()); right.Controls.Add(BuildSurroundCard());
        grid.Controls.Add(right, 1, 0); page.Controls.Add(grid); grid.BringToFront();
    }

    private Control BuildAudioCard(bool mic)
    {
        var card = new CardPanel { Width = 390, Height = 145, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(20, 15, 20, 12) };
        var title = Label(mic ? "MIKROFON" : "GŁOŚNOŚĆ", 8.5f, FontStyle.Bold, Theme.Muted); title.SetBounds(20, 14, 220, 22);
        var value = Label("--%", 18, FontStyle.Bold, Theme.Text); value.TextAlign = ContentAlignment.TopRight; value.Anchor = AnchorStyles.Top | AnchorStyles.Right; value.SetBounds(card.Width - 95, 10, 70, 32);
        var slider = new ZeusSlider { Accent = mic ? Theme.Green : Theme.Purple, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right }; slider.SetBounds(18, 47, card.Width - 36, 28);
        var mute = new ZeusButton { Text = mic ? "WYCISZ MIKROFON" : "WYCISZ SŁUCHAWKI", Accent = Theme.Panel2, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top }; mute.SetBounds(20, 81, card.Width - 40, 38);
        var name = Label(mic ? "Domyślny mikrofon" : "Domyślne wyjście", 7.8f, FontStyle.Regular, Theme.Muted); name.AutoEllipsis = true; name.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; name.SetBounds(23, 122, card.Width - 46, 18);
        if (mic) { micSlider = slider; micValue = value; micMute = mute; micName = name; slider.ValueChanged += (_, _) => ChangeVolume(AudioFlow.Capture, slider); mute.Click += (_, _) => ToggleMute(AudioFlow.Capture); }
        else { outputSlider = slider; outputValue = value; outputMute = mute; outputName = name; slider.ValueChanged += (_, _) => ChangeVolume(AudioFlow.Render, slider); mute.Click += (_, _) => ToggleMute(AudioFlow.Render); }
        card.Controls.AddRange([title, value, slider, mute, name]); return card;
    }

    private Control BuildRgbCard()
    {
        var card = new CardPanel { Width = 390, Height = 105, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(20) };
        var title = Label("RGB WIZUALIZACJI  •  PODGLĄD", 8.5f, FontStyle.Bold, Theme.Muted); title.SetBounds(20, 13, 330, 21); card.Controls.Add(title);
        Color[] colors = [Theme.Purple, Color.FromArgb(56, 189, 248), Theme.Green, Theme.Red, Theme.Gold];
        for (int i = 0; i < colors.Length; i++) { var color = colors[i]; var b = new ZeusButton { Text = "●", ForeColor = color, Font = new Font("Segoe UI", 17, FontStyle.Bold), Accent = Theme.Panel2 }; b.SetBounds(20 + i * 67, 43, 52, 42); b.Click += (_, _) => { settings.RgbColor = color; visual.RgbColor = color; visual.Invalidate(); SaveSettings(); }; card.Controls.Add(b); }
        return card;
    }

    private Control BuildSurroundCard()
    {
        var card = new CardPanel { Width = 390, Height = 122, Margin = new Padding(0, 0, 0, 12) };
        var title = Label("WBUDOWANE AUDIO 7.1", 8.5f, FontStyle.Bold, Theme.Muted); title.SetBounds(20, 13, 320, 22);
        var info = Label("H510 Pro przełącza 7.1 fizycznym przyciskiem.", 8.5f, FontStyle.Regular, Theme.Text); info.SetBounds(20, 39, 345, 22);
        var help = new ZeusButton { Text = "JAK WŁĄCZYĆ / WYŁĄCZYĆ 7.1", Accent = Color.FromArgb(49, 46, 129) }; help.SetBounds(20, 70, 345, 38);
        help.Click += (_, _) => MessageBox.Show(this, "Naciśnij fizyczny przycisk 7.1 na słuchawkach. Redragon nie udostępnia aplikacji komendy ani stanu tego przycisku, dlatego program nie pokazuje fałszywego przełącznika.", "Sprzętowe 7.1", MessageBoxButtons.OK, MessageBoxIcon.Information);
        card.Controls.AddRange([title, info, help]); return card;
    }

    private void BuildEqualizer(Panel page)
    {
        page.Controls.Add(Header("Korektor i profil brzmienia", "10 PASM  •  PRESETY MUZYCZNE"));
        var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(24) }; card.Top = 92;
        equalizer = new EqualizerControl { Dock = DockStyle.Fill, Values = settings.Eq };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(0, 12, 0, 0) };
        bottom.Controls.Add(PresetButton("FLAT", [0,0,0,0,0,0,0,0,0,0])); bottom.Controls.Add(PresetButton("GAMING", [-2,-1,0,1,2,3,4,3,1,-1])); bottom.Controls.Add(PresetButton("MUZYKA", [3,2,1,0,0,1,2,2,1,1]));
        var note = Label("Profil EQ jest zapisywany. Systemowe przetwarzanie wymaga APO/DSP.", 8.2f, FontStyle.Regular, Theme.Muted); note.AutoSize = true; note.Margin = new Padding(20, 14, 0, 0); bottom.Controls.Add(note);
        card.Controls.Add(equalizer); card.Controls.Add(bottom); var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 92, 0, 0) }; wrap.Controls.Add(card); page.Controls.Add(wrap); wrap.BringToFront();
    }

    private Control PresetButton(string name, int[] values)
    {
        var b = new ZeusButton { Text = name, Width = 125, Accent = name == "GAMING" ? Color.FromArgb(49, 46, 129) : Theme.Panel2, Margin = new Padding(0, 0, 10, 0) };
        b.Click += (_, _) => { equalizer.Values = values; settings.Eq = [.. values]; SaveSettings(); }; return b;
    }

    private void BuildProfiles(Panel page)
    {
        page.Controls.Add(Header("Profile", "JEDNO KLIKNIĘCIE — GŁOŚNOŚĆ, MIKROFON I CHARAKTER BRZMIENIA"));
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 105, 0, 0), ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        grid.Controls.Add(ProfileCard("GAMING", "75% audio  •  85% mikrofon\nWyraźniejsze kroki", Theme.Purple, () => ApplyProfile(75, 85, [-2,-1,0,1,2,3,4,3,1,-1])), 0, 0);
        grid.Controls.Add(ProfileCard("MUZYKA", "65% audio  •  pełne pasmo\nLekko podbity bas", Color.FromArgb(56, 189, 248), () => ApplyProfile(65, 80, [3,2,1,0,0,1,2,2,1,1])), 1, 0);
        grid.Controls.Add(ProfileCard("NOC", "28% audio  •  65% mikrofon\nŁagodniejsza góra", Theme.Gold, () => ApplyProfile(28, 65, [0,0,1,1,0,0,-1,-2,-3,-4])), 2, 0);
        page.Controls.Add(grid); grid.BringToFront();
    }

    private Control ProfileCard(string name, string description, Color color, Action apply)
    {
        var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(7), Padding = new Padding(25) };
        var icon = Label(name[0].ToString(), 27, FontStyle.Bold, Theme.Text); icon.BackColor = color; icon.TextAlign = ContentAlignment.MiddleCenter; icon.SetBounds(55, 55, 82, 82);
        var title = Label(name, 18, FontStyle.Bold, Theme.Text); title.SetBounds(35, 175, 260, 40);
        var desc = Label(description, 10, FontStyle.Regular, Theme.Muted); desc.SetBounds(35, 228, 275, 70);
        var button = new ZeusButton { Text = "AKTYWUJ PROFIL", Accent = color, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom }; button.SetBounds(35, 500, 270, 48); button.Click += (_, _) => apply();
        card.Controls.AddRange([icon, title, desc, button]); return card;
    }

    private void BuildDiagnostics(Panel page)
    {
        page.Controls.Add(Header("Diagnostyka", "SPRZĘT  •  WINDOWS CORE AUDIO  •  STATUS APLIKACJI"));
        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(28) };
        diagnostics = Label("Oczekiwanie na odczyt…", 10, FontStyle.Regular, Theme.Text); diagnostics.Dock = DockStyle.Fill;
        var refresh = new ZeusButton { Text = "ODŚWIEŻ TERAZ", Accent = Color.FromArgb(49, 46, 129), Dock = DockStyle.Bottom, Height = 45 }; refresh.Click += (_, _) => RefreshAudio();
        card.Controls.Add(diagnostics); card.Controls.Add(refresh); var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 95, 0, 0) }; wrap.Controls.Add(card); page.Controls.Add(wrap); wrap.BringToFront();
    }

    private Panel Header(string title, string subtitle)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Theme.Background };
        var t = Label(title, 20, FontStyle.Bold, Theme.Text); t.SetBounds(0, 0, 700, 38); var s = Label(subtitle, 8.5f, FontStyle.Bold, Theme.Muted); s.SetBounds(2, 42, 750, 25); p.Controls.AddRange([t, s]); return p;
    }
    private static Label Label(string text, float size, FontStyle style, Color color) => new() { Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color, BackColor = Color.Transparent };

    private void SetPage(string name)
    {
        foreach (var pair in pages) pair.Value.Visible = pair.Key == name;
        foreach (var pair in nav) pair.Value.Accent = pair.Key == name ? Color.FromArgb(49, 46, 129) : Theme.Panel2;
        pages[name].BringToFront();
    }

    private void RefreshAudio()
    {
        snapshot = AudioService.Read(); syncing = true;
        try
        {
            outputSlider.Value = (int)Math.Round(snapshot.OutputVolume * 100); micSlider.Value = (int)Math.Round(snapshot.MicVolume * 100);
            outputValue.Text = outputSlider.Value + "%"; micValue.Text = micSlider.Value + "%"; outputName.Text = snapshot.OutputName; micName.Text = snapshot.MicName;
            outputMute.Text = snapshot.OutputMuted ? "WŁĄCZ SŁUCHAWKI" : "WYCISZ SŁUCHAWKI"; micMute.Text = snapshot.MicMuted ? "WŁĄCZ MIKROFON" : "WYCISZ MIKROFON";
            micSlider.Accent = snapshot.MicMuted ? Theme.Red : Theme.Green; visual.MicMuted = snapshot.MicMuted; visual.Volume = outputSlider.Value; visual.Invalidate();
            deviceBadge.Text = snapshot.IsZeus ? "●  ZEUS POŁĄCZONY\n     Native .NET  •  v3.0" : snapshot.Available ? "●  WINDOWS AUDIO\n     Ustaw Zeus jako domyślny" : "●  BRAK AUDIO";
            deviceBadge.ForeColor = snapshot.IsZeus ? Theme.Green : snapshot.Available ? Theme.Gold : Theme.Red;
            heroStatus.Text = snapshot.IsZeus ? "ZEUS PRO WYKRYTY" : snapshot.Available ? "STEROWANIE DOMYŚLNYM AUDIO" : "BRAK DOSTĘPU DO URZĄDZENIA";
            heroStatus.ForeColor = snapshot.IsZeus ? Theme.Green : snapshot.Available ? Theme.Gold : Theme.Red;
            diagnostics.Text = $"STATUS AUDIO\n\nWarstwa Core Audio: {(snapshot.Available ? "POŁĄCZONA" : "BŁĄD")}\nModel Zeus: {(snapshot.IsZeus ? "WYKRYTY" : "NIE WYKRYTY W NAZWIE DOMYŚLNEGO URZĄDZENIA")}\n\nDOMYŚLNE URZĄDZENIA\n\nWyjście: {snapshot.OutputName}\nMikrofon: {snapshot.MicName}\n\nPoziom wyjścia: {outputSlider.Value}%  /  mute: {snapshot.OutputMuted}\nPoziom mikrofonu: {micSlider.Value}%  /  mute: {snapshot.MicMuted}\n\nWersja: 3.0.0  •  .NET {Environment.Version}\nSystem: {Environment.OSVersion}\n\n{snapshot.Error}";
        }
        finally { syncing = false; }
    }

    private void ChangeVolume(AudioFlow flow, ZeusSlider slider)
    {
        if (syncing || selfTest) return;
        try { AudioService.SetVolume(flow, slider.Value / 100f); if (flow == AudioFlow.Render) outputValue.Text = slider.Value + "%"; else micValue.Text = slider.Value + "%"; visual.Volume = outputSlider.Value; visual.Invalidate(); }
        catch (Exception ex) { ShowAudioError(ex); }
    }
    private void ToggleMute(AudioFlow flow)
    {
        try { AudioService.SetMute(flow, flow == AudioFlow.Render ? !snapshot.OutputMuted : !snapshot.MicMuted); RefreshAudio(); }
        catch (Exception ex) { ShowAudioError(ex); }
    }
    private void ApplyProfile(int output, int mic, int[] eq)
    {
        try { AudioService.SetVolume(AudioFlow.Render, output / 100f); AudioService.SetVolume(AudioFlow.Capture, mic / 100f); equalizer.Values = eq; settings.Eq = [.. eq]; SaveSettings(); RefreshAudio(); SetPage("PULPIT"); }
        catch (Exception ex) { ShowAudioError(ex); }
    }
    private void ShowAudioError(Exception ex) => MessageBox.Show(this, "Windows odrzucił zmianę urządzenia audio.\n\n" + ex.Message, "ZEUS CONTROL — Core Audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void EnableDarkTitleBar() { try { int enabled = 1; DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)); } catch { } }

    private void LoadSettings()
    {
        try { if (File.Exists(settingsPath)) settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new(); } catch { settings = new(); }
    }
    private void SaveSettings()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); settings.Eq = equalizer?.Values ?? settings.Eq; File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings)); } catch { }
    }
}

internal sealed class AppSettings
{
    public int RgbArgb { get; set; } = Theme.Purple.ToArgb();
    public int[] Eq { get; set; } = new int[10];
    [System.Text.Json.Serialization.JsonIgnore]
    public Color RgbColor { get => Color.FromArgb(RgbArgb); set => RgbArgb = value.ToArgb(); }
}
