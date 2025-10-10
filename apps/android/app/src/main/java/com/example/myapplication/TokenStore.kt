package com.example.myapplication

import android.content.Context
import android.content.SharedPreferences

class TokenStore(ctx: Context) {
    private val prefs: SharedPreferences =
        ctx.getSharedPreferences("auth_prefs", Context.MODE_PRIVATE)

    fun save(token: String?) {
        prefs.edit().putString(KEY_TOKEN, token).apply()
    }

    fun get(): String? = prefs.getString(KEY_TOKEN, null)

    fun clear() {
        prefs.edit().remove(KEY_TOKEN).apply()
    }

    companion object {
        private const val KEY_TOKEN = "jwt_token"
    }
}
