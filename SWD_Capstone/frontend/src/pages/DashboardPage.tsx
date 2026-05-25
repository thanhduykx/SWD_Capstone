const metrics = [
  { label: "Active groups", value: "128", note: "SP26 intake" },
  { label: "Review 1 passed", value: "96", note: "75.0%" },
  { label: "Defense sessions", value: "18", note: "6 live today" },
  { label: "Reports completed", value: "84", note: "CLO evaluated" },
];

const alerts = [
  "3 councils require conflict verification before assignment.",
  "Review 2 must not repeat reviewers assigned in Review 1.",
  "TEF export remains blocked until file format is confirmed.",
];

export function DashboardPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Operations overview</h2>
          <p>Central control for review rounds, defense panels and syllabus evidence.</p>
        </div>
        <button className="primary">Import group list</button>
      </div>
      <div className="metric-grid">
        {metrics.map((metric) => (
          <article className="metric" key={metric.label}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
            <small>{metric.note}</small>
          </article>
        ))}
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <h3>Required controls</h3>
          {alerts.map((alert) => (
            <p className="alert" key={alert}>{alert}</p>
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
