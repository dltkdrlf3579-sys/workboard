@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "fx=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%fx%\csc.exe" set "fx=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%fx%\WPF\PresentationFramework.dll" (
  echo Windows .NET Framework 4.x with WPF is required.
  popd
  exit /b 1
)
if not exist "bin\" mkdir "bin" || exit /b 1
"%fx%\csc.exe" /nologo /target:winexe /out:"bin\WorkBoard.exe" /r:"%fx%\WPF\PresentationFramework.dll" /r:"%fx%\WPF\PresentationCore.dll" /r:"%fx%\WPF\WindowsBase.dll" /r:Microsoft.CSharp.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xaml.dll /r:System.Xml.Linq.dll /r:System.IO.Compression.dll /resource:Theme.xaml,WorkBoard.Theme.xaml WorkBoard.cs WorkBoardView.cs ExcelReport.cs WorkBoardTray.cs
set "result=%errorlevel%"
if "%result%"=="0" echo Built bin\WorkBoard.exe
popd
exit /b %result%
