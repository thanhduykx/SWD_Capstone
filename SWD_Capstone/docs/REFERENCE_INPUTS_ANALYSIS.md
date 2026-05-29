# Reference Files Analysis

This document records the business structure observed from the user-provided reference files.
It is guidance for implementation only. The application must not seed or import this data automatically.

## Excel Review And Defense Workbooks

Reference files:

- `SE_CapstoneProject_FA24_Review&Defense2.xlsx`
- `SE_CapstoneProject_SU24_Review&Defense.xlsx`

Observed sheets:

- `Projects`: official capstone group/topic data, supervisors, group codes, review assignments and conflict result columns.
- `Review 1`, `Review2`, `Review3`: review session schedule with code, day code, slot code, group code, date, weekday, room and reviewers.
- `Defense 1`, `Defense 2`: council setup with council code, chairman, secretary, members, member list and number of assigned topics.
- `Summary`: lecturer workload by review and defense activities.
- `Dashboard`: aggregate evaluation indicators.

Implementation implications:

- Excel import must be explicit and controlled by Training Department/admin users.
- Import validation must reject malformed schedules, missing group codes, missing reviewers and spreadsheet formula errors such as `#REF!`.
- Review assignment must preserve reviewer history so Review 2/Review 3 can be checked against earlier rounds.
- Defense council membership must distinguish chairman, secretary and normal members.
- Workload reporting should be derived from stored assignments, not hardcoded UI counters.

## TEF Archive

Reference file:

- `_File tef_HD204.rar`

Observed archive structure:

- Files are grouped by capstone group.
- Each group has separate scoring categories: `ChamBaoVe` and `ChamNguoi`.
- Each scoring category contains one `.tef` file per lecturer/student scoring artifact.

Implementation implications:

- TEF export should be generated only after real defense scores exist.
- Export should keep category separation between defense score and individual score.
- Export naming should include lecturer code, course/assessment name, student code and assessment identifiers.
- The system should store export history so every generated TEF file can be traced to group, student, scorer, score type and generation time.
