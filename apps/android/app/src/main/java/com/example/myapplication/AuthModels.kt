package com.example.myapplication

data class LoginRequest(
    val username: String?,
    val password: String?
)

data class AuthResponse(
    val token: String?,
    val role: String?,
    val username: String?
)
