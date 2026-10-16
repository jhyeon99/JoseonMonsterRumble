# 조선요괴대소동 (Joseon Monster Rumble)

> 요괴가 들끓는 조선 땅, 부적과 곰방대 한 자루로 맞서라!

조선시대풍 배경과 한국 설화 속 요괴를 소재로 한 **2D 횡스크롤 액션 플랫포머**입니다.  
근접 공격과 가드·패링·회피, 부적 원거리 공격을 조합해 스테이지를 돌파하고 마지막 보스에 도전합니다.

- **플레이:** [itch.io에서 플레이하기](https://ccwcs.itch.io/joseonmonsterrumble)
- **영상:** [플레이 영상 보기](https://www.youtube.com/watch?v=qx33LbJG1Qg)
- **팀:** 제6볶음
- **개발 기간:** 2026.09.17 ~ 2026.10.15
- **개발 환경:** Unity 6 (6000.3.22f1), C#, URP 2D

---

## 🎮 조작키

| 키 | 동작 |
|---|---|
| ← / → | 이동 |
| C 또는 Space | 점프 — 길게 누르면 높이 점프 |
| X | 근접 공격 |
| V | 부적 공격 — 누르고 있으면 충전 |
| ↓ | 가드 — 적의 공격에 타이밍을 맞추면 패링 |
| Z | 회피 |
| 1 | 물약 사용 |
| Enter | 대화·안내 넘기기 |
| Esc | 일시정지 |

---

## 👥 팀원 및 담당 작업

| 이름 | 역할 | 주요 작업 |
|---|---|---|
| 하지호 | 팀장 · PM · 기획 · 콘텐츠 구현 | 일정 관리, 게임 기획, 스테이지 제작, 적 배치·영혼 수급 밸런스 조정, 오디오 적용·조정 |
| 송재현 | 메인 프로그래머 | 플레이어, 보스, 전투 시스템, 빌드 통합 |
| 박욱영 | TA | UI/UX, VFX, 애니메이션, 게임필 |
| 김상곤 | 서브 프로그래머 | 적 시스템, 일부 레벨 디자인 |

---

## 📦 프로젝트 실행 안내

> 게임 플레이만 목적이라면 별도 설치 없이 [itch.io 빌드](https://ccwcs.itch.io/joseonmonsterrumble)를 이용해 주세요.

### 유료 에셋 미포함

이 저장소에는 유료 에셋 **[ProCamera2D](https://marketplace.unity.com/packages/2d/pro-camera-2d-the-definitive-2d-2-5d-camera-plugin-for-unity-42095)**가 포함되어 있지 않습니다. 에셋 원본의 공개 재배포를 피하기 위해 커밋 기록에서도 제거했습니다.

따라서 저장소를 그대로 열면 ProCamera2D를 참조하는 스크립트에서 컴파일 오류가 발생합니다.

| 경로 | 사용 기능 |
|---|---|
| `Assets/02.Scripts/Camera/` | 카메라 흔들림, 경계, 줌, 레터박스 |
| `Assets/02.Scripts/Manager/` | 프롤로그·튜토리얼 카메라 연출 |
| `Assets/02.Scripts/UI/StageManager.cs` | 스테이지 전환 시 카메라 처리 |
| `Assets/Editor/` | 카메라 관련 에디터 설정 도구 |

### 실행 순서

1. Unity Asset Store에서 **ProCamera2D**를 구매하거나 보유 여부를 확인합니다.
2. 저장소를 클론하고 **Unity 6000.3.22f1**로 프로젝트를 엽니다.
3. **Package Manager → My Assets**에서 ProCamera2D를 임포트합니다. 기본 경로인 `Assets/ProCamera2D`를 유지해 주세요.
4. 임포트 후 ProCamera2D 관련 컴파일 오류가 해소됐는지 확인합니다.

---

## 🛠️ 팀원 개발 환경 설정

커밋하기 전에 아래 설정을 완료해 주세요. 저장소를 처음 클론한 뒤 **개발 환경별로 한 번** 실행하면 됩니다.

이 프로젝트는 **.editorconfig**와 **Husky.Net**으로 C# 코드 스타일과 커밋 메시지 규칙을 검증합니다.

### 사전 준비

**.NET SDK**가 필요합니다. 터미널에서 설치 여부를 확인할 수 있습니다.

```bash
dotnet --version
```

설치되어 있지 않다면 [.NET SDK 다운로드](https://dotnet.microsoft.com/download)에서 설치한 뒤 IDE와 터미널을 다시 실행해 주세요.

### 설정 방법

아래 두 방법 중 하나를 선택합니다.

**자동 설정 — Windows**

프로젝트 루트의 `setup.bat`를 실행합니다.

**수동 설정**

프로젝트 루트에서 터미널을 열고 아래 명령을 실행합니다. Rider에서는 `Alt + F12`로 터미널을 열 수 있습니다.

```bash
dotnet tool restore
dotnet husky install
```

### 코드 스타일 확인

프로젝트 루트에서 아래 명령으로 스크립트의 포맷을 확인할 수 있습니다.

```bash
dotnet format "2d-team06.sln" --verify-no-changes --include Assets/02.Scripts/
```

검사에 실패하면 저장소의 `.editorconfig`와 훅 규칙에 맞게 수정한 뒤 다시 확인해 주세요.
