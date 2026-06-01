import React from 'react';
import {
  DashboardIcon,
  SemestersIcon,
  SessionsIcon,
  BoardsIcon,
  GroupsIcon,
  ReviewIcon,
  PermissionsIcon,
  AccountsIcon,
  SettingsIcon,
  SupportIcon
} from '../icons';

function Sidebar({ activeTab, setActiveTab }) {
  const menuItems = [
    { id: 'dashboard', label: 'Dashboard', icon: DashboardIcon },
    { id: 'semesters', label: 'Academic Semesters', icon: SemestersIcon },
    { id: 'sessions', label: 'Defense Sessions', icon: SessionsIcon },
    { id: 'boards', label: 'Defense Boards', icon: BoardsIcon },
    { id: 'groups', label: 'Groups & Topics', icon: GroupsIcon },
    { id: 'review', label: '2nd Review', icon: ReviewIcon },
    { id: 'permissions', label: 'Permissions', icon: PermissionsIcon },
    { id: 'accounts', label: 'Accounts', icon: AccountsIcon }
  ];

  return (
    <aside className="sidebar">
      <div className="sidebar-top">
        <div className="sidebar-header">
          <div className="logo-circle">DM</div>
          <div className="sidebar-info">
            <span className="sidebar-title">Defense Manager</span>
            <span className="sidebar-subtitle">Academic Admin</span>
          </div>
        </div>

        <nav className="sidebar-nav">
          {menuItems.map((item) => {
            const IconComponent = item.icon;
            return (
              <div
                key={item.id}
                className={`nav-item ${activeTab === item.id ? 'active' : ''}`}
                onClick={() => setActiveTab(item.id)}
              >
                <IconComponent className="nav-icon" />
                <span>{item.label}</span>
              </div>
            );
          })}
        </nav>
      </div>

      <div className="sidebar-footer">
        <div className="nav-item">
          <SettingsIcon className="nav-icon" />
          <span>Settings</span>
        </div>
        <div className="nav-item">
          <SupportIcon className="nav-icon" />
          <span>Support</span>
        </div>
      </div>
    </aside>
  );
}

export default Sidebar;
