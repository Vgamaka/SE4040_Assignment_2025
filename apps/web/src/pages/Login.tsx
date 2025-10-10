import { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function Login() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const handleLogin = (e: React.FormEvent) => {
    e.preventDefault();
    if (email && password) {
      navigate("/dashboard");
    } else {
      alert("Please enter email and password");
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-green-50">
      <div className="w-full max-w-md">
        <form
          onSubmit={handleLogin}
          className="bg-white p-10 rounded-2xl shadow-lg flex flex-col gap-6"
        >
          <h1 className="text-3xl font-extrabold text-center text-blue-600">
            Welcome Back
          </h1>

          <div className="flex flex-col gap-2">
            <label className="text-gray-600 font-medium">Email</label>
            <input
              type="email"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-transparent transition"
            />
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-gray-600 font-medium">Password</label>
            <input
              type="password"
              placeholder="••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-transparent transition"
            />
          </div>

          <button
            type="submit"
            className="w-full bg-gradient-to-r from-blue-500 to-green-500 text-white font-semibold py-3 rounded-xl hover:from-blue-600 hover:to-green-600 shadow-md transition"
          >
            Login
          </button>

          <p className="text-center text-gray-500 text-sm">
            Don’t have an account?{" "}
            <span className="text-blue-600 font-medium hover:underline cursor-pointer">
              Sign Up
            </span>
          </p>
        </form>
      </div>
    </div>
  );
}
