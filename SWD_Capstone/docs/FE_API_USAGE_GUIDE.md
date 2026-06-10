# FE API Usage Guide

Tài liệu này dành cho FE khi dùng các API đang có trong Swagger của CPMS. Mục tiêu là gọi đúng endpoint, đúng role, đúng payload, không phải đoán từ Swagger.

## 1. Môi trường và convention FE

### URL

| Mục đích | URL |
| --- | --- |
| FE dev | `http://localhost:5173` |
| Backend API trực tiếp | `http://localhost:5122` |
| Swagger | `http://localhost:5122/swagger` |
| Health check | `http://localhost:5122/health/database` |

Trong code FE không gọi full URL backend. Dùng `apiClient` hiện có:

```ts
import { apiClient } from "../api/client";

const response = await apiClient.get("/accounts");
```

`apiClient` đang dùng `baseURL: "/api"`. Vite proxy sẽ forward:

| FE gọi | Proxy sang |
| --- | --- |
| `/api/*` | `http://localhost:5122/api/*` |
| `/hubs/*` | `http://localhost:5122/hubs/*` |

### Auth header

`apiClient` tự đọc `sessionStorage.getItem("cpms_access_token")` và gắn:

```http
Authorization: Bearer <accessToken>
```

FE chỉ cần lưu token sau login:

```ts
sessionStorage.setItem("cpms_access_token", response.data.accessToken);
sessionStorage.setItem("cpms_refresh_token", response.data.refreshToken);
```

### Format dữ liệu

| Loại | Format FE gửi |
| --- | --- |
| `DateOnly` | `yyyy-MM-dd`, ví dụ `2026-06-15` |
| `DateTime` | ISO string, ví dụ `2026-06-15T00:00:00Z` |
| Enum | string, ví dụ `"Lecturer"`, `"Review1"`, `"Published"` |
| Boolean | `true` / `false` |
| Upload file | `FormData`, không set cứng `Content-Type: application/json` |

Với lịch rảnh review, FE phải tách rõ hai hành động: `PUT /api/review-availability/week` chỉ lưu nháp; `POST /api/review-availability/week/submit` mới gửi lịch cho Moderator. Moderator chỉ thấy slot đã submit.

Backend trả JSON camelCase. Request body nên dùng camelCase để thống nhất với FE.

## 2. Role sử dụng trong FE

Role thực tế FE cần xử lý:

| Role API | Tên hiển thị FE nên dùng | Ghi chú |
| --- | --- | --- |
| `TrainingDepartment` | Moderator | Role vận hành chính: tạo account, quản lý lịch review, defense |
| `Lecturer` | Lecturer | Giảng viên, không bắt buộc có bộ môn khi tạo account |
| `Student` | Student | Sinh viên |

`SystemAdministrator` vẫn còn trong backend để tương thích dữ liệu cũ. FE không nên đưa role này vào flow chính. Nếu cần chức năng admin cũ thì gộp UI vào Moderator.

## 3. Tài khoản test local

| Role | Username | Password |
| --- | --- | --- |
| Moderator | `test.training` | `Test@123456` |
| Lecturer | `test.lecturer` | `Test@123456` |
| Lecturer không bộ môn | `test.lecturer.nodept` | `Test@123456` |
| Student | `test.student` | `Test@123456` |

API hỗ trợ xem tài khoản test local:

```ts
await apiClient.get("/test-support/test-accounts");
```

Endpoint `test-support` chỉ dùng local/dev. Không xây UI production phụ thuộc vào nó.

## 4. Auth APIs

### POST `/api/auth/login`

Dùng cho login username/password.

Role: anonymous.

Request:

```json
{
  "username": "test.training",
  "examinerCode": null,
  "password": "Test@123456"
}
```

Response:

```json
{
  "accessToken": "jwt...",
  "refreshToken": "refresh...",
  "refreshTokenExpiresAt": "2026-06-17T10:00:00Z"
}
```

FE xử lý:

```ts
const response = await apiClient.post("/auth/login", {
  username,
  examinerCode: null,
  password,
});

sessionStorage.setItem("cpms_access_token", response.data.accessToken);
sessionStorage.setItem("cpms_refresh_token", response.data.refreshToken);
```

