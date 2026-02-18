namespace CIIP.Backend.DTOs;

public class ThresholdDto
{
    public string Parameter { get; set; } = default!;
    public decimal WarningValue { get; set; }
    public decimal CriticalValue { get; set; }
    public string? MachineType { get; set; }
    public Guid? TenantId { get; set; }
}
