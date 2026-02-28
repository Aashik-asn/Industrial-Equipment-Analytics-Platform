import { useState, useEffect, useMemo } from 'react';
import { useQuery } from '@apollo/client';
import { useNavigate } from 'react-router-dom';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  BarChart,
  Bar,
  AreaChart,
  Area,
} from 'recharts';
import dayjs from 'dayjs';

import { PLANTS_SIMPLE_QUERY, DASHBOARD_QUERY } from '../graphql/queries';

import '../styles/dashboard.css';

// Simple plant for filter dropdown
interface SimplePlant {
  plantId: string;
  plantName: string;
}

// Plant card from dashboard (with machines and efficiency)
interface PlantCard {
  plantId: string;
  plantName: string;
  machines: number;
  efficiency: number;
}

interface AlertDistribution {
  count: number;
  severity: string;
}

interface OeeTrendPoint {
  availability: number;
  performance: number;
  quality: number;
  time: string;
}

interface ProductionTrendPoint {
  actual: number;
  target: number;
  time: string;
}

interface EnergyTrendPoint {
  energy: number;
  time: string;
}

interface DashboardData {
  activeAlerts: number;
  avgEfficiency: number;
  totalActiveMachines: number;
  alertDistribution: AlertDistribution[];
  oeeTrend: OeeTrendPoint[];
  productionTrend: ProductionTrendPoint[];
  energyTrend: EnergyTrendPoint[];
  plants: PlantCard[];
}

const formatNumber = (value: number): string => {
  return Number(value).toFixed(2);
};

const formatDateFull = (dateString: string): string => {
  return dayjs(dateString).format('YYYY-MM-DD HH:mm');
};

const formatDateOnly = (dateString: string): string => {
  return dayjs(dateString).format('YYYY-MM-DD');
};

const formatTimeOnly = (dateString: string): string => {
  return dayjs(dateString).format('HH:mm');
};

const differenceInDays = (from: string, to: string): number => {
  const start = dayjs(from);
  const end = dayjs(to);
  return end.diff(start, 'day');
};

const COLORS = ['#ef4444', '#f97316', '#eab308', '#22c55e', '#3b82f6'];

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