Lỗi cần xử lý:

| Status | Nghĩa |
| --- | --- |
| `400` | Thiếu `username`/`examinerCode` |
| `401` | Sai credentials, account inactive, bị lock |
| `429` | Login quá nhiều lần |

### POST `/api/auth/refresh`

Dùng để đổi refresh token lấy access token mới.

Role: anonymous.

Request:

```json
{
  "refreshToken": "refresh..."
}
```

Response giống login.

Lưu ý: backend rotate refresh token. FE phải ghi đè cả access token và refresh token mới.

### POST `/api/auth/google`

Dùng cho Google login nếu backend đã cấu hình Google ClientId.

Role: anonymous.

Request:

```json
{
  "idToken": "google-id-token"
}
```

Response giống login.

Lỗi cần xử lý:

| Status | Nghĩa |
| --- | --- |
| `503` | Backend chưa cấu hình Google |
| `401` | Token Google invalid hoặc email chưa verify/chưa có user trong hệ thống |

### POST `/api/auth/bootstrap-admin`

Chỉ dùng Development khi database chưa có user nào. FE bình thường không cần gọi.

## 5. Accounts APIs

Nhóm này dành cho Moderator.

### Quy tắc tạo username

FE không cho Moderator nhập username.

Backend tự sinh username từ `fullName` và `identityCode`:

```text
<Tên cuối đã bỏ dấu và viết hoa chữ đầu><Chữ cái đầu các phần tên trước><Mã số viết hoa>
```

Ví dụ:

```text
Dương Thành Thanh Duy + SE194673 = DuyDTTSE194673
```

Sau khi tạo account thành công, FE phải hiển thị popup cho Moderator:

- username được sinh ra
- email nhận account đã gửi được chưa
- password ban đầu nếu FE muốn nhắc Moderator xác nhận

Email gửi cho user được backend xử lý sau khi tạo account. Nếu email fail, account vẫn đã được tạo.

### GET `/api/accounts`

Role: `TrainingDepartment`, `SystemAdministrator` legacy.

Lấy danh sách account.

```ts
const response = await apiClient.get("/accounts");
```

Response item:

```json
{
  "id": 10,
  "username": "DuyDTTSE194673",
  "email": "duy@example.com",
  "role": "Lecturer",
  "isActive": true,
  "lastLoginAt": null,
  "lockedUntil": null
}
```

### POST `/api/accounts`

Role: `TrainingDepartment`, `SystemAdministrator` legacy.

Request chung:

```json
{
  "username": null,
  "identityCode": "SE194673",
  "email": "duy@example.com",
  "password": "Test@123456",
  "role": "Lecturer",
  "fullName": "Dương Thành Thanh Duy",
  "department": null,
  "position": null,
  "permissionScope": null,
  "isPartTime": false,
  "classCode": null,
  "batch": null,
  "major": null
}
```

Response:

```json
{
  "id": 25,
  "username": "DuyDTTSE194673",
  "email": "duy@example.com",
  "role": "Lecturer",
  "isActive": true,
  "lastLoginAt": null,
  "lockedUntil": null,
  "identityCode": "SE194673",
  "emailDeliveryStatus": "Sent",
  "emailDeliveryError": null
}
```

`emailDeliveryStatus`:

| Value | FE hiển thị |
| --- | --- |
| `Sent` | Đã gửi email cho user |
| `Skipped` | SMTP chưa cấu hình, account vẫn đã tạo |
| `Failed` | Gửi email lỗi, account vẫn đã tạo |

Field bắt buộc theo role:

| Role tạo | Bắt buộc | Không bắt buộc |
| --- | --- | --- |
| `Lecturer` | `identityCode`, `email`, `password`, `fullName` | `department`, `isPartTime` |
| `TrainingDepartment` | `identityCode`, `email`, `password`, `fullName` | `department`, `position` |
| `Student` | `identityCode`, `email`, `password`, `fullName`, `classCode` | `batch`, `major` |

