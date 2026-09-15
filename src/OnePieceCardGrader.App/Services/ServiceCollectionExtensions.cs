using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnePieceCardGrader.App.ViewModels;
using OnePieceCardGrader.App.Views;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Grading.Profiles;
using OnePieceCardGrader.Grading.Rules;
using OnePieceCardGrader.Grading.Services;
using OnePieceCardGrader.Imaging;
using OnePieceCardGrader.Imaging.Centering;
using OnePieceCardGrader.Imaging.Corners;
using OnePieceCardGrader.Imaging.Detection;
using OnePieceCardGrader.Imaging.Edges;
using OnePieceCardGrader.Imaging.Pipeline;
using OnePieceCardGrader.Imaging.Quality;
using OnePieceCardGrader.Imaging.Surface;
using OnePieceCardGrader.Infrastructure.Database;
using OnePieceCardGrader.Infrastructure.FileStorage;
using OnePieceCardGrader.Infrastructure.Repositories;
using OnePieceCardGrader.Infrastructure.Settings;
using IoPath = System.IO.Path;

namespace OnePieceCardGrader.App.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGraderApplication(this IServiceCollection services)
    {
        var options = App.LoadJson<AnalysisOptions>(IoPath.Combine("Config", "analysis.json"));
        services.AddSingleton(options);
        services.AddSingleton<IGradingProfileProvider>(_ =>
            new JsonGradingProfileProvider(IoPath.Combine(AppContext.BaseDirectory, "Profiles")));
        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton<IImageStorage, FileImageStorage>();
        services.AddSingleton<IImageQualityAnalyzer, ImageQualityAnalyzer>();
        services.AddSingleton<ICardDetector, CardDetector>();
        services.AddSingleton<IPerspectiveCorrector, PerspectiveCorrector>();
        services.AddSingleton<ICenteringAnalyzer, CenteringAnalyzer>();
        services.AddSingleton<ICornerAnalyzer, NotImplementedCornerAnalyzer>();
        services.AddSingleton<IEdgeAnalyzer, NotImplementedEdgeAnalyzer>();
        services.AddSingleton<ISurfaceAnalyzer, NotImplementedSurfaceAnalyzer>();
        services.AddSingleton<ICriticalDefectEvaluator, CriticalDefectEvaluator>();
        services.AddSingleton<IGradingEngine, GradingEngine>();
        services.AddSingleton<IGradeExplanationService, GradeExplanationService>();
        services.AddSingleton<ICardTemplateProvider, NullCardTemplateProvider>();
        services.AddSingleton<IDefectDetectionModel, NullDefectDetectionModel>();
        services.AddSingleton<ICardPreviewService, CardPreviewService>();
        services.AddSingleton<ICardAnalysisPipeline, CardAnalysisPipeline>();
        services.AddSingleton<IAnalysisRepository, AnalysisRepository>();
        services.AddDbContext<GraderDbContext>(db =>
            db.UseSqlite($"Data Source={AppPaths.DatabasePath}"), ServiceLifetime.Singleton);

        services.AddSingleton<MainViewModel>();
        services.AddTransient<WizardViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<StandardsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
