package com.kezry.controller.ui;

import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ListView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import com.kezry.controller.R;
import com.kezry.controller.model.AudioDevice;
import com.kezry.controller.model.DlnaGroup;
import com.kezry.controller.network.DeviceConfigClient;

import java.util.ArrayList;
import java.util.List;

/**
 * DLNA 多机组管理器
 */
public class DlnaGroupManager {

    private static final String TAG = "DlnaGroupManager";

    private Context context;
    private List<DlnaGroup> groups;
    private List<AudioDevice> availableDevices;
    private DeviceConfigClient configClient;

    public DlnaGroupManager(Context context) {
        this.context = context;
        this.groups = new ArrayList<>();
        this.configClient = new DeviceConfigClient(context);
        loadGroups();
    }

    public void setAvailableDevices(List<AudioDevice> devices) {
        this.availableDevices = devices;
    }

    public void showGroupList() {
        AlertDialog.Builder builder = new AlertDialog.Builder(context);
        builder.setTitle("DLNA 多机组管理");

        ListView groupList = new ListView(context);
        String[] groupNames = new String[groups.size() + 1];
        groupNames[0] = "+ 创建新组";

        for (int i = 0; i < groups.size(); i++) {
            groupNames[i + 1] = groups.get(i).getName();
        }

        groupList.setAdapter(new ArrayAdapter<>(context, android.R.layout.simple_list_item_1, groupNames));

        groupList.setOnItemClickListener((parent, view, position, id) -> {
            if (position == 0) {
                showCreateGroupDialog();
            } else {
                showGroupDetail(groups.get(position - 1));
            }
        });

        builder.setView(groupList);
        builder.setPositiveButton("关闭", null);
        builder.show();
    }

    private void showCreateGroupDialog() {
        AlertDialog.Builder builder = new AlertDialog.Builder(context);
        builder.setTitle("创建 DLNA 多机组");

        View dialogView = LayoutInflater.from(context).inflate(R.layout.dialog_create_dlna_group, null);
        builder.setView(dialogView);

        EditText nameInput = dialogView.findViewById(R.id.group_name_input);
        Spinner syncModeSpinner = dialogView.findViewById(R.id.sync_mode_spinner);

        String[] syncModes = {"统一起播", "自由播放"};
        ArrayAdapter<String> syncAdapter = new ArrayAdapter<>(context, android.R.layout.simple_spinner_item, syncModes);
        syncAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        syncModeSpinner.setAdapter(syncAdapter);

        builder.setPositiveButton("创建", (dialog, which) -> {
            String name = nameInput.getText().toString();
            if (name.isEmpty()) {
                Toast.makeText(context, "请输入组名称", Toast.LENGTH_SHORT).show();
                return;
            }

            boolean syncTogether = syncModeSpinner.getSelectedItemPosition() == 0;
            createGroup(name, syncTogether);
        });

        builder.setNegativeButton("取消", null);
        builder.show();
    }

    private void createGroup(String name, boolean syncTogether) {
        DlnaGroup group = new DlnaGroup(name, syncTogether);
        groups.add(group);
        saveGroups();
        Toast.makeText(context, "组已创建: " + name, Toast.LENGTH_SHORT).show();
        showGroupDetail(group);
    }

    private void showGroupDetail(DlnaGroup group) {
        AlertDialog.Builder builder = new AlertDialog.Builder(context);
        builder.setTitle(group.getName());

        View dialogView = LayoutInflater.from(context).inflate(R.layout.dialog_dlna_group_detail, null);
        builder.setView(dialogView);

        // 显示组成员
        TextView memberCount = dialogView.findViewById(R.id.member_count);
        memberCount.setText(String.format("成员设备: %d 台", group.getMemberIds().size()));

        // 显示同步模式
        TextView syncMode = dialogView.findViewById(R.id.sync_mode);
        syncMode.setText(group.isSyncTogether() ? "统一起播" : "自由播放");

        // 添加设备按钮
        Button addDeviceButton = dialogView.findViewById(R.id.add_device_button);
        addDeviceButton.setOnClickListener(v -> showAddDeviceDialog(group));

        // 移除设备按钮
        Button removeDeviceButton = dialogView.findViewById(R.id.remove_device_button);
        removeDeviceButton.setOnClickListener(v -> showRemoveDeviceDialog(group));

        // 同步测试按钮
        Button syncTestButton = dialogView.findViewById(R.id.sync_test_button);
        syncTestButton.setOnClickListener(v -> runSyncTest(group));

        builder.setNeutralButton("删除组", (dialog, which) -> deleteGroup(group));
        builder.setPositiveButton("关闭", null);
        builder.show();
    }

