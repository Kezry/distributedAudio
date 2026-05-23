package com.kezry.soundplayer.network;

import android.util.Log;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

/**
 * RTP 数据包解析器
 * 解析 RTP 格式的音频数据包
 */
public class RtpPacket {
    private static final String TAG = "RtpPacket";
    private static final int RTP_HEADER_SIZE = 12;

    private int version;
    private boolean padding;
    private boolean extension;
    private int csrcCount;
    private boolean marker;
    private int payloadType;
    private int sequenceNumber;
    private long timestamp;
    private long ssrc;
    private byte[] payload;

    public static RtpPacket parse(byte[] data) {
        if (data == null || data.length < RTP_HEADER_SIZE) {
            Log.w(TAG, "Invalid RTP packet: too short");
            return null;
        }

        try {
            RtpPacket packet = new RtpPacket();
            ByteBuffer buffer = ByteBuffer.wrap(data).order(ByteOrder.BIG_ENDIAN);

            // Parse header
            byte firstByte = buffer.get();
            packet.version = (firstByte >> 6) & 0x03;
            packet.padding = ((firstByte >> 5) & 0x01) == 1;
            packet.extension = ((firstByte >> 4) & 0x01) == 1;
            packet.csrcCount = firstByte & 0x0F;

            byte secondByte = buffer.get();
            packet.marker = ((secondByte >> 7) & 0x01) == 1;
            packet.payloadType = secondByte & 0x7F;

            packet.sequenceNumber = buffer.getShort() & 0xFFFF;
            packet.timestamp = buffer.getInt() & 0xFFFFFFFFL;
            packet.ssrc = buffer.getInt() & 0xFFFFFFFFL;

            // Skip CSRC if present
            if (packet.csrcCount > 0) {
                buffer.position(buffer.position() + packet.csrcCount * 4);
            }

            // Extract payload
            int payloadSize = data.length - buffer.position();
            packet.payload = new byte[payloadSize];
            buffer.get(packet.payload);

            return packet;
        } catch (Exception e) {
            Log.e(TAG, "Error parsing RTP packet", e);
            return null;
        }
    }

    public byte[] getPayload() {
        return payload;
    }

    public int getSequenceNumber() {
        return sequenceNumber;
    }

    public long getTimestamp() {
        return timestamp;
    }

    public long getSsrc() {
        return ssrc;
    }

    public boolean isMarker() {
        return marker;
    }

    public int getPayloadType() {
        return payloadType;
    }
}
