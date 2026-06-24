using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using SweetPlayer.Core.Data;
using SweetPlayer.Services;
using SweetPlayer.Services.BackgroundTasks;
using SweetPlayer.Services.Browse;
using SweetPlayer.Services.Detection;
using SweetPlayer.Services.MediaSources;
using SweetPlayer.Services.Playback;
using SweetPlayer.Services.Scanning;
using SweetPlayer.Services.Scraping;
using SweetPlayer.Services.Security;
using SweetPlayer.Services.Subtitles;
using SweetPlayer.ViewModels;
using SweetPlayer.Views;

namespace SweetPlayer;

public partial class App : Application
{
    private Window? _window;

    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// 全局主窗口引用，供 WinUI 3 桌面应用中需要 HWND 的场景调用
    /// （如 FolderPicker.InitializeWithWindow）。与 <see cref="Views.MainWindowAccessor"/>
    /// 保持同步。
    /// </summary>
    public static Window? MainWindow => Views.MainWindowAccessor.Current;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    /// <summary>
    /// 注册 DbContext、导航服务、各 ViewModel 与页面。
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 加载配置文件
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // 数据库路径放在 LocalAppData/SweetPlayer 下
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SweetPlayer");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "sweetplayer.db");

        services.AddDbContextFactory<SweetPlayerDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // 日志
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
            // 屏蔽 EF Core 所有日志（Query / Command / Connection 等）
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        });

        // HttpClient（含 WebDAV 命名客户端）
        services.AddHttpClient();
        services.AddHttpClient(MediaSourceService.WebDavHttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 应用服务
        services.AddSingleton<INavigationService, NavigationService>();

        // 元数据刮削服务
        services.AddSingleton<ScrapingQueueOptions>();
        services.AddSingleton<IFileNameParser, FileNameParser>();
        services.AddHttpClient<IDoubanClient, DoubanClient>();

        // TMDB配置和客户端
        services.Configure<TmdbOptions>(configuration.GetSection("Tmdb"));
        services.AddHttpClient<ITmdbClient, TmdbClient>();

        services.AddSingleton<IPosterCacheService, PosterCacheService>();
        services.AddScoped<IScrapingService, ScrapingService>();
        services.AddScoped<ISeriesAggregationService, SeriesAggregationService>();
        services.AddSingleton<IScrapingQueueService, ScrapingQueueService>();
        services.AddSingleton<IPasswordProtector, Base64PasswordProtector>();
        services.AddSingleton<IMediaSourceService, MediaSourceService>();
        services.AddSingleton<IMediaScannerService, MediaScannerService>();
        services.AddSingleton<IDirectoryBrowseService, DirectoryBrowseService>();

        // 用户设置服务
        services.AddSingleton<SweetPlayer.Services.Settings.IUserSettingsService, 
                            SweetPlayer.Services.Settings.UserSettingsService>();

        // HDR / 杜比检测服务
        services.AddSingleton<VideoAnalysisOptions>();
        services.AddSingleton<IVideoAnalysisService, VideoAnalysisService>();
        services.AddSingleton<IHdrDetectionService, HdrDetectionService>();
        services.AddSingleton<IWindowsHdrService, WindowsHdrService>();

        // 视频播放引擎
        services.AddSingleton<IMpvPlayerService, MpvPlayerService>();
        services.AddSingleton<IPlaybackProgressService, PlaybackProgressService>();
        services.AddSingleton<IPlaybackControlService, PlaybackControlService>();
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();

        // 字幕管理
        services.AddHttpClient(ShooterApiClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<ISubtitleDiscoveryService, SubtitleDiscoveryService>();
        services.AddSingleton<ISubtitleLoadService, SubtitleLoadService>();
        services.AddSingleton<IShooterApiClient, ShooterApiClient>();
        services.AddSingleton<IOnlineSubtitleService, OnlineSubtitleService>();
        services.AddSingleton<ISubtitleTrackService, SubtitleTrackService>();
        services.AddSingleton<ISubtitleSettingsService, SubtitleSettingsService>();

        // 后台定时扫描
        services.AddSingleton<MediaSourceAutoScanService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MediaSourceAutoScanService>());

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PlayerViewModel>();
        services.AddTransient<MovieDetailViewModel>();
        services.AddTransient<SeriesDetailViewModel>();
        services.AddTransient<FileSourceBrowserViewModel>();

        // Views（让 DI 也能解析 Page 实例）
        services.AddTransient<HomePage>();
        services.AddTransient<SourcesPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<PlayerPage>();
        services.AddTransient<MovieDetailPage>();
        services.AddTransient<SeriesDetailPage>();
        services.AddTransient<FileSourceBrowserPage>();

        var provider = services.BuildServiceProvider();

        // 应用数据库迁移
        using (var scope = provider.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SweetPlayerDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.Migrate();
        }

        return provider;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        Views.MainWindowAccessor.Current = _window;
        _window.Activate();

        // 加载用户设置
        var userSettings = Services.GetRequiredService<SweetPlayer.Services.Settings.IUserSettingsService>();
        await userSettings.LoadAsync();

        // 启动刮削队列后台服务
        var scrapingQueue = Services.GetRequiredService<IScrapingQueueService>();
        scrapingQueue.Start(CancellationToken.None);

        // 启动后台定时扫描
        foreach (var hosted in Services.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }
    }
}
