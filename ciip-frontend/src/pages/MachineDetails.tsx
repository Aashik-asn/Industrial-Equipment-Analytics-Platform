import React, { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useQuery, useApolloClient } from '@apollo/client';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  AreaChart,
  Area,
} from 'recharts';
import dayjs from 'dayjs';
import { MACHINE_DETAILS_QUERY } from '../graphql/queries';
import '../styles/plantDashboard.css';

// TypeScript interfaces matching GraphQL response
interface Alert {
  parameter: string;
  severity: string;
  status: string;
}

interface Electrical {
  rVoltage: number;
  yVoltage: number;
  bVoltage: number;
  rCurrent: number;
  yCurrent: number;
  bCurrent: number;
  frequency: number;
  powerFactor: number;
  energyImportKwh: number;
  energyImportKvah: number;
  energyExportKwh: number;
}

interface Environmental {
  temperature: number;
  humidity: number;
  pressure: number;
  flowRate: number;
}

interface Mechanical {
  rpm: number;
  vibrationX: number;
  vibrationY: number;
  vibrationZ: number;
}

interface SystemHealth {
  overallHealth: number;
  performanceIndex: number;
  efficiencyScore: number;
}

interface TrendPoint {
  time: string;
  value?: number;
  vibrationX?: number;
  vibrationY?: number;
  vibrationZ?: number;
}

interface MachineDetailsData {
  healthScore: number;
  machineCode: string;
  machineId: string;
  machineName: string;
  runtimeHours: number;
  status: string;
  alerts: Alert[];
  acknowledgedAlerts: Alert[];
  electrical: Electrical;
  environmental: Environmental;
  mechanical: Mechanical;
  systemHealth: SystemHealth;
  healthTrend: TrendPoint[];
  loadTrend: TrendPoint[];
  powerConsumptionTrend: TrendPoint[];
  temperatureTrend: TrendPoint[];
  vibrationTrend: TrendPoint[];
}

// Helper functions
const formatNumber = (value: number | undefined | null): string => {
  if (value === undefined || value === null) return '0.00';
  return Number(value).toFixed(2);
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

// Generate 4-hour interval ticks
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

const formatTick = (value: number): string => {
  return dayjs(value).format('HH:mm');
};

// Get status color
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
  return status.toUpperCase();
};

// Get health score color
const getHealthScoreColor = (score: number): string => {
  if (score >= 80) return '#22c55e';
  if (score >= 50) return '#eab308';
  return '#ef4444';
};

// Get alert severity color
const getAlertSeverityColor = (severity: string): string => {
  const severityLower = severity.toLowerCase();
  if (severityLower === 'critical') return '#ef4444';
  if (severityLower === 'warning') return '#eab308';
  if (severityLower === 'info') return '#3b82f6';
  return '#9ca3af';
};

type TabType = 'overview' | 'electrical' | 'environmental' | 'mechanical';

