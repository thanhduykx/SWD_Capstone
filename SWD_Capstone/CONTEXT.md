# Capstone Review Scheduling

This context coordinates availability, assignment, publication, and completion of capstone review rounds.

## Language

**Review Round**:
A Monday-to-Saturday registration and scheduling period for one review type in one semester.
_Avoid_: Event, campaign

**Student Availability**:
Exactly five distinct review slots selected by a capstone group's leader for a review round; it is a preference set, not a reservation.
_Avoid_: Student booking, registered session

**Lecturer Availability**:
The Monday-to-Saturday review slots a lecturer submits as times they can review during a week.
_Avoid_: Lecturer assignment

**Review Session**:
A published or draft panel assignment for one reviewer pair, room, date, and slot, containing at most three capstone groups in an explicit order.
_Avoid_: Student availability, registration

**Review Session Access Code**:
A session-scoped secret generated or rotated by the Training Department; an assigned lecturer must verify it before entering that review session's working area.
_Avoid_: Room code, invitation code, lecturer password

**Publication**:
The moderator action that makes draft review sessions visible and sends schedule notifications.
_Avoid_: Assignment

**Review Completion**:
The final state of a group's participation in a review session after attendance and reviewer feedback have been recorded.
_Avoid_: Submission
