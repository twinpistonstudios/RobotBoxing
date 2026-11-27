using IronKnuckle.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronKnuckle.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameDatabase Database { get; private set; }
        public PlayerSaveData SaveData { get; private set; }
        public ProgressionService Progression { get; private set; }

        public string SelectedOpponentId { get; set; }
        public bool IsQuickSparring { get; set; }
        public int QuickSparDifficultyLevel { get; set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBootstrapped()
        {
            if (Instance == null)
            {
                var go = new GameObject("[GameManager]");
                go.AddComponent<GameManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (AudioManager.Instance == null)
            {
                var audioGo = new GameObject("[AudioManager]");
                audioGo.AddComponent<AudioManager>();
            }
            if (GraphicsSettingsManager.Instance == null)
            {
                var gfxGo = new GameObject("[GraphicsSettingsManager]");
                gfxGo.AddComponent<GraphicsSettingsManager>();
            }

            Database = Resources.Load<GameDatabase>("GameDatabase");
            SaveData = SaveSystem.Load();
            Progression = new ProgressionService(Database, SaveData);

            AudioManager.Instance?.SetVolumes(SaveData.sfxVolume, SaveData.bgmVolume);
            GraphicsSettingsManager.Instance?.ApplySettings(SaveData);
        }

        public void PersistSave()
        {
            SaveSystem.Save(SaveData);
        }

        public void ResetSaveData()
        {
            SaveData = SaveSystem.CreateDefaultSave();
            Progression = new ProgressionService(Database, SaveData);
            PersistSave();
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void LoadGarage()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Garage");
        }

        public void StartCareerMatch(string opponentId)
        {
            SelectedOpponentId = opponentId;
            IsQuickSparring = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene("Arena");
        }

        public void StartQuickSparring()
        {
            IsQuickSparring = true;
            SelectedOpponentId = "";
            Time.timeScale = 1f;
            SceneManager.LoadScene("Arena");
        }

        public OpponentData GetActiveOpponent()
        {
            if (Database == null) return null;
            if (!IsQuickSparring && !string.IsNullOrEmpty(SelectedOpponentId))
            {
                var opp = Database.GetOpponent(SelectedOpponentId);
                if (opp != null) return opp;
            }

            // Pick a sparring opponent matched to player's completed progress
            if (Database.opponents.Count > 0)
            {
                int idx = Mathf.Clamp(SaveData.completedOpponentIds.Count, 0, Database.opponents.Count - 1);
                return Database.opponents[idx];
            }
            return null;
        }
    }
}
