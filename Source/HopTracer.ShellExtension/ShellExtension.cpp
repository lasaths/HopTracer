#include <windows.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <shellapi.h>

#include <new>
#include <string>
#include <vector>

namespace
{
    const CLSID CLSID_HopTracerConvertCommand =
    { 0x2bd57837, 0x61df, 0x4270, { 0xaf, 0xf3, 0x46, 0x1d, 0x8e, 0x86, 0x2d, 0x09 } };

    HINSTANCE g_moduleInstance = nullptr;
    long g_dllRefCount = 0;

    void AddDllRef()
    {
        InterlockedIncrement(&g_dllRefCount);
    }

    void ReleaseDllRef()
    {
        InterlockedDecrement(&g_dllRefCount);
    }

    long GetDllRefCount()
    {
        return InterlockedCompareExchange(&g_dllRefCount, 0, 0);
    }

    bool IsGhPath(const std::wstring& path)
    {
        if (path.size() < 3)
        {
            return false;
        }

        return _wcsicmp(path.c_str() + path.size() - 3, L".gh") == 0;
    }

    std::wstring QuoteCommandArgument(const std::wstring& argument)
    {
        std::wstring result;
        result.push_back(L'"');

        size_t slashCount = 0;
        for (wchar_t ch : argument)
        {
            if (ch == L'\\')
            {
                ++slashCount;
                continue;
            }

            if (ch == L'"')
            {
                result.append((slashCount * 2) + 1, L'\\');
                result.push_back(L'"');
                slashCount = 0;
                continue;
            }

            if (slashCount > 0)
            {
                result.append(slashCount, L'\\');
                slashCount = 0;
            }

            result.push_back(ch);
        }

        if (slashCount > 0)
        {
            result.append(slashCount * 2, L'\\');
        }

        result.push_back(L'"');
        return result;
    }

    std::wstring GetHopTracerExePath()
    {
        wchar_t modulePath[MAX_PATH] = {};
        const DWORD len = GetModuleFileNameW(g_moduleInstance, modulePath, MAX_PATH);
        if (len == 0 || len >= MAX_PATH)
        {
            return L"";
        }

        std::wstring path(modulePath, len);
        const size_t lastSlash = path.find_last_of(L"\\/");
        if (lastSlash == std::wstring::npos)
        {
            return L"";
        }

        return path.substr(0, lastSlash + 1) + L"HopTracer.exe";
    }

    HRESULT CollectGhPaths(IShellItemArray* shellItems, std::vector<std::wstring>& outPaths)
    {
        if (shellItems == nullptr)
        {
            return E_INVALIDARG;
        }

        DWORD count = 0;
        HRESULT hr = shellItems->GetCount(&count);
        if (FAILED(hr))
        {
            return hr;
        }

        for (DWORD i = 0; i < count; ++i)
        {
            IShellItem* item = nullptr;
            hr = shellItems->GetItemAt(i, &item);
            if (FAILED(hr) || item == nullptr)
            {
                continue;
            }

            PWSTR path = nullptr;
            hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
            if (SUCCEEDED(hr) && path != nullptr)
            {
                std::wstring asString(path);
                if (IsGhPath(asString))
                {
                    outPaths.push_back(asString);
                }
            }

            if (path != nullptr)
            {
                CoTaskMemFree(path);
            }

            item->Release();
        }

        return S_OK;
    }

