#define COBJMACROS
#include <windows.h>
#include <objbase.h>
#include <shobjidl.h>
#include <shellapi.h>
#include <shlwapi.h>

// {5E57551A-5925-4B90-9D55-70759F511A91}
static const CLSID CLSID_BatchRenameCommand =
    {0x5e57551a, 0x5925, 0x4b90, {0x9d, 0x55, 0x70, 0x75, 0x9f, 0x51, 0x1a, 0x91}};

static HINSTANCE g_instance;
static LONG g_objectCount;

typedef struct ExplorerCommand
{
    IExplorerCommand iface;
    LONG references;
} ExplorerCommand;

typedef struct CommandClassFactory
{
    IClassFactory iface;
    LONG references;
} CommandClassFactory;

static HRESULT STDMETHODCALLTYPE Command_QueryInterface(IExplorerCommand* self, REFIID iid, void** result);
static ULONG STDMETHODCALLTYPE Command_AddRef(IExplorerCommand* self);
static ULONG STDMETHODCALLTYPE Command_Release(IExplorerCommand* self);
static HRESULT STDMETHODCALLTYPE Command_GetTitle(IExplorerCommand* self, IShellItemArray* items, LPWSTR* title);
static HRESULT STDMETHODCALLTYPE Command_GetIcon(IExplorerCommand* self, IShellItemArray* items, LPWSTR* icon);
static HRESULT STDMETHODCALLTYPE Command_GetToolTip(IExplorerCommand* self, IShellItemArray* items, LPWSTR* tooltip);
static HRESULT STDMETHODCALLTYPE Command_GetCanonicalName(IExplorerCommand* self, GUID* canonicalName);
static HRESULT STDMETHODCALLTYPE Command_GetState(IExplorerCommand* self, IShellItemArray* items, BOOL okToBeSlow, EXPCMDSTATE* state);
static HRESULT STDMETHODCALLTYPE Command_Invoke(IExplorerCommand* self, IShellItemArray* items, IBindCtx* bindContext);
static HRESULT STDMETHODCALLTYPE Command_GetFlags(IExplorerCommand* self, EXPCMDFLAGS* flags);
static HRESULT STDMETHODCALLTYPE Command_EnumSubCommands(IExplorerCommand* self, IEnumExplorerCommand** commands);

static IExplorerCommandVtbl g_commandVtable =
{
    Command_QueryInterface,
    Command_AddRef,
    Command_Release,
    Command_GetTitle,
    Command_GetIcon,
    Command_GetToolTip,
    Command_GetCanonicalName,
    Command_GetState,
    Command_Invoke,
    Command_GetFlags,
    Command_EnumSubCommands
};

static HRESULT STDMETHODCALLTYPE Factory_QueryInterface(IClassFactory* self, REFIID iid, void** result);
static ULONG STDMETHODCALLTYPE Factory_AddRef(IClassFactory* self);
static ULONG STDMETHODCALLTYPE Factory_Release(IClassFactory* self);
static HRESULT STDMETHODCALLTYPE Factory_CreateInstance(IClassFactory* self, IUnknown* outer, REFIID iid, void** result);
static HRESULT STDMETHODCALLTYPE Factory_LockServer(IClassFactory* self, BOOL lock);

static IClassFactoryVtbl g_factoryVtable =
{
    Factory_QueryInterface,
    Factory_AddRef,
    Factory_Release,
    Factory_CreateInstance,
    Factory_LockServer
};

static HRESULT DuplicateString(PCWSTR value, PWSTR* result)
{
    SIZE_T bytes;
    PWSTR copy;
    if (!result) return E_POINTER;
    *result = 0;
    bytes = ((SIZE_T)lstrlenW(value) + 1) * sizeof(WCHAR);
    copy = (PWSTR)CoTaskMemAlloc(bytes);
    if (!copy) return E_OUTOFMEMORY;
    CopyMemory(copy, value, bytes);
    *result = copy;
    return S_OK;
}

static BOOL GetApplicationPath(PWSTR path, DWORD capacity)
{
    DWORD length = GetModuleFileNameW(g_instance, path, capacity);
    PWSTR slash;
    if (!length || length >= capacity) return FALSE;
    slash = path + length;
    while (slash > path && slash[-1] != L'\\' && slash[-1] != L'/') --slash;
    *slash = L'\0';
    if ((DWORD)(slash - path) + 16 >= capacity) return FALSE;
    lstrcatW(path, L"BatchRename.exe");
    return TRUE;
}

static HRESULT STDMETHODCALLTYPE Command_QueryInterface(IExplorerCommand* self, REFIID iid, void** result)
{
    if (!result) return E_POINTER;
    *result = 0;
    if (IsEqualIID(iid, &IID_IUnknown) || IsEqualIID(iid, &IID_IExplorerCommand))
    {
        *result = self;
        Command_AddRef(self);
        return S_OK;
    }
    return E_NOINTERFACE;
}

