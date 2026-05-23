# Add project specific ProGuard rules here.

# Keep line number information for debugging
-keepattributes SourceFile,LineNumberTable

# Keep native methods
-keepclasseswithmembernames class * {
    native <methods>;
}

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
