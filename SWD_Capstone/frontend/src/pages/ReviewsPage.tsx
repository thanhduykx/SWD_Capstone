const sessions = [
  { code: "2501", round: "Review 1", room: "P.204", reviewers: "MaiNT / KhoaPV", conflicts: "Clear" },
  { code: "3602", round: "Review 2", room: "P.301", reviewers: "LienHT / TuanNV", conflicts: "Clear" },
  { code: "4101", round: "Review 3", room: "Online", reviewers: "Supervisor", conflicts: "Verify match" },
];

export function ReviewsPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div><h2>Review scheduling</h2><p>Reviewer assignments are checked server-side for supervisor conflict and duplicated rounds.</p></div>
        <button className="primary">Schedule review</button>
      </div>
      <article className="panel table-panel">
        <table>
          <thead><tr><th>Session</th><th>Round</th><th>Room</th><th>Reviewers</th><th>Rule check</th></tr></thead>
          <tbody>
            {sessions.map((session) => (
              <tr key={session.code}><td>{session.code}</td><td>{session.round}</td><td>{session.room}</td><td>{session.reviewers}</td><td><span className="tag">{session.conflicts}</span></td></tr>
            ))}
          </tbody>
        </table>
      </article>
    </section>
  );
}
