# Add project specific ProGuard rules here.
# You can control the set of applied configuration files using the
# proguardFiles setting in build.gradle.
#
# For more details, see
#   http://developer.android.com/guide/developing/tools/proguard.html

# If your project uses WebView with JS, uncomment the following
# and specify the fully qualified class name to the JavaScript interface
# class:
#-keepclass class com.example.MainActivity { *; }

# Uncomment this to preserve the line number information for
# debugging stack traces.
#-keepattributes SourceFile,LineNumberTable

# Keep source file name and line number for better debugging
-keepattributes SourceFile,LineNumberTable

# Keep native methods
-keepclasseswithmembernames class * {
    native <methods>;
}

# Keep line number information for debugging
-keepattributes *RuntimeVisibleAnnotations*

# Keep public methods
-keep public class * extends android.app.Activity {
    public void *(android.view.View);
}

# Keep data classes
-keepclassmembers class * {
    public <init>(...);
}

# Keep interfaces
-keep interface * {*;}

# Keep enums
-keepclassmembers enum * {
    public static **[] values();
    public static ** valueOf(java.lang.String);
}
