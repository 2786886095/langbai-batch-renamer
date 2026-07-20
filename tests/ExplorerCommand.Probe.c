#define COBJMACROS
#include <windows.h>
#include <objbase.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <stdio.h>

static const CLSID CLSID_BatchRenameCommand =
    {0x5e57551a, 0x5925, 0x4b90, {0x9d, 0x55, 0x70, 0x75, 0x9f, 0x51, 0x1a, 0x91}};

int wmain(int argc, wchar_t** argv)
{
    IExplorerCommand* command = 0;
    PWSTR title = 0;
    HRESULT hr = CoInitializeEx(0, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) return 2;
    hr = CoCreateInstance(&CLSID_BatchRenameCommand, 0, CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER, &IID_IExplorerCommand, (void**)&command);
    if (FAILED(hr))
    {
        wprintf(L"COM activation failed: 0x%08lX\n", (unsigned long)hr);
        CoUninitialize();
        return 3;
    }
    hr = IExplorerCommand_GetTitle(command, 0, &title);
    if (SUCCEEDED(hr) && title)
    {
        wprintf(L"COM activation succeeded: %ls\n", title);
        CoTaskMemFree(title);
    }
    if (SUCCEEDED(hr) && argc > 1)
    {
        UINT count = (UINT)(argc - 1);
        PIDLIST_ABSOLUTE* pidls = (PIDLIST_ABSOLUTE*)CoTaskMemAlloc(sizeof(PIDLIST_ABSOLUTE) * count);
        IShellItemArray* items = 0;
        UINT index;
        if (!pidls) hr = E_OUTOFMEMORY;
        if (pidls)
        {
            ZeroMemory(pidls, sizeof(PIDLIST_ABSOLUTE) * count);
            for (index = 0; index < count; ++index)
            {
                hr = SHParseDisplayName(argv[index + 1], 0, &pidls[index], 0, 0);
                if (FAILED(hr)) break;
            }
            if (SUCCEEDED(hr)) hr = SHCreateShellItemArrayFromIDLists(count, (PCIDLIST_ABSOLUTE*)pidls, &items);
            if (SUCCEEDED(hr)) hr = IExplorerCommand_Invoke(command, items, 0);
            if (SUCCEEDED(hr)) wprintf(L"Explorer command invoke succeeded for %u items.\n", count);
            if (items) IShellItemArray_Release(items);
            for (index = 0; index < count; ++index) if (pidls[index]) CoTaskMemFree(pidls[index]);
            CoTaskMemFree(pidls);
        }
    }
    IExplorerCommand_Release(command);
    CoUninitialize();
    return FAILED(hr) ? 4 : 0;
}
