import { useLanguage } from "../i18n/LanguageContext";

const importSteps = {
  vi: [
    "Tao hoac kich hoat hoc ky truoc khi import du lieu chinh thuc.",
    "Import danh sach giang vien va tai khoan hoi dong do Phong dao tao phe duyet.",
    "Import sinh vien, nhom capstone, de tai va GVHD tu sheet Projects.",
    "Kiem tra Review 1, Review 2 va Review 3 truoc khi cong bo.",
    "Lap hoi dong Defense 1 va Defense 2 sau khi co ket qua review.",
    "Chi export TEF sau khi chu tich hoi dong da khoa diem.",
  ],
  en: [
    "Create or activate the semester before importing official data.",
    "Import lecturer and council account lists approved by the Training Department.",
    "Import students, capstone groups, topics and supervisors from the Projects sheet.",
    "Validate Review 1, Review 2 and Review 3 schedule sheets before publishing.",
    "Create Defense 1 and Defense 2 councils only after review results are confirmed.",
    "Export TEF only after chairman-locked defense scores exist.",
  ],
};

const requiredControls = {
  vi: [
    "Tu choi dong Excel thieu ma nhom, ma de tai, ma GVHD hoac loi cong thuc.",
    "Chan conflict GVHD khi phan cong review va hoi dong.",
    "Tinh workload giang vien tu assignment trong database.",
    "Luu audit cho moi lan cap tai khoan/mat khau.",
  ],
  en: [
    "Reject Excel rows with missing group code, topic code, supervisor code or formula errors.",
    "Block supervisor conflict in review and defense council assignment.",
    "Track lecturer workload from stored assignments, not manual counters.",
    "Keep all account/password issuance auditable.",
  ],
};

export function TrainingDepartmentPage() {
  const { language, t } = useLanguage();

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>{t.trainingTitle}</h2>
          <p>{t.trainingSubtitle}</p>
        </div>
        <button className="primary">{t.importOfficialExcel}</button>
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <h3>{t.semesterOperationFlow}</h3>
          <div className="flow vertical-flow">
            {importSteps[language].map((step) => <span key={step}>{step}</span>)}
          </div>
        </article>
        <article className="panel">
          <h3>{t.importValidationControls}</h3>
          {requiredControls[language].map((control) => <p className="alert" key={control}>{control}</p>)}
        </article>
      </div>
      <article className="panel table-panel">
        <div className="panel-header">
          <h3>{t.importBatches}</h3>
          <button className="secondary">{t.viewHistory}</button>
        </div>
        <table>
          <thead><tr><th>{t.batch}</th><th>{t.type}</th><th>{t.status}</th><th>{t.validatedRows}</th><th>{t.errors}</th></tr></thead>
          <tbody>
            <tr><td colSpan={5} className="muted">{t.noImportBatches}</td></tr>
          </tbody>
        </table>
      </article>
    </section>
  );
}
