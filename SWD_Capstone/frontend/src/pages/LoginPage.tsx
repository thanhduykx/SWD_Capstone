import { useCallback, useEffect, useRef, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { AxiosError } from "axios";
import { getHomePathForRole, useAuth } from "../auth/AuthContext";
import { useLanguage } from "../i18n/LanguageContext";
import type { FormEvent } from "react";

export function LoginPage() {
  const { isAuthenticated, role, signIn, signInWithGoogle } = useAuth();
  const { language, setLanguage, t } = useLanguage();
  const navigate = useNavigate();
  const [loginCode, setLoginCode] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const googleButtonRef = useRef<HTMLDivElement | null>(null);
  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;

  const handleGoogleCredential = useCallback(async (idToken: string) => {
    setError(null);
    try {
      setIsSubmitting(true);
      const nextRole = await signInWithGoogle(idToken);
      navigate(getHomePathForRole(nextRole), { replace: true });
    } catch {
      setError(t.googleFailed);
    } finally {
      setIsSubmitting(false);
    }
  }, [navigate, signInWithGoogle, t.googleFailed]);

  useEffect(() => {
    if (!googleClientId || !googleButtonRef.current) {
      return;
    }

    let isDisposed = false;
    const renderGoogleButton = () => {
      if (isDisposed || !window.google || !googleButtonRef.current) {
        return;
      }

      window.google.accounts.id.initialize({
        client_id: googleClientId,
        callback: (response) => {
          if (response.credential) {
            void handleGoogleCredential(response.credential);
          }
        },
      });
      googleButtonRef.current.innerHTML = "";
      window.google.accounts.id.renderButton(googleButtonRef.current, {
        theme: "outline",
        size: "large",
        width: 328,
        text: "signin_with",
      });
    };

    if (window.google) {
      renderGoogleButton();
      return () => {
        isDisposed = true;
      };
    }

    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = renderGoogleButton;
    document.head.appendChild(script);

    return () => {
      isDisposed = true;
    };
  }, [googleClientId, handleGoogleCredential, language]);

  if (isAuthenticated) {
    return <Navigate to={getHomePathForRole(role)} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (!loginCode.trim() || !password) {
      setError(t.loginRequired);
      return;
    }

    try {
      setIsSubmitting(true);
      const nextRole = await signIn(loginCode, password);
      navigate(getHomePathForRole(nextRole), { replace: true });
    } catch (exception) {
      if (exception instanceof AxiosError && exception.response?.status === 401) {
        setError(t.loginUnauthorized);
        return;
      }

      setError(t.loginFailed);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="login-shell">
      <form className="login-card" onSubmit={handleSubmit}>
        <div className="login-heading">
          <p className="eyebrow">FPT UNIVERSITY</p>
          <div className="language-toggle" aria-label={t.language}>
            <button className={language === "vi" ? "active" : ""} type="button" onClick={() => setLanguage("vi")}>VN</button>
            <button className={language === "en" ? "active" : ""} type="button" onClick={() => setLanguage("en")}>EN</button>
          </div>
        </div>
        <h1>{t.loginTitle}</h1>
        <div className="google-login-block">
          {googleClientId ? (
            <div ref={googleButtonRef} />
          ) : (
            <button className="secondary" type="button" disabled>{t.googleSignIn}</button>
          )}
          {!googleClientId && <small>{t.googleNotConfigured}</small>}
        </div>
        <div className="login-divider"><span>{t.or}</span></div>
        <label>
          {t.loginCode}
          <input
            autoComplete="username"
            type="text"
            placeholder={t.loginCodePlaceholder}
            value={loginCode}
            onChange={(event) => setLoginCode(event.target.value)}
          />
        </label>
        <label>
          {t.password}
          <input
            autoComplete="current-password"
            type="password"
            placeholder={t.passwordPlaceholder}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        {error && <p className="form-error">{error}</p>}
        <button className="primary" type="submit" disabled={isSubmitting}>
          {isSubmitting ? t.signingIn : t.signIn}
        </button>
        <small>{t.loginLockNote}</small>
      </form>
    </div>
  );
}
