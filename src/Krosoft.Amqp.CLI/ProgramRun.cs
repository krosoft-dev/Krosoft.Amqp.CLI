//using System.Reflection;

//namespace Krosoft.Amqp.CLI;

//internal static class ProgramRun
//{
//    public static async Task<int> Run(Options.RunOptions opts)
//    {
//        var currentAssembly = Assembly.GetExecutingAssembly();

//        var assemblies = new[]
//        {
//            currentAssembly
//        };

//        var agentSettings = ProtectedDataHelper.Load<AgentSettings>(Constantes.ConfigFilePath);
//        var builder = HostApplicationBuilderHelper.Create(agentSettings, opts.RunAsService, opts.AutoUpdate, true);

//        builder.Services.AddWindowsService(options => { options.ServiceName = $"{Constantes.PrefixServiceName}_{agentSettings.Agent}"; });
//        builder.Services.AddHostedService<AgentBackgroundService>();
//        builder.Services.AddTransient<ISelfUpdateManager, SelfUpdateManager>();
//        builder.Services.AddTransient<ISelfUpdateValidator, SelfUpdateValidator>();
//        builder.Services.AddTransient<ISelfUpdateFilesManager, SelfUpdateFilesManager>();
//        builder.Services.AddTransient<IAgentEngine, AgentEngine>();
//        builder.Services.AddTransient<IUpdateEngine, UpdateEngine>();
//        builder.Services.AddTransient<IPackageExtractor, PackageExtractor>();
//        //builder.Services.AddTransient<IDependencyResolver, DependencyResolver>();
//        builder.Services.AddTransient<INuGetPackageDownloader, NuGetPackageDownloader>();
//        //builder.Services.AddTransient<IDependencyAwareAssemblyLoader, DependencyAwareAssemblyLoader>();
//        builder.Services.AddTransient<TaskProcessInvoker>();
//        //builder.Services.AddTransient<NuGetPackageProvider>();
//        //builder.Services.AddTransient<LocalFolderPackageProvider>();
//        builder.Services.AddTransient<IPackagesManager, PackagesManager>();
//        builder.Services.AddTransient<IPackageDownloaderService, HttpPackageDownloaderService>();
//        //builder.Services.AddTransient<IInvocableLoader, InvocableLoader>();
//        //builder.Services.AddOptionsValidator<PackageProviderSettings, PackageProviderSettingsValidateOptions>(builder.Configuration);
//        builder.Services.AddOptionsValidator<ModulusSettings, ModulusSettingsValidateOptions>(builder.Configuration);
//        builder.Services.AddDateTimeService();
//        builder.Services.AddSystemMetrics();
//        builder.Services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
//        builder.Services.AddHttpClient<IApiHttpService, ApiHttpService>((serviceProvider, client) =>
//        {
//            var settings = serviceProvider.GetRequiredService<IOptions<ModulusSettings>>().Value;
//            client.BaseAddress = new Uri(settings.ApiUrl);
//            client.DefaultRequestHeaders.Add(Constantes.Headers.ApiKey, settings.Token);
//            client.DefaultRequestHeaders.Add(Constantes.Headers.AgentId, settings.AgentName);
//        });

//        builder.Services.AddHttpClient<IAgentReleaseHttpService, AgentReleaseHttpService>((serviceProvider, client) =>
//        {
//            var settings = serviceProvider.GetRequiredService<IOptions<ModulusSettings>>().Value;
//            client.BaseAddress = new Uri(settings.ApiUrl);
//            client.DefaultRequestHeaders.Add(Constantes.Headers.ApiKey, settings.Token);
//            client.DefaultRequestHeaders.Add(Constantes.Headers.AgentId, settings.AgentName);
//        });
//        var host = builder.Build();
//        await host.RunAsync();

//        return 1;
//    }
//}