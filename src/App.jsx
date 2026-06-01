import React, { useState } from 'react';
import './App.css';
import Sidebar from './components/Sidebar';
import Header from './components/Header';
import Dashboard from './pages/Dashboard';
import DefenseBoards from './pages/DefenseBoards';
import DefenseSessions from './pages/DefenseSessions';
import Permissions from './pages/Permissions';

// A helper placeholder component for tabs not yet fully built
function PlaceholderView({ title, description }) {
  return (
    <div className="placeholder-view-wrapper" style={{ width: '100%' }}>
      <div className="boards-header-action">
        <div className="boards-header-info">
          <h1>{title}</h1>
          <p className="header-subtitle">Tính năng thuộc phần mềm quản lý chấm đồ án tốt nghiệp.</p>
        </div>
      </div>
      <div className="placeholder-view">
        <h2>{title} View</h2>
        <p>{description || 'Trang này đang được phát triển theo kịch bản vận hành của hệ thống.'}</p>
      </div>
    </div>
  );
}

function App() {
  const [activeTab, setActiveTab] = useState('permissions'); // Active 'permissions' tab by default to display the new view!

  // Central State: Initial list of 12 boards matching "Showing 1 to 3 of 12 boards"
  const [boards, setBoards] = useState([
    {
      id: 'HB-24A-01',
      size: '5 Members',
      president: { initials: 'JD', name: 'Dr. Jane Doe', color: '#0f172a' },
      groups: 4,
      status: 'active',
      secretary: 'Prof. Alice Johnson',
      member1: 'Dr. Emily Davis',
      member2: 'Dr. Robert Chen'
    },
    {
      id: 'HB-24A-02',
      size: '3 Members',
      president: { initials: 'MS', name: 'Prof. Mark Smith', color: '#2563eb' },
      groups: 2,
      status: 'pending',
      secretary: 'Dr. Lisa Wong',
      member1: 'Dr. James Wilson',
      member2: ''
    },
    {
      id: 'HB-23B-15',
      size: '5 Members',
      president: { initials: 'AL', name: 'Dr. Alan Lee', color: '#ef4444' },
      groups: 5,
      status: 'closed',
      secretary: 'Dr. Sarah Jenkins',
      member1: 'Dr. David Clark',
      member2: 'Prof. Tom Cruise'
    },
    {
      id: 'HB-24A-03',
      size: '5 Members',
      president: { initials: 'JD', name: 'Dr. Jane Doe', color: '#0f172a' },
      groups: 3,
      status: 'active',
      secretary: 'Prof. Alice Johnson',
      member1: 'Dr. Emily Davis',
      member2: 'Dr. Robert Chen'
    },
    {
      id: 'HB-24A-04',
      size: '3 Members',
      president: { initials: 'MS', name: 'Prof. Mark Smith', color: '#2563eb' },
      groups: 1,
      status: 'pending',
      secretary: 'Dr. Lisa Wong',
      member1: 'Dr. James Wilson',
      member2: ''
    },
    {
      id: 'HB-24A-05',
      size: '5 Members',
      president: { initials: 'AL', name: 'Dr. Alan Lee', color: '#ef4444' },
      groups: 4,
      status: 'active',
      secretary: 'Dr. Sarah Jenkins',
      member1: 'Dr. David Clark',
      member2: 'Prof. Tom Cruise'
    },
    {
      id: 'HB-23B-16',
      size: '3 Members',
      president: { initials: 'SJ', name: 'Dr. Sarah Jenkins', color: '#16a34a' },
      groups: 3,
      status: 'closed',
      secretary: 'Dr. Alan Lee',
      member1: 'Dr. David Clark',
      member2: ''
    },
    {
      id: 'HB-23B-17',
      size: '5 Members',
      president: { initials: 'DC', name: 'Dr. David Clark', color: '#8b5cf6' },
      groups: 2,
      status: 'closed',
      secretary: 'Dr. Alan Lee',
      member1: 'Dr. Sarah Jenkins',
      member2: 'Prof. Tom Cruise'
    },
    {
      id: 'HB-24A-06',
      size: '3 Members',
      president: { initials: 'ED', name: 'Dr. Emily Davis', color: '#ea580c' },
      groups: 2,
      status: 'pending',
      secretary: 'Prof. Alice Johnson',
      member1: 'Dr. Jane Doe',
      member2: ''
    },
    {
      id: 'HB-24A-07',
      size: '5 Members',
      president: { initials: 'RC', name: 'Dr. Robert Chen', color: '#0d9488' },
      groups: 5,
      status: 'active',
      secretary: 'Prof. Alice Johnson',
      member1: 'Dr. Jane Doe',
      member2: 'Dr. Emily Davis'
    },
    {
      id: 'HB-24B-01',
      size: '3 Members',
      president: { initials: 'LW', name: 'Dr. Lisa Wong', color: '#db2777' },
      groups: 3,
      status: 'pending',
      secretary: 'Prof. Mark Smith',
      member1: 'Dr. James Wilson',
      member2: ''
    },
    {
      id: 'HB-24B-02',
      size: '5 Members',
      president: { initials: 'JW', name: 'Dr. James Wilson', color: '#4f46e5' },
      groups: 4,
      status: 'active',
      secretary: 'Prof. Mark Smith',
      member1: 'Dr. Lisa Wong',
      member2: 'Dr. Robert Chen'
    }
  ]);

  // Statistics offsets for dynamic dashboard integration
  const baseBoardsCount = 24;
  const initialBoardsLength = 12;
  const dynamicTotalBoards = baseBoardsCount + (boards.length - initialBoardsLength);

  const baseGroupsCount = 120;
  // Initial group sum of the 12 boards is 38
  const initialGroupsSum = 38;
  const currentGroupsSum = boards.reduce((acc, curr) => acc + curr.groups, 0);
  const dynamicTotalGroups = baseGroupsCount + (currentGroupsSum - initialGroupsSum);

  // Monitor boards (today's active sessions shown on Dashboard)
  const monitorBoards = [
    { id: 'HĐ-001', president: 'Nguyễn Văn A', status: 'active' },
    { id: 'HĐ-002', president: 'Trần Thị B', status: 'closed' },
    { id: 'HĐ-003', president: 'Chưa có dữ liệu', status: 'pending' }
  ];

  // Handlers for Boards page
  const handleAddBoard = (newBoard) => {
    setBoards(prev => [newBoard, ...prev]);
  };

  const handleEditBoard = (updatedBoard) => {
    setBoards(prev => prev.map(b => b.id === updatedBoard.id ? updatedBoard : b));
  };

  // Rendering content view dynamically
  const renderContentView = () => {
    switch (activeTab) {
      case 'dashboard':
        return (
          <div style={{ width: '100%' }}>
            <div className="boards-header-action" style={{ marginBottom: '24px' }}>
              <div className="boards-header-info">
                <h1>Dashboard Overview</h1>
                <p className="header-subtitle">Fall Semester 2023 - Academic Defense Period</p>
              </div>
            </div>
            <Dashboard 
              totalBoards={dynamicTotalBoards}
              totalGroups={dynamicTotalGroups}
              totalDays={5}
              monitorBoards={monitorBoards}
            />
          </div>
        );
      case 'boards':
        return (
          <DefenseBoards 
            boards={boards}
            onAddBoard={handleAddBoard}
            onEditBoard={handleEditBoard}
          />
        );
      case 'sessions':
        return (
          <DefenseSessions />
        );
      case 'semesters':
        return (
          <PlaceholderView 
            title="Academic Semesters" 
            description="Quản lý cấu trúc đào tạo: Học kỳ, loại dự án khóa luận và phân công bộ môn." 
          />
        );
      case 'groups':
        return (
          <PlaceholderView 
            title="Groups & Topics" 
            description="Danh sách các nhóm sinh viên, đề tài đăng ký và phân công lịch báo vệ chi tiết." 
          />
        );
      case 'review':
        return (
          <PlaceholderView 
            title="2nd Review" 
            description="Quy trình xử lý hậu kỳ: Gom các nhóm rớt lần 1, thiết lập hội đồng mới chấm lần 2." 
          />
        );
      case 'permissions':
        return (
          <Permissions />
        );
      case 'accounts':
        return (
          <PlaceholderView 
            title="Accounts" 
            description="Quản lý danh sách tài khoản giảng viên, sinh viên đồng bộ thông qua cổng trường." 
          />
        );
      default:
        return <PlaceholderView title="Dashboard" />;
    }
  };

  const getSearchPlaceholder = (tab) => {
    switch (tab) {
      case 'dashboard': return 'Search dashboards...';
      case 'sessions': return 'Search sessions...';
      case 'boards': return 'Search boards...';
      case 'permissions': return 'Tìm kiếm giảng viên...';
      default: return 'Search...';
    }
  };

  return (
    <div className="dashboard-container">
      {/* Sidebar Navigation */}
      <Sidebar activeTab={activeTab} setActiveTab={setActiveTab} />
      
      {/* Main Content Workspace */}
      <main className="main-content" style={{ padding: 0, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        {/* Global Top Navbar */}
        <Header searchPlaceholder={getSearchPlaceholder(activeTab)} />

        {/* Scrollable page view content area */}
        <div className="page-content-scroll" style={{ 
          padding: '32px 40px', 
          overflowY: 'auto', 
          flexGrow: 1, 
          display: 'flex', 
          width: '100%', 
          boxSizing: 'border-box' 
        }}>
          {renderContentView()}
        </div>
      </main>
    </div>
  );
}

export default App;
