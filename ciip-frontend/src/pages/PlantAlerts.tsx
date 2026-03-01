import React, { useState } from 'react';
import { useQuery, useMutation, useApolloClient } from '@apollo/client';
import { ALERT_DASHBOARD_QUERY, ACKNOWLEDGE_ALERT_MUTATION, ACKNOWLEDGED_ALERT_QUERY } from '../graphql/alertQueries';
import { PLANTS_QUERY } from '../graphql/queries';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { useParams } from 'react-router-dom';
import '../styles/plantAlerts.css';

dayjs.extend(relativeTime);

// --- Typings ---
interface AlertData {
    actualValue: number;
    alertId: string;
    generatedAt: string;
    machineCode: string;
    machineName: string;
    parameter: string;
    plantName: string;
    severity: string;
    status: string;
}

interface AlertDashboardResponse {
    alertDashboard: {
        acknowledged: number;
        critical: number;
        warning: number;
        alerts: AlertData[];
    };
}

const getDerivedMessage = (parameter: string) => {
    const p = parameter.toLowerCase();

    if (p === 'rpm') return 'RPM threshold exceeded';
    if (p === 'vibration') return 'Abnormal vibration detected';
    if (p === 'temperature') return 'Temperature threshold exceeded';
    if (p === 'load') return 'Machine load exceeded limit';
    if (p === 'status') return 'Machine status anomaly detected';

    return `Anomalous ${parameter} reading detected`;
};

const formatAlertId = (uuid: string) => {
    if (!uuid || uuid.length < 6) return uuid;
    return `ALT-${uuid.substring(0, 3).toUpperCase()}${uuid.substring(uuid.length - 3).toUpperCase()}`;
};

// --- Modal Component ---
interface AcknowledgeModalProps {
    isOpen: boolean;
    onClose: () => void;
    alert: AlertData | null;
    onSuccess: () => void;
}

