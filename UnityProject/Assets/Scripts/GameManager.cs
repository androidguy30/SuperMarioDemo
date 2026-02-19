using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central game manager - handles score, lives, game states, and UI.
/// Singleton pattern for global access.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text livesText;
    [SerializeField] private Text worldText;
    [SerializeField] private Text timerText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject courseClearPanel;
    [SerializeField] private GameObject titlePanel;

    [Header("Settings")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private float levelTime = 400f;
    [SerializeField] private float deathDelay = 2.5f;
    [SerializeField] private float winDelay = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip musicOverworld;
    [SerializeField] private AudioClip sfxJump;
    [SerializeField] private AudioClip sfxCoin;
    [SerializeField] private AudioClip sfxStomp;
    [SerializeField] private AudioClip sfxBrickBreak;
    [SerializeField] private AudioClip sfxPowerup;
    [SerializeField] private AudioClip sfxDie;
    [SerializeField] private AudioClip sfxFlagpole;
    [SerializeField] private AudioClip sfxGameOver;

    // Game state
    private int score;
    private int coins;
    private int lives;
    private float timer;
    private bool isPlaying;
    private bool isPaused;
    private AudioSource audioSource;
    private AudioSource sfxSource;

    public int Score => score;
    public int Coins => coins;
    public int Lives => lives;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
    }

    private void Start()
    {
        lives = startingLives;
        timer = levelTime;
        UpdateUI();
        ShowTitle();
    }

    private void Update()
    {
        if (!isPlaying) return;

        if (timer > 0)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                timer = 0;
                // Time up - kill player
                FindFirstObjectByType<PlayerController>()?.Die();
            }
        }

        UpdateUI();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void ShowTitle()
    {
        isPlaying = false;
        if (titlePanel != null) titlePanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (courseClearPanel != null) courseClearPanel.SetActive(false);
    }

    public void StartGame()
    {
        score = 0;
        coins = 0;
        lives = startingLives;
        timer = levelTime;
        isPlaying = true;

        if (titlePanel != null) titlePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (courseClearPanel != null) courseClearPanel.SetActive(false);

        PlayMusic(musicOverworld);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateUI();
    }

    public void AddCoin()
    {
        coins++;
        score += 200;
        PlaySFX(sfxCoin);

        if (coins >= 100)
        {
            coins = 0;
            lives++;
        }
        UpdateUI();
    }

    public void OnPlayerDeath()
    {
        isPlaying = false;
        PlaySFX(sfxDie);
        audioSource.Stop();

        lives--;
        if (lives <= 0)
        {
            Invoke(nameof(ShowGameOver), deathDelay);
        }
        else
        {
            Invoke(nameof(RespawnLevel), deathDelay);
        }
    }

    public void OnFlagReached()
    {
        isPlaying = false;
        PlaySFX(sfxFlagpole);
        audioSource.Stop();

        // Score bonus for remaining time
        int timeBonus = Mathf.FloorToInt(timer) * 50;
        score += timeBonus;

        Invoke(nameof(ShowCourseClear), winDelay);
    }

    private void ShowGameOver()
    {
        PlaySFX(sfxGameOver);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    private void ShowCourseClear()
    {
        if (courseClearPanel != null) courseClearPanel.SetActive(true);
    }

    private void RespawnLevel()
    {
        timer = levelTime;
        isPlaying = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        PlayMusic(musicOverworld);
    }

    public void RestartFromTitle()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = score.ToString("D6");
        if (coinText != null) coinText.text = "x" + coins.ToString("D2");
        if (livesText != null) livesText.text = "x " + lives;
        if (timerText != null) timerText.text = Mathf.CeilToInt(timer).ToString();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    // Public SFX accessors for other scripts
    public void PlayJumpSFX() => PlaySFX(sfxJump);
    public void PlayStompSFX() => PlaySFX(sfxStomp);
    public void PlayBrickBreakSFX() => PlaySFX(sfxBrickBreak);
    public void PlayPowerupSFX() => PlaySFX(sfxPowerup);
}
