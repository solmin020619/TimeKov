@echo off
setlocal enabledelayedexpansion

REM ===================================================================
REM  TimeKov - 스팀 업로드
REM
REM  [인코딩 주의] 이 파일은 CP949 로 저장해야 한다.
REM    UTF-8 로 저장하고 chcp 65001 을 쓰면, cmd 가 배치 파일을 바이트 단위로
REM    읽으면서 한글(3바이트) 뒤에서 읽는 위치가 어긋난다. 그러면 echo 같은
REM    앞부분이 잘려 나가고 남은 한글이 명령어로 해석돼 오류가 난다.
REM    한국어 윈도우 콘솔 기본값이 949 라 CP949 면 chcp 없이 그대로 보인다.
REM
REM  쓰는 법
REM    1) 아래 [설정] 네 줄을 채운다
REM    2) 유니티에서  Tools > TIMEKOV > 스팀 > 빌드 만들기  실행
REM    3) 이 파일을 더블클릭
REM
REM  vdf 설정 파일은 아래 값으로 매번 새로 만든다.
REM  두 파일에 ID 를 따로 적어 두면 한쪽만 고치는 사고가 반드시 난다.
REM ===================================================================

REM ── [설정] ─────────────────────────────────────────────────────────
REM 본편과 체험판은 스팀에서 별개의 앱이다. ID 를 한 세트만 두고 그때그때
REM 바꿔 쓰면, 되돌리는 걸 잊은 채 체험판 빌드가 본편에 올라가는 사고가 난다.
REM 두 세트를 다 적어 두고 실행할 때 고른다.

REM [본편]  store.steampowered.com/app/5137440/TimeKov/
set MAIN_APPID=5137440
set MAIN_DEPOTID=5137441

REM [체험판] Steamworks > TIMEKOV > 관련 항목 보기 > 체험판 추가 로 만든 뒤
REM          그 앱의 SteamPipe > Depots 에서 확인. 비워 두면 체험판 업로드가 막힌다.
set DEMO_APPID=5224840
set DEMO_DEPOTID=5224841

REM steamcmd.exe 경로 : Steamworks SDK 를 푼 위치
set STEAMCMD=D:\sdk\tools\ContentBuilder\builder\steamcmd.exe

REM 업로드 권한이 있는 파트너 계정 ID
set STEAMUSER=dolman11
REM ───────────────────────────────────────────────────────────────────

set HERE=%~dp0
set CONTENT=%HERE%content
set SCRIPTS=%HERE%scripts
set OUTPUT=%HERE%output

echo.
echo ============================================
echo   TimeKov  스팀 업로드
echo ============================================
echo.
echo   1 = 본편   (App %MAIN_APPID%)
echo   2 = 체험판 (App %DEMO_APPID%)
echo.
set /p TARGET="어디에 올릴까요? (1/2): "

if "%TARGET%"=="1" (
    set APPID=%MAIN_APPID%
    set DEPOTID=%MAIN_DEPOTID%
    set LABEL=Main
    goto :picked
)
if "%TARGET%"=="2" (
    if "%DEMO_APPID%"=="" (
        echo.
        echo [중단] 체험판 App ID 가 비어 있습니다.
        echo        Steamworks 에서 체험판을 먼저 만들고 이 파일의 DEMO_APPID / DEMO_DEPOTID 를 채우세요.
        goto :fail
    )
    set APPID=%DEMO_APPID%
    set DEPOTID=%DEMO_DEPOTID%
    set LABEL=Demo
    goto :picked
)
echo 취소했습니다.
goto :end

:picked
echo.

REM ── 설정 확인 ──────────────────────────────────────────────────────
if "%APPID%"=="" (
    echo [중단] APPID 가 비어 있습니다. 이 파일 위쪽 [설정] 을 채우세요.
    goto :fail
)
if "%DEPOTID%"=="" (
    echo [중단] DEPOTID 가 비어 있습니다.
    goto :fail
)
if "%STEAMUSER%"=="" (
    echo [중단] STEAMUSER 가 비어 있습니다.
    goto :fail
)
if not exist "%STEAMCMD%" (
    echo [중단] steamcmd.exe 를 찾을 수 없습니다:
    echo        %STEAMCMD%
    echo        Steamworks SDK 를 받아 압축을 풀고 경로를 맞추세요.
    goto :fail
)

