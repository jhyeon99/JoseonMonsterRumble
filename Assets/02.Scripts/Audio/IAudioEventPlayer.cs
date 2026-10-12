namespace GameAudio
{
    /// <summary>기존 AudioManager에 구현할 데이터 기반 재생 계약.</summary>
    public interface IAudioEventPlayer
    {
        // 미등록/비활성/클립 미지정 요청은 Empty를 반환한다.
        // 요청 시 데이터를 스냅샷하고 지연 중에는 채널을 점유하지 않는다.
        // 동일 set/event/owner/action의 반복 요청은 기존 핸들을 반환한다.
        PlayHandle PlayEvent(SoundSetSO set, string eventId, AudioContext context);

        // ID와 세대를 검증한다. 종료된 핸들은 아무 동작도 하지 않는다.
        void StopHandle(PlayHandle handle);

        // 정상 종료: 행동 반복음 종료, 일반 동작음의 남은 예약은 유지.
        void CompleteAction(AudioOwnerKey owner, ulong actionExecutionId);

        // 중단: 행동 예약/반복음 취소. 재생 중 일회성 동작음과 결과음은 유지.
        void CancelAction(AudioOwnerKey owner, ulong actionExecutionId);

        // 해당 개체 세대의 행동/개체 수명 요청 정리. 결과음은 씬까지 유지.
        void ReleaseOwner(AudioOwnerKey owner);

        // 해당 씬의 예약과 효과음 정리. 전역 요청은 유지.
        // BGM 전환은 최신 전환 번호를 검증하는 별도 음악 경로에서 처리.
        void ReleaseScene(AudioSceneKey scene);
        void StopBgm(float fadeDuration = 0.5f);
    }
}
