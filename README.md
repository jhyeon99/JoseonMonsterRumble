# 조선요괴대소동 (Joseon Monster Rumble)

> 요괴가 들끓는 조선 땅, 부적과 곰방대 한 자루로 맞서라!

한국 설화 속 요괴를 소재로 한 2D 횡스크롤 보스 액션 게임입니다.
가드와 패링, 회피, 부적 원거리 공격을 조합해 요괴들을 물리치고 마지막 보스에 도전합니다.

- **플레이:** [itch.io에서 플레이하기]([https://itch.io/](https://ccwcs.itch.io/joseonmonsterrumble))
- **팀:** 제6볶음
- **엔진:** Unity 6 (6000.3.22f1), URP 2D

---

## ⚠️ 유료 에셋 미포함 안내

이 저장소에는 **유료 에셋 [ProCamera2D](https://marketplace.unity.com/packages/2d/pro-camera-2d-the-definitive-2d-2-5d-camera-plugin-for-unity-42095)가 포함되어 있지 않습니다.**
Unity Asset Store 라이선스(EULA)상 에셋 원본을 공개 저장소에 재배포할 수 없어, 커밋 기록 전체에서 제거했습니다.

그래서 저장소를 그대로 열면 ProCamera2D를 사용하는 아래 스크립트에서 **컴파일 에러가 발생합니다.**

| 경로 | 내용 |
|---|---|
| `Assets/02.Scripts/Camera/` | 카메라 흔들림, 경계, 줌, 레터박스 |
| `Assets/02.Scripts/Manager/` | 프롤로그·튜토리얼 카메라 연출 |
| `Assets/02.Scripts/UI/StageManager.cs` | 스테이지 전환 시 카메라 처리 |
| `Assets/Editor/` | 카메라 관련 에디터 설정 도구 |

### 프로젝트를 직접 열어보려면
1. Unity Asset Store에서 **ProCamera2D**를 구매합니다.
2. Unity 6000.3.22f1로 프로젝트를 엽니다. 에러 때문에 Safe Mode로 열리면 **Ignore**를 선택합니다.
3. Package Manager → My Assets에서 ProCamera2D를 임포트합니다. 기본 경로 `Assets/ProCamera2D`를 그대로 사용하세요.
4. 컴파일이 끝나면 정상적으로 실행할 수 있습니다.

> 게임 플레이만 목적이라면 위 과정 없이 itch.io 빌드를 이용해 주세요.

---

## 🎮 조작키

| 키 | 동작 |
|---|---|
| ← / → | 이동 |
| C 또는 Space | 점프 (길게 누르면 높이 점프) |
| X | 근접 공격 |
| V | 부적 던지기 (누르고 있으면 충전) |
| ↓ | 가드 (타이밍 맞춰 막으면 패링) |
| Z | 회피 |
| 1 | 물약 사용 |
| Enter | 대화·안내 넘기기 |
| Esc | 일시정지 |

---

## 🛠️ 팀원 개발 환경 세팅 가이드 (최초 1회 실행)

본 프로젝트는 **.editorconfig** 및 **Husky.Net**을 활용하여 커밋 메시지 규격과 C# 코딩 컨벤션을 자동으로 검증합니다.
프로젝트를 `clone` 또는 `pull` 받은 후 **최초 1회** 아래 세팅을 완료해 주세요.

### 1. 전제 조건 (Prerequisites)
- 컴퓨터에 **.NET SDK**가 설치되어 있어야 합니다. (`dotnet --version`으로 확인)
    - 미설치 시: [.NET SDK 다운로드](https://dotnet.microsoft.com/download) 후 설치 및 IDE/터미널 재시작

### 2. 세팅 방법 (택 1)

#### Option A. 자동 세팅 (권장 - Windows)
프로젝트 루트 폴더에 있는 **`setup.bat`** 파일을 더블 클릭하여 실행합니다.

#### Option B. 수동 세팅 (Rider Terminal / Git Bash)
Rider 하단 **Terminal** (`Alt + F12`)을 열고 아래 명령어 두 줄을 실행합니다:
```bash
dotnet tool restore
dotnet husky install
```
