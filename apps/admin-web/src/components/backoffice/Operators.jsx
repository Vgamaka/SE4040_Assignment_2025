import { useEffect, useMemo, useState } from "react";
import {
  listBackOfficeOperators,
  createBackOfficeOperator,
  attachOperatorStations,
  listBackOfficeStations,
} from "../../services/api";

function Badge({ children }) {
  return (
    <span className="text-xs bg-blue-50 text-blue-700 border border-blue-200 px-2 py-0.5 rounded">
      {children}
    </span>
  );
}

function Modal({ open, onClose, title, children }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0 bg-black/30"
        onClick={onClose}
        aria-hidden="true"
      />
      <div className="relative bg-white w-full max-w-xl rounded-xl shadow-lg border">
        <div className="px-4 py-3 border-b flex items-center justify-between">
          <h3 className="font-semibold">{title}</h3>
          <button onClick={onClose} className="text-gray-500 hover:text-black">
            ✕
          </button>
        </div>
        <div className="p-4">{children}</div>
      </div>
    </div>
  );
}

function Paginator({ page, pageSize, total, onPage }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="flex items-center justify-between text-sm mt-2">
      <div>
        Page <b>{page}</b> of <b>{pages}</b> • Total <b>{total}</b>
      </div>
      <div className="flex gap-2">
        <button
          className="px-2 py-1 border rounded disabled:opacity-50"
          onClick={() => onPage(1)}
          disabled={page <= 1}
        >
          « First
        </button>
        <button
          className="px-2 py-1 border rounded disabled:opacity-50"
          onClick={() => onPage(page - 1)}
          disabled={page <= 1}
        >
          ‹ Prev
        </button>
        <button
          className="px-2 py-1 border rounded disabled:opacity-50"
          onClick={() => onPage(page + 1)}
          disabled={page >= pages}
        >
          Next ›
        </button>
        <button
          className="px-2 py-1 border rounded disabled:opacity-50"
          onClick={() => onPage(pages)}
          disabled={page >= pages}
        >
          Last »
        </button>
      </div>
    </div>
  );
}