    HRESULT LaunchHopTracer(const std::vector<std::wstring>& paths)
    {
        if (paths.empty())
        {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        const std::wstring exePath = GetHopTracerExePath();
        if (exePath.empty() || GetFileAttributesW(exePath.c_str()) == INVALID_FILE_ATTRIBUTES)
        {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        std::wstring parameters = L"--convert";
        for (const auto& path : paths)
        {
            parameters.push_back(L' ');
            parameters.append(QuoteCommandArgument(path));
        }

        SHELLEXECUTEINFOW executeInfo = {};
        executeInfo.cbSize = sizeof(executeInfo);
        executeInfo.fMask = SEE_MASK_FLAG_NO_UI;
        executeInfo.lpFile = exePath.c_str();
        executeInfo.lpParameters = parameters.c_str();
        executeInfo.nShow = SW_SHOWNORMAL;

        if (!ShellExecuteExW(&executeInfo))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        return S_OK;
    }
}

class HopTracerConvertCommand final : public IExplorerCommand
{
public:
    HopTracerConvertCommand() : _refCount(1)
    {
        AddDllRef();
    }

    ~HopTracerConvertCommand()
    {
        ReleaseDllRef();
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (ppv == nullptr)
        {
            return E_POINTER;
        }

        *ppv = nullptr;
        if (riid == IID_IUnknown || riid == IID_IExplorerCommand)
        {
            *ppv = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&_refCount));
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const auto ref = static_cast<ULONG>(InterlockedDecrement(&_refCount));
        if (ref == 0)
        {
            delete this;
        }

        return ref;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* ppszName) override
    {
        return SHStrDupW(L"Convert to GHX (HopTracer)", ppszName);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* ppszIcon) override
    {
        if (ppszIcon == nullptr)
        {
            return E_POINTER;
        }

        auto iconPath = GetHopTracerExePath();
        if (iconPath.empty())
        {
            return E_FAIL;
        }

        iconPath.append(L",0");
        return SHStrDupW(iconPath.c_str(), ppszIcon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* ppszInfotip) override
    {
        return SHStrDupW(L"Convert selected .gh files to .ghx", ppszInfotip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) override
    {
        if (pguidCommandName == nullptr)
        {
            return E_POINTER;
        }

        *pguidCommandName = CLSID_HopTracerConvertCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL, EXPCMDSTATE* pCmdState) override
    {
        if (pCmdState == nullptr)
        {
            return E_POINTER;
        }

        std::vector<std::wstring> ghPaths;
        const auto hr = CollectGhPaths(psiItemArray, ghPaths);
        if (FAILED(hr) || ghPaths.empty())
        {
            *pCmdState = ECS_HIDDEN;
            return S_OK;
        }

        *pCmdState = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx*) override
    {
        std::vector<std::wstring> ghPaths;
        const auto hr = CollectGhPaths(psiItemArray, ghPaths);
        if (FAILED(hr))
        {
            return hr;
        }

        if (ghPaths.empty())
        {
            return S_FALSE;
        }

        return LaunchHopTracer(ghPaths);
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override
    {
        if (pFlags == nullptr)
        {
            return E_POINTER;
        }

        *pFlags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override
    {
        if (ppEnum != nullptr)
        {
            *ppEnum = nullptr;
        }

        return E_NOTIMPL;
    }

private:
    long _refCount;
};

class HopTracerClassFactory final : public IClassFactory
{
public:
    HopTracerClassFactory() : _refCount(1)
    {
        AddDllRef();
    }

    ~HopTracerClassFactory()
    {
        ReleaseDllRef();
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (ppv == nullptr)
        {
            return E_POINTER;
        }

        *ppv = nullptr;
        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&_refCount));
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const auto ref = static_cast<ULONG>(InterlockedDecrement(&_refCount));
        if (ref == 0)
        {
            delete this;
        }

        return ref;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (outer != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        auto* instance = new (std::nothrow) HopTracerConvertCommand();
        if (instance == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        const auto hr = instance->QueryInterface(riid, ppv);
        instance->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
        {
            AddDllRef();
        }
        else
        {
            ReleaseDllRef();
        }

        return S_OK;
    }

private:
    long _refCount;
};

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_moduleInstance = instance;
        DisableThreadLibraryCalls(instance);
    }

    return TRUE;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return GetDllRefCount() == 0 ? S_OK : S_FALSE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    if (ppv == nullptr)
    {
        return E_POINTER;
    }

    *ppv = nullptr;
    if (!IsEqualCLSID(rclsid, CLSID_HopTracerConvertCommand))
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) HopTracerClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    const auto hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

