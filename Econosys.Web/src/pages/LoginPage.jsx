import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

import bg from '../assets/login-bg.png';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('admin@econosys.com');
  const [password, setPassword] = useState('Admin@12345Secure!');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  const getRedirectTarget = () => {
    const fromLocation = location.state?.from;
    if (fromLocation?.pathname) {
      return `${fromLocation.pathname}${fromLocation.search || ''}`;
    }

    const storedRedirect = sessionStorage.getItem('postLoginRedirect');
    if (storedRedirect) {
      return storedRedirect;
    }

    return '/';
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setSuccess(false);
    setLoading(true);

    try {
      const result = await login(email, password);

      if (result.success) {
        setSuccess(true);
        setEmail('');
        setPassword('');
        const redirectTarget = getRedirectTarget();
        sessionStorage.removeItem('postLoginRedirect');
        navigate(redirectTarget, { replace: true });
      } else {
        setError(result.error);
      }
    } catch {
      setError('An unexpected error occurred. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="relative min-h-screen overflow-hidden text-stone-50">
      <img
        src={bg}
        alt=""
        aria-hidden="true"
        className="absolute inset-0 h-full w-full scale-105 object-cover object-center blur-xs"
      />
      <div className="absolute inset-0 bg-[#02040a]/70" />

      <div className="relative flex min-h-screen items-stretch px-3 py-4 sm:px-6 sm:py-6 lg:pl-[30rem] lg:py-8">
        <section className="flex w-full max-w-[500px] flex-col rounded-2xl border border-white/10 bg-[#141820]/95 p-6 shadow-[0_35px_80px_rgba(0,0,0,0.45)] backdrop-blur-sm sm:p-8">
          <header className="mb-10 flex items-center justify-between">
            <BrandMark />
          </header>

          <div className="flex flex-1 items-center pb-10 lg:pb-16">
            <div className="w-full space-y-6">
              <div>
                <h1 className="text-md font-light text-stone-200">Välkommen till Econosys</h1>
                <p className="mt-1 text-xs text-stone-400">Logga in för att fortsätta</p>
              </div>

              <div className="inline-flex w-50 max-w-[365px] rounded-full border border-white/10 bg-[#1a1f2a] p-1 text-xs">
                <button
                  type="button"
                  aria-pressed="true"
                  className="flex-1 rounded-full bg-lime-200 px-4 py-2 text-black shadow-sm"
                >
                  Login
                </button>
                <button
                  type="button"
                  disabled
                  aria-disabled="true"
                  className="flex-1 cursor-not-allowed rounded-full px-4 py-2 text-stone-400 opacity-80"
                >
                  Demo
                </button>
              </div>

              {error && (
                <div className="rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-100">
                  {error}
                </div>
              )}

              {success && (
                <div className="rounded-2xl border border-emerald-400/20 bg-emerald-400/10 px-4 py-3 text-sm text-emerald-100">
                  Inloggningen lyckades.
                </div>
              )}

              <form className="space-y-4 text-sm" onSubmit={handleSubmit}>
                <InputField
                  id="email"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="you@econopack.se"
                  icon={<UserIcon />}
                />
                <InputField
                  id="password"
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="••••••••"
                  icon={<KeyIcon />}
                />

                <div className="mb-6 flex justify-end pt-2">
                  <button
                    type="submit"
                    disabled={loading}
                    className="inline-flex items-center gap-x-2 rounded-none border border-transparent bg-teal-500 px-16 py-3 text-xs font-light text-white uppercase tracking-[0.1em] transition hover:bg-teal-600 focus:bg-teal-600 focus:outline-none disabled:pointer-events-none disabled:opacity-50"
                  >
                    {loading ? 'Loggar in ...' : 'Logga in'}
                  </button>
                </div>
              </form>
            </div>
          </div>

          <footer className="mb-10 mt-8 space-y-3 text-xs leading-2 text-stone-400">
            <p>Behöver du hjälp? Ring oss så hjälper vi dig.</p>
            <p>Genom att logga in godkänner du våra villkor och säkerhetspolicy.</p>
          </footer>
        </section>
      </div>
    </main>
  );
}

function InputField({ id, type, value, onChange, placeholder, icon }) {
  return (
    <div className="text-white">
      <div className="relative">
        <input
          id={id}
          type={type}
          value={value}
          onChange={onChange}
          placeholder={placeholder}
          required
          className="peer block w-full border-x-transparent border-b border-t-transparent bg-transparent py-2.5 pe-0 ps-8 text-sm text-stone-100 placeholder:text-stone-500 focus:border-x-transparent focus:border-b-teal-200 focus:border-t-transparent focus:outline-none focus:ring-0 sm:py-3"
        />
        <div className="pointer-events-none absolute inset-y-0 start-0 flex items-center ps-2 text-gray-500">
          {icon}
        </div>
      </div>
    </div>
  );
}

function BrandMark() {
  return (
    <div className="flex items-center gap-3 text-[#8eaba7]">
      <svg
        aria-hidden="true"
        className="h-8 w-8"
        viewBox="0 0 32 32"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <path d="M31.066 10.416 25.99 10.408l3.708-6.683L19.317 1.01l-3.13 4.269L14.573.396 3.577 3.694l1.969 5.049L.039 7.741 0 19.971l4.643 1.522-3.517 3.616 9.837 6.304 3.624-4.968 4.07 4.373 10.61-4.597-3.536-3.747 5.284.33.05-12.388Z" fill="currentColor" />
        <path d="m16.18 13.165 8.713-2.86-.135 9.87-4.622 1.957.172-7.025s-4.127-1.925-4.128-1.942Z" fill="#141820" />
      </svg>
      <span className="text-lg font-light tracking-[0.22em]">Econosys</span>
    </div>
  );
}

function UserIcon() {
  return (
    <svg className="size-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" />
      <circle cx="12" cy="7" r="4" />
    </svg>
  );
}

function KeyIcon() {
  return (
    <svg className="size-4" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M2 18v3c0 .6.4 1 1 1h4v-3h3v-3h2l1.4-1.4a6.5 6.5 0 1 0-4-4Z" />
      <circle cx="16.5" cy="7.5" r=".5" />
    </svg>
  );
}
