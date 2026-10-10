using System.ComponentModel;
using System.Windows.Media;

namespace Aion2MapOverlay.App;

public sealed class MarkerGroup : INotifyPropertyChanged
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required int Count { get; init; }
    public ImageSource? Icon { get; init; }
    public string TypeLabel => Type == "gatherable" ? "Gatherable" : "Collectible";
    private bool enabled;
    public bool Enabled { get => enabled; set { if (enabled == value) return; enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
