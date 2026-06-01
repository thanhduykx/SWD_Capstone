import React, { useState, useMemo } from 'react';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  FilterIcon,
  ImportIcon,
  CloseIcon,
  BoardsIcon
} from '../icons';

function DefenseSessions() {
  const [currentYear, setCurrentYear] = useState(2026);
  const [currentMonth, setCurrentMonth] = useState(2); // March (0-indexed: 2)
  const [viewMode, setViewMode] = useState('Month'); // Month or Week
  const [selectedSemester, setSelectedSemester] = useState('Spring 2026');
  const [selectedProjectType, setSelectedProjectType] = useState('All Types');
  const [activeSemesterPill, setActiveSemesterPill] = useState(true);

  // State to hold schedule detail drawer/modal
  const [activeDayDetails, setActiveDayDetails] = useState(null);

  // Month names in English
  const monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ];

  // Specific session schedule configuration for mock dates
  // key format: 'YYYY-MM-DD'
  const mockSchedules = {
    '2026-03-05': {
      count: 4,
      details: [
        { id: 'HB-24A-01', president: 'Dr. Jane Doe', time: '08:00 AM', room: 'Phòng 402', groups: 4 },
        { id: 'HB-24A-03', president: 'Dr. Jane Doe', time: '10:00 AM', room: 'Phòng 402', groups: 3 },
        { id: 'HB-24A-05', president: 'Dr. Alan Lee', time: '01:30 PM', room: 'Phòng 405', groups: 4 },
        { id: 'HB-24B-02', president: 'Dr. James Wilson', time: '03:30 PM', room: 'Phòng 408', groups: 4 }
      ]
    },
    '2026-03-10': {
      count: 8,
      subtitle: '4 AM / 4 PM',
      details: [
        { id: 'HB-24A-01', president: 'Dr. Jane Doe', time: '08:00 AM', room: 'Phòng 301', groups: 4 },
        { id: 'HB-24A-02', president: 'Prof. Mark Smith', time: '10:00 AM', room: 'Phòng 302', groups: 2 },
        { id: 'HB-24A-03', president: 'Dr. Jane Doe', time: '01:30 PM', room: 'Phòng 301', groups: 3 },
        { id: 'HB-24A-04', president: 'Prof. Mark Smith', time: '03:30 PM', room: 'Phòng 302', groups: 1 },
        { id: 'HB-24A-05', president: 'Dr. Alan Lee', time: '08:00 AM', room: 'Phòng 303', groups: 4 },
        { id: 'HB-24A-06', president: 'Dr. Emily Davis', time: '10:00 AM', room: 'Phòng 304', groups: 2 },
        { id: 'HB-24A-07', president: 'Dr. Robert Chen', time: '01:30 PM', room: 'Phòng 303', groups: 5 },
        { id: 'HB-24B-01', president: 'Dr. Lisa Wong', time: '03:30 PM', room: 'Phòng 304', groups: 3 }
      ]
    },
    '2026-03-11': {
      count: 12,
      details: [
        { id: 'HB-24A-01', president: 'Dr. Jane Doe', time: '08:00 AM', room: 'Phòng A1', groups: 4 },
        { id: 'HB-24A-02', president: 'Prof. Mark Smith', time: '08:00 AM', room: 'Phòng A2', groups: 2 },
        { id: 'HB-24A-03', president: 'Dr. Jane Doe', time: '10:00 AM', room: 'Phòng A1', groups: 3 },
        { id: 'HB-24A-04', president: 'Prof. Mark Smith', time: '10:00 AM', room: 'Phòng A2', groups: 1 },
        { id: 'HB-24A-05', president: 'Dr. Alan Lee', time: '01:30 PM', room: 'Phòng A3', groups: 4 },
        { id: 'HB-24A-06', president: 'Dr. Emily Davis', time: '01:30 PM', room: 'Phòng A4', groups: 2 },
        { id: 'HB-24A-07', president: 'Dr. Robert Chen', time: '03:30 PM', room: 'Phòng A3', groups: 5 },
        { id: 'HB-24B-01', president: 'Dr. Lisa Wong', time: '03:30 PM', room: 'Phòng A4', groups: 3 },
        { id: 'HB-23B-15', president: 'Dr. Alan Lee', time: '08:00 AM', room: 'Phòng B1', groups: 5 },
        { id: 'HB-23B-16', president: 'Dr. Sarah Jenkins', time: '10:00 AM', room: 'Phòng B2', groups: 3 },
        { id: 'HB-23B-17', president: 'Dr. David Clark', time: '01:30 PM', room: 'Phòng B1', groups: 2 },
        { id: 'HB-24B-02', president: 'Dr. James Wilson', time: '03:30 PM', room: 'Phòng B2', groups: 4 }
      ]
    }
  };

  // Generate calendar grid days (42 days)
  const calendarDays = useMemo(() => {
    // Days in current month
    const daysInMonth = new Date(currentYear, currentMonth + 1, 0).getDate();
    // Start day of current month (Sunday is 0, Monday is 1, etc.)
    // Shift so Monday is 0, Sunday is 6
    let startDayOfWeek = new Date(currentYear, currentMonth, 1).getDay();
    startDayOfWeek = startDayOfWeek === 0 ? 6 : startDayOfWeek - 1;

    // Days in previous month
    const prevMonthYear = currentMonth === 0 ? currentYear - 1 : currentYear;
    const prevMonth = currentMonth === 0 ? 11 : currentMonth - 1;
    const daysInPrevMonth = new Date(prevMonthYear, prevMonth + 1, 0).getDate();

    const days = [];

    // Previous month padding days
    for (let i = startDayOfWeek - 1; i >= 0; i--) {
      const dayNum = daysInPrevMonth - i;
      const dateStr = `${prevMonthYear}-${String(prevMonth + 1).padStart(2, '0')}-${String(dayNum).padStart(2, '0')}`;
      days.push({
        dayNumber: dayNum,
        isCurrentMonth: false,
        dateString: dateStr,
        isWeekend: false // Don't color padded weekends in blue
      });
    }

    // Current month days
    for (let i = 1; i <= daysInMonth; i++) {
      const dateStr = `${currentYear}-${String(currentMonth + 1).padStart(2, '0')}-${String(i).padStart(2, '0')}`;
      // Check day of week for weekend styling
      const dayIndex = new Date(currentYear, currentMonth, i).getDay();
      const isWeekend = dayIndex === 0 || dayIndex === 6; // Sat or Sun

      days.push({
        dayNumber: i,
        isCurrentMonth: true,
        dateString: dateStr,
        isWeekend: isWeekend
      });
    }

    // Next month padding days to fill 42 cells
    const remainingCells = 42 - days.length;
    const nextMonthYear = currentMonth === 11 ? currentYear + 1 : currentYear;
    const nextMonth = currentMonth === 11 ? 0 : currentMonth + 1;
    for (let i = 1; i <= remainingCells; i++) {
      const dateStr = `${nextMonthYear}-${String(nextMonth + 1).padStart(2, '0')}-${String(i).padStart(2, '0')}`;
      days.push({
        dayNumber: i,
        isCurrentMonth: false,
        dateString: dateStr,
        isWeekend: false
      });
    }

    return days;
  }, [currentYear, currentMonth]);

  // Navigate Months
  const handlePrevMonth = () => {
    if (currentMonth === 0) {
      setCurrentMonth(11);
      setCurrentYear(prev => prev - 1);
    } else {
      setCurrentMonth(prev => prev - 1);
    }
  };

  const handleNextMonth = () => {
    if (currentMonth === 11) {
      setCurrentMonth(0);
      setCurrentYear(prev => prev + 1);
    } else {
      setCurrentMonth(prev => prev + 1);
    }
  };

  // Mock File Upload action
  const handleImportExcel = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.xlsx, .xls';
    input.onchange = (e) => {
      const file = e.target.files[0];
      if (file) {
        alert(`Đã nhận file "${file.name}". Hệ thống đang xử lý import danh sách lịch bảo vệ...`);
      }
    };
    input.click();
  };

  return (
    <div className="sessions-view-content">
      {/* Page Header */}
      <div className="boards-header-action">
        <div className="boards-header-info">
          <h1>Defense Sessions</h1>
          <p className="header-subtitle">Manage and schedule defense boards across semesters.</p>
        </div>
        
        <div className="header-actions-right" style={{ display: 'flex', gap: '12px' }}>
          <button className="secondary-btn">
            <FilterIcon className="btn-icon-s" />
            <span>Filters</span>
          </button>
          
          <button className="primary-btn" onClick={handleImportExcel}>
            <ImportIcon className="btn-icon" />
            <span>Import from Excel</span>
          </button>
        </div>
      </div>

      {/* Filter Card */}
      <div className="filter-bar" style={{ padding: '14px 20px', marginBottom: '24px' }}>
        <div className="filter-left" style={{ display: 'flex', gap: '24px', alignItems: 'center', width: '100%' }}>
          <div className="form-item" style={{ width: '160px', gap: '4px' }}>
            <label style={{ fontSize: '10px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: '700' }}>Semester</label>
            <select 
              value={selectedSemester} 
              onChange={(e) => {
                setSelectedSemester(e.target.value);
                setActiveSemesterPill(true);
              }}
              style={{ height: '36px', fontSize: '13px', backgroundColor: '#f8fafc' }}
            >
              <option value="Spring 2026">Spring 2026</option>
              <option value="Fall 2025">Fall 2025</option>
              <option value="Spring 2025">Spring 2025</option>
            </select>
          </div>

          <div className="form-item" style={{ width: '160px', gap: '4px' }}>
            <label style={{ fontSize: '10px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: '700' }}>Project Type</label>
            <select 
              value={selectedProjectType} 
              onChange={(e) => setSelectedProjectType(e.target.value)}
              style={{ height: '36px', fontSize: '13px', backgroundColor: '#f8fafc' }}
            >
              <option value="All Types">All Types</option>
              <option value="Graduation Thesis">Graduation Thesis</option>
              <option value="Interdisciplinary Project">Interdisciplinary Project</option>
            </select>
          </div>

          <div style={{ height: '28px', width: '1px', backgroundColor: '#e2e8f0', marginInline: '8px' }}></div>

          <div className="active-filters-section" style={{ display: 'flex', alignItems: 'center', gap: '10px', flexGrow: 1 }}>
            <span style={{ fontSize: '12.5px', color: '#64748b', fontWeight: '500' }}>Active Filters:</span>
            {activeSemesterPill && (
              <div className="filter-pill">
                <span style={{ fontSize: '12px' }}>{selectedSemester}</span>
                <button className="pill-close-btn" onClick={() => setActiveSemesterPill(false)}>
                  <CloseIcon />
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Calendar Card View */}
      <div className="table-card" style={{ padding: '0', borderRadius: '16px' }}>
        {/* Calendar Nav Header */}
        <div className="calendar-nav-header" style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '18px 24px',
          borderBottom: '1px solid #f1f5f9'
        }}>
          <div className="month-selector" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <button className="pagination-btn" onClick={handlePrevMonth} style={{ width: '32px', height: '32px' }}>
              <ChevronLeftIcon />
            </button>
            <h2 style={{ fontSize: '18px', fontWeight: '700', color: '#0b1a30', width: '140px', textAlign: 'center' }}>
              {monthNames[currentMonth]} {currentYear}
            </h2>
            <button className="pagination-btn" onClick={handleNextMonth} style={{ width: '32px', height: '32px' }}>
              <ChevronRightIcon />
            </button>
          </div>

          <div className="view-toggle-pill" style={{
            display: 'flex',
            backgroundColor: '#f1f5f9',
            padding: '4px',
            borderRadius: '8px'
          }}>
            <button 
              className={`toggle-btn ${viewMode === 'Month' ? 'active' : ''}`}
              onClick={() => setViewMode('Month')}
              style={{
                border: 'none',
                padding: '6px 16px',
                borderRadius: '6px',
                fontSize: '12.5px',
                fontWeight: '600',
                cursor: 'pointer',
                backgroundColor: viewMode === 'Month' ? '#ffffff' : 'transparent',
                color: viewMode === 'Month' ? '#0f172a' : '#64748b',
                boxShadow: viewMode === 'Month' ? '0 1px 3px rgba(0, 0, 0, 0.05)' : 'none',
                transition: 'all 0.2s'
              }}
            >
              Month
            </button>
            <button 
              className={`toggle-btn ${viewMode === 'Week' ? 'active' : ''}`}
              onClick={() => setViewMode('Week')}
              style={{
                border: 'none',
                padding: '6px 16px',
                borderRadius: '6px',
                fontSize: '12.5px',
                fontWeight: '600',
                cursor: 'pointer',
                backgroundColor: viewMode === 'Week' ? '#ffffff' : 'transparent',
                color: viewMode === 'Week' ? '#0f172a' : '#64748b',
                boxShadow: viewMode === 'Week' ? '0 1px 3px rgba(0, 0, 0, 0.05)' : 'none',
                transition: 'all 0.2s'
              }}
            >
              Week
            </button>
          </div>
        </div>

        {/* Days of Week Title Header */}
        <div className="days-of-week-grid" style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(7, 1fr)',
          borderBottom: '1px solid #f1f5f9',
          backgroundColor: '#fafbfc'
        }}>
          {['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'].map((d) => (
            <div key={d} style={{
              textAlign: 'center',
              padding: '12px 0',
              fontSize: '11px',
              fontWeight: '700',
              color: '#94a3b8',
              letterSpacing: '0.05em'
            }}>
              {d}
            </div>
          ))}
        </div>

        {/* Calendar Cells Grid */}
        <div className="calendar-cells-grid" style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(7, 1fr)',
          gridAutoRows: 'minmax(110px, auto)'
        }}>
          {calendarDays.map((day, idx) => {
            const hasSchedule = mockSchedules[day.dateString];
            const isSelected = day.isCurrentMonth && day.dayNumber === 10 && currentMonth === 2 && currentYear === 2026;
            
            return (
              <div 
                key={idx} 
                onClick={() => hasSchedule && setActiveDayDetails({ date: day.dateString, data: hasSchedule })}
                style={{
                  borderRight: (idx + 1) % 7 === 0 ? 'none' : '1px solid #f1f5f9',
                  borderBottom: idx >= 35 ? 'none' : '1px solid #f1f5f9',
                  padding: '12px',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between',
                  backgroundColor: isSelected ? '#eff6ff' : '#ffffff',
                  outline: isSelected ? '2px solid #2563eb' : 'none',
                  outlineOffset: '-2px',
                  cursor: hasSchedule ? 'pointer' : 'default',
                  transition: 'all 0.2s ease',
                  position: 'relative',
                  zIndex: isSelected ? '5' : '1'
                }}
                className={`calendar-cell ${hasSchedule ? 'has-events' : ''}`}
              >
                {/* Day Number Row */}
                <div style={{ display: 'flex', justifyContent: 'flex-end', width: '100%' }}>
                  <span style={{
                    fontSize: '13.5px',
                    fontWeight: isSelected ? '700' : '500',
                    color: !day.isCurrentMonth 
                      ? '#cbd5e1' 
                      : (day.isWeekend ? '#2563eb' : '#475569')
                  }}>
                    {day.dayNumber}
                  </span>
                </div>

                {/* Day Content Area */}
                <div style={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center', marginTop: '8px' }}>
                  {day.isCurrentMonth && day.dayNumber === 3 && currentMonth === 2 && currentYear === 2026 && (
                    <span style={{ fontSize: '11px', color: '#94a3b8', fontStyle: 'italic', textAlign: 'center' }}>
                      No boards
                    </span>
                  )}

                  {hasSchedule && (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                      <span className="live-badge" style={{ 
                        backgroundColor: '#eff6ff', 
                        color: '#1e62ff',
                        padding: '4px 8px',
                        borderRadius: '6px',
                        fontSize: '11px',
                        fontWeight: '700',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: '4px'
                      }}>
                        <span className="live-dot" style={{ backgroundColor: '#1e62ff', width: '5px', height: '5px' }}></span>
                        {hasSchedule.count} Boards
                      </span>
                      {hasSchedule.subtitle && (
                        <span style={{ 
                          fontSize: '10px', 
                          color: '#64748b', 
                          fontWeight: '700',
                          textAlign: 'center',
                          marginTop: '2px'
                        }}>
                          {hasSchedule.subtitle}
                        </span>
                      )}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Interactive Day Details Modal/Drawer */}
      {activeDayDetails && (
        <div className="modal-overlay" onClick={() => setActiveDayDetails(null)}>
          <div className="modal-content-card" onClick={(e) => e.stopPropagation()} style={{ maxWidth: '640px' }}>
            <div className="modal-header">
              <div>
                <h2 style={{ fontSize: '18px', fontWeight: '700' }}>
                  Chi tiết Lịch chấm - Ngày {activeDayDetails.date.split('-')[2]} Tháng {activeDayDetails.date.split('-')[1]} năm {activeDayDetails.date.split('-')[0]}
                </h2>
                <p style={{ fontSize: '12.5px', color: '#64748b', marginTop: '4px' }}>
                  Tổng cộng có {activeDayDetails.data.count} hội đồng hoạt động vào ngày này.
                </p>
              </div>
              <button className="close-modal-btn" onClick={() => setActiveDayDetails(null)}>
                <CloseIcon />
              </button>
            </div>

            <div className="day-boards-list" style={{ display: 'flex', flexDirection: 'column', gap: '12px', maxHeight: '400px', overflowY: 'auto', paddingRight: '4px' }}>
              {activeDayDetails.data.details.map((b) => (
                <div key={b.id} style={{
                  border: '1px solid #e2e8f0',
                  borderRadius: '12px',
                  padding: '16px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  backgroundColor: '#fafbfc'
                }}>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <span style={{ fontWeight: '700', color: '#0f172a', fontSize: '14.5px' }}>Hội đồng: {b.id}</span>
                    <span style={{ fontSize: '13px', color: '#475569' }}>Chủ tịch: <strong>{b.president}</strong></span>
                    <span style={{ fontSize: '12.5px', color: '#64748b' }}>Phân công: {b.groups} nhóm đồ án</span>
                  </div>

                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '6px' }}>
                    <span className="status-badge grading" style={{ fontSize: '11px', fontWeight: '700' }}>
                      {b.room}
                    </span>
                    <span style={{ fontSize: '12px', fontWeight: '600', color: '#0b1a30' }}>
                      🕒 {b.time}
                    </span>
                  </div>
                </div>
              ))}
            </div>
            
            <div className="modal-actions-footer">
              <button className="secondary-btn" onClick={() => setActiveDayDetails(null)}>Đóng</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default DefenseSessions;
