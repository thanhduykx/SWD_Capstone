export function DocumentsPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Documents and CLO evaluation</h2>
          <p>Versioned submissions mapped to syllabus evidence and panel feedback.</p>
        </div>
        <button className="primary">Upload document</button>
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <h3>Submissions</h3>
          <p className="muted">No capstone document has been uploaded yet.</p>
          <div className="progress"><span style={{ width: "0%" }} /></div>
          <small>CLO matching will be calculated after a real submission is uploaded.</small>
        </article>
        <article className="panel">
          <h3>CLO coverage</h3>
          <p className="muted">No syllabus evidence is available yet.</p>
        </article>
      </div>
    </section>
  );
}
