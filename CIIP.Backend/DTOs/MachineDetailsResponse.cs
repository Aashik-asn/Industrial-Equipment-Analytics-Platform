namespace CIIP.Backend.DTOs;

public class MachineDetailsResponse
{
    public Guid MachineId { get; set; }
    public string? MachineCode { get; set; }
    public string? MachineName { get; set; }
    public string? Status { get; set; }

    public double HealthScore { get; set; }
    public double RuntimeHours { get; set; }

    public List<TrendPoint> HealthTrend { get; set; } = new();
    public List<TrendPoint> LoadTrend { get; set; } = new();
    public List<TrendPoint> VibrationTrend { get; set; } = new();

    public ElectricalSnapshot? Electrical { get; set; }
    public EnvironmentalSnapshot? Environmental { get; set; }
    public MechanicalSnapshot? Mechanical { get; set; }

    public SystemHealthIndicators? SystemHealth { get; set; }

    public List<AlertSummary> Alerts { get; set; } = new();
}

public class TrendPoint
{
    public DateTime Time { get; set; }
    public decimal Value { get; set; }
}

public class AlertSummary
{
    public string? Severity { get; set; }
    public string? Parameter { get; set; }
}

public class ElectricalSnapshot
{
    public decimal RVoltage { get; set; }
    public decimal YVoltage { get; set; }
    public decimal BVoltage { get; set; }

    public decimal RCurrent { get; set; }
    public decimal YCurrent { get; set; }
    public decimal BCurrent { get; set; }

    public decimal PowerFactor { get; set; }
    public decimal Frequency { get; set; }
}

public class EnvironmentalSnapshot
{
    public decimal Temperature { get; set; }
    public decimal Humidity { get; set; }
    public decimal FlowRate { get; set; }
    public decimal Pressure { get; set; }
}

public class MechanicalSnapshot
{
    public decimal VibrationX { get; set; }
    public decimal VibrationY { get; set; }
    public decimal VibrationZ { get; set; }
    public decimal RPM { get; set; }
}

public class SystemHealthIndicators
{
    public double OverallHealth { get; set; }
    public double PerformanceIndex { get; set; }
    public double EfficiencyScore { get; set; }
}
