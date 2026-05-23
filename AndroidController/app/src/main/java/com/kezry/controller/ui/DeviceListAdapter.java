package com.kezry.controller.ui;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.CheckBox;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.kezry.controller.R;
import com.kezry.controller.model.AudioDevice;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * 设备列表适配器
 * 支持多选模式
 */
public class DeviceListAdapter extends RecyclerView.Adapter<DeviceListAdapter.ViewHolder> {

    private static final String TAG = "DeviceListAdapter";

    private Context context;
    private List<AudioDevice> devices;
    private Set<String> selectedDeviceIds;
    private boolean isSelectionMode;
    private OnDeviceClickListener clickListener;
    private OnDeviceLongClickListener longClickListener;

    public interface OnDeviceClickListener {
        void onDeviceClick(AudioDevice device, int position);
    }

    public interface OnDeviceLongClickListener {
        void onDeviceLongClick(AudioDevice device, int position);
    }

    public DeviceListAdapter(Context context) {
        this.context = context;
        this.devices = new ArrayList<>();
        this.selectedDeviceIds = new HashSet<>();
    }

    public void setDevices(List<AudioDevice> devices) {
        this.devices = devices;
        notifyDataSetChanged();
    }

    public void setOnDeviceClickListener(OnDeviceClickListener listener) {
        this.clickListener = listener;
    }

    public void setOnDeviceLongClickListener(OnDeviceLongClickListener listener) {
        this.longClickListener = listener;
    }

    public void setSelectionMode(boolean selectionMode) {
        this.isSelectionMode = selectionMode;
        if (!selectionMode) {
            selectedDeviceIds.clear();
        }
        notifyDataSetChanged();
    }

    public boolean isSelectionMode() {
        return isSelectionMode;
    }

    public void toggleSelection(String deviceId) {
        if (selectedDeviceIds.contains(deviceId)) {
            selectedDeviceIds.remove(deviceId);
        } else {
            selectedDeviceIds.add(deviceId);
        }
        notifyDataSetChanged();
    }

    public void selectAll() {
        selectedDeviceIds.clear();
        for (AudioDevice device : devices) {
            selectedDeviceIds.add(device.getDeviceId());
        }
        notifyDataSetChanged();
    }

    public void clearSelection() {
        selectedDeviceIds.clear();
        notifyDataSetChanged();
    }

    public List<AudioDevice> getSelectedDevices() {
        List<AudioDevice> selected = new ArrayList<>();
        for (AudioDevice device : devices) {
            if (selectedDeviceIds.contains(device.getDeviceId())) {
                selected.add(device);
            }
        }
        return selected;
    }

    public int getSelectedCount() {
        return selectedDeviceIds.size();
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        View view = LayoutInflater.from(context).inflate(R.layout.item_device, parent, false);
        return new ViewHolder(view);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        AudioDevice device = devices.get(position);
        holder.bind(device, position);
    }

    @Override
    public int getItemCount() {
        return devices.size();
    }

    class ViewHolder extends RecyclerView.ViewHolder {
        private TextView deviceName;
        private TextView deviceId;
        private TextView deviceInfo;
        private TextView deviceStatus;
        private CheckBox checkBox;
        private View signalIndicator;

        public ViewHolder(@NonNull View itemView) {
            super(itemView);
            deviceName = itemView.findViewById(R.id.device_name);
            deviceId = itemView.findViewById(R.id.device_id);
            deviceInfo = itemView.findViewById(R.id.device_info);
            deviceStatus = itemView.findViewById(R.id.device_status);
            checkBox = itemView.findViewById(R.id.checkbox);
            signalIndicator = itemView.findViewById(R.id.signal_indicator);

            itemView.setOnClickListener(v -> {
                int pos = getAdapterPosition();
                if (pos != RecyclerView.NO_POSITION && clickListener != null) {
                    if (isSelectionMode) {
                        toggleSelection(devices.get(pos).getDeviceId());
                    } else {
                        clickListener.onDeviceClick(devices.get(pos), pos);
                    }
                }
            });

            itemView.setOnLongClickListener(v -> {
                int pos = getAdapterPosition();
                if (pos != RecyclerView.NO_POSITION && longClickListener != null) {
                    longClickListener.onDeviceLongClick(devices.get(pos), pos);
                    return true;
                }
                return false;
            });

            checkBox.setOnClickListener(v -> {
                int pos = getAdapterPosition();
                if (pos != RecyclerView.NO_POSITION) {
                    toggleSelection(devices.get(pos).getDeviceId());
                }
            });
        }

        public void bind(AudioDevice device, int position) {
            deviceName.setText(device.getAlias());
            deviceId.setText(device.getDeviceId().substring(0, 8) + "...");

            // 设备信息：模式、声道
            String info = context.getString(R.string.device_info_format,
                device.getMode(), device.getChannel());
            deviceInfo.setText(info);

            // 设备状态：延迟、信号强度
            String status = context.getString(R.string.device_status_format,
                device.getLatencyMs(), device.getRssi());
            deviceStatus.setText(status);

            // 信号指示器颜色
            int signalColor = getSignalColor(device.getRssi());
            signalIndicator.setBackgroundColor(signalColor);

            // 选择框状态
            checkBox.setVisibility(isSelectionMode ? View.VISIBLE : View.GONE);
            checkBox.setChecked(selectedDeviceIds.contains(device.getDeviceId()));
        }

        private int getSignalColor(int rssi) {
            if (rssi >= -50) return 0xFF4CAF50; // 绿色
            if (rssi >= -70) return 0xFFFFC107; // 黄色
            return 0xFFF44336; // 红色
        }
    }
}