Ví dụ tạo Lecturer không bộ môn:

```ts
const response = await apiClient.post("/accounts", {
  username: null,
  identityCode: "SE194673",
  email: "duy@example.com",
  password: "Test@123456",
  role: "Lecturer",
  fullName: "Dương Thành Thanh Duy",
  department: null,
  position: null,
  permissionScope: null,
  isPartTime: false,
  classCode: null,
  batch: null,
  major: null,
});

showCreatedAccountPopup(response.data.username, response.data.emailDeliveryStatus);
```

Lỗi cần xử lý:

| Status | Nghĩa |
| --- | --- |
| `400` | Thiếu field bắt buộc, password dưới 6 ký tự |
| `409` | Username sinh ra hoặc email đã tồn tại |
| `403` | User login không phải Moderator |

### PATCH `/api/accounts/{userId}/status`

Role: `TrainingDepartment`, `SystemAdministrator` legacy.

Dùng để khóa/mở account hoặc clear lock.

Request:

```json
{
  "isActive": false,
  "unlock": false
}
```

Mở lại account và xóa lock:

```json
{
  "isActive": true,
  "unlock": true
}
```

## 6. Semesters APIs

### GET `/api/semesters`

Role: authenticated.

Lấy tất cả semester, backend tự đảm bảo có semester cho ngày hiện tại.

```ts
const response = await apiClient.get("/semesters");
```

### GET `/api/semesters/resolve?date=yyyy-MM-dd`

Role: authenticated.

Dùng để lấy semester theo một ngày cụ thể. FE nên gọi API này trước các flow theo tuần.

```ts
const response = await apiClient.get("/semesters/resolve", {
  params: { date: "2026-06-15" },
});
```

### POST `/api/semesters`

Role: Moderator.

Request:

```json
{
  "code": "SU26",
  "name": "Summer 2026",
  "academicYear": "2025-2026",
  "startDate": "2026-05-01",
  "endDate": "2026-08-31",
  "isActive": true
}
```

Nếu `isActive = true`, backend sẽ tắt active của semester cũ.

## 7. Review Availability APIs

Nhóm này dành cho Lecturer tự đăng ký lịch rảnh review.

### GET `/api/review-availability/week`

Role: `Lecturer`.

Query:

| Query | Ví dụ | Ghi chú |
| --- | --- | --- |
| `semesterId` | `1` | Hiện backend vẫn resolve semester theo `weekStart`; truyền id lấy từ `/semesters/resolve` để đồng bộ FE |
| `weekStart` | `2026-06-15` | Backend normalize về thứ Hai của tuần |

```ts
const response = await apiClient.get("/review-availability/week", {
  params: {
    semesterId: 1,
    weekStart: "2026-06-15",
  },
});
```

Response:

```json
{
  "semesterId": 1,
  "lecturerId": 3,
  "weekStart": "2026-06-15",
  "isSubmitted": true,
  "submittedAt": "2026-06-10T10:00:00Z",
  "slots": [
    { "dayOfWeek": 1, "slot": 1 },
    { "dayOfWeek": 3, "slot": 2 }
  ]
}
```

### PUT `/api/review-availability/week`

Role: `Lecturer`.

Query giống GET. Body là toàn bộ slot muốn lưu cho tuần đó, không phải patch từng slot. Đây là lưu nháp; nếu lịch đã submit trước đó thì save lại sẽ đưa tuần đó về trạng thái chưa submit để bắt giảng viên xác nhận lại.

```json
{
  "slots": [
    { "dayOfWeek": 1, "slot": 1 },
    { "dayOfWeek": 3, "slot": 2 }
  ]
}
```

Quy tắc:

| Field | Rule |
| --- | --- |
| `dayOfWeek` | Monday=1, Sunday=7 |
| `slot` | 1 đến 8 |

### POST `/api/review-availability/week/submit`

Role: `Lecturer`.

Submit lịch rảnh đã lưu cho Moderator. Phải có ít nhất 1 slot.

```ts
const response = await apiClient.post("/review-availability/week/submit", null, {
  params: {
    semesterId: 1,
    weekStart: "2026-06-15",
  },
});
```