const Dashboard = () => {
  const navigate = useNavigate();
  const [selectedPlant, setSelectedPlant] = useState<string | null>(null);
  const [dateFrom, setDateFrom] = useState<string>('');
  const [dateTo, setDateTo] = useState<string>('');

  // Query for simple plants list (filter dropdown)
  const { 
    data: simplePlantsData, 
    loading: simplePlantsLoading, 
    error: simplePlantsError 
  } = useQuery(PLANTS_SIMPLE_QUERY);

  const getDashboardVariables = () => {
    const variables: Record<string, unknown> = {};
    if (selectedPlant) variables.plantId = selectedPlant;
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
  } = useQuery(DASHBOARD_QUERY, {
    variables: getDashboardVariables(),
    skip: false,
  });

  useEffect(() => {
    refetchDashboard(getDashboardVariables());
  }, [selectedPlant, dateFrom, dateTo, refetchDashboard]);

  const simplePlants: SimplePlant[] = simplePlantsData?.plants || [];
  const dashboard: DashboardData | null = dashboardData?.dashboard || null;
  const plantCards: PlantCard[] = dashboard?.plants || [];

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
    if (!dashboard?.oeeTrend || dashboard.oeeTrend.length === 0) return undefined;
    const times = dashboard.oeeTrend.map(p => dayjs(p.time).valueOf());
    return [Math.min(...times), Math.max(...times)];
  }, [dashboard?.oeeTrend]);

  // Process data
  const processedData = useMemo(() => {
    if (!dashboard) return null;
    return {
      ...dashboard,
      oeeTrend: dashboard.oeeTrend?.map((point) => ({
        ...point,
        timeFormatted: formatDateFull(point.time),
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      productionTrend: dashboard.productionTrend?.map((point) => ({
        ...point,
        timeFormatted: formatDateFull(point.time),
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      energyTrend: dashboard.energyTrend?.map((point) => ({
        ...point,
        timeFormatted: formatDateFull(point.time),
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
    };
  }, [dashboard, isMultiDay]);

  const calculateProductionPercentage = (): number => {
    if (!dashboard?.productionTrend || dashboard.productionTrend.length === 0) return 0;
    const latest = dashboard.productionTrend[dashboard.productionTrend.length - 1];
    if (!latest || latest.target === 0) return 0;
    return Number((latest.actual / latest.target) * 100);
  };

  const getAlertDistributionWithPercentages = () => {
    if (!dashboard?.alertDistribution || dashboard.alertDistribution.length === 0) return [];
    const total = dashboard.alertDistribution.reduce((sum, item) => sum + item.count, 0);
    return dashboard.alertDistribution.map((item, index) => ({
      ...item,
      percentage: total > 0 ? Number(((item.count / total) * 100).toFixed(2)) : 0,
      color: COLORS[index % COLORS.length],
    }));
  };

  // Get alert counts by severity
  const getAlertCountsBySeverity = () => {
    const counts = { critical: 0, warning: 0, others: 0 };
    if (!dashboard?.alertDistribution) return counts;
    dashboard.alertDistribution.forEach(item => {
      const severity = item.severity?.toLowerCase() || '';
      if (severity.includes('critical')) counts.critical += item.count;
      else if (severity.includes('warning')) counts.warning += item.count;
      else counts.others += item.count;
    });
    return counts;
  };

  const alertData = getAlertDistributionWithPercentages();
  const alertCounts = getAlertCountsBySeverity();
  const isLoading = simplePlantsLoading || dashboardLoading;

  const handleRefresh = () => {
    refetchDashboard(getDashboardVariables());
  };

  const handlePlantClick = (plantId: string) => {
    navigate(`/plant-dashboard/${plantId}`);
  };

  // Custom tooltip formatter for OEE chart
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const oeeTooltipFormatter = (value: any, name?: string) => {
    return [`${formatNumber(Number(value))}%`, name ? name.charAt(0).toUpperCase() + name.slice(1) : ''];
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

  // Label formatter for all charts
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipLabelFormatter = (label: any) => {
    if (typeof label === 'number') {
      return dayjs(label).format('YYYY-MM-DD HH:mm');
    }
    return String(label);
  };

  return (
    <div className="dashboard-container">
      <div className="dashboard-header">
        <div className="dashboard-title-section">
          <h1 className="dashboard-title">Dashboard</h1>
          <p className="dashboard-subtitle">Live overview of plant operations</p>
        </div>
        <div className="dashboard-filters">
          <div className="filter-group">
            <label htmlFor="plant-filter" className="filter-label">Plant</label>
            <select
              id="plant-filter"
              className="filter-select"
              value={selectedPlant || ''}
              onChange={(e) => setSelectedPlant(e.target.value === '' ? null : e.target.value)}
              disabled={simplePlantsLoading}
            >
              <option value="">Overall</option>
              {simplePlants.map((plant) => (
                <option key={plant.plantId} value={plant.plantId}>{plant.plantName}</option>
              ))}
            </select>
          </div>
          <div className="filter-group">
            <label className="filter-label">Date Range</label>
            <div className="date-range-inputs">
              <input type="date" className="filter-date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
              <span className="date-separator">to</span>
              <input type="date" className="filter-date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
            </div>
          </div>
          <button className="refresh-button" onClick={handleRefresh} disabled={isLoading}>
            {isLoading ? 'Loading...' : 'Refresh'}
          </button>
        </div>
      </div>

      {(simplePlantsError || dashboardError) && (
        <div className="error-container">
          <h3>Error Loading Data</h3>
          {simplePlantsError && <p>Plants Error: {simplePlantsError.message}</p>}
          {dashboardError && <p>Dashboard Error: {dashboardError.message}</p>}
          <button className="retry-button" onClick={handleRefresh}>Retry</button>
        </div>
      )}

      {isLoading && (
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p>Loading dashboard data...</p>
        </div>
      )}

      {!isLoading && processedData && (
        <>
          {/* Row 1: KPI Cards */}
          <div className="kpi-cards-grid">
            {/* Active Machines Card */}
            <div className="kpi-card kpi-card-machines">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" stroke="#3b82f6"/>
                </svg>
              </div>
              <div className="kpi-card-title">Active Machines</div>
              <div className="kpi-card-value">{processedData.totalActiveMachines}</div>
              <div className="kpi-card-subtitle">Across all operations</div>
            </div>

            {/* Active Alerts Card */}
            <div className="kpi-card kpi-card-alerts">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
                  <line x1="12" y1="9" x2="12" y2="13"/>
                  <line x1="12" y1="17" x2="12.01" y2="17"/>
                </svg>
              </div>
              <div className="kpi-card-title">Active Alerts</div>
              <div className="kpi-card-value">{processedData.activeAlerts}</div>
              <div className="kpi-card-subtitle">
                {alertCounts.critical} critical, {alertCounts.warning} warnings
              </div>
            </div>

            {/* Average Efficiency Card */}
            <div className="kpi-card kpi-card-efficiency">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M22 12h-4l-3 9L9 3l-3 9H2"/>
                </svg>
              </div>
              <div className="kpi-card-title">Average Efficiency</div>
              <div className="kpi-card-value">{formatNumber(processedData.avgEfficiency)}%</div>
              <div className="kpi-card-subtitle">Across all operations</div>
            </div>

            {/* Production vs Target Card */}
            <div className="kpi-card kpi-card-production">
              <div className="kpi-card-icon">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <line x1="18" y1="20" x2="18" y2="10"/>
                  <line x1="12" y1="20" x2="12" y2="4"/>
                  <line x1="6" y1="20" x2="6" y2="14"/>
                </svg>
              </div>
              <div className="kpi-card-title">Production vs Target</div>
              <div className="kpi-card-value">{formatNumber(calculateProductionPercentage())}%</div>
              <div className="kpi-card-subtitle">Current period</div>
            </div>
          </div>

          {/* Row 2: OEE Chart + Alert Distribution */}
          <div className="charts-row row-oee-alert">
            <div className="chart-container chart-oee">
              <h3 className="chart-title">
                Overall Equipment Effectiveness (OEE)
                <span className="chart-subtitle">Availability, Performance, Quality metrics</span>
              </h3>
              {processedData.oeeTrend && processedData.oeeTrend.length > 0 ? (
                <ResponsiveContainer width="100%" height={380}>
                  <LineChart data={processedData.oeeTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
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
                    <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" domain={[0, 100]} tickFormatter={(value) => `${value}%`} label={{ value: 'Efficiency (%)', angle: -90, position: 'insideLeft', fontSize: 12, fill: '#6b7280' }} />
                    <Tooltip 
                      formatter={oeeTooltipFormatter} 
                      labelFormatter={tooltipLabelFormatter}
                      contentStyle={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)' }} 
                    />
                    <Legend wrapperStyle={{ paddingTop: '10px' }} />
                    <Line type="monotone" dataKey="availability" stroke="#3b82f6" strokeWidth={3} dot={{ r: 4, strokeWidth: 2, fill: '#fff' }} activeDot={{ r: 6 }} name="Availability" />
                    <Line type="monotone" dataKey="performance" stroke="#22c55e" strokeWidth={3} dot={{ r: 4, strokeWidth: 2, fill: '#fff' }} activeDot={{ r: 6 }} name="Performance" />
                    <Line type="monotone" dataKey="quality" stroke="#f97316" strokeWidth={3} dot={{ r: 4, strokeWidth: 2, fill: '#fff' }} activeDot={{ r: 6 }} name="Quality" />
                  </LineChart>
                </ResponsiveContainer>
              ) : (
                <div className="empty-state">No OEE trend data available</div>
              )}
            </div>

            <div className="chart-container chart-alert">
              <h3 className="chart-title">
                Alert Severity Distribution
                <span className="chart-subtitle">Current alert status overview</span>
              </h3>
              {alertData.length > 0 ? (
                <div className="alert-donut-container">
                  <ResponsiveContainer width="100%" height={250}>
                    <PieChart>
                      <Pie 
                        data={alertData} 
                        cx="50%" 
                        cy="50%" 
                        innerRadius={60} 
                        outerRadius={90} 
                        paddingAngle={2} 
                        dataKey="count"
                      >
                        {alertData.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={entry.color} stroke="#fff" strokeWidth={2} />
                        ))}
                      </Pie>
                      <Tooltip 
                        formatter={(value, _name, props) => {
                          const item = alertData[(props as { payload?: { index?: number } })?.payload?.index || 0];
                          return [`${value} alerts (${item?.percentage || 0}%)`, item?.severity || ''];
                        }} 
                        contentStyle={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)' }} 
                      />
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="alert-legend">
                    {alertData.map((item, index) => (
                      <div key={index} className="alert-legend-item">
                        <span className="alert-legend-color" style={{ backgroundColor: item.color }}></span>
                        <span className="alert-legend-severity">{item.severity}</span>
                        <span className="alert-legend-count">{item.count}</span>
                        <span className="alert-legend-percentage">({item.percentage}%)</span>
                      </div>
                    ))}
                  </div>
                </div>
              ) : (
                <div className="empty-state">No alert distribution data available</div>
              )}
            </div>
          </div>

          {/* Row 3: Production & Energy */}
          <div className="charts-row row-production-energy">
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

            <div className="chart-container chart-energy">
              <h3 className="chart-title">
                Energy Consumption Trend
                <span className="chart-subtitle">Total energy usage in kWh</span>
              </h3>
              {processedData.energyTrend && processedData.energyTrend.length > 0 ? (
                <ResponsiveContainer width="100%" height={380}>
                  <AreaChart data={processedData.energyTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                    <defs>
                      <linearGradient id="energyGradient" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#22c55e" stopOpacity={0.8}/>
                        <stop offset="95%" stopColor="#22c55e" stopOpacity={0.2}/>
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
                    <Area type="monotone" dataKey="energy" stroke="#22c55e" strokeWidth={3} fillOpacity={1} fill="url(#energyGradient)" name="Energy Consumption" />
                  </AreaChart>
                </ResponsiveContainer>
              ) : (
                <div className="empty-state">No energy trend data available</div>
              )}
            </div>
          </div>

          {/* Row 4: Quick Access to Plants - At the bottom, centered */}
          {plantCards.length > 0 && (
            <div className="quick-access-section">
              <h3 className="quick-access-title">Quick Access - Plant Dashboards</h3>
              <div className="quick-access-grid">
                {plantCards.map((plant) => (
                  <div
                    key={plant.plantId}
                    className="quick-access-card"
                    onClick={() => handlePlantClick(plant.plantId)}
                  >
                    <div className="plant-card-header">
                      <span className="plant-card-name">{plant.plantName}</span>
                    </div>
                    <div className="plant-card-stats">
                      <div className="plant-stat-left">
                        <span className="plant-stat-value">{plant.machines}</span>
                        <span className="plant-stat-label">Machines</span>
                      </div>
                      <div className="plant-stat-right">
                        <span className="plant-stat-value">{formatNumber(plant.efficiency)}%</span>
                        <span className="plant-stat-label">Efficiency</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}

      {!isLoading && !processedData && !simplePlantsError && !dashboardError && (
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

export default Dashboard;
