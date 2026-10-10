using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2MapOverlay.Capture;
using Aion2MapOverlay.Core.Markers;
using Aion2MapOverlay.Data;
using Aion2MapOverlay.Vision;
using Microsoft.Win32;

namespace Aion2MapOverlay.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<MarkerGroup> groups = [];
    private Dictionary<string, ImageSource> icons = [];
    private MarkerIndex? index;
    private HashSet<string> selected = [];
    private OverlayWindow? overlay;
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private readonly DispatcherTimer saveTimer;
    private readonly DispatcherTimer resourceTimer;
    private readonly Process ownProcess = Process.GetCurrentProcess();
    private readonly Stopwatch resourceClock = Stopwatch.StartNew();
    private TimeSpan previousCpuTime;
    private double previousSampleSeconds;
    private bool ready, closing;
    private DateTime lastStatus;
    private sealed record SavedSettings(string[] Enabled, double Size, double Opacity, int Display);

    public MainWindow()
    {
        InitializeComponent();
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveSettings(); };
        previousCpuTime = ownProcess.TotalProcessorTime;
        previousSampleSeconds = resourceClock.Elapsed.TotalSeconds;
        resourceTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        resourceTimer.Tick += (_, _) => UpdateResourceUsage();
        resourceTimer.Start();
        Closed += (_, _) => { resourceTimer.Stop(); ownProcess.Dispose(); };
        DisplayPicker.ItemsSource = DesktopCapture.Displays(); DisplayPicker.SelectedIndex = 0;
        for (var i = 0; i < DisplayPicker.Items.Count; i++)
            if (DisplayPicker.Items[i] is DisplayArea { X: 0, Y: 0 }) DisplayPicker.SelectedIndex = i;
        SourceInitialized += (_, _) => OverlayWindow.Exclude(this);
        Loaded += (_, _) => LoadData();
        Closing += OnClosing;
    }

    private void LoadData()
    {
        try
        {
            index = QuestlogData.LoadVerteron(Paths.Markers, TerrainTracker.ReferenceSize);
            icons = IconCache.Load();
            SavedSettings? saved = null;
            if (File.Exists(Paths.Settings))
            {
                try { saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(Paths.Settings)); }
                catch (JsonException) { StatusText.Text = "Previous settings could not be read; using defaults."; }
            }
            foreach (var group in index.Markers.GroupBy(m => m.GroupKey).OrderBy(g => g.First().Type).ThenBy(g => g.First().Name))
            {
                var first = group.First();
                var item = new MarkerGroup { Key = group.Key, Name = first.Name, Type = first.Type, Count = group.Count(),
                    Icon = icons.GetValueOrDefault(group.Key), Enabled = saved is null ? first.Name == "Sapphire Gemstone" : saved.Enabled.Contains(group.Key) };
                item.PropertyChanged += (_, _) => SelectionChanged(); groups.Add(item);
            }
            if (saved is not null)
            {
                IconSize.Value = Math.Clamp(saved.Size, 10, 36); IconOpacity.Value = Math.Clamp(saved.Opacity, .25, 1);
                DisplayPicker.SelectedIndex = Math.Clamp(saved.Display, 0, Math.Max(0, DisplayPicker.Items.Count - 1));
            }
            GroupsList.ItemsSource = groups; ready = true; SelectionChanged();
            StatusText.Text = $"{index.Markers.Count:N0} locations · {groups.Count} toggleable categories. Ready.";
            UpdateResourceUsage();
        }
        catch (Exception error) { StartButton.IsEnabled = false; StatusText.Text = "Data could not be loaded: " + error.Message; }
    }

    internal void PrepareForCheck() => LoadData();

    private void UpdateResourceUsage()
    {
        ownProcess.Refresh();
        var seconds = resourceClock.Elapsed.TotalSeconds;
        var cpuTime = ownProcess.TotalProcessorTime;
        var elapsed = seconds - previousSampleSeconds;
        var cpu = elapsed > 0 ? (cpuTime - previousCpuTime).TotalSeconds / elapsed / Environment.ProcessorCount * 100 : 0;
        ResourceUsageText.Text = $"App CPU {Math.Clamp(cpu, 0, 100):F1}% · RAM {ownProcess.WorkingSet64 / (1024.0 * 1024):F0} MiB";
        previousCpuTime = cpuTime;
        previousSampleSeconds = seconds;
    }

    private void SelectionChanged()
    {
        Volatile.Write(ref selected, groups.Where(g => g.Enabled).Select(g => g.Key).ToHashSet());
        overlay?.Update(null, IconSize.Value, IconOpacity.Value);
        if (ready) { saveTimer.Stop(); saveTimer.Start(); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (index is null || DisplayPicker.SelectedItem is not DisplayArea display || worker is not null) return;
        overlay = new OverlayWindow(display, icons); overlay.Show(); overlay.EnsureCaptureExcluded();
        if (!overlay.CaptureExcluded)
        {
            overlay.Close(); overlay = null;
            StatusText.Text = "Windows could not exclude the overlay from capture. Live capture was not started."; return;
        }
        cancellation = new(); StartButton.IsEnabled = false; StopButton.IsEnabled = true; DisplayPicker.IsEnabled = false;
        StatusText.Text = "Preparing reference terrain…";
        var token = cancellation.Token;
        worker = Task.Run(async () =>
        {
            OpenCvSharp.Cv2.SetNumThreads(Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
            using var tracker = new TerrainTracker(Paths.Reference, Paths.Title);
            using var capture = new DesktopCapture(display);
            while (!token.IsCancellationRequested)
            {
                var watch = Stopwatch.StartNew();
                if (!OverlayWindow.IsGameForeground())
                {
                    tracker.Reset(); await Deliver(null, "Waiting for Aion 2 to be in front.");
                    await Task.Delay(200, token); continue;
                }
                var capturedAt = DateTime.UtcNow;
                using var gray = capture.GrabGray();
                var aligned = tracker.Process(gray);
                OverlayFrame? frame = null;
                if (aligned is not null)
                {
                    var t = aligned.Transform;
                    var start = t.Inverse(capture.Width * .18 - 30, capture.Height * .12 - 30);
                    var end = t.Inverse(capture.Width * .94 + 30, capture.Height * .94 + 30);
                    var enabled = Volatile.Read(ref selected);
                    var markers = index.Query(start.X, start.Y, end.X, end.Y).Where(m => enabled.Contains(m.GroupKey))
                        .Select(m => { var point = t.Apply(m.ReferenceX, m.ReferenceY); return new PlacedMarker(point.X, point.Y, m.GroupKey); }).ToArray();
                    frame = new(markers, capture.Width, capture.Height, capturedAt);
                }
                var elapsed = watch.Elapsed.TotalMilliseconds;
                await Deliver(frame, aligned is null ? "Verteron map not visible, or alignment lost." :
                    $"{aligned.Method} · {aligned.Inliers} points · {elapsed:F0} ms · {frame!.Markers.Length} visible markers");
                var interval = aligned is null ? 200 : 50;
                await Task.Delay(Math.Max(1, (int)(interval - watch.Elapsed.TotalMilliseconds)), token);
            }
        }, token);
        try { await worker; }
        catch (OperationCanceledException) { }
        catch (Exception error) { StatusText.Text = "Overlay stopped: " + error.Message; }
        finally
        {
            overlay?.Close(); overlay = null; worker = null; cancellation?.Dispose(); cancellation = null;
            StartButton.IsEnabled = true; StopButton.IsEnabled = false; DisplayPicker.IsEnabled = true;
        }
    }

    private async Task Deliver(OverlayFrame? frame, string status)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            overlay?.Update(frame, IconSize.Value, IconOpacity.Value);
            if (DateTime.UtcNow - lastStatus > TimeSpan.FromMilliseconds(250)) { StatusText.Text = status; lastStatus = DateTime.UtcNow; }
        }, DispatcherPriority.Render);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) { cancellation?.Cancel(); overlay?.Update(null, 20, 1); StatusText.Text = "Stopped."; }
    private void Appearance_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (ready) SelectionChanged(); }
    private void Search_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (GroupsList?.ItemsSource is null) return;
        CollectionViewSource.GetDefaultView(GroupsList.ItemsSource).Filter = item => ((MarkerGroup)item).Name.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase);
    }
    private void SetGroups(Func<MarkerGroup, bool> predicate) { foreach (var group in groups) group.Enabled = predicate(group); }
    private void All_Click(object sender, RoutedEventArgs e) => SetGroups(_ => true);
    private void None_Click(object sender, RoutedEventArgs e) => SetGroups(_ => false);
    private void Gatherables_Click(object sender, RoutedEventArgs e) => SetGroups(g => g.Type == "gatherable");
    private void Collectibles_Click(object sender, RoutedEventArgs e) => SetGroups(g => g.Type == "collectable");

    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Screenshots|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        var output = Path.Combine(AppContext.BaseDirectory, "captures", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        StatusText.Text = "Matching screenshots…";
        try
        {
            await Task.Run(() => Diagnostics.Run(dialog.FileNames, output, false));
            StatusText.Text = "Screenshot results saved to " + output;
            Process.Start(new ProcessStartInfo(output) { UseShellExecute = true });
        }
        catch (Exception error) { StatusText.Text = "Screenshot test failed: " + error.Message; }
    }

    private void SaveSettings()
    {
        if (!ready) return;
        try
        {
            var settings = new SavedSettings(groups.Where(g => g.Enabled).Select(g => g.Key).ToArray(), IconSize.Value, IconOpacity.Value, DisplayPicker.SelectedIndex);
            var temporary = Paths.Settings + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, Paths.Settings, true);
        }
        catch (Exception error) { StatusText.Text = "Settings could not be saved: " + error.Message; }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        SaveSettings(); saveTimer.Stop();
        if (worker is null) { overlay?.Close(); return; }
        e.Cancel = true; closing = true; cancellation?.Cancel();
        try { await worker; } catch { }
        overlay?.Close(); Close();
    }
}
