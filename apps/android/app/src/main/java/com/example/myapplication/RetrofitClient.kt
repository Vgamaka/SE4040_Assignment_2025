package com.example.myapplication

import android.content.Context
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

object RetrofitClient {
  @Volatile private var _api: ApiService? = null
  private lateinit var store: TokenStore

  fun init(context: Context) {
    if (!::store.isInitialized) {
      store = TokenStore(context.applicationContext)
    }
  }

  fun api(): ApiService {
    _api?.let { return it }

    val logging = HttpLoggingInterceptor().apply {
      level = HttpLoggingInterceptor.Level.BODY
    }
    val auth = AuthInterceptor { store.get() }

    val client = OkHttpClient.Builder()
      .addInterceptor(auth)      // adds Authorization: Bearer <token> when available
      .addInterceptor(logging)
      .build()

    val created = Retrofit.Builder()
      .baseUrl(ApiConfig.BASE_URL)
      .client(client)
      .addConverterFactory(GsonConverterFactory.create())
      .build()
      .create(ApiService::class.java)

    _api = created
    return created
  }

  fun tokenStore(): TokenStore = store
}
