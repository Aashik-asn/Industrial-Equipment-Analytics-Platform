namespace CIIP.Backend.DTOs.Dashboard;

public class DashboardResponse
{
    public int TotalActiveMachines { get; set; }
    public int ActiveAlerts { get; set; }
    public double AvgEfficiency { get; set; }

    public List<AlertSeverityDto> AlertDistribution { get; set; } = new();
    public List<EnergyPoint> EnergyTrend { get; set; } = new();
    public List<PlantCardDto> Plants { get; set; } = new();
    public List<OeePoint> OeeTrend { get; set; } = new();
    public List<ProductionPoint> ProductionTrend { get; set; } = new();

}

public class AlertSeverityDto
{
    public string Severity { get; set; } = "";
    public int Count { get; set; }
}

public class EnergyPoint
{
    public DateTime Time { get; set; }
    public double Energy { get; set; }
}

public class PlantCardDto
{
    public Guid PlantId { get; set; }
    public string PlantName { get; set; } = "";
    public int Machines { get; set; }
    public double Efficiency { get; set; }
}