    private void showAddDeviceDialog(DlnaGroup group) {
        if (availableDevices == null || availableDevices.isEmpty()) {
            Toast.makeText(context, "没有可用设备", Toast.LENGTH_SHORT).show();
            return;
        }

        String[] deviceNames = new String[availableDevices.size()];
        boolean[] checked = new boolean[availableDevices.size()];

        for (int i = 0; i < availableDevices.size(); i++) {
            AudioDevice device = availableDevices.get(i);
            deviceNames[i] = device.getAlias();
            checked[i] = group.getMemberIds().contains(device.getDeviceId());
        }

        AlertDialog.Builder builder = new AlertDialog.Builder(context);
        builder.setTitle("添加设备到组");
        builder.setMultiChoiceItems(deviceNames, checked, (dialog, which, isChecked) -> {
            checked[which] = isChecked;
        });

        builder.setPositiveButton("确定", (dialog, which) -> {
            for (int i = 0; i < availableDevices.size(); i++) {
                AudioDevice device = availableDevices.get(i);
                if (checked[i]) {
                    group.addMember(device.getDeviceId());
                    // 配置设备加入组
                    configClient.setDlnaGroup(device.getIpAddress(), group.getId(), group.isSyncTogether(),
                        new DeviceConfigClient.ConfigCallback() {
                            @Override
                            public void onSuccess() {}

                            @Override
                            public void onError(String error) {}
                        });
                } else {
                    group.removeMember(device.getDeviceId());
                    // 配置设备退出组
                    configClient.setDlnaGroup(device.getIpAddress(), "", false,
                        new DeviceConfigClient.ConfigCallback() {
                            @Override
                            public void onSuccess() {}

                            @Override
                            public void onError(String error) {}
                        });
                }
            }
            saveGroups();
        });

        builder.setNegativeButton("取消", null);
        builder.show();
    }

    private void showRemoveDeviceDialog(DlnaGroup group) {
        // 类似 showAddDeviceDialog，但只显示已添加的设备
    }

    private void runSyncTest(DlnaGroup group) {
        Toast.makeText(context, "开始同步测试...", Toast.LENGTH_SHORT).show();

        for (String deviceId : group.getMemberIds()) {
            AudioDevice device = findDeviceById(deviceId);
            if (device != null) {
                configClient.startCalibration(device.getIpAddress(), new DeviceConfigClient.ConfigCallback() {
                    @Override
                    public void onSuccess() {
                        // 设备开始校准
                    }

                    @Override
                    public void onError(String error) {
                        // 处理错误
                    }
                });
            }
        }
    }

    private void deleteGroup(DlnaGroup group) {
        new AlertDialog.Builder(context)
            .setTitle("删除组")
            .setMessage("确定要删除组 \"" + group.getName() + "\" 吗？")
            .setPositiveButton("删除", (dialog, which) -> {
                groups.remove(group);
                saveGroups();
                Toast.makeText(context, "组已删除", Toast.LENGTH_SHORT).show();
            })
            .setNegativeButton("取消", null)
            .show();
    }

    private AudioDevice findDeviceById(String deviceId) {
        if (availableDevices == null) return null;

        for (AudioDevice device : availableDevices) {
            if (device.getDeviceId().equals(deviceId)) {
                return device;
            }
        }
        return null;
    }

    private void saveGroups() {
        // 保存到 SharedPreferences
    }

    private void loadGroups() {
        // 从 SharedPreferences 加载
    }

    public void release() {
        if (configClient != null) {
            configClient.release();
        }
    }
}
