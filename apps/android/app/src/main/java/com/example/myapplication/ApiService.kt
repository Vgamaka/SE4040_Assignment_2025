package com.example.myapplication

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

interface ApiService {
    @GET("/api/Test/all")
    suspend fun getAllTests(): Response<List<TestModel>>

    @POST("/api/Auth/login")
    suspend fun ownerLogin(@Body body: LoginRequest): Response<AuthResponse>
}
