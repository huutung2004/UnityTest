using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public event Action<eStateGame> StateChangedAction = delegate { };

    public enum eLevelMode
    {
        TIMER,
        MOVES
    }

    public enum eStateGame
    {
        SETUP,
        MAIN_MENU,
        GAME_STARTED,
        PAUSE,
        GAME_OVER,
        GAME_COMPLETE,
        AUTOPLAY_WIN,
        AUTOPLAY_LOSE
    }
    private eLevelMode _mode;
    private eStateGame m_state;
    public eStateGame State
    {
        get { return m_state; }
        private set
        {
            m_state = value;
            StateChangedAction(m_state);
        }
    }

    private AutoplayManager m_autoplayManager;
    private GameSettings m_gameSettings;

    private BoardController m_boardController;

    private UIMainManager m_uiMenu;

    private LevelCondition m_levelCondition;


    private void Awake()
    {
        State = eStateGame.SETUP;

        m_gameSettings = Resources.Load<GameSettings>(Constants.GAME_SETTINGS_PATH);

        m_uiMenu = FindObjectOfType<UIMainManager>();
        m_uiMenu.Setup(this);
        m_autoplayManager = this.gameObject.AddComponent<AutoplayManager>();
    }

    void Start()
    {
        State = eStateGame.MAIN_MENU;
    }

    void Update()
    {
        if (m_boardController != null) m_boardController.Update();
    }


    internal void SetState(eStateGame state)
    {
        State = state;

        if (State == eStateGame.PAUSE)
        {
            DOTween.PauseAll();
        }
        else
        {
            DOTween.PlayAll();
        }
    }

    public void LoadLevel(eLevelMode mode)
    {
        // 1. Khởi tạo BoardController
        m_boardController = new GameObject("BoardController").AddComponent<BoardController>();
        m_boardController.StartGame(this, m_gameSettings);

        // 2. Khởi tạo LevelCondition
        if (mode == eLevelMode.MOVES)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelMoves>();
            m_levelCondition.Setup(m_gameSettings.LevelMoves, m_uiMenu.GetLevelConditionView(), m_boardController);
        }
        else if (mode == eLevelMode.TIMER)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelTime>();
            SetMode(eLevelMode.TIMER);
            m_levelCondition.Setup(m_gameSettings.LevelTime, m_uiMenu.GetLevelConditionView(), this);
        }

        m_levelCondition.ConditionCompleteEvent += GameOver;
        m_levelCondition.ConditionCompleteEvent += GameComplete;


        State = eStateGame.GAME_STARTED;
    }

    public void GameOver()
    {
        StartCoroutine(WaitBoardController());
    }
    public void GameComplete()
    {
        StartCoroutine(WaitBoardControllerForWin());
    }

    internal void ClearLevel()
    {
        if (m_boardController)
        {
            m_boardController.Clear();
            Destroy(m_boardController.gameObject);
            m_boardController = null;
        }
    }

    private IEnumerator WaitBoardController()
    {
        while (m_boardController.IsBusy)
        {
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(1f);

        State = eStateGame.GAME_OVER;

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameOver;

            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }
    private IEnumerator WaitBoardControllerForWin()
    {
        while (m_boardController.IsBusy)
        {
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(1f);

        State = eStateGame.GAME_COMPLETE;

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameComplete;

            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }


    public void StartAutoplayWin(float delay)
    {
        // Kiểm tra các đối tượng cần thiết
        if (m_boardController == null || m_levelCondition == null)
        {
            Debug.LogError("BoardController or LevelCondition not loaded. Load level first.");
            return;
        }

        SetState(eStateGame.AUTOPLAY_WIN);

        // TRUYỀN ĐỐI TƯỢNG ĐÃ KHỞI TẠO VÀ ÉP KIỂU SANG INTERFACE
        m_autoplayManager.StartAutoplay((IAutoplayBoard)m_boardController, (ILevelCondition)m_levelCondition, delay, true);
    }

    public void StartAutoplayLose(float delay)
    {
        // Kiểm tra các đối tượng cần thiết
        if (m_boardController == null || m_levelCondition == null)
        {
            Debug.LogError("BoardController or LevelCondition not loaded. Load level first.");
            return;
        }

        SetState(eStateGame.AUTOPLAY_LOSE);

        // TRUYỀN ĐỐI TƯỢNG ĐÃ KHỞI TẠO VÀ ÉP KIỂU SANG INTERFACE
        m_autoplayManager.StartAutoplay((IAutoplayBoard)m_boardController, (ILevelCondition)m_levelCondition, delay, false);
    }
    public void SetMode(eLevelMode mode)
    {
        _mode = mode;
    }
    public eLevelMode GetMode() { return _mode;}
}