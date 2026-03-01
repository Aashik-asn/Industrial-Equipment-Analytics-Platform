import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useApolloClient } from '@apollo/client';
import { TENANT_PROFILE_QUERY } from '../graphql/queries';
import { UPDATE_PROFILE_MUTATION, CHANGE_PASSWORD_MUTATION, UPDATE_TENANT_NAME_MUTATION } from '../graphql/mutations';
import dayjs from 'dayjs';
import '../styles/profile.css';

// To execute pure string mutations using Apollo Client
import { gql } from '@apollo/client';

const UPDATE_PROFILE_GQL = gql`${UPDATE_PROFILE_MUTATION}`;
const CHANGE_PASSWORD_GQL = gql`${CHANGE_PASSWORD_MUTATION}`;
const UPDATE_TENANT_NAME_GQL = gql`${UPDATE_TENANT_NAME_MUTATION}`;

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

      </div>
    </div>
  );
};

export default Profile;
