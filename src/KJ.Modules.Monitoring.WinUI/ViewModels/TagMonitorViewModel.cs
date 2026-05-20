using System.Collections.ObjectModel;
using KJ.Domain;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class TagMonitorViewModel : BindableBase
{
    private readonly ITagStore _tagStore;

    public ObservableCollection<TagDisplayItem> Tags { get; } = new();

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set => SetProperty(ref _filterText, value);
    }

    public DelegateCommand ClearCommand { get; }

    public TagMonitorViewModel(ITagStore tagStore)
    {
        _tagStore = tagStore;
        _tagStore.TagUpdated += OnTagUpdated;
        ClearCommand = new DelegateCommand(() => Tags.Clear());
    }

    private void OnTagUpdated(object? sender, TagValue value)
    {
        // Must dispatch to UI thread for WinUI
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            var existing = Tags.FirstOrDefault(t => t.Key == value.Id.Value);
            if (existing is not null)
            {
                existing.Value = value.Value?.ToString() ?? string.Empty;
                existing.Quality = value.Quality.ToString();
                existing.Timestamp = value.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                Tags.Add(new TagDisplayItem
                {
                    Key = value.Id.Value,
                    Value = value.Value?.ToString() ?? string.Empty,
                    Quality = value.Quality.ToString(),
                    Timestamp = value.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                });
            }
        });
    }
}

public sealed class TagDisplayItem : BindableBase
{
    private string _key = string.Empty;
    public string Key { get => _key; set => SetProperty(ref _key, value); }

    private string _value = string.Empty;
    public string Value { get => _value; set => SetProperty(ref _value, value); }

    private string _quality = string.Empty;
    public string Quality { get => _quality; set => SetProperty(ref _quality, value); }

    private string _timestamp = string.Empty;
    public string Timestamp { get => _timestamp; set => SetProperty(ref _timestamp, value); }
}
