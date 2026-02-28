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


  const { data: plantsData } = useQuery(PLANTS_QUERY);
  const plants: Plant[] = plantsData?.plants || [];

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('tenantId');
    localStorage.removeItem('tenantName');
    localStorage.removeItem('role');
    localStorage.removeItem('userId');
    navigate('/login');
  };

  const isPlantDashboardActive = location.pathname.startsWith('/plant-dashboard');

  const togglePlantSection = () => {
    setIsPlantSectionExpanded(!isPlantSectionExpanded);
  };

  const handlePlantClick = (plantId: string) => {
    navigate(`/plant-dashboard/${plantId}`);
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

          {/* Alert Management */}
          <li className="nav-item">
            <NavLink
              to="/alerts"
              className={({ isActive }) =>
                isActive ? 'nav-link active' : 'nav-link'
              }
            >
              <span className="nav-icon">{getIcon('alert')}</span>
              <span className="nav-label">Alert Management</span>
            </NavLink>
          </li>

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
