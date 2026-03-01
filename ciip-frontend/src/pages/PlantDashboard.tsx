import { useState, useMemo, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useApolloClient } from '@apollo/client';
import {

  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  BarChart,
  Bar,
  AreaChart,
  Area,
} from 'recharts';
import dayjs from 'dayjs';
import { PLANT_DASHBOARD_QUERY, PLANTS_QUERY } from '../graphql/queries';
import '../styles/plantDashboard.css';

// TypeScript interfaces matching GraphQL response
interface EnergyTrendPoint {
  energy: number;
  time: string;
}

interface ProductionTrendPoint {
  actual: number;
  target: number;
  time: string;
}

interface UptimeDowntimePoint {
  label: string;
  uptime: number;
  downtime: number;
}

interface Machine {
  healthScore: number;
  machineCode: string;
  machineId: string;
  machineName: string;
  machineType: string;
  runtimeHours: number;
  status: string;
  avgLoad: number;
  currentLoad: number;
}

interface PlantDashboardData {
  activeMachines: number;
  avgRuntime: number;
  plantEfficiency: number;
  totalEnergy: number;
  totalMachines: number;
  energyTrend: EnergyTrendPoint[];
  productionTrend: ProductionTrendPoint[];
  uptimeDowntime: UptimeDowntimePoint[];
  machines: Machine[];
}

// Helper functions
const formatNumber = (value: number): string => {
  return Number(value).toFixed(2);
};

const formatDateFull = (dateString: string): string => {
  return dayjs(dateString).format('DD-MM-YYYY HH:mm');
};

const formatDateOnly = (dateString: string): string => {
  return dayjs(dateString).format('DD-MM-YYYY');
};

const formatTimeOnly = (dateString: string): string => {
  return dayjs(dateString).format('HH:mm');
};

const differenceInDays = (from: string, to: string): number => {
  const start = dayjs(from);
  const end = dayjs(to);
  return end.diff(start, 'day');
};

// Generate 4-hour interval ticks for the given date range
const generateFourHourTicks = (fromDate: string, toDate: string): number[] => {
  const ticks: number[] = [];
  let current = dayjs(fromDate).startOf('day');
  const end = dayjs(toDate).endOf('day');

  while (current.isBefore(end) || current.isSame(end, 'day')) {
    ticks.push(current.valueOf());
    current = current.add(4, 'hour');
  }

  return ticks;
};

// Format tick value to time string
const formatTick = (value: number): string => {
  return dayjs(value).format('HH:mm');
};

// Get status color based on machine status
const getStatusColor = (status: string | null | undefined): string => {
  if (!status) return '#9ca3af';
  const statusLower = status.toLowerCase();
  if (statusLower === 'running') return '#22c55e';
  if (statusLower === 'warning') return '#eab308';
  if (statusLower === 'critical') return '#ef4444';
  return '#9ca3af';
};

const getStatusDisplay = (status: string | null | undefined): string => {
  if (!status) return 'Unknown';
  const statusUpper = status.toUpperCase();
  if (statusUpper === 'RUNNING' || statusUpper === 'IDLE') return statusUpper;
  return statusUpper;
};

// Get health score color
const getHealthScoreColor = (score: number): string => {
  if (score >= 80) return '#22c55e';
  if (score >= 50) return '#eab308';
  return '#ef4444';
};

