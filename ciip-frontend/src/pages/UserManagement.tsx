import React, { useState } from 'react';
import { gql, useMutation } from '@apollo/client';
import { CREATE_USER_MUTATION } from '../graphql/mutations';
import '../styles/profile.css';

const CREATE_USER_GQL = gql`${CREATE_USER_MUTATION}`;

const ROLES = ['ADMIN', 'TECHNICIAN', 'USER'];

const UserManagement: React.FC = () => {
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [role, setRole] = useState('TECHNICIAN');
    const [status, setStatus] = useState<{ type: 'success' | 'error'; msg: string } | null>(null);

    const [createUser, { loading }] = useMutation(CREATE_USER_GQL);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setStatus(null);

        if (!firstName.trim() || !lastName.trim() || !email.trim() || !password.trim()) {
            setStatus({ type: 'error', msg: 'All fields are required.' });
            return;
        }
        if (password.length < 6) {
            setStatus({ type: 'error', msg: 'Password must be at least 6 characters.' });
            return;
        }

        try {
            await createUser({ variables: { email, firstName, lastName, password, role } });
            setStatus({ type: 'success', msg: `User ${firstName} ${lastName} created successfully.` });
            // Reset form
            setFirstName('');
            setLastName('');
            setEmail('');
            setPassword('');
            setRole('TECHNICIAN');
        } catch (err: any) {
            setStatus({ type: 'error', msg: err.message || 'Failed to create user.' });
        }
    };

    return (
        <div className="profile-container">
            <div className="profile-header">
                <h1 className="profile-title">User Management</h1>
                <p className="profile-subtitle">Create new users and assign roles for your organization</p>
            </div>

            <div className="profile-grid">
                <div className="profile-card" style={{ maxWidth: '520px' }}>
                    <div className="profile-card-header">
                        <h2 className="profile-card-title">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"></path>
                                <circle cx="9" cy="7" r="4"></circle>
                                <line x1="19" y1="8" x2="19" y2="14"></line>
                                <line x1="22" y1="11" x2="16" y2="11"></line>
                            </svg>
                            Create New User
                        </h2>
                    </div>

                    <form className="profile-card-content" onSubmit={handleSubmit}>
                        <div style={{ display: 'flex', gap: '12px' }}>
                            <div className="form-group" style={{ flex: 1 }}>
                                <label>First Name</label>
                                <input
                                    type="text"
                                    className="form-input"
                                    value={firstName}
                                    onChange={(e) => setFirstName(e.target.value)}
                                    placeholder="First name"
                                    required
                                />
                            </div>
                            <div className="form-group" style={{ flex: 1 }}>
                                <label>Last Name</label>
                                <input
                                    type="text"
                                    className="form-input"
                                    value={lastName}
                                    onChange={(e) => setLastName(e.target.value)}
                                    placeholder="Last name"
                                    required
                                />
                            </div>
                        </div>

                        <div className="form-group">
                            <label>Email Address</label>
                            <input
                                type="email"
                                className="form-input"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                placeholder="user@example.com"
                                required
                            />
                        </div>

                        <div className="form-group">
                            <label>Password</label>
                            <div style={{ position: 'relative' }}>
                                <input
                                    type={showPassword ? 'text' : 'password'}
                                    className="form-input"
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    placeholder="Minimum 6 characters"
                                    minLength={6}
                                    required
                                    style={{ paddingRight: '44px' }}
                                />
                                <button
                                    type="button"
                                    onClick={() => setShowPassword(!showPassword)}
                                    style={{
                                        position: 'absolute', right: '12px', top: '50%', transform: 'translateY(-50%)',
                                        background: 'none', border: 'none', cursor: 'pointer', color: '#6b7280', padding: 0
                                    }}
                                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                                >
                                    {showPassword ? (
                                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                            <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path>
                                            <line x1="1" y1="1" x2="23" y2="23"></line>
                                        </svg>
                                    ) : (
                                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                                            <circle cx="12" cy="12" r="3"></circle>
                                        </svg>
                                    )}
                                </button>
                            </div>
                        </div>

                        <div className="form-group">
                            <label>Role</label>
                            <select
                                className="form-input"
                                value={role}
                                onChange={(e) => setRole(e.target.value)}
                                style={{ cursor: 'pointer' }}
                            >
                                {ROLES.map((r) => (
                                    <option key={r} value={r}>{r}</option>
                                ))}
                            </select>
                        </div>

                        {status && (
                            <div className={`status-message ${status.type}`}>
                                {status.msg}
                            </div>
                        )}

                        <div className="profile-actions">
                            <button type="submit" className="btn-primary" disabled={loading}>
                                {loading ? 'Creating...' : 'Create User'}
                            </button>
                        </div>
                    </form>
                </div>

                {/* Info card */}
                <div className="profile-card" style={{ maxWidth: '360px', alignSelf: 'flex-start' }}>
                    <div className="profile-card-header">
                        <h2 className="profile-card-title">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <circle cx="12" cy="12" r="10"></circle>
                                <line x1="12" y1="8" x2="12" y2="12"></line>
                                <line x1="12" y1="16" x2="12.01" y2="16"></line>
                            </svg>
                            Role Descriptions
                        </h2>
                    </div>
                    <div className="profile-card-content">
                        {[
                            { role: 'ADMIN', desc: 'Full access — can create users, manage plants, machines, thresholds, and tenant settings.' },
                            { role: 'TECHNICIAN', desc: 'Can view dashboards and acknowledge alerts. Their name is auto-recorded on acknowledgement.' },
                            { role: 'USER', desc: 'Read-only access. Can view plant dashboards and alert statuses.' },
                        ].map((item) => (
                            <div key={item.role} style={{ marginBottom: '16px' }}>
                                <div style={{ display: 'inline-block', padding: '2px 10px', borderRadius: '9999px', background: '#eff6ff', color: '#1d4ed8', fontSize: '12px', fontWeight: 700, marginBottom: '4px' }}>
                                    {item.role}
                                </div>
                                <p style={{ margin: 0, fontSize: '13px', color: '#4b5563', lineHeight: '1.5' }}>{item.desc}</p>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default UserManagement;
