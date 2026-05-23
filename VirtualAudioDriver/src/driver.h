/*
 * DistributedAudio Virtual Audio Driver
 * Driver Header File
 */

#ifndef _DISTRIBUTEDAUDIO_DRIVER_H_
#define _DISTRIBUTEDAUDIO_DRIVER_H_

#include <ntddk.h>
#include <portcls.h>
#include <ksmedia.h>

// 设备名称和符号链接
#define DEVICE_NAME L"\\Device\\DistributedAudio"
#define DOS_DEVICE_NAME L"\\DosDevices\\DistributedAudio"

// 共享内存大小 (1MB)
#define SHARED_MEMORY_SIZE (1024 * 1024)

// IOCTL 控制码
#define IOCTL_DISTRIBUTEDAUDIO_BASE 0x800
#define IOCTL_DISTRIBUTEDAUDIO_GET_FORMAT \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS)
#define IOCTL_DISTRIBUTEDAUDIO_GET_SHARED_MEMORY \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS)
#define IOCTL_DISTRIBUTEDAUDIO_SET_EVENT \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_ANY_ACCESS)

// DPF (Debug Print Facility) 宏
#define DPF_VERBOSE 0
#define DPF_TERSE 1
#define DPF_ERROR 2

#if DBG
#define DPF(l, x) \
    if ((l) >= DPF_TERSE) { \
        DbgPrint x; \
    }
#else
#define DPF(l, x)
#endif

// 音频格式结构
typedef struct _AUDIO_FORMAT {
    ULONG SampleRate;      // 采样率 (Hz)
    ULONG Channels;        // 声道数
    ULONG BitsPerSample;   // 位深度
    ULONG BlockAlign;      // 块对齐 (Channels * BitsPerSample / 8)
    ULONG AvgBytesPerSec;  // 平均每秒字节数
} AUDIO_FORMAT, *PAUDIO_FORMAT;

// 共享内存头部
typedef struct _SHARED_MEMORY_HEADER {
    volatile ULONG WriteOffset;      // 写指针
    volatile ULONG ReadOffset;       // 读指针
    ULONG BufferSize;                // 缓冲区大小
    ULONG SampleRate;                // 采样率
    ULONG Channels;                  // 声道数
    ULONG BitsPerSample;             // 位深度
    volatile ULONG Active;           // 激活状态
    ULONG Reserved[4];               // 保留字段
} SHARED_MEMORY_HEADER, *PSHARED_MEMORY_HEADER;

// 共享内存缓冲区
typedef struct _SHARED_MEMORY_BUFFER {
    SHARED_MEMORY_HEADER Header;
    BYTE Data[ANYSIZE_ARRAY];        // 环形缓冲区数据
} SHARED_MEMORY_BUFFER, *PSHARED_MEMORY_BUFFER;

// 共享内存信息
typedef struct _SHARED_MEMORY_INFO {
    PVOID Address;                   // 用户空间地址
    SIZE_T Size;                     // 内存大小
} SHARED_MEMORY_INFO, *PSHARED_MEMORY_INFO;

// 设备上下文
typedef struct _DEVICE_CONTEXT {
    PDEVICE_OBJECT DeviceObject;     // 设备对象
    LONG RefCount;                   // 引用计数

    AUDIO_FORMAT Format;             // 音频格式

    struct {
        PVOID Memory;                // 内核空间地址
        PVOID UserAddress;           // 用户空间地址
        PMDL Mdl;                    // MDL描述符
        SIZE_T MemorySize;           // 内存大小
        HANDLE DataReadyEvent;       // 数据就绪事件
    } SharedMemory;

    KSPIN_LOCK BufferLock;           // 缓冲区自旋锁
    KEVENT StreamEvent;              // 流事件
    KSTREAM StreamState;             // 流状态
    KMUTEX StreamMutex;              // 流互斥锁
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

// 流状态
typedef enum _KSTREAM {
    KS_STOPPED,
    KS_RUNNING,
    KS_PAUSED
} KSTREAM;

// 函数声明
DRIVER_INITIALIZE DriverEntry;
DRIVER_UNLOAD DriverUnload;
NTSTATUS InitializeDevice(_In_ PDRIVER_OBJECT DriverObject);
NTSTATUS CreateSharedMemory(_In_ PDEVICE_CONTEXT Context);
NTSTATUS DispatchCreate(_In_ PDEVICE_OBJECT DeviceObject, _In_ PIRP Irp);
NTSTATUS DispatchClose(_In_ PDEVICE_OBJECT DeviceObject, _In_ PIRP Irp);
NTSTATUS DispatchWrite(_In_ PDEVICE_OBJECT DeviceObject, _In_ PIRP Irp);
NTSTATUS DispatchDeviceControl(_In_ PDEVICE_OBJECT DeviceObject, _In_ PIRP Irp);

#endif // _DISTRIBUTEDAUDIO_DRIVER_H_
