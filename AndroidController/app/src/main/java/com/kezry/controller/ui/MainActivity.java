package com.kezry.controller.ui;

import android.os.Bundle;
import android.widget.TextView;
import androidx.appcompat.app.AppCompatActivity;
import com.kezry.controller.R;

/**
 * 主界面 (临时简化版)
 *
 * 注意: 原 MainActivity 依赖 NetworkScannerEnhanced / DeviceListAdapter / com.kezry.controller.model 等
 * 尚未实现的类，CI 编译失败。当前是 stub 让构建过线，等业务模块补齐后再恢复完整 UI。
 * 待办: 见 todolist.md "🚨 待补回功能" 段。
 */
public class MainActivity extends AppCompatActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        TextView status = findViewById(R.id.status_text);
        if (status != null) {
            status.setText("Audio Controller (stub)");
        }
    }
}
