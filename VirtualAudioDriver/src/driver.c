/*
 * DistributedAudio Virtual Audio Driver
 * WaveCyclic Port Class Miniport Driver
 *
 * Copyright (c) 2026 Kezry
 * License: CC BY-NC 4.0 (Commercial use requires authorization)
 */

#include <initguid.h>
#include <wdm.h>
#include <portcls.h>
#include <ksmedia.h>
#include "driver.h"

// 驱动全局变量
PDRIVER_OBJECT gDriverObject = NULL;

/*
 * DriverEntry
 * 驱动程序入口点
 */
NTSTATUS DriverEntry(
    _In_ PDRIVER_OBJECT DriverObject,
    _In_ PUNICODE_STRING RegistryPath
)
{
    NTSTATUS status = STATUS_SUCCESS;

    UNREFERENCED_PARAMETER(RegistryPath);

    DPF(D_TERSE, ("[DistributedAudio] DriverEntry"));

    // 保存驱动对象
    gDriverObject = DriverObject;

    // 设置驱动卸载例程
    DriverObject->DriverUnload = DriverUnload;

    // 设置IRP处理函数
    for (ULONG i = 0; i <= IRP_MJ_MAXIMUM_FUNCTION; i++) {
        DriverObject->MajorFunction[i] = DispatchDeviceControl;
    }

    DriverObject->MajorFunction[IRP_MJ_CREATE] = DispatchCreate;
    DriverObject->MajorFunction[IRP_MJ_CLOSE] = DispatchClose;
    DriverObject->MajorFunction[IRP_MJ_WRITE] = DispatchWrite;
    DriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = DispatchDeviceControl;

    // 初始化设备
    status = InitializeDevice(DriverObject);

    if (!NT_SUCCESS(status)) {
        DPF(D_ERROR, ("[DistributedAudio] InitializeDevice failed: 0x%X", status));
    }

    return status;
}

/*
 * DriverUnload
 * 驱动卸载例程
 */
VOID DriverUnload(
    _In_ PDRIVER_OBJECT DriverObject
)
{
    DPF(D_TERSE, ("[DistributedAudio] DriverUnload"));

    PDEVICE_OBJECT deviceObject = DriverObject->DeviceObject;

    // 清理设备
    while (deviceObject != NULL) {
        PDEVICE_OBJECT nextDevice = deviceObject->NextDevice;
        PVOID deviceExtension = deviceObject->DeviceExtension;

        if (deviceExtension != NULL) {
            PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)deviceExtension;

            // 关闭共享内存
            if (ctx->SharedMemory.UserAddress != NULL) {
                MmUnmapLockedPages(ctx->SharedMemory.UserAddress, &ctx->SharedMemory.Mdl);
            }

            if (ctx->SharedMemory.Mdl != NULL) {
                IoFreeMdl(ctx->SharedMemory.Mdl);
            }

            if (ctx->SharedMemory.Memory != NULL) {
                MmFreeContiguousMemory(ctx->SharedMemory.Memory);
            }

            // 关闭事件
            if (ctx->SharedMemory.DataReadyEvent != NULL) {
                ZwClose(ctx->SharedMemory.DataReadyEvent);
            }
        }

        deviceObject = nextDevice;
    }

    DPF(D_TERSE, ("[DistributedAudio] Driver unloaded successfully"));
}

/*
 * InitializeDevice
 * 初始化音频设备
 */
