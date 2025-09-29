package com.example.myapplication

import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {

    private lateinit var resultText: TextView
    private lateinit var etUser: EditText
    private lateinit var etPass: EditText
    private lateinit var btnLogin: Button

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContentView(R.layout.activity_main)

        // Initialize Retrofit (TokenStore + interceptors)
        RetrofitClient.init(applicationContext)

        // Bind views
        resultText = findViewById(R.id.resultText)
        etUser = findViewById(R.id.etUser)
        etPass = findViewById(R.id.etPass)
        btnLogin = findViewById(R.id.btnLogin)

        // Insets handling
        ViewCompat.setOnApplyWindowInsetsListener(findViewById(R.id.main)) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        // Login handler
        btnLogin.setOnClickListener {
            val u = etUser.text?.toString()?.trim()
            val p = etPass.text?.toString()?.trim()
            if (u.isNullOrBlank() || p.isNullOrBlank()) {
                resultText.text = "Enter username and password"
                return@setOnClickListener
            }
            loginOwner(u, p)
        }

        // Optional: ping the Test endpoint on start to prove connectivity
        fetchTests()
    }

    private fun loginOwner(username: String, password: String) {
        lifecycleScope.launch {
            resultText.text = "Logging in..."
            try {
                val res = RetrofitClient.api().ownerLogin(LoginRequest(username, password))
                if (res.isSuccessful) {
                    val body = res.body()
                    val token = body?.token
                    RetrofitClient.tokenStore().save(token)
                    val msg = if (!token.isNullOrBlank()) {
                        "Login OK. Token saved.\nUser=${body?.username}, Role=${body?.role}"
                    } else {
                        "Login response missing token."
                    }
                    resultText.text = msg
                    Log.d("AUTH_SUCCESS", msg)
                } else {
                    val msg = "Login failed: ${res.code()} ${res.message()}"
                    resultText.text = msg
                    Log.e("AUTH_ERROR", msg)
                }
            } catch (e: Exception) {
                val msg = "Login exception: ${e.message}"
                resultText.text = msg
                Log.e("AUTH_EXCEPTION", msg, e)
            }
        }
    }

    private fun fetchTests() {
        lifecycleScope.launch {
            try {
                val response = RetrofitClient.api().getAllTests()
                if (response.isSuccessful) {
                    val data = response.body()
                    val output = data?.joinToString("\n") { t ->
                        "ID=${t.id}, Name=${t.name}, Age=${t.age}"
                    } ?: "No data"
                    resultText.text = output
                    Log.d("API_SUCCESS", output)
                } else {
                    val errorMsg = "Error ${response.code()} - ${response.message()}"
                    Log.e("API_ERROR", errorMsg)
                }
            } catch (e: Exception) {
                val exMsg = "Exception: ${e.message}"
                Log.e("API_EXCEPTION", exMsg, e)
            }
        }
    }
}
