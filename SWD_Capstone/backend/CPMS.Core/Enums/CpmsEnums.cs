namespace CPMS.Core.Enums;

public enum UserRole
{
    Student,
    Lecturer,
    EvaluationPanel,
    TrainingDepartment,
    SystemAdministrator
}

public enum GroupStatus { Active, Completed, Dropped }
public enum DocumentType { Proposal, Progress, Final }
public enum DocumentStatus { Submitted, Evaluating, NeedsRevision, Approved, Rejected }
public enum EvaluationTriggerType { Auto, Manual }
public enum ReviewType { Review1, Review2, Review3 }
public enum ReviewResult { Pass, Fail, Defense2, Drop }
public enum CouncilType { Defense1, Defense2 }
public enum CouncilStatus { Pending, Active, Closed }
public enum CouncilMemberRole { Member, Chairman, Secretary }
public enum ScoreType { BaoVe, Nguoi }
public enum FinalResult { Pending, Pass, Fail, Drop }
public enum CommentStatus { Open, Resolved }
public enum NotificationType { DocStatusChange, NewComment, ReportDone, ScoreLocked }
