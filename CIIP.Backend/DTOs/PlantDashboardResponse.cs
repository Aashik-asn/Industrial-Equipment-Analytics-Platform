namespace CIIP.Backend.Services;

public class PlantDashboardResponse
{
    public int ActiveMachines { get; set; }
    public int TotalMachines { get; set; }

    public double PlantEfficiency { get; set; }
    public decimal TotalEnergy { get; set; }
    public double AvgRuntime { get; set; }

    public List<EnergyTrendPoint> EnergyTrend { get; set; } = new();
    public List<ProductionTrendPoint> ProductionTrend { get; set; } = new();
    public List<UptimePoint> UptimeDowntime { get; set; } = new();

    public List<MachineOverviewCard> Machines { get; set; } = new();
}

public class EnergyTrendPoint
{
    public DateTime Time { get; set; }
    public decimal Energy { get; set; }
}

public class ProductionTrendPoint
{
    public DateTime Time { get; set; }
    public decimal Actual { get; set; }
    public decimal Target { get; set; }
}

public class UptimePoint
{
    public string Label { get; set; } = "";
    public double Uptime { get; set; }
    public double Downtime { get; set; }
}

public class MachineOverviewCard
{
    public Guid MachineId { get; set; }
    public string MachineCode { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string MachineType { get; set; } = "";
    public string? Status { get; set; }

    public double HealthScore { get; set; }
    public double RuntimeHours { get; set; }
    public double AvgLoad { get; set; }
    public double CurrentLoad { get; set; }
}
