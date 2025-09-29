package com.example.myapplication

import android.os.Bundle
import android.util.Log
import android.widget.TextView
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {

  private lateinit var resultText: TextView

  override fun onCreate(savedInstanceState: Bundle?) {
    super.onCreate(savedInstanceState)
    enableEdgeToEdge()
    setContentView(R.layout.activity_main)

    // Match TextView with ID resultText in activity_main.xml
    resultText = findViewById(R.id.resultText)

    // Preserve your edge-to-edge inset handling
    ViewCompat.setOnApplyWindowInsetsListener(findViewById(R.id.main)) { v, insets ->
      val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
      v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
      insets
    }

    // 🔥 Call backend API
    fetchTests()
  }

  private fun fetchTests() {
    lifecycleScope.launch {
      try {
        val response = RetrofitClient.instance.getAllTests()
        if (response.isSuccessful) {
          val data = response.body()
          val output = data?.joinToString("\n") { t ->
            "ID=${t.id}, Name=${t.name}, Age=${t.age}"
          } ?: "No data"
          resultText.text = output
          Log.d("API_SUCCESS", output)
        } else {
          val errorMsg = "Error ${response.code()} - ${response.message()}"
          resultText.text = errorMsg
          Log.e("API_ERROR", errorMsg)
        }
      } catch (e: Exception) {
        val exMsg = "Exception: ${e.message}"
        resultText.text = exMsg
        Log.e("API_EXCEPTION", exMsg, e)
      }
    }
  }
}
