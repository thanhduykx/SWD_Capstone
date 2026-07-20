using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CleanDemoAcademicData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $cleanup$
                DECLARE
                    canonical_defense_round_id integer;
                    source_defense_round_id integer;
                BEGIN
                    -- Preserve exact pre-cleanup values for traceability.
                    INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                    SELECT 'clean-demo-academic-data-v1', 'user', id::text, to_jsonb(u)
                    FROM users u WHERE username LIKE 'demo.%'
                    ON CONFLICT DO NOTHING;
                    INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                    SELECT 'clean-demo-academic-data-v1', 'lecturer', id::text, to_jsonb(l)
                    FROM lecturers l WHERE code LIKE 'DEMO_2026_%' OR full_name IN ('unassigned', 'Lecturer Account', 'Dr. Test Lecturer')
                    ON CONFLICT DO NOTHING;
                    INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                    SELECT 'clean-demo-academic-data-v1', 'student', id::text, to_jsonb(s)
                    FROM students s WHERE code LIKE 'DEMO_2026_%' OR full_name IN ('Test Student', 'Student Account')
                    ON CONFLICT DO NOTHING;
                    INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                    SELECT 'clean-demo-academic-data-v1', 'topic', id::text, to_jsonb(t)
                    FROM topics t WHERE code LIKE 'DEMO_2026_%'
                    ON CONFLICT DO NOTHING;
                    INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                    SELECT 'clean-demo-academic-data-v1', 'capstone_group', id::text, to_jsonb(g)
                    FROM capstone_groups g WHERE code LIKE 'DEMO_2026_%'
                    ON CONFLICT DO NOTHING;
                    INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                    SELECT 'clean-demo-academic-data-v1', 'defense_round', id::text, to_jsonb(d)
                    FROM defense_rounds d
                    WHERE code = 'Def1-Su26' OR code LIKE 'REV_1_Review1_%' OR code LIKE 'DEMO_2026_%'
                    ON CONFLICT DO NOTHING;

                    UPDATE users SET username = 'admin.swd', email = 'admin.swd@fpt.edu.vn'
                    WHERE username = 'demo.admin';
                    UPDATE users SET username = 'training.swd', email = 'training.swd@fpt.edu.vn'
                    WHERE username = 'demo.training';
                    UPDATE users SET username = 'HungTQGV24004', email = 'hungtq.gv24004@fpt.edu.vn'
                    WHERE username = 'demo.lecturer1';
                    UPDATE users SET username = 'LanVTGV24005', email = 'lanvt.gv24005@fpt.edu.vn'
                    WHERE username = 'demo.lecturer2';
                    UPDATE users SET username = 'TuanBMGV24006', email = 'tuanbm.gv24006@fpt.edu.vn'
                    WHERE username = 'demo.lecturer3';
                    UPDATE users SET username = 'AnhTMSE195001', email = 'se195001@fpt.edu.vn'
                    WHERE username = 'demo.student1';
                    UPDATE users SET username = 'LongNHSE195002', email = 'se195002@fpt.edu.vn'
                    WHERE username = 'demo.student2';
                    UPDATE users SET username = 'PhuongLTSE195003', email = 'se195003@fpt.edu.vn'
                    WHERE username = 'demo.student3';
                    UPDATE users SET username = 'HuyPGSE195004', email = 'se195004@fpt.edu.vn'
                    WHERE username = 'demo.student4';
                    UPDATE users SET username = 'MaiVNSE195005', email = 'se195005@fpt.edu.vn'
                    WHERE username = 'demo.student5';
                    UPDATE users SET username = 'VietDQSE195006', email = 'se195006@fpt.edu.vn'
                    WHERE username = 'demo.student6';

                    UPDATE lecturers SET code = 'UNASSIGNED', full_name = 'Giảng viên chưa phân công'
                    WHERE code = 'unassigned';
                    UPDATE lecturers SET code = 'GV24004', full_name = 'Trần Quốc Hưng'
                    WHERE code = 'DEMO_2026_LEC01';
                    UPDATE lecturers SET code = 'GV24005', full_name = 'Võ Thị Lan'
                    WHERE code = 'DEMO_2026_LEC02';
                    UPDATE lecturers SET code = 'GV24006', full_name = 'Bùi Minh Tuấn'
                    WHERE code = 'DEMO_2026_LEC03';
                    UPDATE lecturers SET code = 'GV24007', full_name = 'Trần Minh Khôi'
                    WHERE code = 'LEC_GMAIL_001';
                    UPDATE lecturers SET full_name = 'Đỗ Anh Tuấn'
                    WHERE code = 'LEC_TEST';
                    UPDATE evaluation_panels ep SET full_name = l.full_name
                    FROM lecturers l WHERE ep.user_id = l.user_id;

                    UPDATE students SET full_name = 'Nguyễn Minh Khang' WHERE full_name = 'Test Student';
                    UPDATE students SET code = 'SE195001', full_name = 'Trần Minh Anh' WHERE code = 'DEMO_2026_SE001';
                    UPDATE students SET code = 'SE195002', full_name = 'Nguyễn Hoàng Long' WHERE code = 'DEMO_2026_SE002';
                    UPDATE students SET code = 'SE195003', full_name = 'Lê Thu Phương' WHERE code = 'DEMO_2026_SE003';
                    UPDATE students SET code = 'SE195004', full_name = 'Phạm Gia Huy' WHERE code = 'DEMO_2026_SE004';
                    UPDATE students SET code = 'SE195005', full_name = 'Võ Ngọc Mai' WHERE code = 'DEMO_2026_SE005';
                    UPDATE students SET code = 'SE195006', full_name = 'Đặng Quốc Việt' WHERE code = 'DEMO_2026_SE006';
                    UPDATE students SET code = 'SE190001', full_name = 'Lê Hải Yến' WHERE code = 'STU_GMAIL_001';

                    UPDATE topics SET code = 'SU26_CPMS_01',
                        name_vn = 'Hệ thống quản lý tiến độ đồ án tốt nghiệp',
                        name_en = 'Capstone Progress Management System'
                    WHERE code = 'DEMO_2026_TOPIC01';
                    UPDATE topics SET code = 'SU26_CPMS_02',
                        name_vn = 'Nền tảng quản lý lịch review và bảo vệ',
                        name_en = 'Review and Defense Scheduling Platform'
                    WHERE code = 'DEMO_2026_TOPIC02';
                    UPDATE capstone_groups SET code = 'SE2607' WHERE code = 'DEMO_2026_G01';
                    UPDATE capstone_groups SET code = 'SE2608' WHERE code = 'DEMO_2026_G02';

                    UPDATE system_administrators SET admin_level = 'Quản trị hệ thống', permission_scope = 'CPMS'
                    WHERE permission_scope = 'DEMO_2026';
                    UPDATE training_departments SET staff_code = 'TD_SU26', position = 'Cán bộ Phòng Đào tạo'
                    WHERE staff_code = 'DEMO_2026_TD';
                    UPDATE syllabuses SET name = 'SWD Capstone Syllabus 2026' WHERE name LIKE 'DEMO_2026%';
                    UPDATE review_sessions SET code = 'SU26_R1_IMPORTED01', room = 'R101' WHERE code = 'DEMO_2026_REV01';
                    UPDATE councils SET code = 'SU26_HD_IMPORTED01' WHERE code = 'DEMO_2026_COUNCIL01';
                    UPDATE defense_sessions SET code = 'SU26_BV1_CA01', room = 'A101' WHERE code = 'DEMO_2026_SESSION01';

                    SELECT id INTO canonical_defense_round_id FROM defense_rounds WHERE code = 'SU26_BV_DOT1' LIMIT 1;
                    SELECT id INTO source_defense_round_id FROM defense_rounds WHERE code = 'DEMO_2026_DEF01' LIMIT 1;
                    IF canonical_defense_round_id IS NULL AND source_defense_round_id IS NOT NULL THEN
                        RAISE EXCEPTION 'Cannot preserve demo defense session because SU26_BV_DOT1 is missing';
                    END IF;
                    IF source_defense_round_id IS NOT NULL THEN
                        UPDATE defense_sessions SET defense_round_id = canonical_defense_round_id
                        WHERE defense_round_id = source_defense_round_id;
                    END IF;
                    DELETE FROM defense_rounds
                    WHERE code = 'Def1-Su26' OR code LIKE 'REV_1_Review1_%' OR code LIKE 'DEMO_2026_%';

                    UPDATE documents SET file_name = 'SU26_final-report.docx', file_path = '/documents/su26/final-report.docx'
                    WHERE file_name LIKE 'DEMO_2026%' OR file_path LIKE '%DEMO_2026%';
                    UPDATE defense_evidences
                    SET file_name = 'SU26_defense-evidence.jpg', file_path = '/evidence/su26/defense-evidence.jpg',
                        note = 'Minh chứng buổi bảo vệ', user_agent = 'CPMS'
                    WHERE file_name LIKE 'DEMO_2026%' OR file_path LIKE '%DEMO_2026%';
                    UPDATE notifications
                    SET title = 'Báo cáo đánh giá đã hoàn tất', message = 'Báo cáo đánh giá đồ án đã sẵn sàng.'
                    WHERE title LIKE 'DEMO_2026%';
                END
                $cleanup$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible. Exact source rows are retained in
            // data_normalization_archives under clean-demo-academic-data-v1.
        }
    }
}