static ULONG STDMETHODCALLTYPE Command_AddRef(IExplorerCommand* self)
{
    ExplorerCommand* command = CONTAINING_RECORD(self, ExplorerCommand, iface);
    return (ULONG)InterlockedIncrement(&command->references);
}

static ULONG STDMETHODCALLTYPE Command_Release(IExplorerCommand* self)
{
    ExplorerCommand* command = CONTAINING_RECORD(self, ExplorerCommand, iface);
    LONG count = InterlockedDecrement(&command->references);
    if (!count)
    {
        InterlockedDecrement(&g_objectCount);
        HeapFree(GetProcessHeap(), 0, command);
    }
    return (ULONG)count;
}

static HRESULT STDMETHODCALLTYPE Command_GetTitle(IExplorerCommand* self, IShellItemArray* items, LPWSTR* title)
{
    UNREFERENCED_PARAMETER(self); UNREFERENCED_PARAMETER(items);
    return DuplicateString(L"批量重命名", title);
}

static HRESULT STDMETHODCALLTYPE Command_GetIcon(IExplorerCommand* self, IShellItemArray* items, LPWSTR* icon)
{
    WCHAR path[MAX_PATH];
    UNREFERENCED_PARAMETER(self); UNREFERENCED_PARAMETER(items);
    if (!GetApplicationPath(path, ARRAYSIZE(path))) return E_FAIL;
    return DuplicateString(path, icon);
}

static HRESULT STDMETHODCALLTYPE Command_GetToolTip(IExplorerCommand* self, IShellItemArray* items, LPWSTR* tooltip)
{
    UNREFERENCED_PARAMETER(self); UNREFERENCED_PARAMETER(items);
    if (tooltip) *tooltip = 0;
    return E_NOTIMPL;
}

static HRESULT STDMETHODCALLTYPE Command_GetCanonicalName(IExplorerCommand* self, GUID* canonicalName)
{
    UNREFERENCED_PARAMETER(self);
    if (!canonicalName) return E_POINTER;
    *canonicalName = CLSID_BatchRenameCommand;
    return S_OK;
}

static HRESULT STDMETHODCALLTYPE Command_GetState(IExplorerCommand* self, IShellItemArray* items, BOOL okToBeSlow, EXPCMDSTATE* state)
{
    DWORD count = 0;
    UNREFERENCED_PARAMETER(self); UNREFERENCED_PARAMETER(okToBeSlow);
    if (!state) return E_POINTER;
    if (items) IShellItemArray_GetCount(items, &count);
    *state = count ? ECS_ENABLED : ECS_HIDDEN;
    return S_OK;
}