NTSTATUS InitializeDevice(
    _In_ PDRIVER_OBJECT DriverObject
)
{
    NTSTATUS status = STATUS_SUCCESS;
    PDEVICE_OBJECT deviceObject = NULL;
    PDEVICE_CONTEXT deviceContext = NULL;
    UNICODE_STRING deviceName;
    UNICODE_STRING dosDeviceName;

    DPF(D_TERSE, ("[DistributedAudio] InitializeDevice"));

    // 创建设备名称
    RtlInitUnicodeString(&deviceName, L"\\Device\\DistributedAudio");
    RtlInitUnicodeString(&dosDeviceName, L"\\DosDevices\\DistributedAudio");

    // 创建设备对象
    status = IoCreateDevice(
        DriverObject,
        sizeof(DEVICE_CONTEXT),
        &deviceName,
        FILE_DEVICE_UNKNOWN,
        FILE_DEVICE_SECURE_OPEN,
        FALSE,
        &deviceObject
    );

    if (!NT_SUCCESS(status)) {
        DPF(D_ERROR, ("[DistributedAudio] IoCreateDevice failed: 0x%X", status));
        return status;
    }

    // 初始化设备上下文
    deviceContext = (PDEVICE_CONTEXT)deviceObject->DeviceExtension;
    RtlZeroMemory(deviceContext, sizeof(DEVICE_CONTEXT));

    deviceContext->DeviceObject = deviceObject;
    deviceContext->RefCount = 1;

    // 初始化音频格式
    deviceContext->Format.SampleRate = 48000;
    deviceContext->Format.Channels = 2;
    deviceContext->Format.BitsPerSample = 16;
    deviceContext->Format.BlockAlign = deviceContext->Format.Channels * (deviceContext->Format.BitsPerSample / 8);
    deviceContext->Format.AvgBytesPerSec = deviceContext->Format.SampleRate * deviceContext->Format.BlockAlign;

    // 创建共享内存
    status = CreateSharedMemory(deviceContext);
    if (!NT_SUCCESS(status)) {
        DPF(D_ERROR, ("[DistributedAudio] CreateSharedMemory failed: 0x%X", status));
        IoDeleteDevice(deviceObject);
        return status;
    }

    // 创建符号链接
    status = IoCreateSymbolicLink(&dosDeviceName, &deviceName);
    if (!NT_SUCCESS(status)) {
        DPF(D_ERROR, ("[DistributedAudio] IoCreateSymbolicLink failed: 0x%X", status));
        IoDeleteDevice(deviceObject);
        return status;
    }

    // 初始化同步事件
    KeInitializeEvent(&deviceContext->StreamEvent, NotificationEvent, FALSE);
    KeInitializeMutex(&deviceContext->StreamMutex, 0);

    deviceObject->Flags |= DO_BUFFERED_IO;
    deviceObject->Flags &= ~DO_DEVICE_INITIALIZING;

    DPF(D_TERSE, ("[DistributedAudio] Device initialized successfully"));
    return STATUS_SUCCESS;
}

/*
 * CreateSharedMemory
 * 创建环形缓冲区共享内存
 */
