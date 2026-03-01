import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useApolloClient } from '@apollo/client';
import { TENANT_PROFILE_QUERY, PLANTS_QUERY, THRESHOLDS_QUERY, MACHINES_QUERY } from '../graphql/queries';
import { UPDATE_PROFILE_MUTATION, CHANGE_PASSWORD_MUTATION, UPDATE_TENANT_NAME_MUTATION, UPSERT_PLANT_MUTATION, INSERT_THRESHOLD_MUTATION } from '../graphql/mutations';
import dayjs from 'dayjs';
import '../styles/profile.css';

// To execute pure string mutations using Apollo Client
import { gql } from '@apollo/client';

const UPDATE_PROFILE_GQL = gql`${UPDATE_PROFILE_MUTATION}`;
const CHANGE_PASSWORD_GQL = gql`${CHANGE_PASSWORD_MUTATION}`;
const UPDATE_TENANT_NAME_GQL = gql`${UPDATE_TENANT_NAME_MUTATION}`;
const UPSERT_PLANT_GQL = gql`${UPSERT_PLANT_MUTATION}`;
const INSERT_THRESHOLD_GQL = gql`${INSERT_THRESHOLD_MUTATION}`;

interface TenantProfile {
  createdAt: string;
  email: string;
  role: string;
  tenantId: string;
  userId: string;
}

