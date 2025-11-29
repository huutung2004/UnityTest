using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelMain : MonoBehaviour, IMenu
{
    [SerializeField] private Button btnTimer;

    [SerializeField] private Button btnMoves;
    [SerializeField] private Button autoLose;
    [SerializeField] private Button autoWin;


    private UIMainManager m_mngr;

    private void Awake()
    {
        btnMoves.onClick.AddListener(OnClickMoves);
        autoLose.onClick.AddListener(OnClickLose);
        autoWin.onClick.AddListener(OnClickWin);
        btnTimer.onClick.AddListener(OnClickTimer);

    }

    private void OnDestroy()
    {
        if (btnMoves) btnMoves.onClick.RemoveAllListeners();
        if (btnTimer) btnTimer.onClick.RemoveAllListeners();
        if (autoLose) autoLose.onClick.RemoveAllListeners();
        if (autoWin) autoWin.onClick.RemoveAllListeners();
    }

    public void Setup(UIMainManager mngr)
    {
        m_mngr = mngr;
    }

    private void OnClickTimer()
    {
        m_mngr.LoadLevelTimer();
    }
    private void OnClickLose()
    {
        m_mngr.LoadLevelAndAutoplayLose();
    }
    private void OnClickWin()
    {
        Debug.Log("Hererere");
        m_mngr.LoadLevelAndAutoplayWin();
    }


    private void OnClickMoves()
    {
        m_mngr.LoadLevelMoves();
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }
}