Sau API này, Moderator mới thấy slot trong board và random assign mới được dùng các slot đó.

## 8. Review Scheduling APIs

Nhóm này dành cho Moderator.

### GET `/api/review-scheduling/board`

Role: Moderator.

Lấy data cho màn hình xếp lịch review: lecturers, submitted availability, availability submission status, groups, sessions.

```ts
const response = await apiClient.get("/review-scheduling/board", {
  params: {
    semesterId: 1,
    reviewType: "Review1",
    weekStart: "2026-06-15",
  },
});
```

Response chính:

```json
{
  "semesterId": 1,
  "reviewType": "Review1",
  "weekStart": "2026-06-15",
  "lecturers": [],
  "availability": [],
  "availabilitySubmissions": [],
  "groups": [],
  "sessions": []
}
```

FE lấy id thật từ response:

| ID | Lấy từ |
| --- | --- |
| `lecturerId` | `lecturers[].id` |
| `groupId` | `groups[].id` |
| `sessionId` | `sessions[].id` |

Không hard-code các id này.

### POST `/api/review-scheduling/random-assign`

Role: Moderator.

Random xếp các nhóm active chưa có session của review round vào các slot giảng viên đã submit. Backend tự kiểm tra:

| Rule | Ý nghĩa |
| --- | --- |
| Chỉ dùng submitted availability | Slot save nháp không được xếp |
| Không cho GVHD tự review nhóm mình | Dựa trên `capstoneGroups.lecturerId` |
| Không trùng lịch reviewer | Một reviewer không bị xếp 2 nhóm cùng ngày/slot |
| Review2 không lặp reviewer Review1 | Backend lấy reviewer Review1 của cùng group |
| Chỉ xếp group active đã có sinh viên và chưa có session round đó | Group chưa có team/student hoặc đã có Review1/2/3 session tương ứng sẽ bỏ qua |

Request:

```json
{
  "semesterId": 1,
  "reviewType": "Review1",
  "weekStart": "2026-06-15",
  "reviewersPerSession": 2,
  "roomPrefix": "AUTO",
  "seed": null
}
```

Response:

```json
{
  "totalCandidateGroups": 12,
  "assignedCount": 10,
  "unassignedGroups": [
    {
      "groupId": 5,
      "groupCode": "G05",
      "reason": "No submitted available reviewer slot satisfies supervisor, previous reviewer, and conflict rules."
    }
  ],
  "sessions": []
}
```

FE sau khi gọi xong nên reload `GET /api/review-scheduling/board`.

### POST `/api/review-sessions`

Role: Moderator.

Tạo một review session.

```json
{
  "code": "RV1-G01-20260615-S1",
  "groupId": 1,
  "groupPosition": 1,
  "type": "Review1",
  "reviewer1Id": 3,
  "reviewer2Id": 4,
  "previousReviewerIds": [],
  "slot": 1,
  "room": "B101",
  "sessionDate": "2026-06-15T00:00:00Z"
}
```

Backend sẽ tạo checklist submission cho từng reviewer.

### POST `/api/review-sessions/bulk-assign`

Role: Moderator.

Dùng cho màn hình xếp nhiều lịch một lần.

```json
{
  "sessions": [
    {
      "code": "RV1-G01-20260615-S1",
      "groupId": 1,
      "groupPosition": 1,
      "type": "Review1",
      "reviewerIds": [3, 4],
      "previousReviewerIds": [],
      "slot": 1,
      "room": "B101",
      "sessionDate": "2026-06-15T00:00:00Z"
    }
  ]
}
```

Rules backend kiểm tra:

| Rule | Ý nghĩa FE cần biết |
| --- | --- |
| Ít nhất 1 reviewer | `reviewerIds` không được rỗng |
| Reviewer phải tồn tại | lấy từ board API |
| Không được trùng slot reviewer | cùng ngày + slot không gán cùng lecturer nhiều session |
| Supervisor không được tự review nhóm của mình | backend chặn |
| Review2 cần `previousReviewerIds` | dùng để tránh sai quy tắc phân công |
| Reviewer phải submit availability đúng ngày/slot | backend chặn cả manual assign và random assign |