REM ── 빌드 결과 확인 ─────────────────────────────────────────────────
REM 빈 폴더를 올리면 스팀에 '파일 0개' 빌드가 만들어진다. 미리 막는다.
if not exist "%CONTENT%\TimeKov.exe" (
    echo [중단] 빌드 결과가 없습니다:
    echo        %CONTENT%\TimeKov.exe
    echo.
    echo        유니티에서 Tools ^> TIMEKOV ^> 스팀 ^> 빌드 만들기 를 먼저 실행하세요.
    goto :fail
)

for /f %%A in ('dir /b /s "%CONTENT%" ^| find /c /v ""') do set FILECOUNT=%%A
echo   대상      : %LABEL%
echo   App ID   : %APPID%
echo   Depot ID : %DEPOTID%
echo   계정      : %STEAMUSER%
echo   올릴 파일 : %FILECOUNT% 개
echo   경로      : %CONTENT%
echo.

echo   Y = 실제 업로드     P = 미리보기(전송하지 않음)     N = 취소
echo.
set /p GO="선택: "

REM 미리보기는 SteamPipe 의 preview 모드다. 로그인하고 파일을 전부 훑어
REM 무엇이 올라갈지 계산하지만 서버로 전송하지 않는다.
REM 계정/권한/vdf 문법/제외 규칙을 5.6GB 를 보내보지 않고 확인할 수 있다.
if /i "%GO%"=="Y" (
    set PREVIEW=0
    goto :run
)
if /i "%GO%"=="P" (
    set PREVIEW=1
    goto :run
)
echo 취소했습니다.
goto :end

:run

REM ── vdf 생성 ───────────────────────────────────────────────────────
if not exist "%SCRIPTS%" mkdir "%SCRIPTS%"
if not exist "%OUTPUT%"  mkdir "%OUTPUT%"

REM setlive 를 비워 두면 업로드만 되고 어느 브랜치에도 적용되지 않는다.
REM 실수로 바로 배포되는 것을 막기 위한 것이며, 적용은 Steamworks 웹에서 한다.
> "%SCRIPTS%\app_build.vdf" (
    echo "appbuild"
    echo {
    echo     "appid"       "%APPID%"
    echo     "desc"        "TimeKov %LABEL% %DATE% %TIME%"
    echo     "buildoutput" "..\output\"
    echo     "contentroot" "..\content\"
    echo     "setlive"     ""
    echo     "preview"     "%PREVIEW%"
    echo     "depots"
    echo     {
    echo         "%DEPOTID%" "depot_build.vdf"
    echo     }
    echo }
)

> "%SCRIPTS%\depot_build.vdf" (
    echo "DepotBuild"
    echo {
    echo     "DepotID"     "%DEPOTID%"
    echo     "contentroot" "..\content\"
    echo     "FileMapping"
    echo     {
    echo         "LocalPath"  "*"
    echo         "DepotPath"  "."
    echo         "recursive"  "1"
    echo     }
    echo     "FileExclusion" "*.pdb"
    echo     "FileExclusion" "*_BurstDebugInformation_DoNotShip*"
    echo     "FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame*"
    echo }
)

echo   vdf 생성 완료
if "%PREVIEW%"=="1" (
    echo   [미리보기] 파일만 계산하고 전송하지 않습니다.
) else (
    echo   [실제 업로드] 파일을 스팀 서버로 전송합니다.
)
echo.
echo   Steam Guard 코드를 물어보면 입력하세요.
echo.

"%STEAMCMD%" +login "%STEAMUSER%" +run_app_build "%SCRIPTS%\app_build.vdf" +quit

if errorlevel 1 (
    echo.
    echo [실패] 업로드가 끝나지 않았습니다. 위 로그와 %OUTPUT% 을 확인하세요.
    goto :fail
)

echo.
if "%PREVIEW%"=="1" (
    echo ============================================
    echo   미리보기 완료
    echo.
    echo   전송은 하지 않았습니다. 스팀에는 아무것도 올라가지 않았습니다.
    echo   로그인·권한·vdf·파일 목록이 정상이라는 뜻입니다.
    echo   결과 목록은 %OUTPUT% 에 있습니다.
    echo ============================================
) else (
    echo ============================================
    echo   업로드 완료
    echo.
    echo   아직 배포된 것이 아닙니다.
    echo   Steamworks ^> SteamPipe ^> Builds 에서
    echo   방금 빌드를 브랜치에 설정하고 Publish 를 눌러야 합니다.
    echo ============================================
)
goto :end

:fail
echo.
exit /b 1

:end
echo.
pause