NTSTATUS CreateSharedMemory(
    _In_ PDEVICE_CONTEXT Context
)
{
    NTSTATUS status = STATUS_SUCCESS;
    PHYSICAL_ADDRESS highestAcceptable;

    DPF(D_TERSE, ("[DistributedAudio] CreateSharedMemory"));

    highestAcceptable.QuadPart = -1;

    // 分配连续物理内存
    Context->SharedMemory.MemorySize = SHARED_MEMORY_SIZE;
    Context->SharedMemory.Memory = MmAllocateContiguousMemory(
        Context->SharedMemory.MemorySize,
        highestAcceptable
    );

    if (Context->SharedMemory.Memory == NULL) {
        DPF(D_ERROR, ("[DistributedAudio] MmAllocateContiguousMemory failed"));
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    // 清零内存
    RtlZeroMemory(Context->SharedMemory.Memory, Context->SharedMemory.MemorySize);

    // 创建MDL
    Context->SharedMemory.Mdl = IoAllocateMdl(
        Context->SharedMemory.Memory,
        (ULONG)Context->SharedMemory.MemorySize,
        FALSE,
        FALSE,
        NULL
    );

    if (Context->SharedMemory.Mdl == NULL) {
        DPF(D_ERROR, ("[DistributedAudio] IoAllocateMdl failed"));
        MmFreeContiguousMemory(Context->SharedMemory.Memory);
        Context->SharedMemory.Memory = NULL;
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    MmBuildMdlForNonPagedPool(Context->SharedMemory.Mdl);

    // 映射到用户空间
    Context->SharedMemory.UserAddress = MmMapLockedPagesSpecifyCache(
        Context->SharedMemory.Mdl,
        UserMode,
        MmCached,
        NULL,
        FALSE,
        NormalPagePriority
    );

    if (Context->SharedMemory.UserAddress == NULL) {
        DPF(D_ERROR, ("[DistributedAudio] MmMapLockedPagesSpecifyCache failed"));
        IoFreeMdl(Context->SharedMemory.Mdl);
        MmFreeContiguousMemory(Context->SharedMemory.Memory);
        Context->SharedMemory.Memory = NULL;
        Context->SharedMemory.Mdl = NULL;
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    // 初始化缓冲区头部
    PSUPPORTED_AUDIO_BUFFER buffer = (PSUPPORTED_AUDIO_BUFFER)Context->SharedMemory.UserAddress;
    buffer->Header.WriteOffset = 0;
    buffer->Header.ReadOffset = 0;
    buffer->Header.BufferSize = SHARED_MEMORY_SIZE - sizeof(SHARED_MEMORY_HEADER);
    buffer->Header.SampleRate = Context->Format.SampleRate;
    buffer->Header.Channels = Context->Format.Channels;
    buffer->Header.BitsPerSample = Context->Format.BitsPerSample;
    buffer->Header.Active = FALSE;

    DPF(D_TERSE, ("[DistributedAudio] Shared memory created: %p (size: %zu)",
        Context->SharedMemory.UserAddress, Context->SharedMemory.MemorySize));

    return STATUS_SUCCESS;
}

/*
 * DispatchCreate
 * 处理IRP_MJ_CREATE
 */
NTSTATUS DispatchCreate(
    _In_ PDEVICE_OBJECT DeviceObject,
    _In_ PIRP Irp
)
{
    PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)DeviceObject->DeviceExtension;
    PIO_STACK_LOCATION irpStack = IoGetCurrentIrpStackLocation(Irp);

    DPF(D_VERBOSE, ("[DistributedAudio] DispatchCreate"));

    InterlockedIncrement(&ctx->RefCount);

    // 激活音频流
    PSUPPORTED_AUDIO_BUFFER buffer = (PSUPPORTED_AUDIO_BUFFER)ctx->SharedMemory.UserAddress;
    buffer->Header.Active = TRUE;

    Irp->IoStatus.Status = STATUS_SUCCESS;
    Irp->IoStatus.Information = 0;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

/*
 * DispatchClose
 * 处理IRP_MJ_CLOSE
 */
NTSTATUS DispatchClose(
    _In_ PDEVICE_OBJECT DeviceObject,
    _In_ PIRP Irp
)
{
    PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)DeviceObject->DeviceExtension;

    DPF(D_VERBOSE, ("[DistributedAudio] DispatchClose"));

    // 停用音频流
    PSUPPORTED_AUDIO_BUFFER buffer = (PSUPPORTED_AUDIO_BUFFER)ctx->SharedMemory.UserAddress;
    buffer->Header.Active = FALSE;

    InterlockedDecrement(&ctx->RefCount);

    Irp->IoStatus.Status = STATUS_SUCCESS;
    Irp->IoStatus.Information = 0;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

/*
 * DispatchWrite
 * 处理IRP_MJ_WRITE
 */
NTSTATUS DispatchWrite(
    _In_ PDEVICE_OBJECT DeviceObject,
    _In_ PIRP Irp
)
{
    PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)DeviceObject->DeviceExtension;
    PIO_STACK_LOCATION irpStack = IoGetCurrentIrpStackLocation(Irp);
    NTSTATUS status = STATUS_SUCCESS;

    ULONG writeLength = irpStack->Parameters.Write.Length;
    PVOID writeBuffer = Irp->AssociatedIrp.SystemBuffer;

    if (writeBuffer == NULL || writeLength == 0) {
        Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INVALID_PARAMETER;
    }

    // 写入环形缓冲区
    KIRQL oldIrql;
    KeAcquireSpinLock(&ctx->BufferLock, &oldIrql);

    PSUPPORTED_AUDIO_BUFFER buffer = (PSUPPORTED_AUDIO_BUFFER)ctx->SharedMemory.UserAddress;
    PULONG writeOffset = &buffer->Header.WriteOffset;
    PULONG readOffset = &buffer->Header.ReadOffset;
    ULONG bufferSize = buffer->Header.BufferSize;

    // 计算可用空间
    ULONG availableSpace;
    if (*writeOffset >= *readOffset) {
        availableSpace = bufferSize - (*writeOffset - *readOffset);
    } else {
        availableSpace = *readOffset - *writeOffset;
    }

    if (writeLength > availableSpace) {
        // 缓冲区空间不足，覆盖旧数据
        DPF(D_WARNING, ("[DistributedAudio] Buffer overflow, dropping data"));
    }

    // 写入数据（处理环形缓冲区）
    ULONG remaining = writeLength;
    ULONG srcOffset = 0;

    while (remaining > 0) {
        ULONG chunkSize = min(remaining, bufferSize - *writeOffset);
        RtlCopyMemory(
            &buffer->Data[*writeOffset],
            (PUCHAR)writeBuffer + srcOffset,
            chunkSize
        );

        *writeOffset = (*writeOffset + chunkSize) % bufferSize;
        srcOffset += chunkSize;
        remaining -= chunkSize;
    }

    KeReleaseSpinLock(&ctx->BufferLock, oldIrql);

    // 设置数据就绪事件
    if (ctx->SharedMemory.DataReadyEvent != NULL) {
        ZwSetEvent(ctx->SharedMemory.DataReadyEvent, NULL);
    }

    Irp->IoStatus.Status = STATUS_SUCCESS;
    Irp->IoStatus.Information = writeLength;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

/*
 * DispatchDeviceControl
 * 处理IRP_MJ_DEVICE_CONTROL
 */
NTSTATUS DispatchDeviceControl(
    _In_ PDEVICE_OBJECT DeviceObject,
    _In_ PIRP Irp
)
{
    PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)DeviceObject->DeviceExtension;
    PIO_STACK_LOCATION irpStack = IoGetCurrentIrpStackLocation(Irp);
    NTSTATUS status = STATUS_SUCCESS;
    ULONG bytesReturned = 0;

    ULONG ioControlCode = irpStack->Parameters.DeviceIoControl.IoControlCode;

    switch (ioControlCode) {
    case IOCTL_DISTRIBUTEDAUDIO_GET_FORMAT:
        // 返回音频格式
        if (irpStack->Parameters.DeviceIoControl.OutputBufferLength >= sizeof(AUDIO_FORMAT)) {
            RtlCopyMemory(
                Irp->AssociatedIrp.SystemBuffer,
                &ctx->Format,
                sizeof(AUDIO_FORMAT)
            );
            bytesReturned = sizeof(AUDIO_FORMAT);
        } else {
            status = STATUS_BUFFER_TOO_SMALL;
        }
        break;

    case IOCTL_DISTRIBUTEDAUDIO_GET_SHARED_MEMORY:
        // 返回共享内存地址
        if (irpStack->Parameters.DeviceIoControl.OutputBufferLength >= sizeof(SHARED_MEMORY_INFO)) {
            PSUPPORTED_AUDIO_BUFFER buffer = (PSUPPORTED_AUDIO_BUFFER)ctx->SharedMemory.UserAddress;
            PSHARED_MEMORY_INFO info = (PSHARED_MEMORY_INFO)Irp->AssociatedIrp.SystemBuffer;
            info->Address = buffer;
            info->Size = ctx->SharedMemory.MemorySize;
            bytesReturned = sizeof(SHARED_MEMORY_INFO);
        } else {
            status = STATUS_BUFFER_TOO_SMALL;
        }
        break;

    case IOCTL_DISTRIBUTEDAUDIO_SET_EVENT:
        // 设置数据就绪事件
        if (irpStack->Parameters.DeviceIoControl.InputBufferLength >= sizeof(HANDLE)) {
            HANDLE eventHandle = *(HANDLE*)Irp->AssociatedIrp.SystemBuffer;
            ctx->SharedMemory.DataReadyEvent = eventHandle;
        } else {
            status = STATUS_INVALID_PARAMETER;
        }
        break;

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
        break;
    }

    Irp->IoStatus.Status = status;
    Irp->IoStatus.Information = bytesReturned;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}
