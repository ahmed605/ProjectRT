extern "C" __int64 InvokeExeMain(void* main);
extern "C" void __Managed_Main();

extern "C" __int64 InvokeMain()
{
	return InvokeExeMain(&__Managed_Main);
}

#pragma comment(linker, "/export:InvokeMain")