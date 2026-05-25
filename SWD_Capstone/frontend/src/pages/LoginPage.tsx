export function LoginPage() {
  return (
    <div className="login-shell">
      <form className="login-card">
        <p className="eyebrow">FPT UNIVERSITY</p>
        <h1>Sign in to CPMS</h1>
        <label>FPT username<input type="text" placeholder="PhuongLHK" /></label>
        <label>Password<input type="password" placeholder="Password" /></label>
        <button className="primary" type="submit">Sign in</button>
        <small>Accounts lock for 15 minutes after 5 failed attempts.</small>
      </form>
    </div>
  );
}
