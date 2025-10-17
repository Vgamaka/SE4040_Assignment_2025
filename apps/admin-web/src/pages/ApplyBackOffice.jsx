import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { applyBackOffice } from "../services/api";

export default function ApplyBackOffice() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    fullName: "",
    email: "",
    phone: "",
    password: "",
    businessName: "",
    contactEmail: "",
    contactPhone: "",
  });
  const [showPwd, setShowPwd] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setResult(null);

    // tiny client-side sanity checks (server is source of truth)
    if (!form.fullName.trim() || form.fullName.trim().length < 2) {
      setError("Please enter a valid full name.");
      return;
    }
    if (!/^\S+@\S+\.\S+$/.test(form.email)) {
      setError("Please enter a valid email.");
      return;
    }
    if (form.password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    if (!form.businessName.trim()) {
      setError("Please enter your business name.");
      return;
    }

    setLoading(true);
    try {
      const res = await applyBackOffice({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phone: form.phone?.trim() || undefined,
        password: form.password,
        businessName: form.businessName.trim(),
        contactEmail: form.contactEmail?.trim() || undefined,
        contactPhone: form.contactPhone?.trim() || undefined,
      });
      setResult(res); // OwnerResponse
      // optional: redirect after a beat
      // setTimeout(() => navigate("/login"), 1200);
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e?.response?.data?.error ||
        e?.message ||
        "Application failed";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b">
        <div className="max-w-3xl mx-auto px-4 py-3 flex items-center justify-between">
          <Link to="/" className="text-lg font-bold">EV Charge</Link>
          <nav className="flex items-center gap-3">
            <Link to="/" className="text-sm text-gray-700 hover:text-black">Home</Link>
            <Link to="/login" className="text-sm text-blue-700 hover:underline">Login</Link>
          </nav>
        </div>
      </header>

      <main className="max-w-3xl mx-auto px-4 py-8">
        <h1 className="text-2xl font-semibold mb-1">Apply as BackOffice</h1>
        <p className="text-sm text-gray-600 mb-6">
          Create a BackOffice account to manage your charging stations and operators.
        </p>

        {result ? (
          <div className="bg-green-50 border border-green-200 text-green-800 rounded-xl p-4">
            <div className="font-medium">Application submitted!</div>
            <p className="text-sm mt-1">
              Your account (<span className="font-mono">{result?.nic}</span>) has been created with role(s):{" "}
              <b>{result?.roles?.join(", ") || "BackOffice"}</b>. An admin may review your application.
            </p>
            <div className="mt-4 flex gap-2">
              <button
                className="px-3 py-2 rounded-md bg-blue-600 text-white text-sm hover:bg-blue-700"
                onClick={() => navigate("/login")}
              >
                Go to Login
              </button>
              <Link to="/" className="px-3 py-2 rounded-md border text-sm hover:bg-gray-100">
                Back to Home
              </Link>
            </div>
          </div>
        ) : (
          <form
            onSubmit={handleSubmit}
            className="bg-white border rounded-xl p-4 space-y-4"
          >
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded p-2">
                {error}
              </div>
            )}

            {/* Personal */}
            <div>
              <div className="font-medium mb-2">Personal</div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-gray-600 mb-1">Full name *</label>
                  <input
                    name="fullName"
                    value={form.fullName}
                    onChange={onChange}
                    required
                    className="w-full border rounded px-3 py-2"
                    placeholder="Jane Doe"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-600 mb-1">Email *</label>
                  <input
                    name="email"
                    type="email"
                    value={form.email}
                    onChange={onChange}
                    required
                    className="w-full border rounded px-3 py-2"
                    placeholder="jane@example.com"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-600 mb-1">Phone</label>
                  <input
                    name="phone"
                    value={form.phone}
                    onChange={onChange}
                    className="w-full border rounded px-3 py-2"
                    placeholder="+94 7X XXX XXXX"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-600 mb-1">Password *</label>
                  <div className="flex">
                    <input
                      name="password"
                      type={showPwd ? "text" : "password"}
                      value={form.password}
                      onChange={onChange}
                      required
                      className="w-full border rounded-l px-3 py-2"
                      placeholder="Min 8 characters"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPwd((s) => !s)}
                      className="px-3 border border-l-0 rounded-r text-sm hover:bg-gray-50"
                    >
                      {showPwd ? "Hide" : "Show"}
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {/* Business */}
            <div>
              <div className="font-medium mb-2">Business</div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div className="sm:col-span-2">
                  <label className="block text-xs text-gray-600 mb-1">Business name *</label>
                  <input
                    name="businessName"
                    value={form.businessName}
                    onChange={onChange}
                    required
                    className="w-full border rounded px-3 py-2"
                    placeholder="ACME EV Ops (Pvt) Ltd"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-600 mb-1">Contact email</label>
                  <input
                    name="contactEmail"
                    type="email"
                    value={form.contactEmail}
                    onChange={onChange}
                    className="w-full border rounded px-3 py-2"
                    placeholder="ops@acme.example"
                  />
                </div>
                <div>
                  <label className="block text-xs text-gray-600 mb-1">Contact phone</label>
                  <input
                    name="contactPhone"
                    value={form.contactPhone}
                    onChange={onChange}
                    className="w-full border rounded px-3 py-2"
                    placeholder="+94 1X XXX XXXX"
                  />
                </div>
              </div>
            </div>

            <div className="pt-2 flex items-center gap-3">
              <button
                type="submit"
                disabled={loading}
                className="px-4 py-2 rounded-md bg-blue-600 text-white text-sm hover:bg-blue-700 disabled:opacity-50"
              >
                {loading ? "Submitting…" : "Submit application"}
              </button>
              <Link to="/" className="text-sm text-gray-700 hover:text-black">
                Cancel
              </Link>
            </div>
          </form>
        )}
      </main>
    </div>
  );
}
