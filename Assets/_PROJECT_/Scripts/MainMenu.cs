using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string _sceneToLoad = "GameScene";
        [SerializeField] private Button _startButton;

        private void Awake()
        {
            _startButton.onClick.AddListener(() => LoadScene().Forget());
        }   

        private async UniTask LoadScene()
        {
            _startButton.interactable = false;

            await SceneManager.LoadSceneAsync(_sceneToLoad, LoadSceneMode.Additive);

            await FadeOutLoading();

            var menuScene = SceneManager.GetSceneByName("MainMenu");
            var gameScene = SceneManager.GetSceneByName(_sceneToLoad);
            
            SceneManager.SetActiveScene(gameScene);
            await SceneManager.UnloadSceneAsync(menuScene);
            
        }

        private async UniTask FadeOutLoading()
        {
            var loadingScreen = GameObject.Find("LoadingScreen");
            if (loadingScreen != null)
            {
                var canvasGroup = loadingScreen.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    while (canvasGroup.alpha > 0f)
                    {
                        canvasGroup.alpha -= Time.deltaTime;
                        await UniTask.Yield();
                    }
                }
            }
        }
    }
}