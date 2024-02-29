#define WIN32_LEAN_AND_MEAN

#include <stdio.h>
#include <stdlib.h>
#include <Windows.h>

#pragma comment(linker, "/nodefaultlib")
#pragma comment(linker, "/defaultlib:uuid.lib")

#pragma section(".modules$A", read)
#pragma section(".modules$Z", read)
extern "C" __declspec(allocate(".modules$A")) void* __modules_a[];
extern "C" __declspec(allocate(".modules$Z")) void* __modules_z[];

__declspec(allocate(".modules$A")) void* __modules_a[] = { nullptr };
__declspec(allocate(".modules$Z")) void* __modules_z[] = { nullptr };

#pragma comment(linker, "/merge:.modules=.rdata")
#pragma comment(linker, "/merge:.unbox=.text")

char _bookend_a;
char _bookend_z;

#pragma code_seg(".unbox$A")
void* __unbox_a() { return &_bookend_a; }
#pragma code_seg(".unbox$Z")
void* __unbox_z() { return &_bookend_z; }
#pragma code_seg()

extern "C"
{
#define _CRTALLOC(x) __declspec(allocate(x))

#pragma data_seg(".tls")

#if defined (_M_IA64) || defined (_M_AMD64)
    _CRTALLOC(".tls")
#endif
        char _tls_start = 0;

#pragma data_seg(".tls$ZZZ")

#if defined (_M_IA64) || defined (_M_AMD64)
    _CRTALLOC(".tls$ZZZ")
#endif
        char _tls_end = 0;
}

extern "C" BYTE GCStressFlag;

extern "C" void* ManagedTextStart;
extern "C" void* ManagedTextEnd;
extern "C" void* GCRootStrings;
extern "C" void* DeltaShortcuts;

extern "C" void InitializeModules(void* osModule, void** modules, int count, void** pClasslibFunctions, int nClasslibFunctions);

extern "C" void RhpSuppressGcStress();
extern "C" void RhpUnsuppressGcStress();
extern "C" BOOL RhGcStress_Initialize2();
extern "C" HINSTANCE RhpRegisterCoffUtcModule(void*, void*, void*, unsigned int, void*, void*, void**, unsigned int);

extern "C" void GetRuntimeException();
extern "C" void FailFast();
extern "C" void AppendExceptionStackFrame();
extern "C" void CheckStaticClassConstruction();
extern "C" void GetSystemArrayEEType();
extern "C" void OnFirstChanceException();
extern "C" void DebugFuncEvalHelper();
extern "C" void DebugFuncEvalAbortHelper();

typedef void(*pfn)();

static const pfn classlibFunctions[] = {
    &GetRuntimeException,
    &FailFast,
    nullptr, // &UnhandledExceptionHandler,
    &AppendExceptionStackFrame,
    &CheckStaticClassConstruction,
    &GetSystemArrayEEType,
    &OnFirstChanceException,
    &DebugFuncEvalHelper,
    &DebugFuncEvalAbortHelper,
};

void Init()
{
    RhpSuppressGcStress();

    HINSTANCE mod = RhpRegisterCoffUtcModule(
        &ManagedTextStart,
        &ManagedTextEnd,
        (void*)&__unbox_a,
        (unsigned int)((char*)&__unbox_z - (char*)&__unbox_a),
        &GCRootStrings,
        &DeltaShortcuts,
        (void**)&classlibFunctions,
        _countof(classlibFunctions));
    InitializeModules(mod, __modules_a, (int)((__modules_z - __modules_a)), (void**)&classlibFunctions, _countof(classlibFunctions));

    RhpUnsuppressGcStress();

    if (GCStressFlag)
        RhGcStress_Initialize2();
}

extern "C" __int64 InvokeExeMain(void* main);
extern "C" void __Managed_Main();

extern "C" int wmain()
{
    Init();
    InvokeExeMain(&__Managed_Main);
    return TRUE;
}