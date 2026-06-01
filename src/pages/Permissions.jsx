import React, { useState, useMemo } from 'react';
import {
  SyncIcon,
  SearchIcon,
  FilterIcon,
  KeyIcon,
  CloseIcon,
  ChevronLeftIcon,
  ChevronRightIcon
} from '../icons';

function Permissions() {
  const [searchTerm, setSearchTerm] = useState('');
  const [deptFilter, setDeptFilter] = useState('CNTT'); // matches mock "Khoa CNTT" pill by default
  const [currentPage, setCurrentPage] = useState(1);
  const [isSyncing, setIsSyncing] = useState(false);
  const [syncToast, setSyncToast] = useState(null);
  
  // Modal State
  const [editingTeacher, setEditingTeacher] = useState(null);
  const [selectedBoards, setSelectedBoards] = useState([]);

  // Mock list of boards available for cross-view selection
  const availableBoards = [
    'BOARD-CS01',
    'BOARD-CS02',
    'BOARD-CS03',
    'BOARD-CS04',
    'BOARD-ISO2',
    'BOARD-DS01',
    'BOARD-DS02'
  ];

  // Mock list of 12 teachers to support multiple pages (showing 3 per page)
  const [teachers, setTeachers] = useState([
    {
      id: 1,
      name: 'Nguyễn Văn Hải',
      email: 'nvhai@university.edu.vn',
      initials: 'NH',
      color: '#3b82f6',
      dept: 'Khoa CNTT - Bộ môn KTPM',
      deptCode: 'CNTT',
      boards: ['BOARD-CS01', 'BOARD-CS03']
    },
    {
      id: 2,
      name: 'Trần Thị Lan',
      email: 'ttlan@university.edu.vn',
      initials: 'TL',
      color: '#ef4444',
      dept: 'Khoa Kinh tế - QTKD',
      deptCode: 'Kinh tế',
      boards: []
    },
    {
      id: 3,
      name: 'Lê Quang Minh',
      email: 'lqminh@university.edu.vn',
      initials: 'LM',
      color: '#10b981',
      dept: 'Khoa CNTT - HTTT',
      deptCode: 'CNTT',
      boards: ['BOARD-ISO2', 'BOARD-CS02', 'BOARD-CS04']
    },
    {
      id: 4,
      name: 'Phạm Văn Nam',
      email: 'pvnam@university.edu.vn',
      initials: 'PN',
      color: '#8b5cf6',
      dept: 'Khoa CNTT - Bộ môn KTPM',
      deptCode: 'CNTT',
      boards: ['BOARD-CS01']
    },
    {
      id: 5,
      name: 'Nguyễn Thị Mai',
      email: 'ntmai@university.edu.vn',
      initials: 'NM',
      color: '#f59e0b',
      dept: 'Khoa Ngoại ngữ - Tiếng Anh',
      deptCode: 'Ngoại ngữ',
      boards: []
    },
    {
      id: 6,
      name: 'Đặng Hoàng Long',
      email: 'dhlong@university.edu.vn',
      initials: 'HL',
      color: '#0d9488',
      dept: 'Khoa CNTT - KHMT',
      deptCode: 'CNTT',
      boards: ['BOARD-DS01', 'BOARD-DS02']
    },
    {
      id: 7,
      name: 'Vũ Thị Hồng',
      email: 'vthong@university.edu.vn',
      initials: 'VH',
      color: '#db2777',
      dept: 'Khoa Kinh tế - Tài chính',
      deptCode: 'Kinh tế',
      boards: ['BOARD-ISO2']
    },
    {
      id: 8,
      name: 'Hoàng Quốc Việt',
      email: 'hqviet@university.edu.vn',
      initials: 'QV',
      color: '#4f46e5',
      dept: 'Khoa CNTT - Kỹ thuật mạng',
      deptCode: 'CNTT',
      boards: ['BOARD-CS03', 'BOARD-CS04']
    }
  ]);

  const itemsPerPage = 3;

  // Sync LDAP Accounts Simulation
  const handleSyncLDAP = () => {
    setIsSyncing(true);
    setSyncToast(null);
    setTimeout(() => {
      setIsSyncing(false);
      setSyncToast('Đồng bộ thành công 125 tài khoản giảng viên từ hệ thống LDAP trường!');
      setTimeout(() => {
        setSyncToast(null);
      }, 5000);
    }, 1500);
  };

  // Open Edit Permissions dialog
  const openEditPermissions = (teacher) => {
    setEditingTeacher(teacher);
    setSelectedBoards(teacher.boards);
  };

  // Toggle board check status in modal
  const handleBoardCheckboxChange = (boardId) => {
    setSelectedBoards(prev => 
      prev.includes(boardId) 
        ? prev.filter(b => b !== boardId) 
        : [...prev, boardId]
    );
  };

  // Save changes to teachers list
  const handleSavePermissions = (e) => {
    e.preventDefault();
    setTeachers(prev => prev.map(t => 
      t.id === editingTeacher.id 
        ? { ...t, boards: selectedBoards } 
        : t
    ));
    setEditingTeacher(null);
  };

  // Filter Logic
  const filteredTeachers = useMemo(() => {
    return teachers.filter(t => {
      const matchSearch = 
        t.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        t.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
        t.dept.toLowerCase().includes(searchTerm.toLowerCase());
      
      const matchDept = deptFilter === 'all' || t.deptCode === deptFilter;

      return matchSearch && matchDept;
    });
  }, [teachers, searchTerm, deptFilter]);

  // Pagination Logic
  // Offset to match the exact total entries count "125" of the mockup when showing CNTT filter
  const mockupTotalEntries = deptFilter === 'CNTT' ? 125 : filteredTeachers.length;
  
  const totalPages = Math.ceil(filteredTeachers.length / itemsPerPage);
  
  const paginatedTeachers = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return filteredTeachers.slice(startIndex, startIndex + itemsPerPage);
  }, [filteredTeachers, currentPage]);

  const handlePageChange = (page) => {
    if (page >= 1 && page <= totalPages) {
      setCurrentPage(page);
    }
  };

  const startIndex = (currentPage - 1) * itemsPerPage + 1;
  const endIndex = Math.min(currentPage * itemsPerPage, filteredTeachers.length);

  return (
    <div className="permissions-view-content" style={{ width: '100%' }}>
      {/* Toast Alert Notification */}
      {syncToast && (
        <div style={{
          position: 'fixed',
          top: '20px',
          right: '20px',
          backgroundColor: '#10b981',
          color: '#ffffff',
          padding: '14px 24px',
          borderRadius: '8px',
          boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.1)',
          zIndex: 1100,
          fontWeight: '600',
          fontSize: '13.5px',
          animation: 'fadeIn 0.2s ease-out',
          display: 'flex',
          alignItems: 'center',
          gap: '8px'
        }}>
          <span>✓</span> {syncToast}
        </div>
      )}

      {/* Page Header */}
      <div className="boards-header-action">
        <div className="boards-header-info">
          <h1>Phân quyền Giảng Viên & Tài Khoản</h1>
          <p className="header-subtitle">Quản lý quyền truy cập xem chéo (Read-only) các Hội đồng Bảo vệ cho giảng viên.</p>
        </div>
        
        <button 
          className="secondary-btn" 
          onClick={handleSyncLDAP}
          disabled={isSyncing}
          style={{ height: '40px', gap: '8px', color: '#0f172a', borderColor: '#cbd5e1' }}
        >
          <SyncIcon className={`btn-icon-s ${isSyncing ? 'spinning' : ''}`} style={{ 
            animation: isSyncing ? 'spin 1s linear infinite' : 'none' 
          }} />
          <span>{isSyncing ? 'Đang đồng bộ...' : 'Đồng bộ tài khoản email trường'}</span>
        </button>
      </div>

      {/* Filter / Search bar */}
      <div className="filter-bar" style={{ padding: '14px 20px', marginBottom: '16px' }}>
        <div className="filter-left" style={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
          <div className="search-input-wrapper" style={{ width: '420px', backgroundColor: '#f8fafc' }}>
            <SearchIcon className="search-box-icon" />
            <input
              type="text"
              placeholder="Tìm kiếm theo Tên giảng viên, Email, Bộ môn..."
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setCurrentPage(1);
              }}
            />
          </div>

          <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
            <select
              value={deptFilter}
              onChange={(e) => {
                setDeptFilter(e.target.value);
                setCurrentPage(1);
              }}
              style={{
                height: '38px',
                borderRadius: '8px',
                border: '1px solid #e2e8f0',
                paddingInline: '12px',
                fontSize: '13px',
                color: '#334155',
                backgroundColor: '#ffffff',
                outline: 'none',
                minWidth: '180px'
              }}
            >
              <option value="all">Tất cả Khoa/Bộ môn</option>
              <option value="CNTT">Khoa CNTT</option>
              <option value="Kinh tế">Khoa Kinh tế</option>
              <option value="Ngoại ngữ">Khoa Ngoại ngữ</option>
            </select>

            <button className="filter-btn-secondary" style={{ height: '38px' }}>
              <FilterIcon className="btn-icon-s" />
            </button>
          </div>
        </div>
      </div>

      {/* Active Filter Pills Row */}
      {deptFilter !== 'all' && (
        <div className="active-filters-row" style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
          <div className="filter-pill">
            <span>Khoa {deptFilter}</span>
            <button className="pill-close-btn" onClick={() => {
              setDeptFilter('all');
              setCurrentPage(1);
            }}>
              <CloseIcon />
            </button>
          </div>
        </div>
      )}

      {/* Table Container */}
      <div className="table-card" style={{ borderRadius: '12px' }}>
        <div className="table-responsive">
          <table className="boards-table">
            <thead>
              <tr>
                <th style={{ width: '70px' }}>STT</th>
                <th>Giảng viên</th>
                <th>Đơn vị công tác</th>
                <th>Quyền xem (Read-only) Boards</th>
                <th style={{ width: '100px', textAlign: 'center' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {paginatedTeachers.length > 0 ? (
                paginatedTeachers.map((teacher, index) => {
                  const globalIndex = startIndex + index;
                  
                  return (
                    <tr key={teacher.id}>
                      <td>{globalIndex}</td>
                      <td>
                        <div className="president-cell">
                          <div 
                            className="avatar-initials" 
                            style={{ 
                              backgroundColor: teacher.color,
                              width: '32px',
                              height: '32px',
                              fontSize: '12px',
                              fontWeight: '700'
                            }}
                          >
                            {teacher.initials}
                          </div>
                          <div style={{ display: 'flex', flexDirection: 'column' }}>
                            <span className="president-name" style={{ fontSize: '13.5px' }}>{teacher.name}</span>
                            <span style={{ fontSize: '11.5px', color: '#64748b', marginTop: '1px' }}>{teacher.email}</span>
                          </div>
                        </div>
                      </td>
                      <td style={{ fontSize: '13px', color: '#475569' }}>
                        {teacher.dept}
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', alignItems: 'center' }}>
                          {teacher.boards.length > 0 ? (
                            <>
                              {teacher.boards.slice(0, 2).map((board) => (
                                <span key={board} className="size-badge" style={{ 
                                  backgroundColor: '#f1f5f9', 
                                  color: '#475569', 
                                  fontSize: '11.5px',
                                  padding: '4px 8px',
                                  fontWeight: '600'
                                }}>
                                  {board}
                                </span>
                              ))}
                              {teacher.boards.length > 2 && (
                                <span className="size-badge" style={{ 
                                  backgroundColor: '#f1f5f9', 
                                  color: '#475569', 
                                  fontSize: '11.5px',
                                  padding: '4px 8px',
                                  fontWeight: '600'
                                }}>
                                  +{teacher.boards.length - 2} nữa
                                </span>
                              )}
                            </>
                          ) : (
                            <span style={{ fontSize: '12.5px', color: '#94a3b8', fontStyle: 'italic' }}>
                              Chưa được cấp quyền
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        <button 
                          className="action-icon-btn" 
                          style={{ color: '#1e62ff', marginInline: 'auto' }}
                          title="Cấp quyền xem Boards"
                          onClick={() => openEditPermissions(teacher)}
                        >
                          <KeyIcon style={{ width: '17px', height: '17px' }} />
                        </button>
                      </td>
                    </tr>
                  );
                })
              ) : (
                <tr>
                  <td colSpan="5" className="no-data-cell">
                    Không tìm thấy giảng viên phù hợp!
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Table Footer / Pagination */}
        <div className="table-footer">
          <span className="showing-entries-text">
            Hiển thị {startIndex}-{endIndex} của {mockupTotalEntries} Giảng viên
          </span>
          
          <div className="pagination-buttons">
            <button
              className={`pagination-btn ${currentPage === 1 ? 'disabled' : ''}`}
              onClick={() => handlePageChange(currentPage - 1)}
              disabled={currentPage === 1}
            >
              <ChevronLeftIcon />
            </button>
            
            {Array.from({ length: totalPages }).map((_, pIdx) => {
              const pNum = pIdx + 1;
              return (
                <button
                  key={pNum}
                  className={`pagination-btn ${currentPage === pNum ? 'active' : ''}`}
                  onClick={() => handlePageChange(pNum)}
                  style={{
                    backgroundColor: currentPage === pNum ? '#1e62ff' : '#ffffff',
                    color: currentPage === pNum ? '#ffffff' : '#475569',
                    border: currentPage === pNum ? 'none' : '1px solid #e2e8f0',
                    fontWeight: '600'
                  }}
                >
                  {pNum}
                </button>
              );
            })}

            {deptFilter === 'CNTT' && totalPages < 13 && (
              <>
                <span style={{ color: '#94a3b8', marginInline: '4px' }}>...</span>
                <button
                  className="pagination-btn"
                  onClick={() => alert('Chuyển tới trang 13')}
                  style={{ fontWeight: '600' }}
                >
                  13
                </button>
              </>
            )}

            <button
              className={`pagination-btn ${currentPage === totalPages ? 'disabled' : ''}`}
              onClick={() => handlePageChange(currentPage + 1)}
              disabled={currentPage === totalPages}
            >
              <ChevronRightIcon />
            </button>
          </div>
        </div>
      </div>

      {/* Permissions Edit Modal Dialog */}
      {editingTeacher && (
        <div className="modal-overlay">
          <div className="modal-content-card" style={{ maxWidth: '520px' }}>
            <div className="modal-header">
              <h2>Cấp quyền xem Boards</h2>
              <button className="close-modal-btn" onClick={() => setEditingTeacher(null)}>
                <CloseIcon />
              </button>
            </div>

            <div style={{ marginBottom: '16px', padding: '12px 16px', backgroundColor: '#f8fafc', borderRadius: '8px', border: '1px solid #f1f5f9' }}>
              <div style={{ fontWeight: '700', color: '#0f172a', fontSize: '14px' }}>{editingTeacher.name}</div>
              <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>{editingTeacher.email}</div>
              <div style={{ fontSize: '12px', color: '#475569', marginTop: '4px', fontWeight: '500' }}>{editingTeacher.dept}</div>
            </div>

            <form onSubmit={handleSavePermissions} className="modal-form">
              <label style={{ fontSize: '12.5px', fontWeight: '700', color: '#475569', textTransform: 'uppercase', letterSpacing: '0.03em' }}>
                Chọn danh sách các Hội đồng được quyền xem:
              </label>
              
              <div style={{ 
                display: 'grid', 
                gridTemplateColumns: 'repeat(2, 1fr)', 
                gap: '10px', 
                border: '1px solid #e2e8f0', 
                borderRadius: '8px', 
                padding: '16px',
                maxHeight: '180px',
                overflowY: 'auto',
                backgroundColor: '#ffffff'
              }}>
                {availableBoards.map((boardId) => {
                  const isChecked = selectedBoards.includes(boardId);
                  return (
                    <label 
                      key={boardId} 
                      style={{ 
                        display: 'flex', 
                        alignItems: 'center', 
                        gap: '8px', 
                        fontSize: '13px', 
                        color: '#0f172a', 
                        cursor: 'pointer',
                        padding: '6px 8px',
                        borderRadius: '4px',
                        backgroundColor: isChecked ? '#eff6ff' : 'transparent',
                        transition: 'background-color 0.2s'
                      }}
                    >
                      <input 
                        type="checkbox" 
                        checked={isChecked}
                        onChange={() => handleBoardCheckboxChange(boardId)}
                        style={{ cursor: 'pointer', width: '15px', height: '15px' }}
                      />
                      <span style={{ fontWeight: isChecked ? '600' : '400' }}>{boardId}</span>
                    </label>
                  );
                })}
              </div>

              <div className="modal-actions-footer">
                <button 
                  type="button" 
                  className="secondary-btn" 
                  onClick={() => setEditingTeacher(null)}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="primary-btn">
                  Lưu thay đổi
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default Permissions;