### PATCH `/api/review-sessions/{sessionId}`

Role: Moderator.

Cập nhật session đã tạo.

```json
{
  "code": "RV1-G01-20260615-S2",
  "reviewerIds": [3, 5],
  "previousReviewerIds": [],
  "slot": 2,
  "room": "B102",
  "sessionDate": "2026-06-15T00:00:00Z",
  "status": "Draft"
}
```

`status` hợp lệ: `Draft`, `Published`, `Cancelled`.

### GET `/api/review-sessions/my`

Role: `Lecturer`.

Lấy danh sách review session được phân công cho user hiện tại.

```ts
const response = await apiClient.get("/review-sessions/my");
```

Response item:

```json
{
  "sessionId": 10,
  "submissionId": 20,
  "code": "RV1-G01-20260615-S1",
  "type": "Review1",
  "sessionStatus": "Published",
  "groupCode": "G01",
  "sessionDate": "2026-06-15T00:00:00Z",
  "slot": 1,
  "room": "B101",
  "submissionStatus": "Draft",
  "lastSavedAt": "2026-06-10T10:00:00Z"
}
```

FE dùng `submissionId` để mở checklist.

## 9. Review Publish API

### POST `/api/review-schedules/publish`

Role: Moderator.

Publish lịch review theo semester, review type và tuần. Backend gửi email cho reviewer nếu SMTP cấu hình.

```json
{
  "semesterId": 1,
  "reviewType": "Review1",
  "weekStart": "2026-06-15",
  "subject": "CPMS review schedule - Review1",
  "message": "Please check your assigned review sessions."
}
```

Response:

```json
{
  "publicationId": 5,
  "publishedSessionCount": 8,
  "sentEmailCount": 8,
  "failedEmailCount": 0
}
```

Nếu chưa có session trong tuần, backend trả `400`.

## 10. Review Submission APIs

### GET `/api/review-submissions/{submissionId}`

Role:

| Role | Điều kiện |
| --- | --- |
| Moderator | Xem được |
| Lecturer | Chỉ reviewer được assign |
| Student | Chỉ submission thuộc group của student và session đã `Published` |

```ts
const response = await apiClient.get(`/review-submissions/${submissionId}`);
```

Response gồm metadata và danh sách checklist item:

```json
{
  "id": 20,
  "sessionId": 10,
  "groupId": 1,
  "groupCode": "G01",
  "projectName": "Capstone Project",
  "type": "Review1",
  "status": "Draft",
  "sessionStatus": "Published",
  "reviewerId": 3,
  "reviewerCode": "GV001",
  "reviewerName": "Nguyen Van A",
  "items": [
    {
      "itemKey": "R1.1",
      "label": "Problem definition",
      "description": null,
      "priority": "High",
      "isSection": false,
      "criteriaCode": "C1",
      "answer": null,
      "comment": null
    }
  ]
}
```

### PUT `/api/review-submissions/{submissionId}/draft`

Role: assigned `Lecturer`.

Lưu nháp checklist.

```json
{
  "workProductVersion": "v1.0",
  "workProductSize": "20 pages",
  "effortHours": 12.5,
  "reviewerComment": "Good progress.",
  "suggestion": "Improve testing evidence.",
  "resultText": "Pass with minor revisions",
  "items": [
    {
      "itemKey": "R1.1",
      "answer": "Yes",
      "comment": "Clear enough"
    },
    {
      "itemKey": "R1.2",
      "answer": "No",
      "comment": "Missing evidence"
    }
  ]
}
```

`answer` hợp lệ: `Yes`, `No`, `NotApplicable`, hoặc `null`.

FE nên render checklist từ `GET` response, sau đó gửi lại `itemKey` đúng từ backend. Không tự chế item key.

### POST `/api/review-submissions/{submissionId}/submit`

Role: assigned `Lecturer`.

Submit checklist. Body rỗng.

```ts
await apiClient.post(`/review-submissions/${submissionId}/submit`);
```

