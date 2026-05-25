export function DocumentsPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div><h2>Documents and CLO evaluation</h2><p>Versioned submissions mapped to syllabus evidence and panel feedback.</p></div>
        <button className="primary">Upload document</button>
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <h3>Final_Report_v3.docx</h3>
          <p className="muted">SP26SE017 | Final | Evaluating | Version 3</p>
          <div className="progress"><span style={{ width: "72%" }} /></div>
          <small>72% CLO evidence match - manual panel review pending</small>
        </article>
        <article className="panel">
          <h3>CLO coverage</h3>
          <p className="alert success">CLO1 Requirements analysis - covered</p>
          <p className="alert warning">CLO2 Architecture - needs evidence</p>
          <p className="alert success">CLO3 Testing - covered</p>
        </article>
      </div>
    </section>
  );
}
