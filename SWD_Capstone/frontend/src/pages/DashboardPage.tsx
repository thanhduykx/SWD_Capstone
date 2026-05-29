const controls = [
  "Import official group, student, supervisor and council lists before operating a semester.",
  "Run server-side conflict checks before assigning reviewers or defense councils.",
  "Open defense scoring only after the chairman starts the session.",
  "Keep score history and audit logs immutable for accountability.",
];

export function DashboardPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Operations overview</h2>
          <p>Central control for review rounds, defense panels and syllabus evidence.</p>
        </div>
        <button className="primary">Import official data</button>
      </div>
      <div className="metric-grid">
        <article className="metric">
          <span>Groups</span>
          <strong>-</strong>
          <small>No official import yet</small>
        </article>
        <article className="metric">
          <span>Review sessions</span>
          <strong>-</strong>
          <small>No schedule created yet</small>
        </article>
        <article className="metric">
          <span>Defense sessions</span>
          <strong>-</strong>
          <small>No council session started yet</small>
        </article>
        <article className="metric">
          <span>Documents</span>
          <strong>-</strong>
          <small>No submission uploaded yet</small>
        </article>
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <h3>Required controls</h3>
          {controls.map((control) => (
            <p className="alert" key={control}>{control}</p>
          ))}
        </article>
        <article className="panel">
          <h3>Evaluation workflow</h3>
          <div className="flow">
            <span>Review 1</span><span>Review 2</span><span>Review 3</span>
            <span>Defense 1</span><span>Defense 2</span><span>Final</span>
          </div>
        </article>
      </div>
    </section>
  );
}