const MachineDetails = (): React.ReactElement => {
  const params = useParams();
  const machineId = params.machineId;
  const navigate = useNavigate();
  const location = useLocation();
  const client = useApolloClient();
  const activePlantId = (location.state as any)?.plantId || '00000000-0000-0000-0000-000000000000';

  const [activeTab, setActiveTab] = useState<TabType>('overview');
  const [dateFrom, setDateFrom] = useState<string>('');
  const [dateTo, setDateTo] = useState<string>('');

  // Removed default 7-day filter to match Dashboard logic

  const getQueryVariables = () => {
    const variables: Record<string, unknown> = {
      machineId: machineId,
      plantId: activePlantId,
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
    data: machineData,
    loading: machineLoading,
    error: machineError,
    refetch: refetchMachine,
  } = useQuery(MACHINE_DETAILS_QUERY, {
    variables: getQueryVariables(),
    skip: !machineId,
  });

  // Refetch when filters change
  useEffect(() => {
    if (machineId) {
      refetchMachine(getQueryVariables());
    }
  }, [dateFrom, dateTo, refetchMachine, machineId]);

  const machine: MachineDetailsData | null = machineData?.machineDetails || null;

  // Determine if multi-day view
  const isMultiDay = useMemo(() => {
    if (!dateFrom || !dateTo) return false;
    return differenceInDays(dateFrom, dateTo) >= 1;
  }, [dateFrom, dateTo]);

  // Generate ticks
  const fourHourTicks = useMemo(() => {
    if (isMultiDay || !dateFrom || !dateTo) return undefined;
    return generateFourHourTicks(dateFrom, dateTo);
  }, [isMultiDay, dateFrom, dateTo]);

  // Get time domain
  const timeDomain = useMemo(() => {
    if (!machine?.healthTrend || machine.healthTrend.length === 0) return undefined;
    const times = machine.healthTrend.map(p => dayjs(p.time).valueOf());
    return [Math.min(...times), Math.max(...times)];
  }, [machine?.healthTrend]);

  // Process trend data
  const processedTrends = useMemo(() => {
    if (!machine) return null;
    return {
      healthTrend: machine.healthTrend?.map((point: TrendPoint) => ({
        ...point,
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      loadTrend: machine.loadTrend?.map((point: TrendPoint) => ({
        ...point,
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      powerConsumptionTrend: machine.powerConsumptionTrend?.map((point: TrendPoint) => ({
        ...point,
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      temperatureTrend: machine.temperatureTrend?.map((point: TrendPoint) => ({
        ...point,
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
      vibrationTrend: machine.vibrationTrend?.map((point: TrendPoint) => ({
        ...point,
        timeTick: isMultiDay ? formatDateOnly(point.time) : formatTimeOnly(point.time),
        timeNumeric: dayjs(point.time).valueOf(),
      })) || [],
    };
  }, [machine, isMultiDay]);

  const handleRefresh = async () => {
    await client.refetchQueries({ include: "active" });
  };

  // Custom tooltip formatter
  const trendTooltipFormatter = (value: number | undefined) => {
    return value !== undefined ? formatNumber(value) : '0.00';
  };

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipLabelFormatter = (label: any) => {
    if (typeof label === 'number') {
      return dayjs(label).format('DD-MM-YYYY HH:mm');
    }
    return String(label);
  };

  if (!machineId) {
    return (
      <div className="plant-dashboard-container">
        <div className="error-container">
          <h3>Missing Machine ID</h3>
          <p>Please select a machine from the Plant Dashboard.</p>
        </div>
      </div>
    );
  }



  if (machineError) {
    return (
      <div className="plant-dashboard-container">
        <div className="error-container">
          <h3>Error Loading Machine</h3>
          <p>{machineError.message}</p>
          <button className="retry-button" onClick={handleRefresh}>Retry</button>
        </div>
      </div>
    );
  }

  return (
    <div className="plant-dashboard-container">
      {/* Header */}
      <div className="dashboard-header">
        <div className="dashboard-title-section">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <button
              onClick={() => navigate(-1)}
              style={{
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                padding: '8px',
                display: 'flex',
                alignItems: 'center'
              }}
              title="Back to Plant Dashboard"
            >
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#6b7280" strokeWidth="2">
                <path d="M19 12H5M12 19l-7-7 7-7" />
              </svg>
            </button>
            <div>
              <h1 className="dashboard-title">
                {machine?.machineCode}
                <span
                  className="machine-status-badge"
                  style={{
                    backgroundColor: getStatusColor(machine?.status),
                    marginLeft: '12px',
                    padding: '4px 12px',
                    borderRadius: '12px',
                    fontSize: '12px',
                    color: '#fff',
                    fontWeight: 500,
                    verticalAlign: 'middle'
                  }}
                >
                  {getStatusDisplay(machine?.status)}
                </span>
              </h1>
              <p className="dashboard-subtitle">{machine?.machineName}</p>
            </div>
          </div>
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
          <button className="refresh-button" onClick={handleRefresh} disabled={machineLoading}>
            {machineLoading ? 'Loading...' : 'Refresh'}
          </button>
        </div>
      </div>

      {machineLoading && (
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p>Loading machine data...</p>
        </div>
      )}

      {!machineLoading && machine && (
        <>
          {/* Tabs */}
          <div className="tabs-container">
            <button
              className={`tab-button ${activeTab === 'overview' ? 'active' : ''}`}
              onClick={() => setActiveTab('overview')}
            >
              Overview
            </button>
            <button
              className={`tab-button ${activeTab === 'electrical' ? 'active' : ''}`}
              onClick={() => setActiveTab('electrical')}
            >
              Electrical
            </button>
            <button
              className={`tab-button ${activeTab === 'environmental' ? 'active' : ''}`}
              onClick={() => setActiveTab('environmental')}
            >
              Environmental
            </button>
            <button
              className={`tab-button ${activeTab === 'mechanical' ? 'active' : ''}`}
              onClick={() => setActiveTab('mechanical')}
            >
              Mechanical
            </button>
          </div>

          {/* Overview Tab */}
          {activeTab === 'overview' && (
            <div className="tab-content">
              {/* Summary Cards */}
              <div className="kpi-cards-grid">
                <div className="kpi-card">
                  <div className="kpi-card-icon" style={{ backgroundColor: '#eff6ff', color: '#3b82f6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
                    </svg>
                  </div>
                  <div className="kpi-card-title">Health Score</div>
                  <div className="kpi-card-value" style={{ color: getHealthScoreColor(machine.healthScore) }}>
                    {formatNumber(machine.healthScore)}%
                  </div>
                </div>

                <div className="kpi-card">
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef3c7', color: '#f59e0b' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M14 4v10.54a4 2 4 0 1 1-4 0V4a2 2 0 0 1 4 0Z" />
                    </svg>
                  </div>
                  <div className="kpi-card-title">Temperature</div>
                  <div className="kpi-card-value">{formatNumber(machine.environmental?.temperature)}°C</div>
                </div>

                <div className="kpi-card">
                  <div className="kpi-card-icon" style={{ backgroundColor: '#dbeafe', color: '#2563eb' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" />
                    </svg>
                  </div>
                  <div className="kpi-card-title">RPM</div>
                  <div className="kpi-card-value">{formatNumber(machine.mechanical?.rpm)}</div>
                </div>

                <div className="kpi-card">
                  <div className="kpi-card-icon" style={{ backgroundColor: '#f0fdf4', color: '#16a34a' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <circle cx="12" cy="12" r="10" />
                      <polyline points="12 6 12 12 16 14" />
                    </svg>
                  </div>
                  <div className="kpi-card-title">Runtime</div>
                  <div className="kpi-card-value">{formatNumber(machine.runtimeHours)} h</div>
                </div>
              </div>

              {/* System Health Progress Bars */}
              <div className="section-card">
                <h3 className="section-title">System Health</h3>
                <div className="health-bars-container">
                  <div className="health-bar-item">
                    <div className="health-bar-header">
                      <span className="health-bar-label">Overall Health</span>
                      <span className="health-bar-value" style={{ color: getHealthScoreColor(machine.systemHealth?.overallHealth) }}>
                        {formatNumber(machine.systemHealth?.overallHealth)}%
                      </span>
                    </div>
                    <div className="health-bar-progress">
                      <div
                        className="health-bar-fill"
                        style={{
                          width: `${machine.systemHealth?.overallHealth}%`,
                          backgroundColor: getHealthScoreColor(machine.systemHealth?.overallHealth)
                        }}
                      />
                    </div>
                  </div>
                  <div className="health-bar-item">
                    <div className="health-bar-header">
                      <span className="health-bar-label">Performance Index</span>
                      <span className="health-bar-value" style={{ color: getHealthScoreColor(machine.systemHealth?.performanceIndex) }}>
                        {formatNumber(machine.systemHealth?.performanceIndex)}%
                      </span>
                    </div>
                    <div className="health-bar-progress">
                      <div
                        className="health-bar-fill"
                        style={{
                          width: `${machine.systemHealth?.performanceIndex}%`,
                          backgroundColor: getHealthScoreColor(machine.systemHealth?.performanceIndex)
                        }}
                      />
                    </div>
                  </div>
                  <div className="health-bar-item">
                    <div className="health-bar-header">
                      <span className="health-bar-label">Efficiency Score</span>
                      <span className="health-bar-value" style={{ color: getHealthScoreColor(machine.systemHealth?.efficiencyScore) }}>
                        {formatNumber(machine.systemHealth?.efficiencyScore)}%
                      </span>
                    </div>
                    <div className="health-bar-progress">
                      <div
                        className="health-bar-fill"
                        style={{
                          width: `${machine.systemHealth?.efficiencyScore}%`,
                          backgroundColor: getHealthScoreColor(machine.systemHealth?.efficiencyScore)
                        }}
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* Alerts */}
              {machine.alerts && machine.alerts.length > 0 && (
                <div className="section-card">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                    <h3 className="section-title" style={{ marginBottom: 0 }}>Alerts</h3>
                    <div
                      className="view-alerts-link"
                      onClick={() => navigate(`/plant-alerts/${getQueryVariables().plantId}`)}
                      style={{ color: '#3b82f6', fontSize: '14px', fontWeight: 500, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '4px' }}
                    >
                      View All Plant Alerts &rarr;
                    </div>
                  </div>
                  <div className="alerts-list">
                    {[...machine.alerts]
                      .filter((a: Alert) => ['ACTIVE', 'PENDING', 'ACKNOWLEDGED'].includes(a.status.toUpperCase()))
                      .sort((a, b) => {
                        const aActive = ['ACTIVE', 'PENDING'].includes(a.status.toUpperCase());
                        const bActive = ['ACTIVE', 'PENDING'].includes(b.status.toUpperCase());
                        if (aActive && !bActive) return -1;
                        if (!aActive && bActive) return 1;
                        return 0;
                      })
                      .map((alert, index) => (
                        <div
                          key={index}
                          className="alert-item"
                          style={{
                            borderLeftColor: getAlertSeverityColor(alert.severity),
                            opacity: alert.status.toUpperCase() === 'ACKNOWLEDGED' ? 0.7 : 1
                          }}
                        >
                          <span className="alert-parameter">
                            {alert.parameter}
                            {alert.status.toUpperCase() === 'ACKNOWLEDGED' && (
                              <span style={{ fontSize: '10px', marginLeft: '8px', background: '#e5e7eb', padding: '2px 6px', borderRadius: '4px', color: '#4b5563' }}>
                                Acknowledged
                              </span>
                            )}
                          </span>
                          <span
                            className="alert-severity"
                            style={{ color: getAlertSeverityColor(alert.severity) }}
                          >
                            {alert.severity}
                          </span>
                        </div>
                      ))}
                  </div>
                </div>
              )}

              {/* Charts */}
              <div className="charts-row">
                <div className="chart-container" style={{ flex: 1, minWidth: 0 }}>
                  <h3 className="chart-title">
                    Health Trend
                    <span className="chart-subtitle">Machine health over time</span>
                  </h3>
                  {processedTrends?.healthTrend && processedTrends.healthTrend.length > 0 ? (
                    <ResponsiveContainer width="100%" height={300}>
                      <AreaChart data={processedTrends.healthTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                        <defs>
                          <linearGradient id="healthGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.8} />
                            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0.0} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                        {isMultiDay ? (
                          <XAxis dataKey="timeTick" tick={{ fontSize: 11 }} stroke="#6b7280" interval={0} axisLine={false} tickLine={false} />
                        ) : (
                          <XAxis type="number" scale="time" dataKey="timeNumeric" domain={timeDomain} ticks={fourHourTicks} tickFormatter={formatTick} tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        )}
                        <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" domain={[0, 100]} axisLine={false} tickLine={false} />
                        <Tooltip formatter={trendTooltipFormatter} labelFormatter={tooltipLabelFormatter} contentStyle={{ backgroundColor: '#fff', border: 'none', borderRadius: '8px', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }} />
                        <Legend iconType="circle" />
                        <Area type="monotone" dataKey="value" stroke="#3b82f6" strokeWidth={3} fillOpacity={1} fill="url(#healthGradient)" name="Health" />
                      </AreaChart>
                    </ResponsiveContainer>
                  ) : (
                    <div className="empty-state">No health trend data</div>
                  )}
                </div>

                <div className="chart-container" style={{ flex: 1, minWidth: 0 }}>
                  <h3 className="chart-title">
                    Load Trend
                    <span className="chart-subtitle">Machine load over time</span>
                  </h3>
                  {processedTrends?.loadTrend && processedTrends.loadTrend.length > 0 ? (
                    <ResponsiveContainer width="100%" height={300}>
                      <AreaChart data={processedTrends.loadTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                        <defs>
                          <linearGradient id="loadGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#8b5cf6" stopOpacity={0.8} />
                            <stop offset="95%" stopColor="#8b5cf6" stopOpacity={0.0} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                        {isMultiDay ? (
                          <XAxis dataKey="timeTick" tick={{ fontSize: 11 }} stroke="#6b7280" interval={0} axisLine={false} tickLine={false} />
                        ) : (
                          <XAxis type="number" scale="time" dataKey="timeNumeric" domain={timeDomain} ticks={fourHourTicks} tickFormatter={formatTick} tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        )}
                        <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        <Tooltip formatter={trendTooltipFormatter} labelFormatter={tooltipLabelFormatter} contentStyle={{ backgroundColor: '#fff', border: 'none', borderRadius: '8px', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }} />
                        <Legend iconType="circle" />
                        <Area type="monotone" dataKey="value" stroke="#8b5cf6" strokeWidth={3} fillOpacity={1} fill="url(#loadGradient)" name="Load %" />
                      </AreaChart>
                    </ResponsiveContainer>
                  ) : (
                    <div className="empty-state">No load trend data</div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* Electrical Tab */}
          {activeTab === 'electrical' && (
            <div className="tab-content">
              <div className="kpi-cards-grid" style={{ marginBottom: '24px' }}>
                {/* Voltage Cards */}
                <div className="kpi-card" style={{ borderLeftColor: '#ef4444' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef2f2', color: '#ef4444' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                  </div>
                  <div className="kpi-card-title">R Phase Voltage</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.rVoltage)} <span style={{ fontSize: '14px', color: '#6b7280' }}>V</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 220 - 240 V</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#f59e0b' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef3c7', color: '#f59e0b' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                  </div>
                  <div className="kpi-card-title">Y Phase Voltage</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.yVoltage)} <span style={{ fontSize: '14px', color: '#6b7280' }}>V</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 220 - 240 V</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#3b82f6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#eff6ff', color: '#3b82f6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                  </div>
                  <div className="kpi-card-title">B Phase Voltage</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.bVoltage)} <span style={{ fontSize: '14px', color: '#6b7280' }}>V</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 220 - 240 V</div>
                </div>

                {/* Current Cards */}
                <div className="kpi-card" style={{ borderLeftColor: '#ef4444' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef2f2', color: '#ef4444' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">R Phase Current</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.rCurrent)} <span style={{ fontSize: '14px', color: '#6b7280' }}>A</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 10 - 50 A</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#f59e0b' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef3c7', color: '#f59e0b' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">Y Phase Current</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.yCurrent)} <span style={{ fontSize: '14px', color: '#6b7280' }}>A</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 10 - 50 A</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#3b82f6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#eff6ff', color: '#3b82f6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">B Phase Current</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.bCurrent)} <span style={{ fontSize: '14px', color: '#6b7280' }}>A</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 10 - 50 A</div>
                </div>

                {/* Power Quality Cards */}
                <div className="kpi-card" style={{ borderLeftColor: '#8b5cf6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#f5f3ff', color: '#8b5cf6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">Power Factor</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.powerFactor)}</div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 0.85 - 1.0</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#14b8a6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#f0fdfa', color: '#14b8a6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" /></svg>
                  </div>
                  <div className="kpi-card-title">Frequency</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.frequency)} <span style={{ fontSize: '14px', color: '#6b7280' }}>Hz</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 49.5 - 50.5 Hz</div>
                </div>

                {/* Energy Cards */}
                <div className="kpi-card" style={{ borderLeftColor: '#10b981' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#ecfdf5', color: '#10b981' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="2" y="3" width="20" height="14" rx="2" ry="2" /><line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" /></svg>
                  </div>
                  <div className="kpi-card-title">Energy Consumed</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.energyImportKwh)} <span style={{ fontSize: '14px', color: '#6b7280' }}>kWh</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: &lt; 500 kWh/day</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#8b5cf6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#f5f3ff', color: '#8b5cf6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="2" y="3" width="20" height="14" rx="2" ry="2" /><line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" /></svg>
                  </div>
                  <div className="kpi-card-title">Apparent Energy</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.energyImportKvah)} <span style={{ fontSize: '14px', color: '#6b7280' }}>kVAh</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: &lt; 550 kVAh/day</div>
                </div>

                <div className="kpi-card" style={{ borderLeftColor: '#f97316' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fff7ed', color: '#f97316' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="17 8 12 3 7 8" /><line x1="12" y1="3" x2="12" y2="15" /></svg>
                  </div>
                  <div className="kpi-card-title">Energy Exported</div>
                  <div className="kpi-card-value">{formatNumber(machine.electrical?.energyExportKwh)} <span style={{ fontSize: '14px', color: '#6b7280' }}>kWh</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 0 - 50 kWh/day</div>
                </div>
              </div>

              <div className="charts-row">
                <div className="chart-container" style={{ flex: 1, minWidth: 0 }}>
                  <h3 className="chart-title">
                    Power Consumption Trend
                    <span className="chart-subtitle">Energy usage over time</span>
                  </h3>
                  {processedTrends?.powerConsumptionTrend && processedTrends.powerConsumptionTrend.length > 0 ? (
                    <ResponsiveContainer width="100%" height={300}>
                      <AreaChart data={processedTrends.powerConsumptionTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                        <defs>
                          <linearGradient id="powerGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#f97316" stopOpacity={0.8} />
                            <stop offset="95%" stopColor="#f97316" stopOpacity={0.0} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                        {isMultiDay ? (
                          <XAxis dataKey="timeTick" tick={{ fontSize: 11 }} stroke="#6b7280" interval={0} axisLine={false} tickLine={false} />
                        ) : (
                          <XAxis type="number" scale="time" dataKey="timeNumeric" domain={timeDomain} ticks={fourHourTicks} tickFormatter={formatTick} tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        )}
                        <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        <Tooltip formatter={trendTooltipFormatter} labelFormatter={tooltipLabelFormatter} contentStyle={{ backgroundColor: '#fff', border: 'none', borderRadius: '8px', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }} />
                        <Legend iconType="circle" />
                        <Area type="monotone" dataKey="value" stroke="#f97316" strokeWidth={3} fillOpacity={1} fill="url(#powerGradient)" name="Power (kW)" />
                      </AreaChart>
                    </ResponsiveContainer>
                  ) : (
                    <div className="empty-state">No power consumption trend data</div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* Environmental Tab */}
          {activeTab === 'environmental' && (
            <div className="tab-content">
              <div className="kpi-cards-grid" style={{ marginBottom: '24px' }}>
                <div className="kpi-card" style={{ borderLeftColor: '#f59e0b' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef3c7', color: '#f59e0b' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M14 4v10.54a4 2 4 0 1 1-4 0V4a2 2 0 0 1 4 0Z" /></svg>
                  </div>
                  <div className="kpi-card-title">Temperature</div>
                  <div className="kpi-card-value" style={{ color: '#f59e0b' }}>{formatNumber(machine.environmental?.temperature)}°C</div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 20 - 45 °C</div>
                </div>
                <div className="kpi-card" style={{ borderLeftColor: '#3b82f6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#eff6ff', color: '#3b82f6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 2.69v18.62" /><path d="M18.36 8.34l-12.72 12.72" /><path d="M5.64 8.34l12.72 12.72" /><path d="M2.69 12h18.62" /></svg>
                  </div>
                  <div className="kpi-card-title">Humidity</div>
                  <div className="kpi-card-value" style={{ color: '#3b82f6' }}>{formatNumber(machine.environmental?.humidity)}%</div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 30 - 60 %</div>
                </div>
                <div className="kpi-card" style={{ borderLeftColor: '#8b5cf6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#f5f3ff', color: '#8b5cf6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" /><line x1="7" y1="7" x2="7.01" y2="7" /></svg>
                  </div>
                  <div className="kpi-card-title">Pressure</div>
                  <div className="kpi-card-value" style={{ color: '#8b5cf6' }}>{formatNumber(machine.environmental?.pressure)} Pa</div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 100 - 150 Pa</div>
                </div>
                <div className="kpi-card" style={{ borderLeftColor: '#10b981' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#ecfdf5', color: '#10b981' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 2v20" /><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" /></svg>
                  </div>
                  <div className="kpi-card-title">Flow Rate</div>
                  <div className="kpi-card-value" style={{ color: '#10b981' }}>{formatNumber(machine.environmental?.flowRate)} L/min</div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 10 - 25 L/min</div>
                </div>
              </div>

              {/* Temperature Trend Chart */}
              <div className="charts-row">
                <div className="chart-container" style={{ flex: 1, minWidth: 0 }}>
                  <h3 className="chart-title">
                    Temperature Trend
                    <span className="chart-subtitle">Temperature over time</span>
                  </h3>
                  {processedTrends?.temperatureTrend && processedTrends.temperatureTrend.length > 0 ? (
                    <ResponsiveContainer width="100%" height={300}>
                      <AreaChart data={processedTrends.temperatureTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                        <defs>
                          <linearGradient id="tempGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#f59e0b" stopOpacity={0.8} />
                            <stop offset="95%" stopColor="#f59e0b" stopOpacity={0.0} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                        {isMultiDay ? (
                          <XAxis dataKey="timeTick" tick={{ fontSize: 11 }} stroke="#6b7280" interval={0} axisLine={false} tickLine={false} />
                        ) : (
                          <XAxis type="number" scale="time" dataKey="timeNumeric" domain={timeDomain} ticks={fourHourTicks} tickFormatter={formatTick} tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        )}
                        <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        <Tooltip formatter={trendTooltipFormatter} labelFormatter={tooltipLabelFormatter} contentStyle={{ backgroundColor: '#fff', border: 'none', borderRadius: '8px', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }} />
                        <Legend iconType="circle" />
                        <Area type="monotone" dataKey="value" stroke="#f59e0b" strokeWidth={3} fillOpacity={1} fill="url(#tempGradient)" name="Temperature (°C)" />
                      </AreaChart>
                    </ResponsiveContainer>
                  ) : (
                    <div className="empty-state">No temperature trend data</div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* Mechanical Tab */}
          {activeTab === 'mechanical' && (
            <div className="tab-content">
              <div className="kpi-cards-grid" style={{ marginBottom: '24px' }}>
                <div className="kpi-card" style={{ borderLeftColor: '#3b82f6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#eff6ff', color: '#3b82f6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" /></svg>
                  </div>
                  <div className="kpi-card-title">RPM</div>
                  <div className="kpi-card-value" style={{ color: '#3b82f6' }}>{formatNumber(machine.mechanical?.rpm)}</div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 1400 - 1500</div>
                </div>
                <div className="kpi-card" style={{ borderLeftColor: '#ef4444' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fef2f2', color: '#ef4444' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">Vibration X</div>
                  <div className="kpi-card-value" style={{ color: '#ef4444' }}>{formatNumber(machine.mechanical?.vibrationX)} <span style={{ fontSize: '14px' }}>mm/s</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 0 - 2.5 mm/s</div>
                </div>
                <div className="kpi-card" style={{ borderLeftColor: '#f97316' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#fff7ed', color: '#f97316' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">Vibration Y</div>
                  <div className="kpi-card-value" style={{ color: '#f97316' }}>{formatNumber(machine.mechanical?.vibrationY)} <span style={{ fontSize: '14px' }}>mm/s</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 0 - 2.5 mm/s</div>
                </div>
                <div className="kpi-card" style={{ borderLeftColor: '#8b5cf6' }}>
                  <div className="kpi-card-icon" style={{ backgroundColor: '#f5f3ff', color: '#8b5cf6' }}>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
                  </div>
                  <div className="kpi-card-title">Vibration Z</div>
                  <div className="kpi-card-value" style={{ color: '#8b5cf6' }}>{formatNumber(machine.mechanical?.vibrationZ)} <span style={{ fontSize: '14px' }}>mm/s</span></div>
                  <div className="kpi-card-subtitle" style={{ marginTop: '4px' }}>Normal Range: 0 - 2.5 mm/s</div>
                </div>
              </div>

              {/* Vibration Trend Chart */}
              <div className="charts-row">
                <div className="chart-container" style={{ flex: 1, minWidth: 0 }}>
                  <h3 className="chart-title">
                    Vibration Trend
                    <span className="chart-subtitle">Vibration X/Y/Z over time</span>
                  </h3>
                  {processedTrends?.vibrationTrend && processedTrends.vibrationTrend.length > 0 ? (
                    <ResponsiveContainer width="100%" height={300}>
                      <LineChart data={processedTrends.vibrationTrend} margin={{ top: 10, right: 30, left: 20, bottom: 30 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
                        {isMultiDay ? (
                          <XAxis dataKey="timeTick" tick={{ fontSize: 11 }} stroke="#6b7280" interval={0} axisLine={false} tickLine={false} />
                        ) : (
                          <XAxis type="number" scale="time" dataKey="timeNumeric" domain={timeDomain} ticks={fourHourTicks} tickFormatter={formatTick} tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        )}
                        <YAxis tick={{ fontSize: 11 }} stroke="#6b7280" axisLine={false} tickLine={false} />
                        <Tooltip formatter={trendTooltipFormatter} labelFormatter={tooltipLabelFormatter} contentStyle={{ backgroundColor: '#fff', border: 'none', borderRadius: '8px', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }} />
                        <Legend iconType="circle" />
                        <Line type="monotone" dataKey="vibrationX" stroke="#ef4444" strokeWidth={3} dot={false} name="Vibration X" />
                        <Line type="monotone" dataKey="vibrationY" stroke="#f97316" strokeWidth={3} dot={false} name="Vibration Y" />
                        <Line type="monotone" dataKey="vibrationZ" stroke="#8b5cf6" strokeWidth={3} dot={false} name="Vibration Z" />
                      </LineChart>
                    </ResponsiveContainer>
                  ) : (
                    <div className="empty-state">No vibration trend data</div>
                  )}
                </div>
              </div>
            </div>
          )}
        </>
      )}

      {!machineLoading && !machine && !machineError && (
        <div className="empty-state-container">
          <div className="empty-state-icon">⚙️</div>
          <h3>No Machine Data Available</h3>
          <p>The machine query returned no data.</p>
          <button className="retry-button" onClick={handleRefresh} style={{ marginTop: '20px' }}>Refresh Data</button>
        </div>
      )}
    </div>
  );
};

export default MachineDetails;
