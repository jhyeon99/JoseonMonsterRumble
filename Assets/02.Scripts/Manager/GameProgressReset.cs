/// <summary>
/// 저장 기능이 없어서 타이틀로 돌아가거나 새 게임을 시작하면 이번 판의 진행 기록을 모두 지움.
/// (사운드 설정처럼 PlayerPrefs에 저장하는 환경 설정은 유지)
/// - EP1 튜토리얼 진행 (본 힌트 / 완료한 연습 / 전투 튜토리얼 단계)
/// - 능력 아이템 보유 (도깨비 감투 등)
/// - 이번 판에 이미 본 보스 등장 연출
/// 체크포인트는 타이틀 씬에 리스폰 지점이 없어 StageManager가 알아서 비움
/// </summary>
public static class GameProgressReset
{
    public static void ResetAll()
    {
        Ep1TutorialManager.ResetShownHints();
        AbilityItemProgress.ResetAll();
        BossCinematicDirector.ResetSeenIntros();
    }
}
