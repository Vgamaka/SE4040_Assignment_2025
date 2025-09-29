package com.example.myapplication

import retrofit2.Response
import retrofit2.http.GET

interface ApiService {
    @GET("/api/Test/all")
    suspend fun getAllTests(): Response<List<TestModel>>
}
