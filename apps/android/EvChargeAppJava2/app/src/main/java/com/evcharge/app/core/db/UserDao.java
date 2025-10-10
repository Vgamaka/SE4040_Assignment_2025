package com.evcharge.app.core.db;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;

import static com.evcharge.app.core.db.AppDbHelper.*;

public final class UserDao {

    private final AppDbHelper helper;

    public UserDao(Context ctx) {
        this.helper = new AppDbHelper(ctx.getApplicationContext());
    }

    // --- DTO/POJO kept tiny on purpose ---
    public static final class UserRecord {
        public String idKey;          // PK
        public String fullName;
        public String phone;
        public String role;           // "Owner" | "Operator"
        public String status;         // "Active" | "Deactivated" | null
        public String lastLoginUtc;   // ISO-8601 string
    }

    public boolean insertOrReplace(UserRecord r) {
        if (r == null || r.idKey == null || r.idKey.trim().isEmpty()) return false;
        SQLiteDatabase db = helper.getWritableDatabase();
        ContentValues cv = new ContentValues();
        cv.put(C_ID_KEY, r.idKey);
        cv.put(C_FULL_NAME, r.fullName);
        cv.put(C_PHONE, r.phone);
        cv.put(C_ROLE, r.role);
        cv.put(C_STATUS, r.status);
        cv.put(C_LAST_LOGIN_UTC, r.lastLoginUtc);
        long rowId = db.insertWithOnConflict(T_USERS, null, cv, SQLiteDatabase.CONFLICT_REPLACE);
        return rowId != -1;
    }

    public UserRecord getByIdKey(String idKey) {
        if (idKey == null || idKey.trim().isEmpty()) return null;
        SQLiteDatabase db = helper.getReadableDatabase();
        Cursor c = null;
        try {
            c = db.query(T_USERS,
                    new String[]{C_ID_KEY, C_FULL_NAME, C_PHONE, C_ROLE, C_STATUS, C_LAST_LOGIN_UTC},
                    C_ID_KEY + "=?",
                    new String[]{idKey},
                    null, null, null);
            if (c.moveToFirst()) {
                UserRecord r = new UserRecord();
                r.idKey = c.getString(0);
                r.fullName = c.getString(1);
                r.phone = c.getString(2);
                r.role = c.getString(3);
                r.status = c.getString(4);
                r.lastLoginUtc = c.getString(5);
                return r;
            }
            return null;
        } finally {
            if (c != null) c.close();
        }
    }

    public int deleteByIdKey(String idKey) {
        if (idKey == null || idKey.trim().isEmpty()) return 0;
        SQLiteDatabase db = helper.getWritableDatabase();
        return db.delete(T_USERS, C_ID_KEY + "=?", new String[]{idKey});
    }

    public int clearAll() {
        SQLiteDatabase db = helper.getWritableDatabase();
        return db.delete(T_USERS, null, null);
    }
}
