package com.evcharge.app.core.db;

import android.content.Context;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;

/**
 * Minimal local DB used ONLY for user snapshot caching.
 * Business logic remains on the server.
 */
public final class AppDbHelper extends SQLiteOpenHelper {

    public static final String DB_NAME = "evcharge.db";
    public static final int DB_VERSION = 1;

    // Table + columns
    public static final String T_USERS = "users";
    public static final String C_ID_KEY = "id_key";           // PK: Owner=NIC, Operator=Email
    public static final String C_FULL_NAME = "full_name";
    public static final String C_PHONE = "phone";
    public static final String C_ROLE = "role";               // "Owner" | "Operator"
    public static final String C_STATUS = "status";           // "Active" | "Deactivated" | null
    public static final String C_LAST_LOGIN_UTC = "last_login_utc";

    private static final String SQL_CREATE_USERS =
            "CREATE TABLE IF NOT EXISTS " + T_USERS + " (" +
                    C_ID_KEY + " TEXT PRIMARY KEY, " +
                    C_FULL_NAME + " TEXT, " +
                    C_PHONE + " TEXT, " +
                    C_ROLE + " TEXT, " +
                    C_STATUS + " TEXT, " +
                    C_LAST_LOGIN_UTC + " TEXT" +
            ");";

    public AppDbHelper(Context ctx) {
        super(ctx, DB_NAME, null, DB_VERSION);
    }

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL(SQL_CREATE_USERS);
    }

    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        // v1 → future: either ALTER TABLEs or simple rebuild.
        // For assignment simplicity, rebuild if schema changes.
        db.execSQL("DROP TABLE IF EXISTS " + T_USERS);
        onCreate(db);
    }
}
