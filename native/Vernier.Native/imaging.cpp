// WIC: scale with the Fant filter and encode PNG, straight from BGRA memory, on any thread.
#include "common.h"
#include <wincodec.h>
#include <combaseapi.h>

namespace
{
    // WIC needs COM on the calling thread. Joining whatever apartment the thread already has is fine: the factory is free-threaded.
    struct ComScope
    {
        HRESULT hr;
        ComScope() : hr(CoInitializeEx(nullptr, COINIT_MULTITHREADED)) {}
        ~ComScope() { if (SUCCEEDED(hr)) CoUninitialize(); }
    };
}

VN_API int32_t vn_version(void) { return VN_ABI; }

VN_API void vn_free(void* p) { CoTaskMemFree(p); }

VN_API int32_t vn_png_encode(const uint8_t* bgra, int32_t width, int32_t height, int32_t stride, int32_t max_edge,
                             uint8_t** out, int32_t* out_len, int32_t* out_width, int32_t* out_height)
{
    if (!bgra || !out || !out_len || width <= 0 || height <= 0 || stride < width * 4) return VN_E_ARG;
    *out = nullptr;
    *out_len = 0;
    ComScope com; // RPC_E_CHANGED_MODE (an STA caller) is fine: WIC works in either apartment
    ComPtr<IWICImagingFactory> factory;
    RETURN_IF_FAILED(CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory)));
    ComPtr<IWICBitmap> bitmap;
    RETURN_IF_FAILED(factory->CreateBitmapFromMemory(UINT(width), UINT(height), GUID_WICPixelFormat32bppBGRA, UINT(stride),
        UINT(stride) * UINT(height), const_cast<BYTE*>(bgra), &bitmap));

    UINT w = UINT(width), h = UINT(height);
    if (max_edge > 0 && std::max(w, h) > UINT(max_edge))
    {
        double s = double(max_edge) / std::max(w, h);
        w = std::max(1u, UINT(w * s + 0.5));
        h = std::max(1u, UINT(h * s + 0.5));
    }
    ComPtr<IWICBitmapSource> source = bitmap;
    if (w != UINT(width) || h != UINT(height))
    {
        ComPtr<IWICBitmapScaler> scaler;
        RETURN_IF_FAILED(factory->CreateBitmapScaler(&scaler));
        RETURN_IF_FAILED(scaler->Initialize(bitmap.Get(), w, h, WICBitmapInterpolationModeHighQualityCubic));
        source = scaler;
    }

    ComPtr<IStream> stream;
    RETURN_IF_FAILED(CreateStreamOnHGlobal(nullptr, TRUE, &stream));
    ComPtr<IWICBitmapEncoder> encoder;
    RETURN_IF_FAILED(factory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder));
    RETURN_IF_FAILED(encoder->Initialize(stream.Get(), WICBitmapEncoderNoCache));
    ComPtr<IWICBitmapFrameEncode> frame;
    RETURN_IF_FAILED(encoder->CreateNewFrame(&frame, nullptr));
    RETURN_IF_FAILED(frame->Initialize(nullptr));
    RETURN_IF_FAILED(frame->SetSize(w, h));
    WICPixelFormatGUID format = GUID_WICPixelFormat24bppBGR; // screenshots are opaque: no alpha channel, smaller files
    RETURN_IF_FAILED(frame->SetPixelFormat(&format));
    ComPtr<IWICFormatConverter> converter;
    RETURN_IF_FAILED(factory->CreateFormatConverter(&converter));
    RETURN_IF_FAILED(converter->Initialize(source.Get(), format, WICBitmapDitherTypeNone, nullptr, 0, WICBitmapPaletteTypeCustom));
    RETURN_IF_FAILED(frame->WriteSource(converter.Get(), nullptr));
    RETURN_IF_FAILED(frame->Commit());
    RETURN_IF_FAILED(encoder->Commit());

    STATSTG stat{};
    RETURN_IF_FAILED(stream->Stat(&stat, STATFLAG_NONAME));
    const ULONG size = ULONG(stat.cbSize.QuadPart);
    auto* buffer = static_cast<uint8_t*>(CoTaskMemAlloc(size));
    if (!buffer) return int32_t(E_OUTOFMEMORY);
    LARGE_INTEGER zero{};
    stream->Seek(zero, STREAM_SEEK_SET, nullptr);
    ULONG read = 0;
    HRESULT hr = stream->Read(buffer, size, &read);
    if (FAILED(hr) || read != size) { CoTaskMemFree(buffer); return int32_t(FAILED(hr) ? hr : E_FAIL); }
    *out = buffer;
    *out_len = int32_t(size);
    if (out_width) *out_width = int32_t(w);
    if (out_height) *out_height = int32_t(h);
    return VN_OK;
}
