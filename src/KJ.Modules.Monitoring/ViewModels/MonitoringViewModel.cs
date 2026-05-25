
using KJ.Domain;
using Prism.Commands;
using Prism.Mvvm;

namespace KJ.Modules.Monitoring.ViewModels;

public sealed class MonitoringViewModel : BindableBase
{
    private readonly ICommsService _commsService;
    private readonly ITagStore _tagStore;

    private string _heartbeat = "-";
    public string Heartbeat
    {
        get => _heartbeat;
        private set => SetProperty(ref _heartbeat, value);
    }

    public DelegateCommand StartCommand { get; }
    public DelegateCommand StopCommand { get; }

    public MonitoringViewModel(ICommsService commsService, ITagStore tagStore)
    {
        _commsService = commsService;
        _tagStore = tagStore;

        StartCommand = new DelegateCommand(async () => await _commsService.StartAsync());
        StopCommand = new DelegateCommand(async () => await _commsService.StopAsync());

        _tagStore.TagUpdated += (_, tv) =>
        {
            if (tv.Id.Value == "Heartbeat")
                Heartbeat = tv.Value?.ToString() ?? "-";
        };
    }
}

