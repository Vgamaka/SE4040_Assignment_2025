import { useState } from "react";
import { useNavigate } from "react-router-dom";
import StationForm from "../../../components/stations/StationForm";
import { createStation } from "../../../services/api";

export default function StationCreate() {
  const nav = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const handleCreate = async (payload) => {
    setSubmitting(true);
    setError("");
    try {
      const res = await createStation(payload);
      // Expect res to be StationResponse with id
      nav(`/backoffice/stations/${res.id}`); // to edit/schedule page (we'll add next)
    } catch (e) {
      console.error(e);
      const msg = e?.response?.data?.message || e?.message || "Failed to create station";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="p-4 max-w-3xl mx-auto">
      <div className="mb-4">
        <h1 className="text-2xl font-semibold">Create Station</h1>
        <p className="text-sm text-gray-600">
          Fill details and save. As a BackOffice, the station will be stamped to your account automatically by the API.
        </p>
      </div>

      {error && <div className="mb-3 text-sm text-red-600">{error}</div>}

      <div className="bg-white border rounded-xl shadow p-4">
        <StationForm onSubmit={handleCreate} submitting={submitting} />
      </div>
    </div>
  );
}

