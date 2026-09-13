# 스팀 업로드

## 한 번만 하면 되는 준비

1. **Steamworks SDK 받기**
   [partner.steamgames.com](https://partner.steamgames.com) → Steamworks SDK 다운로드 → 아무 데나 압축 해제.
   여기서 쓰는 건 `tools/ContentBuilder/builder/steamcmd.exe` 하나뿐이다.

2. **`upload.bat` 위쪽 [설정] 네 줄 채우기**

   | 항목 | 어디서 보나 |
   |---|---|
   | `APPID` | 스토어 페이지 주소의 숫자. `store.steampowered.com/app/1234560/TimeKov/` → `1234560` |
   | `DEPOTID` | Steamworks → 앱 → SteamPipe → Depots. 보통 App ID + 1 이지만 눈으로 확인할 것 |
   | `STEAMCMD` | 1번에서 푼 `steamcmd.exe` 의 전체 경로 |
   | `STEAMUSER` | 업로드 권한이 있는 파트너 계정 ID |

   > 계정 ID를 git 에 남기고 싶지 않으면, 각자 로컬에서만 채우고 커밋하지 않으면 된다.
   > 비밀번호는 이 파일에 적지 않는다. steamcmd 가 실행할 때 직접 묻는다.

## 매 빌드마다

```
① 유니티  Tools > TIMEKOV > 스팀 > ① 출시 전 점검
② 유니티  Tools > TIMEKOV > 스팀 > ② 빌드 만들기      → Steam/content/ 에 생성
③ upload.bat 더블클릭                                 → Steam Guard 코드 입력
④ Steamworks > SteamPipe > Builds 에서 브랜치 설정 → Publish
```

**④를 안 하면 배포되지 않는다.** `upload.bat` 은 일부러 `setlive` 를 비워 두고 올리기만 한다.
실수로 검증 안 된 빌드가 바로 배포되는 걸 막기 위해서다.

## 폴더

```
Steam/
  upload.bat      설정 + 업로드. vdf 를 실행할 때마다 새로 만든다
  content/        빌드 결과물이 들어간다 (git 제외)
  output/         steamcmd 로그 (git 제외)
  scripts/        생성된 vdf (git 제외)
```

## 자주 막히는 것

**"빌드 결과가 없습니다"**
유니티 빌드를 먼저 돌려야 한다. `Steam/content/TimeKov.exe` 가 있어야 진행된다.

**steamcmd 가 로그인에서 멈춤**
Steam Guard 코드를 모바일 앱이나 메일로 받아 입력한다. 첫 로그인만 묻고 이후에는 기억한다.

**올렸는데 스팀에서 안 보임**
Builds 목록에는 올라와 있고, 브랜치 설정 + Publish 를 안 한 상태다. 4단계를 확인한다.

**빌드에 이상한 폴더가 같이 올라감**
`*_BurstDebugInformation_DoNotShip`, `*_BackUpThisFolder_ButDontShipItWithYourGame` 은
`upload.bat` 의 `FileExclusion` 에서 이미 제외한다. 다른 게 보이면 거기에 한 줄 추가한다.

## 주의

`Company Name` 을 출시 후에 바꾸면 **세이브 경로가 통째로 바뀌어 기존 플레이어의 저장 파일이 사라진다.**

```
%USERPROFILE%\AppData\LocalLow\<Company Name>\TimeKov\Saves\
                                 ↑ 여기가 바뀐다
```

`① 출시 전 점검` 이 이 항목을 확인한다.
