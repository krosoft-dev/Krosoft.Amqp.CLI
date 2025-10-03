////namespace Krosoft.Amqp.CLI;

//using System.Diagnostics;

//internal static class ProgramConfig
//{
////    public static Task<int> Configure(Options.ConfigureOptions opts)
////        => GetManager()
////            .Configure(opts.Token, opts.Url, opts.Agent, opts.Validate);

////    public static Task<int> Status() => GetManager().Status();
////    public static Task<int> Remove() => GetManager().Remove();
//    public static Task<int> Pull() => GetManager().Pull();
//    public static Task<int> Clean() => GetManager().Clean();

//    private static IGitManager GetManager() => new GitManager();

////    private static IConfigManager GetManager() => BuildHost()
////                                                  .Services
////                                                  .GetRequiredService<IConfigManager>();

////    private static IHost BuildHost()
////    {
////        var builder = HostApplicationBuilderHelper.Create(null, false, false, false);
////        builder.Services.AddTransient<IConfigManager, ConfigManager>();
////        var host = builder.Build();
////        return host;
////    }
//}

using Krosoft.Amqp.CLI.Interfaces;
using Krosoft.Amqp.CLI.Managers;

namespace Krosoft.Amqp.CLI;

internal static class ProgramGit
{
    public static Task<int> Pull() => GetManager().Pull();
    public static Task<int> Clean() => GetManager().Clean();

    private static IGitManager GetManager() => new GitManager();

    public static Task<int> Handle(Options.RunOptions opts)
    {
        if (opts.Clean)
        {
            return Clean();
        }

        if (opts.Pull)
        {
            return Pull();
        }

        return Task.FromResult(-1);
    }
}