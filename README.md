# ProjectRT
Experimental project aiming at making it possible to use [.NET Native](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/) with [CoreRT](https://github.com/dotnet/corert)'s ILCompiler and MSVC's [`link.exe`](https://learn.microsoft.com/en-us/cpp/build/reference/linking?view=msvc-170) instead of [Bartok](https://en.turkcewiki.org/wiki/Bartok_(compiler))/[Triton](https://web.archive.org/web/20201130194915/https://channel9.msdn.com/Shows/Going+Deep/Mani-Ramaswamy-and-Peter-Sollich-Inside-Compiler-in-the-Cloud-and-MDIL) [MDIL](https://www.freepatentsonline.com/y2011/0258615.html) Compiler of [`nutc_driver.exe` and `rhbind.exe`](https://web.archive.org/web/2020/https://channel9.msdn.com/Shows/Going+Deep/Inside-NET-Native).

Currently only the __x64__ target is tested and confirmed to be working, but support for __x86__ and __ARM32__ is planned.
> [!NOTE] 
> *__.NET Native__ already uses __CoreRT__'s ILCompiler for the __ARM64__ target (codenamed **ProjectX**) unlike for the __x86__, __x64__, and __ARM32__ targets so you don't need this project for __ARM64__ targets.*

## Usage
> [!NOTE]  
> *Usage guide below is temporary until a proper installation method is there.*

1. **Compile** [`bootstrap_dll`](https://github.com/ahmed605/ProjectRT/tree/master/bootstrap_dll), [`shimAppDll`](https://github.com/ahmed605/ProjectRT/tree/master/shimAppDll), and [`shimExe`](https://github.com/ahmed605/ProjectRT/tree/master/shimExe) on the `Release` configuration.

2. **Copy** the compiled `.lib`s to .NET Native's Nuget package ilc `tools` folder (`.nuget\runtime.win10-x64.microsoft.net.native.compiler.2.2.12-rel-31116-00\tools\x64\ilc\tools`).

3. **Download** [`mrt100X_app.lib`](https://github.com/ahmed605/ProjectRT/raw/master/Libs/x64/mrt100X_app.lib) to .NET Native's Nuget package ilc runtime libs folder (`.nuget\runtime.win10-x64.microsoft.net.native.compiler.2.2.12-rel-31116-00\tools\x64\ilc\Lib\Runtime`).

4. **Duplicate** `mrt100_app.dll` and name the duplicated copy `mrt100X_app.dll`.

5. **Copy** `ILCompiler.Compiler.dll`, `ILCompiler.DependencyAnalysisFramework.dll`, `ILCompiler.Host.dll`, `ILCompiler.MetadataTransform.dll`, and `ILCompiler.TypeSystem.dll` from arm64 ilc tools folder to x64 ilc tools folder (`.nuget\runtime.win10-arm64.microsoft.net.native.compiler.2.2.12-rel-31116-00\tools\arm64\ilc\tools` -> `.nuget\runtime.win10-x64.microsoft.net.native.compiler.2.2.12-rel-31116-00\tools\x64\ilc\tools`).

6. **Open** `ILCompiler.Host.dll` in [dnSpy](https://github.com/dnSpyEx/dnSpy), then navigate to `ILCompilerHost.AddTocModule`, then `Edit Method`, then replace `File.OpenRead(filename)` with `File.OpenRead(filename.Replace("win10-x64", "win10-arm64"))`, then `Compile`, and finally `File` > `Save Module`.

7. **Add** this to your app's csproj (tweak the paths as needed)
```xml
<UseDotNetNativeSharedAssemblyFrameworkPackage>false</UseDotNetNativeSharedAssemblyFrameworkPackage>
<IlcParameters>/PureNative /LinkPath:"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\MSVC\14.38.33130\bin\Hostx64\x64" /NativeLibPath:"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\um\x64"</IlcParameters>
```

8. **Profit!**

## Screenshot because why not
![image](https://github.com/ahmed605/ProjectRT/assets/34550324/4b764ead-490c-477a-920b-282be408713c)
