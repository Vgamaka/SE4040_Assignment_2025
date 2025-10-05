import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Login() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [asOwner, setAsOwner] = useState(false);
  const [err, setErr] = useState("");
  const { login } = useAuth();
  const nav = useNavigate();

  const submit = async (e) => {
    e.preventDefault();
    try {
      const userData = await login(username, password, asOwner);
      // Navigate based on role
      if (userData?.role === "StationOperator") {
        nav("/station-dashboard");
      } else {
        nav("/");
      }
    } catch (e) {
      setErr(e?.response?.data?.message || "Login failed");
    }
  };

  return (
    <div className="max-w-sm mx-auto mt-16 bg-white p-6 rounded-xl shadow">
      <h1 className="text-xl font-semibold mb-4">Admin Sign in</h1>
      <form onSubmit={submit} className="space-y-3">
        <input 
          className="w-full border p-2 rounded" 
          placeholder="Username (NIC for Owner)"
          value={username} 
          onChange={e=>setUsername(e.target.value)} 
        />
        <input 
          className="w-full border p-2 rounded" 
          placeholder="Password" 
          type="password"
          value={password} 
          onChange={e=>setPassword(e.target.value)} 
        />
        {err && <div className="text-sm text-red-600">{err}</div>}
        <button className="bg-black text-white px-4 py-2 rounded w-full">
          Login
        </button>
      </form>
    </div>
  );
}