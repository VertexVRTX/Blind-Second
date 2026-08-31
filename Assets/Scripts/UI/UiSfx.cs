using UnityEngine;

namespace BlindTiming.UI
{
    public class UiSfx : MonoBehaviour
    {
        public static UiSfx Instance { get; private set; }

        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip redButtonClip;
        [SerializeField] private AudioClip roundButtonClip;
        [SerializeField] private AudioClip chatOpenClip;
        [SerializeField] private AudioClip chatSendClip;
        [SerializeField] private AudioClip roundWinClip;
        [SerializeField] private AudioClip roundLoseClip;
        [SerializeField] private AudioClip errorClip;

        private AudioSource _source;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public void PlayUiClick() => Play(uiClickClip);
        public void PlayRedButton() => Play(redButtonClip);
        public void PlayRoundButton() => Play(roundButtonClip);
        public void PlayChatOpen() => Play(chatOpenClip);
        public void PlayChatSend() => Play(chatSendClip);
        public void PlayRoundWin() => Play(roundWinClip);
        public void PlayRoundLose() => Play(roundLoseClip);
        public void PlayError() => Play(errorClip);

        private void Play(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip);
        }
    }
}