const AcknowledgeAlertModal: React.FC<AcknowledgeModalProps> = ({ isOpen, onClose, alert, onSuccess }) => {
    const [technicianName, setTechnicianName] = useState('');
    const [reason, setReason] = useState('');
    const [actionTaken, setActionTaken] = useState('');

    const [acknowledgeAlert, { loading }] = useMutation(ACKNOWLEDGE_ALERT_MUTATION, {
        onCompleted: () => {
            onSuccess();
            onClose();
            // Reset form
            setTechnicianName('');
            setReason('');
            setActionTaken('');
        },
        onError: (err) => {
            console.error("Failed to acknowledge:", err);
            // Ideally show a toast here
        }
    });

    const { data: ackData, loading: ackLoading, error: ackError } = useQuery(ACKNOWLEDGED_ALERT_QUERY, {
        variables: { alertId: alert?.alertId },
        skip: !alert || alert?.status?.toUpperCase() !== 'ACKNOWLEDGED',
        fetchPolicy: 'network-only'
    });

    if (!isOpen || !alert) return null;

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (!technicianName.trim() || !reason.trim() || !actionTaken.trim()) return;

        acknowledgeAlert({
            variables: {
                actionTaken,
                alertId: alert.alertId, // String UUID
                reason,
                technicianName
            }
        });
    };

    return (
        <div className="modal-overlay">
            <div className="modal-content">
                <button className="modal-close" onClick={onClose}>&times;</button>
                <h2 className="modal-title">Acknowledge Alert</h2>
                <p className="modal-subtitle">Please provide the necessary details to acknowledge this alert.</p>

                <div className="modal-alert-details">
                    <strong>Alert Details</strong>
                    <div><span>ID:</span> {formatAlertId(alert.alertId)}</div>
                    <div><span>Type:</span> {alert.parameter}</div>
                    <div><span>Message:</span> {getDerivedMessage(alert.parameter)}</div>
                </div>

                {alert.status.toUpperCase() === 'PENDING' ? (
                    <form onSubmit={handleSubmit} className="modal-form">
                        <div className="form-group">
                            <label>Technician Name</label>
                            <input
                                type="text"
                                placeholder="Enter your name"
                                value={technicianName}
                                onChange={(e) => setTechnicianName(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group">
                            <label>Reason for Alert</label>
                            <textarea
                                placeholder="Describe the root cause of the alert"
                                value={reason}
                                onChange={(e) => setReason(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group">
                            <label>Action Taken</label>
                            <textarea
                                placeholder="Describe the corrective action taken"
                                value={actionTaken}
                                onChange={(e) => setActionTaken(e.target.value)}
                                required
                            />
                        </div>
                        <div className="modal-actions">
                            <button type="button" className="btn-cancel" onClick={onClose} disabled={loading}>Cancel</button>
                            <button type="submit" className="btn-submit" disabled={loading}>
                                {loading ? 'Submitting...' : 'Submit Acknowledgement'}
                            </button>
                        </div>
                    </form>
                ) : (
                    <div className="modal-readonly">
                        {ackLoading ? (
                            <p>Loading acknowledgement details...</p>
                        ) : ackError ? (
                            <div className="error-container">Failed to load details.</div>
                        ) : ackData?.acknowledgedAlert ? (
                            <div className="acknowledged-details">
                                <div className="alert-success-box">This alert has already been acknowledged.</div>
                                <div style={{ marginTop: '16px' }}>
                                    <strong>Acknowledged At:</strong> {dayjs(ackData.acknowledgedAlert.acknowledgedAt).format('DD MMM YYYY, HH:mm')}
                                </div>
                                <div style={{ marginTop: '8px' }}>
                                    <strong>Technician:</strong> {ackData.acknowledgedAlert.technicianName}
                                </div>
                                <div style={{ marginTop: '8px' }}>
                                    <strong>Reason:</strong> {ackData.acknowledgedAlert.reason}
                                </div>
                                <div style={{ marginTop: '8px' }}>
                                    <strong>Action Taken:</strong> {ackData.acknowledgedAlert.actionTaken}
                                </div>
                            </div>
                        ) : (
                            <p>No details found.</p>
                        )}
                        <div className="modal-actions" style={{ justifyContent: 'flex-end', marginTop: '20px' }}>
                            <button type="button" className="btn-cancel" onClick={onClose}>Close</button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

// --- Main Page Component ---
const PlantAlerts: React.FC = () => {
    const { plantId } = useParams();

    const [severityFilter, setSeverityFilter] = useState<string | null>(null);
    const [statusFilter, setStatusFilter] = useState<string | null>('PENDING');

    // Date filters
    const [dateFrom, setDateFrom] = useState<string>('');
    const [dateTo, setDateTo] = useState<string>('');

    const getDashboardVariables = () => {
        const variables: Record<string, unknown> = {};
        if (plantId) variables.plantId = plantId;
        if (severityFilter && severityFilter !== 'ALL') variables.severity = severityFilter;
        if (statusFilter && statusFilter !== 'ALL') variables.status = statusFilter;

        if (dateFrom) {
            const fromDate = new Date(dateFrom);
            if (!isNaN(fromDate.getTime())) {
                variables.fromDate = fromDate.toISOString();
            }
        }
        if (dateTo) {
            const toDate = new Date(dateTo);
            if (!isNaN(toDate.getTime())) {
                variables.toDate = toDate.toISOString();
            }
        }
        return variables;
    };

    const [selectedAlert, setSelectedAlert] = useState<AlertData | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const client = useApolloClient();

    const { data: plantsData } = useQuery(PLANTS_QUERY);
    const plantName = plantsData?.plants?.find((p: any) => p.plantId === plantId)?.plantName || '';

    // Reset filters when changing plant
    React.useEffect(() => {
        setSeverityFilter('ALL');
        setStatusFilter('ALL');
        setDateFrom('');
        setDateTo('');
        setCurrentPage(1);
    }, [plantId]);

    // Pagination state
    const [currentPage, setCurrentPage] = useState(1);
    const alertsPerPage = 10;

    const { data, loading, error, refetch: refetchDashboard } = useQuery<AlertDashboardResponse>(ALERT_DASHBOARD_QUERY, {
        variables: getDashboardVariables(),
        fetchPolicy: 'network-only', // Ensure fresh data on mounts/actions
        notifyOnNetworkStatusChange: true
    });

    React.useEffect(() => {
        refetchDashboard(getDashboardVariables());
    }, [plantId, severityFilter, statusFilter, dateFrom, dateTo, refetchDashboard]);

    const dashboardData = data?.alertDashboard;

    // Compute paginated alerts
    const allAlerts = dashboardData?.alerts || [];
    const totalPages = Math.ceil(allAlerts.length / alertsPerPage);
    const paginatedAlerts = allAlerts.slice(
        (currentPage - 1) * alertsPerPage,
        currentPage * alertsPerPage
    );

    const handleRefresh = async () => {
        await client.refetchQueries({ include: "active" });
    };

    const handleOpenModal = (alert: AlertData) => {
        setSelectedAlert(alert);
        setIsModalOpen(true);
    };

    const handleAlertSuccess = async () => {
        await client.refetchQueries({ include: "active" }); // Reload the table behind the modal to update counts/status globally
    };

    if (error) {
        return <div className="plant-dashboard-container"><div className="error-container">Failed to load alerts: {error.message}</div></div>;
    }

    return (
        <div className="plant-dashboard-container relative-container">
            <div className="dashboard-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div className="title-section">
                    <div style={{ display: 'flex', alignItems: 'baseline', gap: '12px' }}>
                        <h1 className="dashboard-title" style={{ margin: 0 }}>
                            Alert Management
                        </h1>
                        {plantName && (
                            <span className="plant-subtitle" style={{ fontSize: '18px', color: '#4b5563', fontWeight: 500 }}>
                                Plant: <span className="highlight-plant">{plantName}</span>
                            </span>
                        )}
                    </div>
                    <p className="plant-location" style={{ marginTop: '8px' }}>Monitor and manage system alerts and notifications</p>
                </div>

                <div className="header-actions">
                    <div className="date-filter-group" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <label className="filter-label" style={{ marginBottom: 0, fontWeight: 500, color: '#4b5563' }}>Date Range</label>
                        <input
                            type="date"
                            className="filter-date"
                            value={dateFrom}
                            max="9999-12-31"
                            onChange={(e) => setDateFrom(e.target.value)}
                            style={{ padding: '6px 12px', border: '1px solid #d1d5db', borderRadius: '6px' }}
                        />
                        <span className="date-separator">to</span>
                        <input
                            type="date"
                            className="filter-date"
                            value={dateTo}
                            max="9999-12-31"
                            onChange={(e) => setDateTo(e.target.value)}
                            style={{ padding: '6px 12px', border: '1px solid #d1d5db', borderRadius: '6px' }}
                        />
                        <button className="refresh-button" onClick={handleRefresh} disabled={loading} style={{ marginLeft: '8px' }}>
                            {loading ? '...' : 'Refresh'}
                        </button>
                    </div>
                </div>
            </div>

            {!loading && !dashboardData ? (
                <div className="empty-state-container">No alerts found.</div>
            ) : (
                <>
                    {/* KPI Cards */}
                    <div className="alerts-kpi-row">
                        <div className="alert-kpi-card critical">
                            <div className="kpi-header">Critical Alerts</div>
                            <div className="kpi-value">
                                {dashboardData?.critical || 0}
                                <span className="icon" style={{ display: 'flex' }}>
                                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                        <circle cx="12" cy="12" r="10"></circle>
                                        <line x1="12" y1="8" x2="12" y2="12"></line>
                                        <line x1="12" y1="16" x2="12.01" y2="16"></line>
                                    </svg>
                                </span>
                            </div>
                        </div>
                        <div className="alert-kpi-card warning">
                            <div className="kpi-header">Warning Alerts</div>
                            <div className="kpi-value">
                                {dashboardData?.warning || 0}
                                <span className="icon" style={{ display: 'flex' }}>
                                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                        <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
                                        <line x1="12" y1="9" x2="12" y2="13"></line>
                                        <line x1="12" y1="17" x2="12.01" y2="17"></line>
                                    </svg>
                                </span>
                            </div>
                        </div>
                        <div className="alert-kpi-card acknowledged">
                            <div className="kpi-header">Acknowledged</div>
                            <div className="kpi-value">
                                {dashboardData?.acknowledged || 0}
                                <span className="icon" style={{ display: 'flex' }}>
                                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                                        <polyline points="22 4 12 14.01 9 11.01"></polyline>
                                    </svg>
                                </span>
                            </div>
                        </div>
                    </div>

                    {/* Filters Bar */}
                    <div className="alerts-filter-bar">
                        <div className="filter-dropdowns">
                            <div className="filter-icon">Filter</div>
                            <select value={severityFilter || 'ALL'} onChange={(e) => setSeverityFilter(e.target.value)}>
                                <option value="ALL">All Severities</option>
                                <option value="CRITICAL">Critical</option>
                                <option value="WARNING">Warning</option>
                            </select>

                            <select value={statusFilter || 'ALL'} onChange={(e) => setStatusFilter(e.target.value)}>
                                <option value="ALL">All Status</option>
                                <option value="PENDING">Pending</option>
                                <option value="ACKNOWLEDGED">Acknowledged</option>
                            </select>
                        </div>

                        <div className="alerts-count">
                            Showing {dashboardData?.alerts?.length || 0} alerts
                        </div>
                    </div>

                    {/* Table */}
                    <div className="alerts-table-container">
                        <table className="alerts-table">
                            <thead>
                                <tr>
                                    <th>ALERT ID</th>
                                    <th>TYPE</th>
                                    <th>LOCATION</th>
                                    <th>MESSAGE</th>
                                    <th>TIME</th>
                                    <th>STATUS</th>
                                    <th>ACTION</th>
                                </tr>
                            </thead>
                            <tbody>
                                {paginatedAlerts.map((alert) => (
                                    <tr key={alert.alertId}>
                                        <td className="fw-bold">{formatAlertId(alert.alertId)}</td>
                                        <td>
                                            <span className={`severity-pill ${alert.severity.toLowerCase()}`}>
                                                {alert.severity === 'CRITICAL' ? '!' : '⚠️'} {alert.severity}
                                            </span>
                                            <div className="type-text">{alert.parameter}</div>
                                        </td>
                                        <td>
                                            <div className="location-text">
                                                {alert.plantName && <>{alert.plantName} →<br /></>}
                                                {alert.machineCode} →<br />
                                                {alert.machineName}
                                            </div>
                                        </td>
                                        <td>
                                            <div className="message-text">
                                                {getDerivedMessage(alert.parameter)}
                                            </div>
                                        </td>
                                        <td className="time-text">🕒 {dayjs(alert.generatedAt).add(5, 'hour').add(30, 'minute').fromNow()}</td>
                                        <td>
                                            <span className="status-pill">{alert.status}</span>
                                        </td>
                                        <td>
                                            {alert.status.toUpperCase() === 'PENDING' ? (
                                                <button className="action-btn pending" onClick={() => handleOpenModal(alert)}>
                                                    Acknowledge
                                                </button>
                                            ) : (
                                                <button className="action-btn acknowledged" onClick={() => handleOpenModal(alert)}>
                                                    View Details
                                                </button>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                                {(!allAlerts || allAlerts.length === 0) && (
                                    <tr>
                                        <td colSpan={7} style={{ textAlign: 'center', padding: '40px' }}>No alerts match the selected criteria</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>

                        {/* Pagination Controls */}
                        {totalPages > 1 && (
                            <div className="pagination-controls" style={{ padding: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderTop: '1px solid #e5e7eb' }}>
                                <button
                                    className="action-btn acknowledged"
                                    disabled={currentPage === 1}
                                    onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                                >
                                    &larr; Previous
                                </button>
                                <span style={{ fontSize: '14px', color: '#6b7280' }}>
                                    Page {currentPage} of {totalPages}
                                </span>
                                <button
                                    className="action-btn acknowledged"
                                    disabled={currentPage === totalPages}
                                    onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                                >
                                    Next &rarr;
                                </button>
                            </div>
                        )}
                    </div>
                </>
            )}

            {/* Modal Overlay */}
            <AcknowledgeAlertModal
                isOpen={isModalOpen}
                onClose={() => setIsModalOpen(false)}
                alert={selectedAlert}
                onSuccess={handleAlertSuccess}
            />
        </div>
    );
};

export default PlantAlerts;