static HRESULT WriteSelectionFile(IShellItemArray* items, PCWSTR listPath)
{
    HANDLE file;
    DWORD count = 0;
    DWORD index;
    WORD bom = 0xFEFF;
    DWORD written;
    HRESULT result;

    result = IShellItemArray_GetCount(items, &count);
    if (FAILED(result) || !count) return FAILED(result) ? result : E_INVALIDARG;
    file = CreateFileW(listPath, GENERIC_WRITE, FILE_SHARE_READ, 0, CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY, 0);
    if (file == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
    if (!WriteFile(file, &bom, sizeof(bom), &written, 0))
    {
        result = HRESULT_FROM_WIN32(GetLastError());
        CloseHandle(file);
        return result;
    }

    result = S_OK;
    for (index = 0; index < count; ++index)
    {
        IShellItem* item = 0;
        PWSTR path = 0;
        result = IShellItemArray_GetItemAt(items, index, &item);
        if (FAILED(result)) break;
        result = IShellItem_GetDisplayName(item, SIGDN_FILESYSPATH, &path);
        IShellItem_Release(item);
        if (FAILED(result)) break;
        if (!WriteFile(file, path, (DWORD)(lstrlenW(path) * sizeof(WCHAR)), &written, 0) ||
            !WriteFile(file, L"\r\n", 2 * sizeof(WCHAR), &written, 0))
        {
            result = HRESULT_FROM_WIN32(GetLastError());
            CoTaskMemFree(path);
            break;
        }
        CoTaskMemFree(path);
    }
    CloseHandle(file);
    return result;
}

static HRESULT STDMETHODCALLTYPE Command_Invoke(IExplorerCommand* self, IShellItemArray* items, IBindCtx* bindContext)
{
    WCHAR tempDirectory[MAX_PATH];
    WCHAR listPath[MAX_PATH];
    WCHAR appPath[MAX_PATH];
    WCHAR commandLine[MAX_PATH * 3];
    STARTUPINFOW startup;
    PROCESS_INFORMATION process;
    HRESULT result;
    UNREFERENCED_PARAMETER(self); UNREFERENCED_PARAMETER(bindContext);

    if (!items) return E_INVALIDARG;
    if (!GetTempPathW(ARRAYSIZE(tempDirectory), tempDirectory)) return HRESULT_FROM_WIN32(GetLastError());
    if (!GetTempFileNameW(tempDirectory, L"LBR", 0, listPath)) return HRESULT_FROM_WIN32(GetLastError());
    result = WriteSelectionFile(items, listPath);
    if (FAILED(result)) { DeleteFileW(listPath); return result; }
    if (!GetApplicationPath(appPath, ARRAYSIZE(appPath))) { DeleteFileW(listPath); return E_FAIL; }

    commandLine[0] = L'\0';
    lstrcatW(commandLine, L"\"");
    lstrcatW(commandLine, appPath);
    lstrcatW(commandLine, L"\" --selection-file \"");
    lstrcatW(commandLine, listPath);
    lstrcatW(commandLine, L"\"");
    ZeroMemory(&startup, sizeof(startup));
    ZeroMemory(&process, sizeof(process));
    startup.cb = sizeof(startup);
    if (!CreateProcessW(appPath, commandLine, 0, 0, FALSE, 0, 0, 0, &startup, &process))
    {
        result = HRESULT_FROM_WIN32(GetLastError());
        DeleteFileW(listPath);
        return result;
    }
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return S_OK;
}

static HRESULT STDMETHODCALLTYPE Command_GetFlags(IExplorerCommand* self, EXPCMDFLAGS* flags)
{
    UNREFERENCED_PARAMETER(self);
    if (!flags) return E_POINTER;
    *flags = ECF_DEFAULT;
    return S_OK;
}

static HRESULT STDMETHODCALLTYPE Command_EnumSubCommands(IExplorerCommand* self, IEnumExplorerCommand** commands)
{
    UNREFERENCED_PARAMETER(self);
    if (commands) *commands = 0;
    return E_NOTIMPL;
}

static HRESULT STDMETHODCALLTYPE Factory_QueryInterface(IClassFactory* self, REFIID iid, void** result)
{
    if (!result) return E_POINTER;
    *result = 0;
    if (IsEqualIID(iid, &IID_IUnknown) || IsEqualIID(iid, &IID_IClassFactory))
    {
        *result = self;
        Factory_AddRef(self);
        return S_OK;
    }
    return E_NOINTERFACE;
}

static ULONG STDMETHODCALLTYPE Factory_AddRef(IClassFactory* self)
{
    CommandClassFactory* factory = CONTAINING_RECORD(self, CommandClassFactory, iface);
    return (ULONG)InterlockedIncrement(&factory->references);
}

static ULONG STDMETHODCALLTYPE Factory_Release(IClassFactory* self)
{
    CommandClassFactory* factory = CONTAINING_RECORD(self, CommandClassFactory, iface);
    LONG count = InterlockedDecrement(&factory->references);
    if (!count)
    {
        InterlockedDecrement(&g_objectCount);
        HeapFree(GetProcessHeap(), 0, factory);
    }
    return (ULONG)count;
}

static HRESULT STDMETHODCALLTYPE Factory_CreateInstance(IClassFactory* self, IUnknown* outer, REFIID iid, void** result)
{
    ExplorerCommand* command;
    HRESULT hr;
    UNREFERENCED_PARAMETER(self);
    if (outer) return CLASS_E_NOAGGREGATION;
    if (!result) return E_POINTER;
    *result = 0;
    command = (ExplorerCommand*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(ExplorerCommand));
    if (!command) return E_OUTOFMEMORY;
    command->iface.lpVtbl = &g_commandVtable;
    command->references = 1;
    InterlockedIncrement(&g_objectCount);
    hr = Command_QueryInterface(&command->iface, iid, result);
    Command_Release(&command->iface);
    return hr;
}

static HRESULT STDMETHODCALLTYPE Factory_LockServer(IClassFactory* self, BOOL lock)
{
    UNREFERENCED_PARAMETER(self);
    if (lock) InterlockedIncrement(&g_objectCount); else InterlockedDecrement(&g_objectCount);
    return S_OK;
}

HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID iid, void** result)
{
    CommandClassFactory* factory;
    HRESULT hr;
    if (!IsEqualCLSID(clsid, &CLSID_BatchRenameCommand)) return CLASS_E_CLASSNOTAVAILABLE;
    if (!result) return E_POINTER;
    *result = 0;
    factory = (CommandClassFactory*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(CommandClassFactory));
    if (!factory) return E_OUTOFMEMORY;
    factory->iface.lpVtbl = &g_factoryVtable;
    factory->references = 1;
    InterlockedIncrement(&g_objectCount);
    hr = Factory_QueryInterface(&factory->iface, iid, result);
    Factory_Release(&factory->iface);
    return hr;
}

HRESULT __stdcall DllCanUnloadNow(void)
{
    return g_objectCount == 0 ? S_OK : S_FALSE;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    UNREFERENCED_PARAMETER(reserved);
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_instance = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}
