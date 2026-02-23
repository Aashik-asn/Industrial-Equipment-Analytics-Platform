namespace CIIP.Backend.Services;

public class ThresholdConfig
{
    // =========================
    // VALUES
    // =========================
    public decimal VibrationWarning { get; set; }
    public decimal VibrationCritical { get; set; }

    public decimal CurrentWarning { get; set; }
    public decimal CurrentCritical { get; set; }

    public decimal RpmWarningLow { get; set; }
    public decimal RpmCriticalLow { get; set; }

    public decimal RpmWarningHigh { get; set; }
    public decimal RpmCriticalHigh { get; set; }

    public decimal TemperatureWarning { get; set; }
    public decimal TemperatureCritical { get; set; }

    public decimal LoadHighWarning { get; set; }
    public decimal LoadHighCritical { get; set; }

    public decimal LoadLowWarning { get; set; }
    public decimal LoadLowCritical { get; set; }


    // =========================
    // VERSION LOCK (VERY IMPORTANT)
    // =========================
    public Guid? VibrationThresholdId { get; set; }
    public Guid? CurrentThresholdId { get; set; }
    public Guid? RpmLowThresholdId { get; set; }
    public Guid? RpmHighThresholdId { get; set; }

    public Guid? TemperatureThresholdId { get; set; }

    public Guid? LoadHighThresholdId { get; set; }
    public Guid? LoadLowThresholdId { get; set; }

}
