using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnePieceCardGrader.Core.DTOs;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Infrastructure.Database;
using OnePieceCardGrader.Infrastructure.Database.Entities;
using OnePieceCardGrader.Infrastructure.FileStorage;

namespace OnePieceCardGrader.Infrastructure.Repositories;

public sealed class AnalysisRepository : IAnalysisRepository
{
    private readonly GraderDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AnalysisRepository(GraderDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(CardAnalysisResult analysis, CancellationToken cancellationToken)
    {
        var existing = await _db.Analyses.FirstOrDefaultAsync(x => x.Id == analysis.AnalysisId, cancellationToken);
        var entity = existing ?? new CardAnalysisEntity { Id = analysis.AnalysisId, CreatedAt = analysis.CreatedAt };
        entity.CardName = analysis.Input.CardName;
        entity.CardNumber = analysis.Input.CardNumber;
        entity.SetName = analysis.Input.SetName;
        entity.Language = analysis.Input.Language;
        entity.Rarity = analysis.Input.Rarity;
        entity.Memo = analysis.Input.Memo;
        entity.PredictedGrade = analysis.Grading?.PredictedGrade;
        entity.GradeRangeMin = analysis.Grading?.GradeRangeMin;
        entity.GradeRangeMax = analysis.Grading?.GradeRangeMax;
        entity.Confidence = analysis.Grading?.AnalysisConfidence;
        entity.CenteringScore = analysis.Grading?.Centering.ConditionScore;
        entity.CornerScore = analysis.Grading?.Corners.Status == Core.Enums.AnalysisStatus.NotImplemented
            ? null
            : analysis.Grading?.Corners.ConditionScore;
        entity.EdgeScore = analysis.Grading?.Edges.Status == Core.Enums.AnalysisStatus.NotImplemented
            ? null
            : analysis.Grading?.Edges.ConditionScore;
        entity.SurfaceScore = analysis.Grading?.Surface.Status == Core.Enums.AnalysisStatus.NotImplemented
            ? null
            : analysis.Grading?.Surface.ConditionScore;
        entity.ThumbnailRelativePath = ToRelative(analysis.Front?.OverlayPath ?? analysis.Front?.NormalizedPath ?? analysis.Front?.OriginalPath);
        entity.MetricsJson = JsonSerializer.Serialize(new
        {
            analysis.Centering,
            analysis.Coverage,
            FrontQuality = analysis.Front?.Quality,
            BackQuality = analysis.Back?.Quality
        }, JsonOptions);
        entity.DefectsJson = JsonSerializer.Serialize(analysis.Defects, JsonOptions);
        entity.ResultJson = JsonSerializer.Serialize(analysis, JsonOptions);
        entity.UpdatedAt = DateTimeOffset.Now;

        if (existing is null)
        {
            _db.Analyses.Add(entity);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnalysisListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        return await _db.Analyses
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AnalysisListItemDto
            {
                Id = x.Id,
                CardName = x.CardName,
                CardNumber = x.CardNumber,
                PredictedGrade = x.PredictedGrade,
                GradeRangeMin = x.GradeRangeMin,
                GradeRangeMax = x.GradeRangeMax,
                Confidence = x.Confidence,
                CreatedAt = x.CreatedAt,
                ActualPsaGrade = x.ActualGrades.OrderByDescending(g => g.CreatedAt).Select(g => (int?)g.ActualGrade).FirstOrDefault(),
                ThumbnailPath = x.ThumbnailRelativePath
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CardAnalysisResult?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Analyses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity?.ResultJson is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CardAnalysisResult>(entity.ResultJson, JsonOptions);
    }

    public async Task SaveActualGradeAsync(ActualGradeFeedbackDto feedback, CancellationToken cancellationToken)
    {
        _db.ActualGrades.Add(new ActualGradeFeedbackEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = feedback.AnalysisId,
            ActualGrade = feedback.ActualGrade,
            CertNumber = feedback.CertNumber,
            SubmissionDate = feedback.SubmissionDate,
            UserNote = feedback.UserNote,
            CreatedAt = DateTimeOffset.Now
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExportJsonAsync(Guid id, string destinationPath, CancellationToken cancellationToken)
    {
        var entity = await _db.Analyses
            .Include(x => x.ActualGrades)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("분석 기록을 찾을 수 없습니다.");

        var export = new AnalysisExportDto
        {
            Card = new
            {
                entity.CardName,
                entity.CardNumber,
                entity.SetName,
                entity.Language,
                entity.Rarity,
                entity.Memo
            },
            Images = new { entity.ThumbnailRelativePath },
            Measurements = entity.MetricsJson is null ? null : JsonSerializer.Deserialize<object>(entity.MetricsJson),
            Defects = entity.DefectsJson is null ? null : JsonSerializer.Deserialize<object>(entity.DefectsJson),
            Prediction = new
            {
                entity.PredictedGrade,
                entity.GradeRangeMin,
                entity.GradeRangeMax,
                entity.Confidence
            },
            ActualGrade = entity.ActualGrades.OrderByDescending(x => x.CreatedAt).FirstOrDefault()
        };

        var json = JsonSerializer.Serialize(export, JsonOptions);
        await File.WriteAllTextAsync(destinationPath, json, cancellationToken);
    }

    private static string? ToRelative(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return null;
        }

        var root = AppPaths.Root;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, fullPath)
            : fullPath;
    }
}
