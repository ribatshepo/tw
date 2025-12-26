namespace USP.Core.Models.Entities;

/// <summary>
/// ML model metadata and versioning
/// </summary>
public class MlModelMetadata
{
    public Guid Id { get; set; }
    public string ModelName { get; set; } = string.Empty; // anomaly_detector, credential_stuffing, brute_force
    public string Version { get; set; } = string.Empty; // v1.0.0
    public string ModelType { get; set; } = string.Empty; // isolation_forest, one_class_svm, dbscan
    public string Algorithm { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty; // S3/MinIO path to .zip model file
    public long ModelSizeBytes { get; set; }
    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
    public Guid TrainedBy { get; set; }
    public int TrainingDatasetSize { get; set; }
    public DateTime TrainingDataStart { get; set; }
    public DateTime TrainingDataEnd { get; set; }
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double FalsePositiveRate { get; set; }
    public double FalseNegativeRate { get; set; }
    public string? ConfusionMatrix { get; set; } // JSON
    public string? FeatureImportance { get; set; } // JSON array of {feature, importance}
    public string? Hyperparameters { get; set; } // JSON
    public string Status { get; set; } = string.Empty; // training, validating, active, deprecated, archived
    public bool IsActive { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? DeprecatedAt { get; set; }
    public int PredictionCount { get; set; }
    public int TruePositives { get; set; }
    public int FalsePositives { get; set; }
    public int TrueNegatives { get; set; }
    public int FalseNegatives { get; set; }
    public double RunningAccuracy { get; set; }
    public string? ValidationNotes { get; set; }
    public string? Metadata { get; set; } // JSON for extensibility
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser Trainer { get; set; } = null!;
}