export default function Operators() {
  // table state
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  // station cache for attach modal
  const [stations, setStations] = useState([]);
  const [stationsLoaded, setStationsLoaded] = useState(false);

  // modals
  const [createOpen, setCreateOpen] = useState(false);
  const [attachOpen, setAttachOpen] = useState(false);
  const [attachTarget, setAttachTarget] = useState(null);

  // create form
  const [cForm, setCForm] = useState({
    fullName: "",
    email: "",
    phone: "",
    password: "",
    stationIds: [],
  });
  const [submitting, setSubmitting] = useState(false);

  const load = async (p = page) => {
    setLoading(true);
    setErr("");
    try {
      const res = await listBackOfficeOperators(p, pageSize);
      setItems(res.items || []);
      setTotal(res.total ?? 0);
      setPage(res.page ?? p);
    } catch (e) {
      console.error(e);
      setErr(
        e?.response?.data?.message ||
          e?.response?.data?.error ||
          "Failed to load operators."
      );
    } finally {
      setLoading(false);
    }
  };

  const loadStations = async () => {
    if (stationsLoaded) return;
    try {
      const res = await listBackOfficeStations(1, 200);
      setStations(res.items || []);
      setStationsLoaded(true);
    } catch (e) {
      console.error(e);
    }
  };

  useEffect(() => {
    load(1);
  }, []);

  const onCreate = async () => {
    setSubmitting(true);
    try {
      const payload = {
        fullName: cForm.fullName,
        email: cForm.email,
        phone: cForm.phone || undefined,
        password: cForm.password,
        stationIds: cForm.stationIds.length ? cForm.stationIds : undefined,
      };
      await createBackOfficeOperator(payload);
      setCreateOpen(false);
      setCForm({ fullName: "", email: "", phone: "", password: "", stationIds: [] });
      await load(1);
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e?.response?.data?.error ||
          "Failed to create operator."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const openAttach = async (op) => {
    setAttachTarget(op);
    setAttachOpen(true);
    await loadStations();
  };

  const currentSelected = useMemo(() => {
    const ids = attachTarget?.operatorStationIds || [];
    return new Set(ids);
  }, [attachTarget]);

  const [attachSelection, setAttachSelection] = useState(new Set());
  useEffect(() => {
    setAttachSelection(new Set(currentSelected));
  }, [attachTarget]); // reset when opening for a different operator

  const toggleStation = (id) => {
    const next = new Set(attachSelection);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setAttachSelection(next);
  };

  const onAttachSave = async () => {
    if (!attachTarget) return;
    try {
      await attachOperatorStations(attachTarget.nic, Array.from(attachSelection));
      setAttachOpen(false);
      setAttachTarget(null);
      await load(page);
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e?.response?.data?.error ||
          "Failed to update station scope."
      );
    }
  };

  return (
    <div className="p-4 max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-2xl font-semibold">Manage Operators</h1>
          <p className="text-sm text-gray-600">
            Create operators and control which stations they can operate.
          </p>
        </div>
        <div className="flex gap-2">
          <button
            className="px-3 py-1.5 border rounded hover:bg-gray-50"
            onClick={() => load(page)}
            disabled={loading}
          >
            Refresh
          </button>
          <button
            className="px-3 py-1.5 bg-blue-600 text-white rounded hover:bg-blue-700"
            onClick={() => setCreateOpen(true)}
          >
            + New Operator
          </button>
        </div>
      </div>

      {err && (
        <div className="text-sm bg-yellow-50 border border-yellow-200 text-yellow-700 rounded p-2 mb-3">
          {err}
        </div>
      )}

      <div className="bg-white border rounded-xl shadow overflow-hidden">
        <div className="hidden md:grid grid-cols-12 gap-2 px-4 py-2 border-b text-xs text-gray-500">
          <div className="col-span-3">Name</div>
          <div className="col-span-3">Email</div>
          <div className="col-span-2">Phone</div>
          <div className="col-span-2">Roles</div>
          <div className="col-span-2 text-right">Actions</div>
        </div>

        {loading ? (
          <div className="p-4 text-sm text-gray-600">Loading…</div>
        ) : items.length === 0 ? (
          <div className="p-6 text-sm text-gray-600">No operators found.</div>
        ) : (
          <ul className="divide-y">
            {items.map((op) => (
              <li key={op.nic} className="px-4 py-3">
                <div className="grid md:grid-cols-12 gap-2">
                  <div className="md:col-span-3">
                    <div className="font-medium">{op.fullName}</div>
                    <div className="text-xs text-gray-500">NIC: {op.nic}</div>
                  </div>
                  <div className="md:col-span-3 text-sm">{op.email || "—"}</div>
                  <div className="md:col-span-2 text-sm">{op.phone || "—"}</div>
                  <div className="md:col-span-2 flex flex-wrap gap-1">
                    {(op.roles || []).map((r) => (
                      <Badge key={r}>{r}</Badge>
                    ))}
                  </div>
                  <div className="md:col-span-2 flex md:justify-end gap-2">
                    <button
                      className="px-2 py-1 border rounded text-sm hover:bg-gray-50"
                      onClick={() => openAttach(op)}
                    >
                      Attach Stations
                    </button>
                  </div>
                </div>

                {/* current stations */}
                <div className="mt-2 text-xs text-gray-600">
                  <span className="text-gray-500">Scoped Stations:</span>{" "}
                  {op.operatorStationIds?.length
                    ? op.operatorStationIds.length
                    : 0}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Paginator
        page={page}
        pageSize={pageSize}
        total={total}
        onPage={(p) => load(p)}
      />

      {/* Create Operator Modal */}
      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="Create Operator"
      >
        <div className="space-y-3">
          <div>
            <label className="text-sm">Full Name</label>
            <input
              className="w-full border rounded px-2 py-1.5"
              value={cForm.fullName}
              onChange={(e) =>
                setCForm((s) => ({ ...s, fullName: e.target.value }))
              }
              placeholder="e.g., Jane Perera"
            />
          </div>
          <div>
            <label className="text-sm">Email</label>
            <input
              className="w-full border rounded px-2 py-1.5"
              value={cForm.email}
              onChange={(e) => setCForm((s) => ({ ...s, email: e.target.value }))}
              placeholder="jane@example.com"
              type="email"
            />
          </div>
          <div>
            <label className="text-sm">Phone (optional)</label>
            <input
              className="w-full border rounded px-2 py-1.5"
              value={cForm.phone}
              onChange={(e) => setCForm((s) => ({ ...s, phone: e.target.value }))}
              placeholder="+94 71 234 5678"
            />
          </div>
          <div>
            <label className="text-sm">Password</label>
            <input
              className="w-full border rounded px-2 py-1.5"
              value={cForm.password}
              onChange={(e) =>
                setCForm((s) => ({ ...s, password: e.target.value }))
              }
              placeholder="Minimum 8 characters"
              type="password"
            />
          </div>

          <details className="rounded border p-3">
            <summary className="cursor-pointer text-sm font-medium">
              Assign stations now (optional)
            </summary>
            <div className="text-xs text-gray-500 mb-2">
              Select stations to scope this operator.
            </div>
            <div className="max-h-48 overflow-auto border rounded">
              {stationsLoaded ? (
                stations.length ? (
                  stations.map((s) => (
                    <label
                      key={s.id}
                      className="flex items-center gap-2 px-3 py-2 border-b last:border-b-0 text-sm"
                    >
                      <input
                        type="checkbox"
                        checked={cForm.stationIds.includes(s.id)}
                        onChange={(e) => {
                          const checked = e.target.checked;
                          setCForm((st) => {
                            const setIds = new Set(st.stationIds);
                            if (checked) setIds.add(s.id);
                            else setIds.delete(s.id);
                            return { ...st, stationIds: Array.from(setIds) };
                          });
                        }}
                      />
                      <span className="font-medium">{s.name}</span>
                      <span className="text-gray-500">({s.type})</span>
                    </label>
                  ))
                ) : (
                  <div className="p-3 text-sm text-gray-500">No stations.</div>
                )
              ) : (
                <div className="p-3 text-sm text-gray-500">Loading…</div>
              )}
            </div>
          </details>

          <div className="flex items-center justify-end gap-2">
            <button
              className="px-3 py-1.5 border rounded"
              onClick={() => setCreateOpen(false)}
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              className="px-3 py-1.5 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
              onClick={onCreate}
              disabled={
                submitting ||
                !cForm.fullName.trim() ||
                !cForm.email.trim() ||
                !cForm.password.trim()
              }
            >
              {submitting ? "Creating..." : "Create"}
            </button>
            {!stationsLoaded && (
              <button
                className="px-3 py-1.5 text-sm border rounded"
                onClick={loadStations}
              >
                Load Stations
              </button>
            )}
          </div>
        </div>
      </Modal>

      {/* Attach Stations Modal */}
      <Modal
        open={attachOpen}
        onClose={() => setAttachOpen(false)}
        title={
          attachTarget
            ? `Attach Stations — ${attachTarget.fullName}`
            : "Attach Stations"
        }
      >
        <div className="space-y-3">
          <div className="text-sm text-gray-600">
            Select which stations this operator can manage.
          </div>
          <div className="max-h-80 overflow-auto border rounded">
            {stationsLoaded ? (
              stations.length ? (
                stations.map((s) => (
                  <label
                    key={s.id}
                    className="flex items-center gap-2 px-3 py-2 border-b last:border-b-0 text-sm"
                  >
                    <input
                      type="checkbox"
                      checked={attachSelection.has(s.id)}
                      onChange={() => toggleStation(s.id)}
                    />
                    <span className="font-medium">{s.name}</span>
                    <span className="text-gray-500">({s.type})</span>
                  </label>
                ))
              ) : (
                <div className="p-3 text-sm text-gray-500">No stations.</div>
              )
            ) : (
              <div className="p-3 text-sm text-gray-500">Loading…</div>
            )}
          </div>

          <div className="flex items-center justify-end gap-2">
            <button
              className="px-3 py-1.5 border rounded"
              onClick={() => setAttachOpen(false)}
            >
              Cancel
            </button>
            <button
              className="px-3 py-1.5 bg-blue-600 text-white rounded hover:bg-blue-700"
              onClick={onAttachSave}
              disabled={!attachTarget}
            >
              Save
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
