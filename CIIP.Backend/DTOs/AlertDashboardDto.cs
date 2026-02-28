namespace CIIP.Backend.DTOs;

public class AlertDashboardDto
{
    // ===== SUMMARY =====
    public int Critical { get; set; }
    public int Warning { get; set; }
    public int Acknowledged { get; set; }

    // ===== ALERT LIST =====
    public List<AlertItemDto> Alerts { get; set; } = new();
}

public class AlertItemDto
{
    public Guid AlertId { get; set; }

    public string Severity { get; set; } = default!;
    public string Parameter { get; set; } = default!;

    public decimal ActualValue { get; set; }

    public string PlantName { get; set; } = default!;
    public string MachineCode { get; set; } = default!;
    public string MachineName { get; set; } = default!;

    public DateTime GeneratedAt { get; set; }

    // PENDING / ACKNOWLEDGED
    public string Status { get; set; } = default!;
}