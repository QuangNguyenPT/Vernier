// Vernier.Native: the parts of Vernier that need Windows facilities managed code cannot reach well.
//  - Desktop Duplication: GPU screen capture with exact dirty rectangles (continuous context, fast asks).
//  - WIC: high-quality downscale and PNG encoding of a capture.
//  - Edge light: the capture light, composited by DWM and animated on its own thread.
// C ABI only. Every function returns 0 on success or a negative VN_E_* / HRESULT on failure; callers fall back to managed code.
#pragma once
#include <stdint.h>

#ifdef VERNIER_NATIVE_EXPORTS
#define VN_API extern "C" __declspec(dllexport)
#else
#define VN_API extern "C" __declspec(dllimport)
#endif

#define VN_OK 0
#define VN_TIMEOUT 1            // no new frame within the timeout: the screen did not change
#define VN_E_ARG (-1)
#define VN_E_ACCESS_LOST (-2)   // mode change, secure desktop, full-screen exclusive app: reopen
#define VN_E_NO_OUTPUT (-3)     // no output matches the monitor
#define VN_E_NO_FRAME (-4)      // nothing captured yet
#define VN_E_UNSUPPORTED (-5)   // duplication refused (remote session, driver, wrong adapter)

typedef struct vn_rect { int32_t left, top, right, bottom; } vn_rect;

typedef struct vn_frame_info
{
    int32_t width, height;      // output size in physical pixels
    int32_t dirty_count;        // dirty + move rectangles reported for this frame
    vn_rect dirty_bounds;       // union of them, output-relative
    double dirty_fraction;      // share of the output covered by them (overlaps counted once per rect, capped at 1)
    int32_t accumulated;        // frames the OS folded into this one
    int64_t present_qpc;        // QueryPerformanceCounter time the frame was presented (0 = pointer-only update)
} vn_frame_info;

VN_API int32_t vn_version(void);

// ---- Desktop Duplication ----
// Opens duplication for the output whose desktop rectangle starts at (left, top), on the adapter that drives it.
VN_API int32_t vn_dup_open(int32_t left, int32_t top, void** handle);
// Waits up to timeout_ms for a frame with new pixels. VN_OK: info filled and the frame kept for copy/thumb. VN_TIMEOUT: unchanged.
VN_API int32_t vn_dup_next(void* handle, int32_t timeout_ms, vn_frame_info* info);
// Copies a region (output-relative) of the latest frame as BGRA, top-down, into dst (stride bytes per row).
VN_API int32_t vn_dup_copy(void* handle, vn_rect region, uint8_t* dst, int32_t stride);
// Box-filtered grayscale thumbnail (w x h bytes) of the latest frame, or of a region of it when region is non-empty.
VN_API int32_t vn_dup_thumb(void* handle, vn_rect region, int32_t w, int32_t h, uint8_t* gray);
VN_API void vn_dup_close(void* handle);

// ---- WIC ----
// Scales BGRA pixels to fit max_edge (Fant filter; never upscales) and encodes PNG. Free *out with vn_free.
VN_API int32_t vn_png_encode(const uint8_t* bgra, int32_t width, int32_t height, int32_t stride, int32_t max_edge,
                             uint8_t** out, int32_t* out_len, int32_t* out_width, int32_t* out_height);
VN_API void vn_free(void* p);

// ---- Edge light ----
// A click-through, topmost light round one monitor, composited by DirectComposition and drawn on its own thread.
VN_API int32_t vn_glow_create(vn_rect monitor, int32_t thickness_px, uint32_t argb, void** handle);
// 0 off, 1 steady (pen armed), 2 travelling wave (looking), 3 a small steady pill at the top centre (keeping context).
// Reduced motion turns 2 into a slow breath.
VN_API int32_t vn_glow_set(void* handle, int32_t mode, int32_t reduced_motion);
// The light's window, so the app can register it with its capture policy.
VN_API void* vn_glow_hwnd(void* handle);
// Frames presented since creation (tests and diagnostics).
VN_API int64_t vn_glow_frames(void* handle);
VN_API void vn_glow_destroy(void* handle);
