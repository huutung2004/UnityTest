using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAutoplayBoard
{
    bool TryFindWinningMove(out Cell cell1, out Cell cell2);
    bool TryFindLosingMove(out Cell cell1, out Cell cell2);
    IEnumerator SwapAndProcess(Cell cell1, Cell cell2);
    bool IsBusy { get; }
    List<Cell> GetAllCells();
    bool CheckWinCondition();
    bool CheckLose();
}

public interface ILevelCondition
{
    bool IsComplete { get; }
    bool IsWinCondition { get; }
}


public class AutoplayManager : MonoBehaviour
{
    private IAutoplayBoard m_boardController;
    private ILevelCondition m_levelCondition;
    private Coroutine m_autoplayCoroutine;
    private float m_actionDelay;
    private bool m_isWinningAutoplay;

    public void StartAutoplay(IAutoplayBoard boardController, ILevelCondition levelCondition, float delay, bool isWinning)
    {
        Debug.Log($"Autoplay requested. Is BoardController null? {boardController == null}");
        Debug.Log($"Is LevelCondition null? {levelCondition == null}");

        if (boardController == null || levelCondition == null)
        {
            Debug.LogError("Autoplay failed to start: Board or LevelCondition reference is null.");
            return;
        }
        m_boardController = boardController;
        m_levelCondition = levelCondition;
        m_actionDelay = delay;
        m_isWinningAutoplay = isWinning;

        if (m_autoplayCoroutine != null)
        {
            StopCoroutine(m_autoplayCoroutine);
        }
        m_autoplayCoroutine = StartCoroutine(AutoplayLoop());
    }

    public void StopAutoplay()
    {
        if (m_autoplayCoroutine != null)
        {
            StopCoroutine(m_autoplayCoroutine);
            m_autoplayCoroutine = null;
        }
    }

    private IEnumerator AutoplayLoop()
    {
        while (m_levelCondition != null && !m_levelCondition.IsComplete)
        {
            while (m_boardController.IsBusy)
            {
                yield return new WaitForEndOfFrame();
            }

            Cell cell1 = null;
            Cell cell2 = null;
            bool moveFound = false;

            if (m_isWinningAutoplay)
            {
                moveFound = m_boardController.TryFindWinningMove(out cell1, out cell2);
            }
            else
            {
                moveFound = m_boardController.TryFindLosingMove(out cell1, out cell2);
            }

            if (moveFound && cell1 != null)
            {
                yield return m_boardController.SwapAndProcess(cell1, cell2);

                yield return new WaitForSeconds(m_actionDelay);
                if (m_boardController.CheckWinCondition())
                {
                    Debug.Log("AutoplayManager: Detected win after swap, stopping autoplay.");
                    break;
                }
                if (m_boardController.CheckLose())
                {
                    break;
                }
            }
            else
            {
                Debug.LogWarning($"Autoplay: No {(m_isWinningAutoplay ? "Winning" : "Losing")} move found. Maybe need a shuffle.");
                break;
            }
        }

        Debug.Log($"Autoplay finished. Condition met: {m_levelCondition.IsComplete}");

    }
}