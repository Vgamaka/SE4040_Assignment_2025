import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Login() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [asOwner, setAsOwner] = useState(false); // for testing owner login if you want
  const [err, setErr] = useState("");
  const { login } = useAuth();
  const nav = useNavigate();

  const submit = async (e) => {
    e.preventDefault();
    try {
      await login(username, password, asOwner);
      nav("/");
    } catch (e) {
      setErr(e?.response?.data?.message || "Login failed");
    }
  };

  return (
    <div className="max-w-sm mx-auto mt-16 bg-white p-6 rounded-xl shadow">
      <h1 className="text-xl font-semibold mb-4">Admin Sign in</h1>
      <form onSubmit={submit} className="space-y-3">
        <input className="w-full border p-2 rounded" placeholder="Username (NIC for Owner)"
          value={username} onChange={e=>setUsername(e.target.value)} />
        <input className="w-full border p-2 rounded" placeholder="Password" type="password"
          value={password} onChange={e=>setPassword(e.target.value)} />
        <label className="text-sm flex items-center gap-2">
          <input type="checkbox" checked={asOwner} onChange={e=>setAsOwner(e.target.checked)} />
          Login as EV Owner (for testing)
        </label>
        {err && <div className="text-sm text-red-600">{err}</div>}
        <button className="bg-black text-white px-4 py-2 rounded w-full">Login</button>
      </form>
    </div>
  );
}
