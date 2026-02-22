namespace CIIP.Backend.DTOs;

public class ThresholdInput
{
    public string? MachineType { get; set; }

    public string Parameter { get; set; } = default!;
    public decimal WarningValue { get; set; }
    public decimal CriticalValue { get; set; }
}