### GET `/api/review-submissions/{submissionId}/export.xlsx`

Role: user có quyền view submission.

Download Excel cho một submission/session.

```ts
const response = await apiClient.get(`/review-submissions/${submissionId}/export.xlsx`, {
  responseType: "blob",
});
```

### GET `/api/review-submissions/export.zip`

Role: Moderator.

Download toàn bộ checklist theo semester và review type.

```ts
const response = await apiClient.get("/review-submissions/export.zip", {
  params: {
    semesterId: 1,
    reviewType: "Review1",
  },
  responseType: "blob",
});
```

## 11. Defense Management APIs

Nhóm này dành cho Moderator để tạo đợt bảo vệ, hội đồng và phân công project.

### GET `/api/defense-management/rounds`

Role: Moderator.

```ts
const response = await apiClient.get("/defense-management/rounds");
```

### POST `/api/defense-management/rounds`

Role: Moderator.

```json
{
  "code": "DEF1-SU26",
  "name": "Defense 1 Summer 2026",
  "semesterId": 1,
  "type": "Defense1",
  "startDate": "2026-07-01",
  "endDate": "2026-07-07"
}
```

`type`: `Defense1` hoặc `Defense2`.

### POST `/api/defense-management/boards`

Role: Moderator.

Tạo hội đồng.

```json
{
  "code": "BOARD-01",
  "semesterId": 1,
  "type": "Defense1",
  "chairmanId": 3,
  "secretaryId": 4,
  "memberLecturerIds": [5]
}
```

ID lecturer lấy từ review scheduling board hoặc data lecturer trong DB/API liên quan.

Response:

```json
{
  "id": 7,
  "code": "BOARD-01",
  "semesterId": 1,
  "type": "Defense1",
  "status": "Pending",
  "chairmanId": 3,
  "secretaryId": 4,
  "memberLecturerIds": [3, 4, 5]
}
```

### POST `/api/defense-management/boards/{councilId}/members`

Role: Moderator.

Thêm member vào hội đồng.

```json
{
  "lecturerId": 6,
  "role": "Member"
}
```

`role`: `Member`, `Chairman`, `Secretary`.

Success trả `204 No Content`.

### POST `/api/defense-management/sessions`

Role: Moderator.

Gán project/group vào hội đồng.

```json
{
  "code": "DEF-G01-01",
  "defenseRoundId": 2,
  "councilId": 7,
  "groupId": 1,
  "sessionDate": "2026-07-01T00:00:00Z",
  "slot": 1,
  "room": "B201"
}
```

Rules backend kiểm tra:

| Rule | Ý nghĩa |
| --- | --- |
| Round, board, group cùng semester | Không được mix dữ liệu khác kỳ |
| Board type khớp round type | `Defense1` đi với `Defense1` |
| Session date nằm trong round | Không tạo ngoài ngày bắt đầu/kết thúc |

### GET `/api/defense-management/my-board-sessions`

Role: `Lecturer`.

Lấy defense sessions mà user hiện tại là member của hội đồng.

```ts
const response = await apiClient.get("/defense-management/my-board-sessions");
```

## 12. Defense Session APIs

Nhóm này dành cho hội đồng bảo vệ.

### GET `/api/defense-sessions/resolve/{code}`

Role: `Lecturer`.

Resolve session bằng session id, session code hoặc council code.

```ts
const response = await apiClient.get(`/defense-sessions/resolve/${encodeURIComponent(code)}`);
```

Response:

```json
{
  "sessionId": 12,
  "sessionCode": "DEF-G01-01",
  "defenseRoundId": 2,
  "councilId": 7,
  "councilCode": "BOARD-01",
  "groupId": 1,
  "sessionDate": "2026-07-01T00:00:00Z",
  "slot": 1,
  "room": "B201",
  "startedAt": null,
  "endedAt": null,
  "isLocked": false,
  "isChairman": true
}
```

FE dùng `isChairman` để bật/tắt nút Start/Close.

### POST `/api/defense-sessions/{sessionId}/start`

Role: hội đồng được assign.

