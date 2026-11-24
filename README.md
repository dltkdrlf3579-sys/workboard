# 업무 목록

Windows 개인 업무 게시판. 본문과 캡처는 실행 파일 옆 업무대장.xml에 저장합니다.

## 빌드와 실행

Windows의 .NET Framework 4.x C# 컴파일러와 WPF 어셈블리를 사용합니다. 외부 패키지를 내려받는 단계는 없습니다. 쓰기 가능한 폴더에 소스를 두고 PowerShell에서 아래 명령을 순서대로 실행하세요.

```powershell
.\build.cmd
.\bin\WorkBoard.exe
```

WorkBoard.cs는 업무 편집과 저장을, WorkBoardView.cs와 Theme.xaml은 화면 구성을, ExcelReport.cs는 선택한 업무의 XLSX 출력을 담당합니다. Theme.xaml은 빌드할 때 실행 파일에 포함됩니다. 빌드 결과와 개인 업무 기록은 Git 추적에서 제외합니다. 기록을 백업할 때는 프로그램을 닫고 bin 폴더의 업무대장.xml을 복사하세요.

## PC를 바꾸거나 새 버전으로 옮길 때

1. 기존 PC에서 편집 중인 글을 저장하고 프로그램을 종료합니다.
2. 사용하던 WorkBoard.exe 옆의 **업무대장.xml**을 복사합니다. 글과 캡처, 비고, 상태, 휴지통 항목이 모두 들어 있습니다. 사내 업무 데이터는 회사에서 승인한 경로로 옮기세요.
3. 새 PC에서 소스를 받아 build.cmd로 빌드한 뒤, 처음 실행하기 전에 bin 폴더의 WorkBoard.exe 옆에 업무대장.xml을 놓습니다.
4. 실행하여 기존 글과 캡처를 확인합니다. 이미 새 PC에도 기록이 있다면 양쪽 파일을 별도 보관하세요. XML 복사는 전체 교체이며 자동 병합하지 않습니다.

업무대장.xml.bak는 직전 저장본입니다. 현재 XML 하나로 이전할 수 있습니다.
