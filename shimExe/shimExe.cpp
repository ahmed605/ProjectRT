// shimExe.cpp : Defines the functions for the static library.
//

#include "framework.h"

#pragma comment(linker, "/nodefaultlib")
#pragma comment(linker, "/defaultlib:uuid.lib")

extern "C" __declspec(dllimport) void InvokeMain();

extern "C" int wmain()
{
	InvokeMain();
	return 0;
}