Chỉ chairman nên thao tác trên UI. Backend service sẽ kiểm tra rule.

```ts
await apiClient.post(`/defense-sessions/${sessionId}/start`);
```

### POST `/api/defense-sessions/{sessionId}/scores`

Role: hội đồng được assign.

```json
{
  "studentId": 10,
  "scoreType": "BaoVe",
  "scoreValue": 8.5
}
```

`scoreType`: `BaoVe`, `Nguoi`.

FE cần lấy `studentId` từ dữ liệu group/student tương ứng. Không dùng `userId` thay cho `studentId`.

### GET `/api/defense-sessions/{sessionId}/evidences`

Role: hội đồng được assign.

Lấy danh sách ảnh minh chứng.

```ts
const response = await apiClient.get(`/defense-sessions/${sessionId}/evidences`);
```

### POST `/api/defense-sessions/{sessionId}/evidences`

Role: hội đồng được assign.

Upload ảnh minh chứng. API nhận multipart form, giới hạn khoảng 5 MB, chỉ nhận image.

```ts
const formData = new FormData();
formData.append("file", file);
formData.append("note", note);

const response = await apiClient.post(`/defense-sessions/${sessionId}/evidences`, formData, {
  headers: { "Content-Type": "multipart/form-data" },
});
```

Response:

```json
{
  "id": 3,
  "defenseSessionId": 12,
  "capturedByLecturerId": 4,
  "fileName": "20260610101010111_xxx.jpg",
  "filePath": "/evidence/12/20260610101010111_xxx.jpg",
  "contentType": "image/jpeg",
  "fileSize": 120000,
  "note": "Board evidence",
  "capturedAt": "2026-06-10T10:10:10Z"
}
```

Để render ảnh:

```ts
const imageUrl = evidence.filePath;
```

Khi chạy qua Vite proxy, static file path `/evidence/...` có thể cần gọi trực tiếp backend nếu proxy chưa khai báo path này.

### POST `/api/defense-sessions/{sessionId}/close`

Role: hội đồng được assign.

Chỉ chairman nên thao tác trên UI.

```ts
await apiClient.post(`/defense-sessions/${sessionId}/close`);
```

## 13. Defense realtime SignalR

Hub không nằm trong Swagger nhưng FE cần dùng cho màn hình chấm defense realtime.

URL:

```text
/hubs/defense
```

Client hiện có:

```ts
import { createDefenseConnection, joinDefenseSession } from "../api/defenseRealtime";

const connection = createDefenseConnection();
await joinDefenseSession(connection, sessionId);
```

Token được truyền qua `accessTokenFactory`, backend đọc query `access_token` cho hub.

Events FE nên lắng nghe:

| Event | Khi nào bắn |
| --- | --- |
| `defenseSessionState` | Server gửi state hiện tại cho caller sau khi join session |
| `defenseSessionStarted` | Chairman start session |
| `scoreSubmitted` | Member submit score |
| `memberJoined` | Có thành viên hội đồng join session |
| `defenseEvidenceCaptured` | Có ảnh evidence mới |
| `defenseSessionClosed` | Chairman close session |

Ví dụ:

```ts
connection.on("scoreSubmitted", (event) => {
  // update local scoreboard
});
```

## 14. Test-support APIs

### GET `/api/test-support/swagger-guide`

Role: anonymous, local/dev only.

Trả hướng dẫn ngắn để test trong Swagger.

### GET `/api/test-support/test-accounts`

Role: anonymous, local/dev only.

Trả danh sách tài khoản test local.

Không dùng hai endpoint này cho production UI.

## 15. Health API

### GET `/health/database`

Không nằm dưới `/api`.

Dùng để kiểm tra backend có kết nối được PostgreSQL không.

```ts
const response = await fetch("/health/database");
```

Response healthy:

```json
{
  "status": "Healthy",
  "database": "PostgreSQL"
}
```

## 16. Flow FE nên triển khai

### Flow login

1. Gọi `POST /api/auth/login`.
2. Lưu `accessToken`, `refreshToken`.
3. Decode JWT hoặc dùng response/user state hiện có để route theo role.
4. Nếu role là `TrainingDepartment`, đưa về màn Moderator.
5. Nếu API trả `401`, clear token và hiển thị lỗi login.

