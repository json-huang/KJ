using Prism.Ioc;
using Prism.DryIoc;
using System.Windows;

namespace KJ
{
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 第一阶段：先把壳窗口和导航跑通；后续再把 Core/Comms/Module 注册补齐
            containerRegistry.RegisterSingleton<KJ.Comms.Abstractions.ITagStore, KJ.Core.TagStore>();

            // 第一阶段：用本机 TCP 配置占位；后续在 Monitoring 模块里做可配置化与 DriverFactory
            containerRegistry.RegisterSingleton<KJ.Comms.Abstractions.ITransport>(
                () => new KJ.Comms.Drivers.Tcp.TcpTransport("127.0.0.1", 9000));

            containerRegistry.RegisterSingleton<KJ.Comms.Abstractions.ICommsService, KJ.Core.CommsService>();
        }

    }

}
