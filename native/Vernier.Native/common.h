#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define VERNIER_NATIVE_EXPORTS
#include <windows.h>
#include <wrl/client.h>
#include <algorithm>
#include <atomic>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>
#include "vernier_native.h"

using Microsoft::WRL::ComPtr;

#define RETURN_IF_FAILED(expr) do { HRESULT hr_ = (expr); if (FAILED(hr_)) return int32_t(hr_); } while (0)
