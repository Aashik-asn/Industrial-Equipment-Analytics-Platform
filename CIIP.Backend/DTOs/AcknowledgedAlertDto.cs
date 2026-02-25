public class AcknowledgedAlertDto
{
    public Guid AlertId { get; set; }
    public string Parameter { get; set; } = default!;
    public string PlantName { get; set; } = default!;
    public string MachineCode { get; set; } = default!;
    public string TechnicianName { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public string ActionTaken { get; set; } = default!;
    public DateTime AcknowledgedAt { get; set; }
}