import { useState } from 'react';
import { NavLink, useNavigate, useParams, useLocation } from 'react-router-dom';
import { useQuery } from '@apollo/client';
import { PLANTS_QUERY } from '../graphql/queries';
import logo from '../assets/logo.png';

interface Plant {
  plantId: string;
  plantName: string;
}

const Sidebar = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const params = useParams();
  const plantId = params.plantId;
  const [isPlantSectionExpanded, setIsPlantSectionExpanded] = useState(false);
  const [isAlertSectionExpanded, setIsAlertSectionExpanded] = useState(false);
  const { data: plantsData } = useQuery(PLANTS_QUERY);
  const plants: Plant[] = plantsData?.plants || [];

  const firstName = localStorage.getItem('firstName') || '';
  const lastName = localStorage.getItem('lastName') || '';
  const role = localStorage.getItem('role') || '';
  const fullName = (firstName || lastName) ? `${firstName} ${lastName}`.trim() : 'User';
  const isAdmin = role === 'ADMIN';

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('tenantId');
    localStorage.removeItem('tenantName');
    localStorage.removeItem('role');
    localStorage.removeItem('userId');
    localStorage.removeItem('firstName');
    localStorage.removeItem('lastName');
    navigate('/login');
  };

  const isPlantDashboardActive = location.pathname.startsWith('/plant-dashboard');
  const isAlertDashboardActive = location.pathname.startsWith('/plant-alerts');

  const togglePlantSection = () => {
    setIsPlantSectionExpanded(!isPlantSectionExpanded);
  };

  const toggleAlertSection = () => {
    setIsAlertSectionExpanded(!isAlertSectionExpanded);
  };

  const handlePlantClick = (id: string) => {
    navigate(`/plant-dashboard/${id}`);
  };

  const handleAlertClick = (id: string) => {
    navigate(`/plant-alerts/${id}`);
  };

  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <img src={logo} alt="Company Logo" className="logo-image" />
      </div>

      <nav className="sidebar-nav">
        <h3 className="nav-section-title">Menu</h3>
        <ul className="nav-list">
          {/* Dashboard */}
          <li className="nav-item">
            <NavLink
              to="/dashboard"
              className={({ isActive }) =>
                isActive ? 'nav-link active' : 'nav-link'
              }
            >
              <span className="nav-icon">{getIcon('dashboard')}</span>
              <span className="nav-label">Dashboard</span>
            </NavLink>
          </li>

          {/* Plant Dashboard - Collapsible Section */}
          <li className="nav-item">
            <div
              className={`nav-link collapsible ${isPlantDashboardActive ? 'active' : ''}`}
              onClick={togglePlantSection}
            >
              <span className="nav-icon">{getIcon('plant')}</span>
              <span className="nav-label">Plant Dashboard</span>
              <span className={`collapse-icon ${isPlantSectionExpanded ? 'expanded' : ''}`}>
                ▼
              </span>
            </div>
            <ul className={`sub-nav-list ${isPlantSectionExpanded ? 'expanded' : ''}`}>
              {plants.map((plant) => (
                <li key={plant.plantId} className="sub-nav-item">
                  <div
                    className={`sub-nav-link ${plantId === plant.plantId ? 'active' : ''}`}
                    onClick={() => handlePlantClick(plant.plantId)}
                  >
                    <span className="sub-nav-bullet">•</span>
                    <span className="sub-nav-label">{plant.plantName}</span>
                  </div>
                </li>
              ))}
            </ul>
          </li>

          {/* Alert Management - Collapsible Section */}
          <li className="nav-item">
            <div
              className={`nav-link collapsible ${isAlertDashboardActive ? 'active' : ''}`}
              onClick={toggleAlertSection}
            >
              <span className="nav-icon">{getIcon('alert')}</span>
              <span className="nav-label">Alert Management</span>
              <span className={`collapse-icon ${isAlertSectionExpanded ? 'expanded' : ''}`}>
                ▼
              </span>
            </div>
            <ul className={`sub-nav-list ${isAlertSectionExpanded ? 'expanded' : ''}`}>
              {plants.map((plant) => (
                <li key={plant.plantId} className="sub-nav-item">
                  <div
                    className={`sub-nav-link ${isAlertDashboardActive && plantId === plant.plantId ? 'active' : ''}`}
                    onClick={() => handleAlertClick(plant.plantId)}
                  >
                    <span className="sub-nav-bullet">•</span>
                    <span className="sub-nav-label">{plant.plantName}</span>
                  </div>
                </li>
              ))}
            </ul>
          </li>

          {/* User Management - ADMIN only */}
          {isAdmin && (
            <li className="nav-item">
              <NavLink
                to="/user-management"
                className={({ isActive }) =>
                  isActive ? 'nav-link active' : 'nav-link'
                }
              >
                <span className="nav-icon">{getIcon('users')}</span>
                <span className="nav-label">User Management</span>
              </NavLink>
            </li>
          )}

          {/* Profile */}
          <li className="nav-item">
            <NavLink
              to="/profile"
              className={({ isActive }) =>
                isActive ? 'nav-link active' : 'nav-link'
              }
            >
              <span className="nav-icon">{getIcon('profile')}</span>
              <span className="nav-label">Profile</span>
            </NavLink>
          </li>
        </ul>
      </nav>

      <div className="sidebar-footer">
        {/* User info display */}
        <div style={{
          padding: '12px 16px',
          borderTop: '1px solid rgba(255,255,255,0.1)',
          marginBottom: '4px',
          display: 'flex',
          alignItems: 'center',
          gap: '10px'
        }}>
          <div style={{
            width: '34px',
            height: '34px',
            borderRadius: '50%',
            background: 'rgba(255,255,255,0.15)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 700,
            fontSize: '14px',
            color: '#fff',
            flexShrink: 0,
          }}>
            {(firstName[0] || '?').toUpperCase()}
          </div>
          <div style={{ overflow: 'hidden' }}>
            <div style={{ fontWeight: 600, fontSize: '13px', color: '#fff', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
              {fullName}
            </div>
            <div style={{ fontSize: '11px', color: 'rgba(255,255,255,0.55)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              {role || 'User'}
            </div>
          </div>
        </div>

        <button className="logout-button" onClick={handleLogout}>
          <span className="nav-icon">{getIcon('logout')}</span>
          <span>Logout</span>
        </button>
      </div>
    </aside>
  );
};

// Simple SVG icons
const getIcon = (name: string) => {
  const icons: Record<string, JSX.Element> = {
    dashboard: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="3" width="7" height="7"></rect>
        <rect x="14" y="3" width="7" height="7"></rect>
        <rect x="14" y="14" width="7" height="7"></rect>
        <rect x="3" y="14" width="7" height="7"></rect>
      </svg>
    ),
    plant: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M2 22h20"></path>
        <path d="M12 2v20"></path>
        <path d="M12 8l-4-4"></path>
        <path d="M12 12l4-4"></path>
        <path d="M12 16l-4-4"></path>
        <path d="M12 20l4-4"></path>
      </svg>
    ),
    alert: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
        <line x1="12" y1="9" x2="12" y2="13"></line>
        <line x1="12" y1="17" x2="12.01" y2="17"></line>
      </svg>
    ),
    users: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
        <circle cx="9" cy="7" r="4"></circle>
        <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
        <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
      </svg>
    ),
    profile: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
        <circle cx="12" cy="7" r="4"></circle>
      </svg>
    ),
    logout: (
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
        <polyline points="16 17 21 12 16 7"></polyline>
        <line x1="21" y1="12" x2="9" y2="12"></line>
      </svg>
    ),
  };

  return icons[name] || null;
};

export default Sidebar;
