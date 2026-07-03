using Core;
using Gameplay.Rope;
using Systems;
using UnityEngine;

namespace UI
{
    public enum ResultAction
    {
        Restart,
        NextLevel
    }

    public class ResultSign : RealGravityRopeConnectable
    {
        [SerializeField] private ResultAction _action;
        [SerializeField] private string _nextLevelSceneName;
        [SerializeField] private string _groundTag = "Finish";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(_groundTag)) return;

            SfxPlayer.Play(SfxId.Drop);

            switch (_action)
            {
                case ResultAction.Restart:
                    PlayerInputLock.Clear();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    break;

                case ResultAction.NextLevel:
                    PlayerInputLock.Clear();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(_nextLevelSceneName);
                    break;
            }
        }
    }
}
