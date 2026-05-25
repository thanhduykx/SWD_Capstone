const groups = [
  { code: "SP26SE001", topic: "Evidence Traceability Platform", supervisor: "PhuongLHK", status: "Active" },
  { code: "SP26SE017", topic: "Automated CLO Mapping", supervisor: "NhanDT", status: "Active" },
  { code: "SP26SE026", topic: "Defense Analytics Portal", supervisor: "ThaoNQ", status: "Completed" },
];

export function SemestersPage() {
  return (
    <section className="page">
      <div className="page-title">
        <div><h2>Semester and capstone groups</h2><p>Manage topics, supervisors and student membership by term.</p></div>
        <button className="primary">Create semester</button>
      </div>
      <article className="panel table-panel">
        <div className="panel-header"><h3>SP26 groups</h3><button className="secondary">Import Excel</button></div>
        <table>
          <thead><tr><th>Group code</th><th>Topic</th><th>Supervisor</th><th>Status</th></tr></thead>
          <tbody>
            {groups.map((group) => (
              <tr key={group.code}><td>{group.code}</td><td>{group.topic}</td><td>{group.supervisor}</td><td><span className="tag">{group.status}</span></td></tr>
            ))}
          </tbody>
        </table>
      </article>
    </section>
  );
}
