using USP.Core.Models.Entities;

namespace USP.Core.Services.Threat;

/// <summary>
/// ML-based threat analytics engine for anomaly detection
/// </summary>
public interface IThreatAnalyticsEngine
{
    /// <summary>
    /// Score an audit log entry for threat using ML models
    /// </summary>
    Task<ThreatScore> ScoreAuditLogAsync(AuditLog auditLog);

    /// <summary>
    /// Train anomaly detection model from historical audit data
    /// </summary>
    Task<MlModelMetadata> TrainAnomalyDetectionModelAsync(DateTime startDate, DateTime endDate, Guid trainedBy);

    /// <summary>
    /// Load active ML model for predictions
    /// </summary>
    Task LoadActiveModelAsync(string modelName);

    /// <summary>
    /// Detect anomalies in user behavior using ML
    /// </summary>
    Task<AnomalyDetectionResult> DetectBehavioralAnomalyAsync(Guid userId, AuditLog auditLog);

    /// <summary>
    /// Get model performance metrics
    /// </summary>
    Task<ModelMetrics> GetModelMetricsAsync(string modelName);

    /// <summary>
    /// Retrain model with new data
    /// </summary>
    Task RetrainModelAsync(string modelName, Guid trainedBy);
}

public class ThreatScore
{
    public double Score { get; set; } // 0-100
    public bool IsAnomaly { get; set; }
    public double Confidence { get; set; } // 0-1
    public string DetectionMethod { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public Dictionary<string, double> FeatureScores { get; set; } = new();
}

public class AnomalyDetectionResult
{
    public bool IsAnomaly { get; set; }
    public double AnomalyScore { get; set; } // 0-100
    public double Confidence { get; set; }
    public List<string> AnomalyReasons { get; set; } = new();
    public Dictionary<string, double> FeatureDeviations { get; set; } = new();
}

public class ModelMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double FalsePositiveRate { get; set; }
    public int TotalPredictions { get; set; }
    public DateTime LastUpdated { get; set; }
}
