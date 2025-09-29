# Android API Base

Use 10.0.2.2 to reach your host machine's IIS/Kestrel from the Android emulator.

Example (Kotlin):
object ApiConfig {
    const val BASE_URL = "http://10.0.2.2:8085"
}

When you create the project structure, place this in your config/constants package.
