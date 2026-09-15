using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnePieceCardGrader.App.Services;
using OnePieceCardGrader.App.Views;
using OnePieceCardGrader.Infrastructure.Database;
using OnePieceCardGrader.Infrastructure.FileStorage;

namespace OnePieceCardGrader.App;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services ?? throw new InvalidOperationException("App host is not started.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((_, services) => services.AddGraderApplication())
            .Build();

        await _host.StartAsync();
        AppPaths.EnsureCreated();
        var db = _host.Services.GetRequiredService<GraderDbContext>();
        await db.Database.EnsureCreatedAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    public static T LoadJson<T>(string relativePath) where T : new()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            return new T();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new T();
    }
}