const Profile = () => {
  const client = useApolloClient();
  const { data, loading, error, refetch } = useQuery(TENANT_PROFILE_QUERY, {
    fetchPolicy: 'network-only' // Ensure always fresh when navigating
  });

  const profile: TenantProfile | null = data?.tenantProfile || null;

  // Global Plant Query
  const { data: plantsData, loading: plantsLoading, refetch: refetchPlants } = useQuery(PLANTS_QUERY, {
    fetchPolicy: 'network-only'
  });
  const plants = plantsData?.plants || [];

  // Machines Query (for Dropdown Population via Multi-Plant Aggregation)
  const [uniqueMachineTypes, setUniqueMachineTypes] = useState<string[]>([]);

  useEffect(() => {
    const fetchAllMachines = async () => {
      if (!plants || plants.length === 0) return;

      let allMachines: any[] = [];
      console.log('Fetching machines for plants:', plants);
      for (const plant of plants) {
        try {
          const { data } = await client.query({
            query: MACHINES_QUERY,
            variables: { plantId: plant.plantId },
            fetchPolicy: 'network-only' // Ensure we get fresh machine associations
          });
          console.log(`Response for plant ${plant.plantCode}:`, data);
          if (data?.machines && Array.isArray(data.machines)) {
            allMachines = [...allMachines, ...data.machines];
          }
        } catch (err) {
          console.error(`Failed to fetch machines for plant ${plant.plantCode}`, err);
        }
      }

      console.log('Aggregated machines:', allMachines);

      // Extract unique types across all active plants
      setUniqueMachineTypes(Array.from(new Set(allMachines.map((m: any) => m.machineType).filter(Boolean))));
    };

    fetchAllMachines();
  }, [plants, client]);



  // Local state for Email form
  const [emailInput, setEmailInput] = useState('');
  const [emailStatus, setEmailStatus] = useState<{ type: 'success' | 'error', msg: string } | null>(null);

  // Local state for Tenant Name form
  const [tenantNameInput, setTenantNameInput] = useState(localStorage.getItem('tenantName') || '');
  const [tenantNameStatus, setTenantNameStatus] = useState<{ type: 'success' | 'error', msg: string } | null>(null);

  // Local state for Password form
  const [passwordInput, setPasswordInput] = useState('');
  const [confirmPasswordInput, setConfirmPasswordInput] = useState('');
  const [passwordStatus, setPasswordStatus] = useState<{ type: 'success' | 'error', msg: string } | null>(null);

  // Mutations
  const [updateProfile, { loading: emailLoading }] = useMutation(UPDATE_PROFILE_GQL);
  const [updateTenantName, { loading: tenantLoading }] = useMutation(UPDATE_TENANT_NAME_GQL);
  const [changePassword, { loading: passwordLoading }] = useMutation(CHANGE_PASSWORD_GQL);
  const [upsertPlant, { loading: plantLoading }] = useMutation(UPSERT_PLANT_GQL);
  const [insertThreshold, { loading: thresholdLoading }] = useMutation(INSERT_THRESHOLD_GQL);

  // Local state for Plant Management form
  const [plantCodeInput, setPlantCodeInput] = useState('');
  const [plantNameInput, setPlantNameInput] = useState('');
  const [plantCityInput, setPlantCityInput] = useState('');
  const [isEditingPlant, setIsEditingPlant] = useState(false);
  const [plantStatus, setPlantStatus] = useState<{ type: 'success' | 'error', msg: string } | null>(null);

  const handleEditPlant = (plant: any) => {
    setPlantCodeInput(plant.plantCode);
    setPlantNameInput(plant.plantName);
    setPlantCityInput(plant.city);
    setIsEditingPlant(true);
    setPlantStatus(null);
  };

  const handleClearPlantForm = () => {
    setPlantCodeInput('');
    setPlantNameInput('');
    setPlantCityInput('');
    setIsEditingPlant(false);
    setPlantStatus(null);
  };

  const handlePlantSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPlantStatus(null);

    if (!plantCodeInput.trim() || !plantNameInput.trim() || !plantCityInput.trim()) {
      setPlantStatus({ type: 'error', msg: 'All fields are required.' });
      return;
    }

    try {
      await upsertPlant({
        variables: {
          plantCode: plantCodeInput,
          plantName: plantNameInput,
          city: plantCityInput
        }
      });
      setPlantStatus({ type: 'success', msg: 'Plant saved successfully.' });
      handleClearPlantForm();
      await refetchPlants();
      await client.refetchQueries({ include: 'active' }); // Refresh Sidebar
    } catch (err: any) {
      setPlantStatus({ type: 'error', msg: err.message || 'Failed to upsert plant.' });
    }
  };

  // Local state for Threshold form
  const [machineTypeInput, setMachineTypeInput] = useState('');
  const [parameterInput, setParameterInput] = useState('');
  const [warningInput, setWarningInput] = useState<number | ''>('');
  const [criticalInput, setCriticalInput] = useState<number | ''>('');
  const [thresholdStatus, setThresholdStatus] = useState<{ type: 'success' | 'error', msg: string } | null>(null);

  // Thresholds State (Multi-Machine Type Aggregation)
  const [thresholds, setThresholds] = useState<any[]>([]);
  const [thresholdsLoading, setThresholdsLoading] = useState(false);
  const [expandedMachineType, setExpandedMachineType] = useState<string | null>(null);

  const fetchAllThresholds = async () => {
    if (uniqueMachineTypes.length === 0) {
      setThresholds([]);
      return;
    }
    setThresholdsLoading(true);

    // Use a Map to deduplicate globals across iteration.
    // The backend returns machineType = null for global defaults.
    const thresholdsMap = new Map();

    for (const mt of uniqueMachineTypes) {
      try {
        const { data } = await client.query({
          query: THRESHOLDS_QUERY,
          variables: { machineType: mt },
          fetchPolicy: 'network-only'
        });

        if (data?.thresholds) {
          for (const t of data.thresholds) {
            // Override implicit null global bindings with the explicit queried machine type
            const mappedThreshold = { ...t, machineType: mt };
            const compositeKey = `${mt}-${t.parameter}`;
            thresholdsMap.set(compositeKey, mappedThreshold);
          }
        }
      } catch (err) {
        console.error(`Failed to fetch thresholds for ${mt}:`, err);
      }
    }

    setThresholds(Array.from(thresholdsMap.values()));
    setThresholdsLoading(false);
  };

  useEffect(() => {
    fetchAllThresholds();
  }, [uniqueMachineTypes]);

  const handleEditThreshold = (threshold: any) => {
    setMachineTypeInput(threshold.machineType);
    setParameterInput(threshold.parameter);
    setWarningInput(threshold.warningValue);
    setCriticalInput(threshold.criticalValue);
    setThresholdStatus(null);
  };

  const handleClearThresholdForm = () => {
    setMachineTypeInput('');
    setParameterInput('');
    setWarningInput('');
    setCriticalInput('');
    setThresholdStatus(null);
  };

  const handleThresholdSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setThresholdStatus(null);

    if (!machineTypeInput.trim() || !parameterInput.trim() || warningInput === '' || criticalInput === '') {
      setThresholdStatus({ type: 'error', msg: 'All threshold fields are required.' });
      return;
    }

    const warnParsed = Math.round(Number(warningInput));
    const critParsed = Math.round(Number(criticalInput));

    const isLowParam = parameterInput.endsWith('_LOW');

    if (isLowParam) {
      if (warnParsed < critParsed) {
        setThresholdStatus({ type: 'error', msg: 'For LOW parameters, Warning value must be greater than or equal to Critical value.' });
        return;
      }
    } else {
      if (warnParsed > critParsed) {
        setThresholdStatus({ type: 'error', msg: 'For HIGH/Standard parameters, Warning value must be less than or equal to Critical value.' });
        return;
      }
    }

    try {
      const response = await insertThreshold({
        variables: {
          input: {
            machineType: machineTypeInput,
            parameter: parameterInput,
            warningValue: warnParsed,
            criticalValue: critParsed
          }
        }
      });
      console.log('insertThreshold response:', response);
      setThresholdStatus({ type: 'success', msg: 'Threshold configuration saved successfully.' });
      handleClearThresholdForm();

      try {
        await fetchAllThresholds();
        console.log('Thresholds refetched globally');
      } catch (refetchErr) {
        console.error('Threshold Refetch failed:', refetchErr);
      }

    } catch (err: any) {
      console.error('Threshold Mutation failed:', err);
      // Try to log deeper into the Apollo error object
      if (err.graphQLErrors) console.error('GraphQL Errors:', err.graphQLErrors);
      if (err.networkError) console.error('Network Error:', err.networkError);

      setThresholdStatus({ type: 'error', msg: err.message || 'Failed to update threshold.' });
    }
  };

  // Sync profile data to forms on load
  useEffect(() => {
    if (profile) {
      setEmailInput(profile.email);
    }
  }, [profile]);

  const handleEmailSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setEmailStatus(null);

    if (!emailInput.trim()) {
      setEmailStatus({ type: 'error', msg: 'Email cannot be empty.' });
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(emailInput)) {
      setEmailStatus({ type: 'error', msg: 'Invalid email format.' });
      return;
    }

    try {
      await updateProfile({ variables: { email: emailInput } });
      setEmailStatus({ type: 'success', msg: 'Email updated successfully.' });
      await refetch();
      await client.refetchQueries({ include: 'active' }); // Update any global navs
    } catch (err: any) {
      setEmailStatus({ type: 'error', msg: err.message || 'Failed to update email.' });
    }
  };

  const handleTenantNameSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setTenantNameStatus(null);

    if (!tenantNameInput.trim()) {
      setTenantNameStatus({ type: 'error', msg: 'Tenant name cannot be empty.' });
      return;
    }

    try {
      await updateTenantName({ variables: { tenantName: tenantNameInput } });
      localStorage.setItem('tenantName', tenantNameInput); // Keep localStorage in sync
      setTenantNameStatus({ type: 'success', msg: 'Tenant name updated successfully.' });
      await refetch();
      // Optional: global trigger if sidebar needs refresh
    } catch (err: any) {
      setTenantNameStatus({ type: 'error', msg: err.message || 'Failed to update tenant name.' });
    }
  };

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordStatus(null);

    if (passwordInput.length < 6) {
      setPasswordStatus({ type: 'error', msg: 'Password must be at least 6 characters.' });
      return;
    }

    if (passwordInput !== confirmPasswordInput) {
      setPasswordStatus({ type: 'error', msg: 'Passwords do not match.' });
      return;
    }

    try {
      await changePassword({ variables: { newPassword: passwordInput } });
      setPasswordStatus({ type: 'success', msg: 'Password changed successfully.' });
      setPasswordInput('');
      setConfirmPasswordInput('');
    } catch (err: any) {
      setPasswordStatus({ type: 'error', msg: err.message || 'Failed to change password.' });
    }
  };

  if (loading) {
    return (
      <div className="profile-container" style={{ textAlign: 'center', paddingTop: '50px' }}>
        <div className="loading-spinner"></div>
        <p style={{ marginTop: '16px', color: '#6b7280' }}>Loading profile...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="profile-container">
        <div className="status-message error">
          Failed to load profile: {error.message}
        </div>
      </div>
    );
  }

  return (
    <div className="profile-container">
      <div className="profile-header">
        <h1 className="profile-title">Profile Settings</h1>
        <p className="profile-subtitle">Manage your account credentials and system preferences</p>
      </div>

      <div className="profile-grid">
        {/* Read-Only Info Card */}
        <div className="profile-card full-width" style={{ marginBottom: '8px' }}>
          <div className="profile-card-header">
            <h2 className="profile-card-title">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                <circle cx="12" cy="7" r="4"></circle>
              </svg>
              Standard Account Info
            </h2>
          </div>
          <div className="profile-card-content" style={{ display: 'flex', gap: '32px', flexWrap: 'wrap' }}>
            <div className="info-row">
              <div className="info-label">Current Role</div>
              <div className="base-badge">{profile?.role || 'Unknown'}</div>
            </div>
            <div className="info-row">
              <div className="info-label">Account Created</div>
              <div className="info-value">
                {profile?.createdAt ? dayjs(profile.createdAt).format('DD MMM YYYY, HH:mm') : '-'}
              </div>
            </div>
            <div className="info-row">
              <div className="info-label">User ID</div>
              <div className="info-value" style={{ fontFamily: 'monospace', fontSize: '14px', color: '#6b7280' }}>
                {profile?.userId || '-'}
              </div>
            </div>
            <div className="info-row">
              <div className="info-label">Tenant ID</div>
              <div className="info-value" style={{ fontFamily: 'monospace', fontSize: '14px', color: '#6b7280' }}>
                {profile?.tenantId || '-'}
              </div>
            </div>
          </div>
        </div>

        {/* Edit Email Card */}
        <div className="profile-card">
          <div className="profile-card-header">
            <h2 className="profile-card-title">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path>
                <polyline points="22,6 12,13 2,6"></polyline>
              </svg>
              Email Address
            </h2>
          </div>
          <form className="profile-card-content" onSubmit={handleEmailSubmit}>
            <div className="form-group">
              <label>Contact Email</label>
              <input
                type="email"
                className="form-input"
                value={emailInput}
                onChange={(e) => setEmailInput(e.target.value)}
                placeholder="Enter new email address"
                required
              />
            </div>

            {emailStatus && (
              <div className={`status-message ${emailStatus.type}`}>
                {emailStatus.msg}
              </div>
            )}

            <div className="profile-actions">
              <button type="submit" className="btn-primary" disabled={emailLoading}>
                {emailLoading ? 'Saving...' : 'Save Email'}
              </button>
            </div>
          </form>
        </div>

        {/* Change Password Card */}
        <div className="profile-card">
          <div className="profile-card-header">
            <h2 className="profile-card-title">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
              </svg>
              Security
            </h2>
          </div>
          <form className="profile-card-content" onSubmit={handlePasswordSubmit}>
            <div className="form-group">
              <label>New Password</label>
              <input
                type="password"
                className="form-input"
                value={passwordInput}
                onChange={(e) => setPasswordInput(e.target.value)}
                placeholder="Minimum 6 characters"
                minLength={6}
                required
              />
            </div>
            <div className="form-group">
              <label>Confirm Password</label>
              <input
                type="password"
                className="form-input"
                value={confirmPasswordInput}
                onChange={(e) => setConfirmPasswordInput(e.target.value)}
                placeholder="Verify new password"
                minLength={6}
                required
              />
            </div>

            {passwordStatus && (
              <div className={`status-message ${passwordStatus.type}`}>
                {passwordStatus.msg}
              </div>
            )}

            <div className="profile-actions">
              <button type="submit" className="btn-primary" disabled={passwordLoading}>
                {passwordLoading ? 'Updating...' : 'Change Password'}
              </button>
            </div>
          </form>
        </div>

        {/* Edit Tenant Name Card */}
        <div className="profile-card full-width">
          <div className="profile-card-header">
            <h2 className="profile-card-title">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
                <polyline points="9 22 9 12 15 12 15 22"></polyline>
              </svg>
              Organization Settings
            </h2>
          </div>
          <form className="profile-card-content" onSubmit={handleTenantNameSubmit}>
            <div className="form-group" style={{ maxWidth: '400px' }}>
              <label>Tenant Name</label>
              <input
                type="text"
                className="form-input"
                value={tenantNameInput}
                onChange={(e) => setTenantNameInput(e.target.value)}
                placeholder="Enter organization name"
                required
              />
            </div>

            {tenantNameStatus && (
              <div className={`status-message ${tenantNameStatus.type}`}>
                {tenantNameStatus.msg}
              </div>
            )}

            <div className="profile-actions" style={{ justifyContent: 'flex-start' }}>
              <button type="submit" className="btn-primary" disabled={tenantLoading}>
                {tenantLoading ? 'Saving...' : 'Update Name'}
              </button>
            </div>
          </form>
        </div>

        {/* Plant Management Section */}
        <div className="profile-card full-width">
          <div className="profile-card-header">
            <h2 className="profile-card-title">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
              </svg>
              Plant Management
            </h2>
          </div>
          <div className="profile-card-content" style={{ display: 'flex', flexDirection: 'column', gap: '32px' }}>

            {/* Split View for Add Form and List */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '40px' }} className="plant-admin-grid">

              {/* Form Side */}
              <div className="plant-form-section">
                <h3 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 16px 0', color: '#374151' }}>
                  {isEditingPlant ? 'Edit Plant' : 'Add New Plant'}
                </h3>
                <form onSubmit={handlePlantSubmit} style={{ background: '#f9fafb', padding: '20px', borderRadius: '8px', border: '1px solid #e5e7eb' }}>
                  <div className="form-group">
                    <label>Plant Code</label>
                    <input
                      type="text"
                      className="form-input"
                      value={plantCodeInput}
                      onChange={(e) => setPlantCodeInput(e.target.value)}
                      placeholder="e.g., P001"
                      disabled={isEditingPlant}
                      required
                      style={isEditingPlant ? { background: '#f3f4f6', cursor: 'not-allowed', color: '#6b7280' } : {}}
                    />
                    <small style={{ display: 'block', color: '#6b7280', fontSize: '12px', marginTop: '4px' }}>
                      {isEditingPlant ? "Plant code cannot be changed after creation." : "Used for internal routing."}
                    </small>
                  </div>
                  <div className="form-group">
                    <label>Plant Name</label>
                    <input
                      type="text"
                      className="form-input"
                      value={plantNameInput}
                      onChange={(e) => setPlantNameInput(e.target.value)}
                      placeholder="e.g., Detroit Assembly"
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label>Location (City)</label>
                    <input
                      type="text"
                      className="form-input"
                      value={plantCityInput}
                      onChange={(e) => setPlantCityInput(e.target.value)}
                      placeholder="e.g., Detroit"
                      required
                    />
                  </div>

                  {plantStatus && (
                    <div className={`status-message ${plantStatus.type}`}>
                      {plantStatus.msg}
                    </div>
                  )}

                  <div className="profile-actions" style={{ marginTop: '16px', paddingTop: '16px', justifyContent: 'flex-start', gap: '12px' }}>
                    <button type="submit" className="btn-primary" disabled={plantLoading}>
                      {plantLoading ? 'Saving...' : 'Save Plant'}
                    </button>
                    {(plantCodeInput || plantNameInput || plantCityInput) && (
                      <button type="button" onClick={handleClearPlantForm} className="btn-outline" style={{ padding: '10px 20px', borderRadius: '8px', background: 'transparent', border: '1px solid #d1d5db', cursor: 'pointer', fontWeight: 500 }}>
                        Clear
                      </button>
                    )}
                  </div>
                </form>
              </div>

              {/* List Side */}
              <div className="plant-list-section">
                <h3 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 16px 0', color: '#374151' }}>Active Plants</h3>
                {plantsLoading ? (
                  <p style={{ color: '#6b7280' }}>Loading plants...</p>
                ) : plants.length === 0 ? (
                  <div style={{ background: '#fef3c7', color: '#92400e', padding: '16px', borderRadius: '8px', border: '1px solid #fde68a' }}>
                    No plants found for this tenant. Create one to get started.
                  </div>
                ) : (
                  <div style={{ border: '1px solid #e5e7eb', borderRadius: '8px', overflow: 'hidden' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                      <thead style={{ background: '#f3f4f6' }}>
                        <tr>
                          <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb' }}>Code</th>
                          <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb' }}>Name</th>
                          <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb' }}>City</th>
                          <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb', textAlign: 'right' }}>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {plants.map((plant: any) => (
                          <tr key={plant.plantId} style={{ borderBottom: '1px solid #f3f4f6' }}>
                            <td style={{ padding: '12px 16px', fontSize: '14px', color: '#111827', fontWeight: 500 }}>{plant.plantCode}</td>
                            <td style={{ padding: '12px 16px', fontSize: '14px', color: '#374151' }}>{plant.plantName}</td>
                            <td style={{ padding: '12px 16px', fontSize: '14px', color: '#6b7280' }}>{plant.city || '-'}</td>
                            <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                              <button
                                onClick={() => handleEditPlant(plant)}
                                style={{ background: 'transparent', border: 'none', color: '#3b82f6', fontWeight: 600, cursor: 'pointer', fontSize: '13px' }}
                              >
                                Edit
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Threshold Management Section */}
        <div className="profile-card full-width" style={{ marginTop: '16px' }}>
          <div className="profile-card-header">
            <h2 className="profile-card-title">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"></polyline>
              </svg>
              Alert Thresholds Configuration
            </h2>
          </div>
          <div className="profile-card-content" style={{ display: 'flex', flexDirection: 'column', gap: '32px' }}>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '40px' }} className="plant-admin-grid">

              {/* Form Side */}
              <div className="threshold-form-section">
                <h3 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 16px 0', color: '#374151' }}>
                  {machineTypeInput !== '' ? 'Edit Threshold' : 'Add New Threshold'}
                </h3>
                <form onSubmit={handleThresholdSubmit} style={{ background: '#f9fafb', padding: '20px', borderRadius: '8px', border: '1px solid #e5e7eb' }}>
                  <div className="form-group">
                    <label>Machine Type</label>
                    <select
                      className="form-input"
                      value={machineTypeInput}
                      onChange={(e) => setMachineTypeInput(e.target.value)}
                      required
                    >
                      <option value="" disabled>Select a valid machine type...</option>
                      {uniqueMachineTypes.map((type: any, index: number) => (
                        <option key={index} value={type}>{type}</option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Parameter</label>
                    <select
                      className="form-input"
                      value={parameterInput}
                      onChange={(e) => setParameterInput(e.target.value)}
                      required
                    >
                      <option value="" disabled>Select metric parameter...</option>
                      <option value="Temperature">Temperature</option>
                      <option value="Vibration">Vibration</option>
                      <option value="Current">Current</option>
                      <option value="RPM_HIGH">RPM High</option>
                      <option value="RPM_LOW">RPM Low</option>
                      <option value="LOAD_HIGH">Load High</option>
                      <option value="LOAD_LOW">Load Low</option>
                    </select>
                  </div>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                    <div className="form-group">
                      <label>Warning Trigger</label>
                      <input
                        type="number"
                        step="any"
                        className="form-input"
                        value={warningInput}
                        onChange={(e) => setWarningInput(e.target.value === '' ? '' : Number(e.target.value))}
                        placeholder="e.g., 75"
                        required
                        style={{ borderLeft: '4px solid #f59e0b' }}
                      />
                    </div>
                    <div className="form-group">
                      <label>Critical Trigger</label>
                      <input
                        type="number"
                        step="any"
                        className="form-input"
                        value={criticalInput}
                        onChange={(e) => setCriticalInput(e.target.value === '' ? '' : Number(e.target.value))}
                        placeholder="e.g., 90"
                        required
                        style={{ borderLeft: '4px solid #ef4444' }}
                      />
                    </div>
                  </div>

                  {thresholdStatus && (
                    <div className={`status-message ${thresholdStatus.type}`}>
                      {thresholdStatus.msg}
                    </div>
                  )}

                  <div className="profile-actions" style={{ marginTop: '16px', paddingTop: '16px', justifyContent: 'flex-start', gap: '12px' }}>
                    <button type="submit" className="btn-primary" disabled={thresholdLoading}>
                      {thresholdLoading ? 'Saving...' : 'Save Threshold'}
                    </button>
                    {(machineTypeInput || parameterInput || warningInput !== '' || criticalInput !== '') && (
                      <button type="button" onClick={handleClearThresholdForm} className="btn-outline" style={{ padding: '10px 20px', borderRadius: '8px', background: 'transparent', border: '1px solid #d1d5db', cursor: 'pointer', fontWeight: 500 }}>
                        Clear
                      </button>
                    )}
                  </div>
                </form>
              </div>

              {/* List Side */}
              <div className="threshold-list-section">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 600, margin: 0, color: '#374151' }}>Active Processing Rules</h3>
                </div>

                {thresholdsLoading ? (
                  <p style={{ color: '#6b7280' }}>Loading thresholds...</p>
                ) : thresholds.length === 0 ? (
                  <div style={{ background: '#f3f4f6', color: '#4b5563', padding: '16px', borderRadius: '8px', border: '1px dashed #d1d5db' }}>
                    No thresholds configured. Default limits will be used.
                  </div>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                    {Object.entries(
                      thresholds.reduce((acc: any, t: any) => {
                        const mt = t.machineType || 'Global Fallback';
                        if (!acc[mt]) acc[mt] = [];
                        acc[mt].push(t);
                        return acc;
                      }, {})
                    ).map(([mt, rules]: [string, any]) => (
                      <div key={mt} style={{ border: '1px solid #e5e7eb', borderRadius: '8px', overflow: 'hidden' }}>
                        <div
                          onClick={() => setExpandedMachineType(prev => prev === mt ? null : mt)}
                          style={{ padding: '12px 16px', background: '#f9fafb', cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontWeight: 600, color: '#374151', userSelect: 'none' }}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#6b7280" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect><line x1="8" y1="21" x2="16" y2="21"></line><line x1="12" y1="17" x2="12" y2="21"></line>
                            </svg>
                            {mt}
                          </div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                            <span style={{ fontSize: '12px', color: '#6b7280', fontWeight: 500, background: '#e5e7eb', padding: '2px 8px', borderRadius: '12px' }}>{rules.length} Rules</span>
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ transform: expandedMachineType === mt ? 'rotate(180deg)' : 'rotate(0deg)', transition: 'transform 0.2s', color: '#9ca3af' }}>
                              <polyline points="6 9 12 15 18 9"></polyline>
                            </svg>
                          </div>
                        </div>

                        {expandedMachineType === mt && (
                          <div style={{ borderTop: '1px solid #e5e7eb' }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                              <thead style={{ background: '#ffffff' }}>
                                <tr>
                                  <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb' }}>Parameter</th>
                                  <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb' }}>Warning</th>
                                  <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb' }}>Critical</th>
                                  <th style={{ padding: '12px 16px', fontSize: '13px', fontWeight: 600, color: '#4b5563', borderBottom: '1px solid #e5e7eb', textAlign: 'right' }}>Actions</th>
                                </tr>
                              </thead>
                              <tbody style={{ background: '#fafafa' }}>
                                {rules.map((t: any, idx: number) => (
                                  <tr key={idx} style={{ borderBottom: idx === rules.length - 1 ? 'none' : '1px solid #f3f4f6' }}>
                                    <td style={{ padding: '12px 16px', fontSize: '14px', color: '#374151' }}>
                                      <span style={{ background: '#e0e7ff', color: '#3730a3', padding: '2px 8px', borderRadius: '4px', fontSize: '12px', fontWeight: 500 }}>
                                        {t.parameter}
                                      </span>
                                    </td>
                                    <td style={{ padding: '12px 16px', fontSize: '14px', color: '#f59e0b', fontWeight: 600 }}>{t.warningValue}</td>
                                    <td style={{ padding: '12px 16px', fontSize: '14px', color: '#ef4444', fontWeight: 600 }}>{t.criticalValue}</td>
                                    <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                                      <button
                                        onClick={() => handleEditThreshold(t)}
                                        style={{ background: 'transparent', border: 'none', color: '#3b82f6', fontWeight: 600, cursor: 'pointer', fontSize: '13px' }}
                                      >
                                        Edit
                                      </button>
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

      </div>
    </div>
  );
};

export default Profile;
