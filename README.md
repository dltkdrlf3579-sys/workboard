# 내 업무대장

Windows 개인 업무 게시판. 본문과 캡처는 실행 파일 옆 업무대장.xml에 저장합니다.

## 빌드와 실행

Windows의 .NET Framework 4.x C# 컴파일러와 WPF 어셈블리를 사용합니다. 외부 패키지를 내려받는 단계는 없습니다. 쓰기 가능한 폴더에 소스를 두고 PowerShell에서 아래 명령을 순서대로 실행하세요.

```powershell
.\build.cmd
.\bin\WorkBoard.exe
```

WorkBoard.cs는 게시판 화면과 저장을, ExcelReport.cs는 선택한 업무의 XLSX 출력을 담당합니다. 빌드 결과와 개인 업무 기록은 Git 추적에서 제외합니다. 기록을 백업할 때는 프로그램을 닫고 bin 폴더의 업무대장.xml을 복사하세요.
