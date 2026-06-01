import React from 'react';
import userAvatar from '../assets/user_avatar.png';
import { SearchIcon, BellIcon } from '../icons';

function Header({ searchPlaceholder = "Search sessions..." }) {
  return (
    <header className="top-navbar" style={{
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      height: '64px',
      padding: '0 40px',
      backgroundColor: '#ffffff',
      borderBottom: '1px solid #e2e8f0',
      width: '100%',
      boxSizing: 'border-box',
      flexShrink: 0
    }}>
      <div className="header-search-wrapper" style={{
        position: 'relative',
        display: 'flex',
        alignItems: 'center',
        backgroundColor: '#f1f5f9',
        borderRadius: '8px',
        paddingLeft: '12px',
        width: '320px',
        height: '38px'
      }}>
        <SearchIcon style={{ color: '#94a3b8', width: '16px', height: '16px', marginRight: '8px' }} />
        <input 
          type="text" 
          placeholder={searchPlaceholder} 
          style={{
            border: 'none',
            outline: 'none',
            background: 'transparent',
            fontSize: '13.5px',
            color: '#0f172a',
            width: '100%',
            height: '100%'
          }} 
        />
      </div>
      
      <div className="header-right" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
        <button className="icon-btn notification-btn" aria-label="Notifications" style={{
          width: '40px',
          height: '40px',
          borderRadius: '50%',
          backgroundColor: '#ffffff',
          border: '1px solid #e2e8f0',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          color: '#475569',
          cursor: 'pointer',
          position: 'relative'
        }}>
          <BellIcon style={{ width: '18px', height: '18px' }} />
          <span className="badge-dot" style={{
            position: 'absolute',
            top: '10px',
            right: '11px',
            width: '6px',
            height: '6px',
            backgroundColor: '#ef4444',
            border: '1px solid #ffffff',
            borderRadius: '50%'
          }}></span>
        </button>
        <img 
          src={userAvatar} 
          alt="Academic Admin Profile" 
          className="profile-avatar" 
          style={{
            width: '40px',
            height: '40px',
            border: '1.5px solid #e2e8f0',
            borderRadius: '50%',
            objectFit: 'cover',
            cursor: 'pointer'
          }}
        />
      </div>
    </header>
  );
}

export default Header;