const PlantDashboard = () => {
  const params = useParams();
  const plantId = params.plantId;
  const navigate = useNavigate();

  const client = useApolloClient();
  const [dateFrom, setDateFrom] = useState<string>('');
  const [dateTo, setDateTo] = useState<string>('');

  // Reset filters when changing plant
  useEffect(() => {
    setDateFrom('');
    setDateTo('');
  }, [plantId]);
  const [searchQuery, setSearchQuery] = useState<string>('');

  const getDashboardVariables = () => {
    const variables: Record<string, unknown> = {
      plantId: plantId,
    };
    if (dateFrom) {
      const fromDate = new Date(dateFrom);
      if (!isNaN(fromDate.getTime())) {
        variables.from = fromDate.toISOString();
      }
    }
    if (dateTo) {
      const toDate = new Date(dateTo);
      if (!isNaN(toDate.getTime())) {
        variables.to = toDate.toISOString();
      }
    }
    return variables;
  };

  const {
    data: dashboardData,
    loading: dashboardLoading,
    error: dashboardError,
    refetch: refetchDashboard,
  } = useQuery(PLANT_DASHBOARD_QUERY, {
    variables: getDashboardVariables(),
    skip: !plantId,
    fetchPolicy: 'cache-and-network', // Ensure fresh data on refetch
  });

  const { data: plantsData } = useQuery(PLANTS_QUERY);
  const plantName = plantsData?.plants?.find((p: any) => p.plantId === plantId)?.plantName || '';

  // Refetch dashboard when filters change
  useEffect(() => {
    if (plantId) {
      refetchDashboard(getDashboardVariables());
    }
  }, [dateFrom, dateTo, refetchDashboard, plantId]);

  const dashboard: PlantDashboardData | null = dashboardData?.plantDashboard || null;

  // Determine if multi-day view
  const isMultiDay = useMemo(() => {
    if (!dateFrom || !dateTo) return false;
    return differenceInDays(dateFrom, dateTo) >= 1;
  }, [dateFrom, dateTo]);

  // Generate 4-hour interval ticks for single-day view
  const fourHourTicks = useMemo(() => {
    if (isMultiDay || !dateFrom || !dateTo) return undefined;
    return generateFourHourTicks(dateFrom, dateTo);
  }, [isMultiDay, dateFrom, dateTo]);

  // Get data min/max for time domain
  const timeDomain = useMemo(() => {
    if (!dashboard?.productionTrend || dashboard.productionTrend.length === 0) return undefined;
    const times = dashboard.productionTrend.map(p => dayjs(p.time).valueOf());
    return [Math.min(...times), Math.max(...times)];
  }, [dashboard?.productionTrend]);

  // Process chart data
  const processData = (data: PlantDashboardData) => {
    return {
      productionTrend: data.productionTrend?.map((point) => ({
        ...point,
        timeFormatted: formatDateFull(point.time),
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      energyTrend: data.energyTrend?.map((point) => ({
        ...point,
        timeFormatted: formatDateFull(point.time),
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      uptimeDowntime: data.uptimeDowntime?.map((point) => ({
        ...point,
        uptimePercent: point.uptime,
        downtimePercent: point.downtime,
      })) || [],
    };
  };

  const processedData = useMemo(() => {
    return dashboardData?.plantDashboard ? processData(dashboardData.plantDashboard) : null;
  }, [dashboardData, isMultiDay]);

  // Filter machines based on search query
  const filteredMachines = useMemo(() => {
    if (!dashboard?.machines) return [];
    if (!searchQuery.trim()) return dashboard.machines;

    const query = searchQuery.toLowerCase();
    return dashboard.machines.filter(machine =>
      machine.machineName.toLowerCase().includes(query) ||
      machine.machineCode.toLowerCase().includes(query) ||
      machine.machineType.toLowerCase().includes(query)
    );
  }, [dashboard?.machines, searchQuery]);

  const handleRefresh = async () => {
    await client.refetchQueries({ include: "active" });
  };

  // Custom tooltip formatter for Production chart
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const productionTooltipFormatter = (value: any, name?: string) => {
    return [formatNumber(Number(value)), name ? name.charAt(0).toUpperCase() + name.slice(1) : ''];
  };

  // Custom tooltip formatter for Energy chart
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const energyTooltipFormatter = (value: any) => {
    return [`${formatNumber(Number(value))} kWh`, 'Energy'];
  };

  // Custom tooltip formatter for Uptime/Downtime chart
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const uptimeTooltipFormatter = (value: any, name?: string) => {
    return [`${formatNumber(Number(value))}%`, name ? name.charAt(0).toUpperCase() + name.slice(1) : ''];
  };

  // Label formatter for all charts
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipLabelFormatter = (label: any) => {
    if (typeof label === 'number') {
      return dayjs(label).format('DD-MM-YYYY HH:mm');
    }
    return String(label);
  };

  if (!plantId) {
    return (
      <div className="plant-dashboard-container">
        <div className="error-container">
          <h3>Missing Plant ID</h3>
          <p>Please select a plant from the dashboard.</p>
        </div>
      </div>
    );
  }



  if (dashboardError) {
    return (
      <div className="plant-dashboard-container">
        <div className="error-container">
          <h3>Error Loading Dashboard</h3>
          <p>{dashboardError.message}</p>
          <button className="retry-button" onClick={handleRefresh}>Retry</button>
        </div>
      </div>
    );
  }

  return (
    <div className="plant-dashboard-container">
      {/* Header Section */}
      <div className="dashboard-header">
        <div className="dashboard-title-section">
          <h1 className="dashboard-title">Plant Dashboard {plantName ? `- ${plantName}` : ''}</h1>
          <p className="dashboard-subtitle">Real-time machine monitoring and plant operations</p>
        </div>
        <div className="dashboard-filters">
          <div className="filter-group">
            <label className="filter-label">Date Range</label>
            <div className="date-range-inputs">
              <input
                type="date"
                className="filter-date"
                value={dateFrom}
                max="9999-12-31"
                onChange={(e) => setDateFrom(e.target.value)}
              />
              <span className="date-separator">to</span>
              <input
                type="date"
                className="filter-date"
                value={dateTo}
                max="9999-12-31"
                onChange={(e) => setDateTo(e.target.value)}
              />
            </div>
          </div>
          <button className="refresh-button" onClick={handleRefresh} disabled={dashboardLoading}>
            {dashboardLoading ? 'Loading...' : 'Refresh'}
          </button>
        </div>
      </div>

      {dashboardLoading && (
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p>Loading dashboard data...</p>
        </div>
      )}

      {!dashboardLoading && dashboard && processedData && (
        <>
          {/* Row 1: KPI Cards */}
          <div className="kpi-cards-grid">
            {/* Active Machines Card */}
            <div className="kpi-card kpi-card-machines">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" stroke="#3b82f6" />
                </svg>
              </div>
              <div className="kpi-card-title">Active Machines</div>
              <div className="kpi-card-value">
                {dashboard.activeMachines} / {dashboard.totalMachines}
              </div>
              <div className="kpi-card-subtitle">Machines running</div>
            </div>

            {/* Plant Efficiency Card */}
            <div className="kpi-card kpi-card-efficiency">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
                </svg>
              </div>
              <div className="kpi-card-title">Plant Efficiency</div>
              <div className="kpi-card-value">{formatNumber(dashboard.plantEfficiency)}%</div>
              <div className="kpi-card-subtitle">Overall efficiency</div>
            </div>

            {/* Total Energy Card */}
            <div className="kpi-card kpi-card-energy">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
                </svg>
              </div>
              <div className="kpi-card-title">Total Energy</div>
              <div className="kpi-card-value">{formatNumber(dashboard.totalEnergy)}</div>
              <div className="kpi-card-subtitle">kWh consumed</div>
            </div>

            {/* Average Runtime Card */}
            <div className="kpi-card kpi-card-runtime">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
              </div>
              <div className="kpi-card-title">Avg Runtime</div>
              <div className="kpi-card-value">{formatNumber(dashboard.avgRuntime)}</div>
              <div className="kpi-card-subtitle">Hours</div>
            </div>
          </div>

          {/* Machine Status Overview Section */}
          <div className="machine-status-section">
            <div className="machine-status-header">
              <h2 className="machine-status-title">Machine Status Overview</h2>
              <div className="machine-search-container">
                <input
                  type="text"
                  className="machine-search-input"
                  placeholder="Search by machine name, code, or type..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
                <svg className="machine-search-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="11" cy="11" r="8" />
                  <line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
              </div>
            </div>

            {/* Machine Cards Grid */}
            <div className="machine-cards-grid">
              {filteredMachines.map((machine) => (
                <div
                  key={machine.machineId}
                  className="machine-card"
                  onClick={() => navigate(`/machine-details/${machine.machineId}`, { state: { plantId: plantId } })}
                >
                  <div className="machine-card-header">
                    <div>
                      <span className="machine-code">{machine.machineCode}</span>
                      <span className="machine-type">{machine.machineType}</span>
                    </div>
                    <span
                      className="machine-status-indicator"
                      style={{ backgroundColor: getStatusColor(machine.status) }}
                      title={getStatusDisplay(machine.status)}
                    />
                  </div>
                  <h3 className="machine-name">{machine.machineName}</h3>

                  {/* Health Score Progress Bar */}
                  <div className="machine-health-section">
                    <div className="machine-health-header">
                      <span className="machine-health-label">Health Score</span>
                      <span className="machine-health-value" style={{ color: getHealthScoreColor(machine.healthScore) }}>
                        {formatNumber(machine.healthScore)}%
                      </span>
                    </div>
                    <div className="machine-health-progress">
                      <div
                        className="machine-health-progress-bar"
                        style={{
                          width: `${machine.healthScore}%`,
                          backgroundColor: getHealthScoreColor(machine.healthScore)
                        }}
                      />
                    </div>
                  </div>

                  <div className="machine-details">
                    <div className="machine-detail-row">
                      <span className="detail-label">Status</span>
                      <span className="detail-value" style={{ color: getStatusColor(machine.status), fontWeight: 600 }}>
                        {getStatusDisplay(machine.status)}
                      </span>
                    </div>
                    <div className="machine-detail-row">
                      <span className="detail-label">Current Load</span>
                      <div className="detail-progress">
                        <span className="detail-value">{formatNumber(machine.currentLoad)}%</span>
                        <div className="detail-progress-bar">
                          <div
                            className="detail-progress-fill"
                            style={{ width: `${Math.min(machine.currentLoad, 100)}%` }}
                          />
                        </div>
                      </div>
                    </div>
                    <div className="machine-detail-row">
                      <span className="detail-label">Avg Load</span>
                      <span className="detail-value">{formatNumber(machine.avgLoad)}%</span>
                    </div>
                    <div className="machine-detail-row">
                      <span className="detail-label">Runtime</span>
                      <span className="detail-value">{formatNumber(machine.runtimeHours)} h</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {filteredMachines.length === 0 && (
              <div className="empty-state">No machines found matching your search</div>
            )}
          </div>

          {/* Charts Section */}
          {/* Row 1: Production vs Target + Energy */}
          <div className="charts-row row-production-energy">
            {/* Production vs Target Chart - Bar Chart */}
            <div className="chart-container chart-production">
              <h3 className="chart-title">
                Production vs Target Trend
                <span className="chart-subtitle">Daily production performance</span>
              </h3>
              {processedData.productionTrend && processedData.productionTrend.length > 0 ? (
                <ResponsiveContainer width="100%" height={380}>
                  <BarChart data={processedData.productionTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                    {isMultiDay ? (
                      <XAxis
                        dataKey="timeTick"
                        tick={{ fontSize: 11 }}
                        stroke="#6b7280"
                        interval={0}
                        label={{ value: 'Date', position: 'insideBottom', offset: -10, fontSize: 12, fill: '#6b7280' }}
                      />
                    ) : (
                      <XAxis
                        type="number"
                        scale="time"
                        dataKey="timeNumeric"
                        domain={timeDomain}
                        ticks={fourHourTicks}
                        tickFormatter={formatTick}
                        tick={{ fontSize: 11 }}
                        stroke="#6b7280"
                        label={{ value: 'Time', position: 'insideBottom', offset: -10, fontSize: 12, fill: '#6b7280' }}
                      />
                    )}
                    <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" label={{ value: 'Production Units', angle: -90, position: 'insideLeft', fontSize: 12, fill: '#6b7280' }} />
                    <Tooltip
                      formatter={productionTooltipFormatter}
                      labelFormatter={tooltipLabelFormatter}
                      contentStyle={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)' }}
                    />
                    <Legend wrapperStyle={{ paddingTop: '10px' }} />
                    <Bar dataKey="actual" fill="#3b82f6" radius={[4, 4, 0, 0]} name="Actual" />
                    <Bar dataKey="target" fill="#f97316" radius={[4, 4, 0, 0]} name="Target" />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <div className="empty-state">No production trend data available</div>
              )}
            </div>

            {/* Energy Trend Chart - Area Chart with gradient */}
            <div className="chart-container chart-energy">
              <h3 className="chart-title">
                Energy Consumption Trend
                <span className="chart-subtitle">Total energy usage in kWh</span>
              </h3>
              {processedData.energyTrend && processedData.energyTrend.length > 0 ? (
                <ResponsiveContainer width="100%" height={380}>
                  <AreaChart data={processedData.energyTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                    <defs>
                      <linearGradient id="energyGradientPlant" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#22c55e" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#22c55e" stopOpacity={0.2} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                    {isMultiDay ? (
                      <XAxis
                        dataKey="timeTick"
                        tick={{ fontSize: 11 }}
                        stroke="#6b7280"
                        interval={0}
                        label={{ value: 'Date', position: 'insideBottom', offset: -10, fontSize: 12, fill: '#6b7280' }}
                      />
                    ) : (
                      <XAxis
                        type="number"
                        scale="time"
                        dataKey="timeNumeric"
                        domain={timeDomain}
                        ticks={fourHourTicks}
                        tickFormatter={formatTick}
                        tick={{ fontSize: 11 }}
                        stroke="#6b7280"
                        label={{ value: 'Time', position: 'insideBottom', offset: -10, fontSize: 12, fill: '#6b7280' }}
                      />
                    )}
                    <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" label={{ value: 'Energy (kWh)', angle: -90, position: 'insideLeft', fontSize: 12, fill: '#6b7280' }} />
                    <Tooltip
                      formatter={energyTooltipFormatter}
                      labelFormatter={tooltipLabelFormatter}
                      contentStyle={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)' }}
                    />
                    <Legend wrapperStyle={{ paddingTop: '10px' }} />
                    <Area type="monotone" dataKey="energy" stroke="#22c55e" strokeWidth={3} fillOpacity={1} fill="url(#energyGradientPlant)" name="Energy Consumption" />
                  </AreaChart>
                </ResponsiveContainer>
              ) : (
                <div className="empty-state">No energy trend data available</div>
              )}
            </div>
          </div>

          {/* Row 2: Uptime vs Downtime (Full Width) */}
          <div className="charts-row">
            <div className="chart-container chart-uptime">
              <h3 className="chart-title">
                Uptime vs Downtime
                <span className="chart-subtitle">Operational availability percentage</span>
              </h3>
              {processedData.uptimeDowntime && processedData.uptimeDowntime.length > 0 ? (
                <ResponsiveContainer width="100%" height={380}>
                  <BarChart data={processedData.uptimeDowntime} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                    <XAxis
                      dataKey="label"
                      tick={{ fontSize: 11 }}
                      stroke="#6b7280"
                      label={{ value: 'Machine', position: 'insideBottom', offset: -10, fontSize: 12, fill: '#6b7280' }}
                    />
                    <YAxis
                      tick={{ fontSize: 11 }}
                      stroke="#6b7280"
                      domain={[0, 100]}
                      tickFormatter={(value) => `${value}%`}
                      label={{ value: 'Percentage', angle: -90, position: 'insideLeft', fontSize: 12, fill: '#6b7280' }}
                    />
                    <Tooltip
                      formatter={uptimeTooltipFormatter}
                      labelFormatter={(label) => String(label)}
                      contentStyle={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)' }}
                    />
                    <Legend wrapperStyle={{ paddingTop: '10px' }} />
                    <Bar dataKey="uptimePercent" stackId="a" fill="#22c55e" name="Uptime" radius={[0, 0, 0, 0]} />
                    <Bar dataKey="downtimePercent" stackId="a" fill="#ef4444" name="Downtime" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <div className="empty-state">No uptime/downtime data available</div>
              )}
            </div>
          </div>
        </>
      )}

      {!dashboardLoading && !dashboard && !dashboardError && (
        <div className="empty-state-container">
          <div className="empty-state-icon">📊</div>
          <h3>No Dashboard Data Available</h3>
          <p>The dashboard query returned no data.</p>
          <button className="retry-button" onClick={handleRefresh} style={{ marginTop: '20px' }}>Refresh Data</button>
        </div>
      )}
    </div>
  );
};

export default PlantDashboard;

