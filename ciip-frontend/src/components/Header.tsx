import { useState, useRef, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';

const Header = () => {
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  // Get tenant name from localStorage
  const tenantName = localStorage.getItem('tenantName') || 'Guest';




  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setDropdownOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, []);

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('tenantId');
    localStorage.removeItem('tenantName');
    localStorage.removeItem('role');
    localStorage.removeItem('userId');
    navigate('/login');
  };



  return (
    <header className="header">
      <div className="header-left">
        <h1 className="header-title">Crest Industrial Intelligence Platform</h1>
      </div>
      <div className="header-right">
        <div className="dropdown-container" ref={dropdownRef}>
          <button 
            className="tenant-button"
            onClick={() => setDropdownOpen(!dropdownOpen)}
          >
            <span className="tenant-name">{tenantName}</span>
            <svg 
              className={`dropdown-arrow ${dropdownOpen ? 'open' : ''}`}
              width="12" 
              height="12" 
              viewBox="0 0 24 24" 
              fill="none" 
              stroke="currentColor" 
              strokeWidth="2" 
              strokeLinecap="round" 
              strokeLinejoin="round"
            >
              <polyline points="6 9 12 15 18 9"></polyline>
            </svg>
          </button>

          {dropdownOpen && (
            <div className="dropdown-menu">
              <Link to="/profile" className="dropdown-item" onClick={() => setDropdownOpen(false)}>
                Profile
              </Link>
              <button className="dropdown-item logout-item" onClick={handleLogout}>
                Logout
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );

};

export default Header;
