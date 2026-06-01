import React from 'react';
import {
  CardBoardsIcon,
  CardGroupsIcon,
  CardCalendarIcon
} from '../icons';

function Dashboard({ totalBoards, totalGroups, totalDays, monitorBoards }) {
  return (
    <div className="dashboard-view-content">
      {/* Stats Grid Cards */}
      <section className="stats-grid">
        {/* Card 1: Total Boards */}
        <div className="stat-card">
          <div className="stat-info">
            <span className="stat-label">Tổng số hội đồng</span>
            <span className="stat-value">{totalBoards}</span>
          </div>
          <div className="stat-bg-decoration"></div>
          <div className="stat-icon-container">
            <CardBoardsIcon />
          </div>
        </div>

        {/* Card 2: Total Groups */}
        <div className="stat-card">
          <div className="stat-info">
            <span className="stat-label">Tổng số nhóm</span>
            <span className="stat-value">{totalGroups}</span>
          </div>
          <div className="stat-bg-decoration"></div>
          <div className="stat-icon-container">
            <CardGroupsIcon />
          </div>
        </div>

        {/* Card 3: Total Days */}
        <div className="stat-card">
          <div className="stat-info">
            <span className="stat-label">Tổng số ngày bảo vệ</span>
            <span className="stat-value">{totalDays}</span>
          </div>
          <div className="stat-bg-decoration"></div>
          <div className="stat-icon-container">
            <CardCalendarIcon />
          </div>
        </div>
      </section>

      {/* Charts & Monitoring Section */}
      <section className="charts-grid">
        {/* Real-time Monitoring List */}
        <div className="chart-card">
          <div className="chart-header">
            <span className="chart-title">Giám sát thời gian thực</span>
            <span className="live-badge">
              <span className="live-dot"></span>
              Live
            </span>
          </div>
          
          <div className="monitor-list">
            {monitorBoards.map((board, idx) => {
              let statusClass = 'not-started';
              let statusText = 'Chưa bắt đầu';
              let statusIcon = <div className="solid-dot grey"></div>;

              if (board.status === 'active') {
                statusClass = 'grading';
                statusText = 'Đang chấm';
                statusIcon = (
                  <div className="concentric-circles">
                    <div className="concentric-circles-inner"></div>
                  </div>
                );
              } else if (board.status === 'closed') {
                statusClass = 'ended';
                statusText = 'Đã kết thúc';
                statusIcon = <div className="solid-dot red"></div>;
              }

              return (
                <div key={board.id || idx} className="monitor-item">
                  <div className="monitor-item-left">
                    <div className="monitor-status-icon">
                      {statusIcon}
                    </div>
                    <div className="monitor-details">
                      <span className="monitor-code">{board.id}</span>
                      <span className="monitor-president">
                        Chủ tịch: {board.president.name || board.president}
                      </span>
                    </div>
                  </div>
                  <div className="monitor-item-right">
                    <span className={`status-badge ${statusClass}`}>{statusText}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Pass/Fail Rate Donut Chart */}
        <div className="chart-card">
          <div className="chart-header">
            <span className="chart-title">Tỷ lệ Đạt/Rớt</span>
          </div>
          
          <div className="donut-chart-container">
            <div className="donut-chart">
              <div className="donut-center">
                <span>85%</span>
              </div>
            </div>
            
            <div className="donut-legend">
              <div className="legend-item">
                <span className="legend-box pass"></span>
                <span>Đạt (102)</span>
              </div>
              <div className="legend-item">
                <span className="legend-box fail"></span>
                <span>Rớt (18)</span>
              </div>
            </div>
          </div>
        </div>

        {/* Daily Progress Bar Chart */}
        <div className="chart-card">
          <div className="chart-header">
            <span className="chart-title">Tiến độ chấm theo ngày</span>
          </div>
          
          <div className="bar-chart-container">
            {/* Grid lines in background */}
            <div className="chart-background-lines">
              <div className="chart-line"></div>
              <div className="chart-line"></div>
              <div className="chart-line"></div>
              <div className="chart-line"></div>
              <div className="chart-line"></div>
            </div>

            {/* Bars */}
            <div className="bars-container">
              {/* Day 1: 35% */}
              <div className="bar-wrapper">
                <div className="bar-column light-blue-1" style={{ height: '35%' }}></div>
              </div>

              {/* Day 2: 68% */}
              <div className="bar-wrapper">
                <div className="bar-column light-blue-2" style={{ height: '68%' }}></div>
              </div>

              {/* Day 3: 95% (Active/High) */}
              <div className="bar-wrapper">
                <div className="bar-column active-blue" style={{ height: '95%' }}>
                  <span className="active-bar-label">45</span>
                </div>
              </div>

              {/* Day 4: 12% */}
              <div className="bar-wrapper">
                <div className="bar-column light-grey" style={{ height: '12%' }}></div>
              </div>

              {/* Day 5: 0% */}
              <div className="bar-wrapper">
                <div className="bar-column empty" style={{ height: '0%' }}></div>
              </div>
            </div>

            {/* X Axis Labels */}
            <div className="x-axis-labels">
              <span className="x-axis-label">Ngày 1</span>
              <span className="x-axis-label">Ngày 2</span>
              <span className="x-axis-label active">Ngày 3</span>
              <span className="x-axis-label">Ngày 4</span>
              <span className="x-axis-label">Ngày 5</span>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

export default Dashboard;
