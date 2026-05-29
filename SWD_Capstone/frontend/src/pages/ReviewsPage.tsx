export function ReviewsPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Review scheduling</h2>
          <p>Reviewer assignments are checked server-side for supervisor conflict and duplicated rounds.</p>
        </div>
        <button className="primary">Schedule review</button>
      </div>
      <article className="panel table-panel">
        <table>
          <thead>
            <tr><th>Session</th><th>Round</th><th>Room</th><th>Reviewers</th><th>Rule check</th></tr>
          </thead>
          <tbody>
            <tr>
              <td colSpan={5} className="muted">No review sessions have been scheduled yet.</td>
            </tr>
          </tbody>
        </table>
      </article>
    </section>
  );
}
