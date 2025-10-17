// apps/admin-web/src/services/api.js
import axios from "axios";

/**
 * @typedef {Object} AuthLoginResponse
 * @property {string} accessToken
 * @property {string} tokenType
 * @property {string} expiresAtUtc
 * @property {string} nic
 * @property {string} fullName
 * @property {string[]} roles
 * @property {string} email
 * @property {string[]} operatorStationIds
 */

const api = axios.create({
  baseURL: "http://localhost:8085/api",
  timeout: 20000,
});

// -------------------- Auth storage helpers --------------------
export const getToken = () => localStorage.getItem("token");

export const setToken = (token) => {
  if (token) localStorage.setItem("token", token);
  else localStorage.removeItem("token");
};

export const setUser = (user) => {
  if (user) localStorage.setItem("user", JSON.stringify(user));
  else localStorage.removeItem("user");
};

export const getUser = () => {
  try {
    const raw = localStorage.getItem("user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
};

export const logout = () => {
  setToken(null);
  setUser(null);
  window.location.replace("/"); // hard redirect clears protected state
};

// -------------------- Axios interceptors --------------------
api.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (err) => {
    const status = err?.response?.status;
    if (status === 401) {
      logout();
    }
    return Promise.reject(err);
  }
);

// -------------------- Convenience API calls --------------------

// Auth: unified login (NIC or Email)
export async function login({ username, password }) {
  const { data } = await api.post("/Auth/login", { username, password });
  // persist
  setToken(data.accessToken);
  setUser(data);
  return /** @type {AuthLoginResponse} */ (data);
}

// BackOffice: apply (public)
export async function applyBackOffice(payload) {
  // payload: { fullName, email, phone?, password, businessName, contactEmail?, contactPhone? }
  const { data } = await api.post("/BackOffice/apply", payload);
  return data;
}

// BackOffice: profile (requires BackOffice role)
export async function getBackOfficeMe() {
  const { data } = await api.get("/BackOffice/me");
  return data;
}

export async function getBackOfficeStations({ page = 1, pageSize = 20 } = {}) {
  const res = await api.get("/BackOffice/stations", {
    params: { page, pageSize },
  });
  // shape: { total, items }
  return res.data;
}

// BackOffice: operators
export async function listBackOfficeOperators({ page = 1, pageSize = 20 } = {}) {
  const { data } = await api.get("/BackOffice/operators", {
    params: { page, pageSize },
  });
  return data; // { total, items }
}

export async function createOperator(payload) {
  // payload: { fullName, email, phone?, password, stationIds? }
  const { data } = await api.post("/BackOffice/operators", payload);
  return data;
}

export async function attachOperatorStations(operatorNic, stationIds) {
  const { data } = await api.put(`/BackOffice/operators/${encodeURIComponent(operatorNic)}/stations`, {
    stationIds,
  });
  return data;
}

// Stations (BackOffice-scoped list)
export async function listMyStations({ page = 1, pageSize = 20 } = {}) {
  const { data } = await api.get("/BackOffice/stations", {
    params: { page, pageSize },
  });
  return data; // { total, items }
}

// Station public list / nearby (for homepage)
export async function listStationsPublic({ type, status = "Active", minConnectors, page = 1, pageSize = 20 } = {}) {
  const { data } = await api.get("/Station", {
    params: { type, status, minConnectors, page, pageSize },
  });
  return data; // { total, items }
}

export async function nearbyStations({ lat, lng, radiusKm = 5, type } = {}) {
  const { data } = await api.get("/Station/nearby", {
    params: { lat, lng, radiusKm, type },
  });
  return data;
}

// Reports (BackOffice/Admin)
export async function getSummaryReport({ fromUtc, toUtc, stationId } = {}) {
  const { data } = await api.get("/Reports/summary", {
    params: { fromUtc, toUtc, stationId },
  });
  return data; // SummaryReportResponse
}

export async function getBookingTimeSeries({ metric = "created", stationId, fromUtc, toUtc, granularity = "day" } = {}) {
  const { data } = await api.get("/Reports/time-series/bookings", {
    params: { metric, stationId, fromUtc, toUtc, granularity },
  });
  return data;
}

export async function getRevenueTimeSeries({ stationId, fromUtc, toUtc, granularity = "day" } = {}) {
  const { data } = await api.get("/Reports/time-series/revenue", {
    params: { stationId, fromUtc, toUtc, granularity },
  });
  return data;
}

export async function getStationUtilization({ stationId, fromLocal, toLocal }) {
  const { data } = await api.get(`/Reports/stations/${encodeURIComponent(stationId)}/utilization`, {
    params: { fromLocal, toLocal },
  });
  return data;
}

export async function getRevenueByStation({ fromUtc, toUtc } = {}) {
  const { data } = await api.get("/Reports/revenue/by-station", {
    params: { fromUtc, toUtc },
  });
  return data;
}

export async function getOccupancyHeatmap({ stationId, fromUtc, toUtc }) {
  const { data } = await api.get(`/Reports/stations/${encodeURIComponent(stationId)}/occupancy-heatmap`, {
    params: { fromUtc, toUtc },
  });
  return data;
}

export default api;
