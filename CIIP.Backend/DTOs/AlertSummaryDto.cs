namespace CIIP.Backend.DTOs;

public class AlertSummaryDto
{
    public int Critical { get; set; }
    public int Warning { get; set; }
    public int Acknowledged { get; set; }
}
