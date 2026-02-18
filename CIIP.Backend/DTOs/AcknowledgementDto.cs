namespace CIIP.Backend.DTOs;

public class AcknowledgementDto
{
    public Guid AlertId { get; set; }

    public string TechnicianName { get; set; } = default!;

    public string Reason { get; set; } = default!;

    public string ActionTaken { get; set; } = default!;
}
