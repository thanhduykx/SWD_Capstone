export function SemestersPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Semester and capstone groups</h2>
          <p>Manage topics, supervisors and student membership by term.</p>
        </div>
        <button className="primary">Create semester</button>
      </div>
      <article className="panel table-panel">
        <div className="panel-header">
          <h3>Official groups</h3>
          <button className="secondary">Import Excel</button>
        </div>
        <table>
          <thead>
            <tr><th>Group code</th><th>Topic</th><th>Supervisor</th><th>Status</th></tr>
          </thead>
          <tbody>
            <tr>
              <td colSpan={4} className="muted">No official group data has been imported yet.</td>
            </tr>
          </tbody>
        </table>
      </article>
    </section>
  );
}
