namespace CIIP.Backend.DTOs;

public class ThresholdInput
{
    public Guid TenantId { get; set; }
    public string? MachineType { get; set; }

    public string Parameter { get; set; } = default!;
    public decimal WarningValue { get; set; }
    public decimal CriticalValue { get; set; }
}
