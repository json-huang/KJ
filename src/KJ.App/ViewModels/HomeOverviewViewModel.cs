using KJ.App;
using KJ.Domain;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.App.ViewModels;

public sealed class HomeOverviewViewModel : BindableBase
{
    private readonly ITagStore _tagStore;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    private string _heartbeat = "-";
    public string Heartbeat
    {
        get => _heartbeat;
        private set => SetProperty(ref _heartbeat, value);
    }

    public DelegateCommand StartCommand { get; }
    public DelegateCommand StopCommand { get; }

    public HomeOverviewViewModel(ITagStore tagStore)
    {
        _tagStore = tagStore;
        StartCommand = new DelegateCommand(Start, () => _loop is null);
        StopCommand = new DelegateCommand(Stop, () => _loop is not null);

        _tagStore.TagUpdated += (_, tv) =>
        {
            if (tv.Id.Value != "Heartbeat")
                return;

            void Apply() => Heartbeat = tv.Value?.ToString() ?? "-";

            if (App.UiDispatcher is { } dq)
                _ = dq.TryEnqueue(Apply);
            else
                Apply();
        };
    }

    private void Start()
    {
        if (_loop is not null)
            return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                _tagStore.Upsert(new TagValue(
                    new TagId("Heartbeat"),
                    DateTimeOffset.Now.ToString("HH:mm:ss.fff"),
                    TagQuality.Good,
                    DateTimeOffset.Now));
                await Task.Delay(500, ct);
            }
        }, ct);

        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    private void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;

        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
