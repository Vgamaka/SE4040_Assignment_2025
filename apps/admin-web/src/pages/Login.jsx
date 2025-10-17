import { useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import { login } from "../services/api";

export default function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  const [form, setForm] = useState({ username: "", password: "" });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    if (!form.username.trim() || !form.password) {
      setError("Please enter username and password.");
      return;
    }

    setLoading(true);
    try {
      const res = await login({
        username: form.username.trim(), // NIC or email
        password: form.password,
      });

      // Persist
      localStorage.setItem("token", res.accessToken);
      localStorage.setItem("user", JSON.stringify(res));

      // Simple role-based navigation (thin client)
      const role = (res.roles && res.roles[0]) || "";
      if (role === "Admin" || role === "SuperAdmin") {
        navigate("/dashboard/admin");
      } else if (role === "BackOffice") {
        navigate("/dashboard/backoffice");
      } else if (role === "Operator") {
        navigate("/dashboard/operator");
      } else {
        // Owner or unknown -> generic landing
        const next = new URLSearchParams(location.search).get("next");
        navigate(next || "/");
      }
    } catch (err) {
      console.error(err);
      const msg =
        err?.response?.data?.message ||
        err?.response?.data?.error ||
        "Login failed. Check your credentials.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="w-full max-w-sm bg-white border rounded-2xl shadow p-6">
        <div className="mb-4 text-center">
          <Link to="/" className="text-xl font-bold">EV Charge</Link>
          <p className="text-xs text-gray-500 mt-1">Sign in to continue</p>
        </div>

        {error && (
          <div className="mb-3 text-sm bg-red-50 border border-red-200 text-red-700 rounded p-2">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-3">
          <div>
            <label className="block text-xs text-gray-600 mb-1">
              Username (NIC or Email)
            </label>
            <input
              name="username"
              value={form.username}
              onChange={onChange}
              placeholder="e.g. 200123456789 or jane@acme.com"
              className="w-full border rounded px-3 py-2"
              autoComplete="username"
              required
            />
          </div>

          <div>
            <label className="block text-xs text-gray-600 mb-1">Password</label>
            <input
              name="password"
              type="password"
              value={form.password}
              onChange={onChange}
              placeholder="••••••••"
              className="w-full border rounded px-3 py-2"
              autoComplete="current-password"
              required
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full rounded-md bg-blue-600 text-white py-2 text-sm hover:bg-blue-700 disabled:opacity-50"
          >
            {loading ? "Logging in…" : "Login"}
          </button>
        </form>

        <div className="mt-4 flex items-center justify-between text-sm">
          <Link to="/apply-backoffice" className="text-blue-700 hover:underline">
            Apply as BackOffice
          </Link>
          <Link to="/" className="text-gray-700 hover:text-black">
            Home
          </Link>
        </div>
      </div>
    </div>
  );
}