### Flow tạo account

1. Moderator mở màn account.
2. Gọi `GET /api/accounts`.
3. Moderator chọn role cần tạo.
4. FE hiển thị field theo role.
5. FE không hiển thị input username.
6. Gọi `POST /api/accounts`.
7. Hiển thị popup với `username` backend trả về.
8. Hiển thị trạng thái email theo `emailDeliveryStatus`.
9. Refresh danh sách account.

### Flow Lecturer đăng ký lịch rảnh

1. Lecturer chọn tuần.
2. FE gọi `GET /api/semesters/resolve?date=<weekStart>`.
3. FE gọi `GET /api/review-availability/week`.
4. Lecturer tick slot.
5. FE gọi `PUT /api/review-availability/week` với toàn bộ slot đã chọn.
6. Lecturer bấm submit, FE gọi `POST /api/review-availability/week/submit`.

### Flow Moderator xếp lịch review

1. Moderator chọn semester, review type, tuần.
2. Gọi `GET /api/review-scheduling/board`.
3. Render groups, lecturers, submitted availability, availability submission status, sessions.
4. Khi assign, dùng `groupId` và `lecturerId` từ board response.
5. Gọi `POST /api/review-scheduling/random-assign` để tự xếp, hoặc `POST /api/review-sessions/bulk-assign` để xếp thủ công.
6. Gọi lại board để refresh.
7. Khi chốt, gọi `POST /api/review-schedules/publish`.

### Flow Reviewer làm checklist

1. Lecturer gọi `GET /api/review-sessions/my`.
2. Chọn session, lấy `submissionId`.
3. Gọi `GET /api/review-submissions/{submissionId}`.
4. Render checklist theo `items`.
5. Save nháp bằng `PUT /draft`.
6. Submit bằng `POST /submit`.
7. Download Excel bằng `GET /export.xlsx` nếu cần.

### Flow Defense

1. Moderator tạo round bằng `POST /defense-management/rounds`.
2. Moderator tạo board bằng `POST /defense-management/boards`.
3. Moderator gán group vào board bằng `POST /defense-management/sessions`.
4. Lecturer gọi `GET /defense-management/my-board-sessions` hoặc resolve bằng code.
5. FE join SignalR `/hubs/defense`.
6. Chairman gọi `POST /start`.
7. Members gọi `POST /scores`.
8. Members upload evidence nếu cần.
9. Chairman gọi `POST /close`.

## 17. Xử lý lỗi thống nhất trên FE

Backend thường trả lỗi dạng:

```json
{
  "error": "Message..."
}
```

FE nên có helper:

```ts
function getApiErrorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.error ?? error.message;
  }

  return "Unexpected error";
}
```

Mapping status:

| Status | FE xử lý |
| --- | --- |
| `400` | Hiển thị validation/business message từ `error` |
| `401` | Token thiếu/hết hạn hoặc login sai; thử refresh hoặc logout |
| `403` | Sai role hoặc không được phân công |
| `404` | Không tìm thấy dữ liệu |
| `409` | Trùng dữ liệu, ví dụ email/username |
| `429` | Rate limit auth, yêu cầu user thử lại sau |
| `500` | Lỗi backend ngoài dự kiến |

## 18. Những lỗi FE không nên lặp lại

- Không hard-code `semesterId`, `lecturerId`, `groupId`, `sessionId`, `submissionId`.
- Không để Moderator nhập username khi tạo account.
- Không hiện `SystemAdministrator` như một role chính trong UI.
- Không bắt buộc `department` khi tạo Lecturer.
- Không gửi enum dạng số. Gửi string.
- Không gửi ngày dạng locale như `10/06/2026`.
- Không dùng `userId` thay cho `lecturerId` hoặc `studentId`.
- Không tự tạo checklist item key ở FE.
- Không gọi upload evidence bằng JSON; phải dùng `FormData`.
- Không phụ thuộc `test-support` endpoints trong production.
